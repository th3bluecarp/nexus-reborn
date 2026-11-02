using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using NexusForever.Game.Static.Entity;
using NexusForever.Game.Static.Entity.Movement.Command;
using NexusForever.Game.Static.Entity.Movement.Spline;
using NexusForever.Network;
using NexusForever.Network.World.Entity;
using NexusForever.Network.World.Entity.Command;

namespace NexusForever.Bot;

internal sealed record TrackedEntity(
    uint Guid,
    EntityType Type,
    uint? CreatureId,
    Vector3? Position,
    EntityCreateFlag CreateFlags,
    byte? QuestChecklistIdx,
    uint? Level,
    uint? Health,
    uint? MaxHealth);

internal sealed record EntityPropertiesUpdate(uint UnitId, uint? MaxHealth);

internal sealed class EntityTracker
{
    private sealed record TrackedPositionPath(
        IReadOnlyList<Vector3> Positions,
        float Speed,
        SplineMode Mode,
        float InitialDistance,
        long ReceivedTimestamp);

    private static readonly IReadOnlyDictionary<EntityCommand, Type> CommandTypes = typeof(SetPositionCommand).Assembly
        .GetTypes()
        .Select(type => (Type: type, Attribute: type.GetCustomAttribute<EntityCommandAttribute>()))
        .Where(pair => pair.Attribute is not null && typeof(IEntityCommandModel).IsAssignableFrom(pair.Type))
        .ToDictionary(pair => pair.Attribute!.Command, pair => pair.Type);

    private readonly Dictionary<uint, TrackedEntity> entities = [];
    private readonly Dictionary<uint, TrackedPositionPath> positionPaths = [];

    public void Clear()
    {
        entities.Clear();
        positionPaths.Clear();
    }

    public void Remove(uint guid)
    {
        entities.Remove(guid);
        positionPaths.Remove(guid);
    }

    /// <summary>
    /// Entity types the bot may fight or talk to: creatures and AI turrets (the arkship Sim-Turrets 73494 are AiTurret, not NonPlayer).
    /// </summary>
    private static bool IsCombatUnit(EntityType type) => type is EntityType.NonPlayer or EntityType.AiTurret;

    public TrackedEntity? Get(uint guid)
    {
        TrackedEntity? entity = entities.GetValueOrDefault(guid);
        if (entity is null || !positionPaths.TryGetValue(guid, out TrackedPositionPath? path))
            return entity;
        return entity with { Position = CalculatePathPosition(path) };
    }

    public void MarkDead(uint guid)
    {
        if (entities.TryGetValue(guid, out TrackedEntity? entity))
            entities[guid] = entity with { Health = 0u };
    }

    public void UpdateHealth(uint guid, uint health)
    {
        if (entities.TryGetValue(guid, out TrackedEntity? entity))
            entities[guid] = entity with { Health = health };
    }

    public void UpdateMaxHealth(uint guid, uint maxHealth)
    {
        if (entities.TryGetValue(guid, out TrackedEntity? entity))
            entities[guid] = entity with { MaxHealth = maxHealth };
    }

    public TrackedEntity? FindCreature(
        uint creatureId,
        Spawn? expected = null,
        IReadOnlySet<uint>? excludedGuids = null)
    {
        IEnumerable<TrackedEntity> matches = entities.Values.Where(entity => entity.CreatureId == creatureId
            && (excludedGuids is null || !excludedGuids.Contains(entity.Guid)));
        if (expected is null)
            return matches.FirstOrDefault();

        return matches
            .Where(entity => entity.Position is Vector3 position
                && expected.DistanceSquared(position.X, position.Y, position.Z) <= 32d * 32d)
            .OrderBy(entity => expected.DistanceSquared(
                entity.Position!.Value.X,
                entity.Position.Value.Y,
                entity.Position.Value.Z))
            .FirstOrDefault();
    }

    public TrackedEntity? FindNearestVisibleNonPlayer(
        IReadOnlySet<uint> creatureIds,
        Vector3 origin,
        float radius,
        IReadOnlySet<uint>? excludedGuids = null)
    {
        double radiusSquared = radius * radius;
        return entities.Values
            .Select(entity => Get(entity.Guid) ?? entity)
            .Where(entity => IsCombatUnit(entity.Type)
                && entity.CreatureId is uint creatureId
                && creatureIds.Contains(creatureId)
                && (excludedGuids is null || !excludedGuids.Contains(entity.Guid))
                && entity.Health is null or > 0u
                && entity.Position is Vector3 position
                && Vector3.DistanceSquared(origin, position) <= radiusSquared)
            .OrderBy(entity => Vector3.DistanceSquared(origin, entity.Position!.Value))
            .ThenBy(entity => entity.Guid)
            .FirstOrDefault();
    }

    public IReadOnlySet<uint> GetVisibleNonPlayerGuids()
        => entities.Values
            .Where(entity => IsCombatUnit(entity.Type))
            .Select(entity => entity.Guid)
            .ToHashSet();

    public TrackedEntity? FindNearestVisibleNonPlayer(
        Vector3 origin,
        float radius,
        IReadOnlySet<uint> excludedGuids,
        Func<uint, bool> creatureFilter)
    {
        float radiusSquared = radius * radius;
        return entities.Values
            .Select(entity => Get(entity.Guid) ?? entity)
            .Where(entity => IsCombatUnit(entity.Type)
                && entity.CreatureId is uint creatureId
                && creatureFilter(creatureId)
                && !excludedGuids.Contains(entity.Guid)
                && entity.Health is null or > 0u
                && entity.Position is Vector3 position
                && Vector3.DistanceSquared(origin, position) <= radiusSquared)
            .OrderBy(entity => Vector3.DistanceSquared(origin, entity.Position!.Value))
            .ThenBy(entity => entity.Guid)
            .FirstOrDefault();
    }

    public IReadOnlyList<(uint CreatureId, float Distance)> GetNearbyCreatureIds(
        Vector3 origin,
        float radius,
        int limit)
    {
        double radiusSquared = radius * radius;
        return entities.Values
            .Where(entity => entity.CreatureId.HasValue
                && entity.Position is Vector3 position
                && Vector3.DistanceSquared(origin, position) <= radiusSquared)
            .GroupBy(entity => entity.CreatureId!.Value)
            .Select(group => (
                CreatureId: group.Key,
                Distance: group.Min(entity => Vector3.Distance(origin, entity.Position!.Value))))
            .OrderBy(item => item.Distance)
            .ThenBy(item => item.CreatureId)
            .Take(limit)
            .ToArray();
    }

    public int? CountOtherNonPlayers(Spawn targetSpawn, float radius)
    {
        TrackedEntity? target = FindCreature(targetSpawn.CreatureId, targetSpawn);
        if (target?.Position is null)
            return null;

        double radiusSquared = radius * radius;
        return entities.Values.Count(entity => entity.Guid != target.Guid
            && IsCombatUnit(entity.Type)
            && entity.Position is Vector3 position
            && entity.Health is > 0u
            && targetSpawn.DistanceSquared(position.X, position.Y, position.Z) <= radiusSquared);
    }

    public TrackedEntity ParseCreate(byte[] body)
    {
        using var stream = new MemoryStream(body, writable: false);
        using var reader = new GamePacketReader(stream);

        uint guid = reader.ReadUInt();
        EntityType type = reader.ReadEnum<EntityType>(6);
        (uint? creatureId, byte? questChecklistIdx) = ReadEntityModel(reader, type);
        EntityCreateFlag createFlags = reader.ReadEnum<EntityCreateFlag>(8);
        byte statCount = reader.ReadByte(5);
        uint? level = null;
        uint? health = null;
        for (int i = 0; i < statCount; i++)
        {
            (Stat stat, uint? integerValue) = ReadStat(reader);
            if (stat == Stat.Level)
                level = integerValue;
            else if (stat == Stat.Health)
                health = integerValue;
        }

        _ = reader.ReadUInt();
        byte commandCount = reader.ReadByte(5);
        Vector3? position = null;
        for (int i = 0; i < commandCount; i++)
        {
            EntityCommand command = reader.ReadEnum<EntityCommand>(5);
            if (!CommandTypes.TryGetValue(command, out Type? modelType)
                || Activator.CreateInstance(modelType) is not IEntityCommandModel model)
                throw new InvalidDataException($"Unknown entity command {(int)command}.");

            model.Read(reader);
            if (model is SetPositionCommand setPosition)
            {
                position = setPosition.Position;
                positionPaths.Remove(guid);
            }
            else if (model is SetPositionPathCommand setPositionPath)
            {
                TrackPositionPath(guid, setPositionPath);
                if (positionPaths.TryGetValue(guid, out TrackedPositionPath? path))
                    position = CalculatePathPosition(path);
            }
        }

        uint? maxHealth = null;
        byte propertyCount = reader.ReadByte();
        for (int i = 0; i < propertyCount; i++)
        {
            Property property = reader.ReadEnum<Property>(8);
            _ = reader.ReadSingle();
            float value = reader.ReadSingle();
            if (property == Property.BaseHealth)
                maxHealth = ToUnsignedProperty(value);
        }

        var entity = new TrackedEntity(
            guid,
            type,
            creatureId,
            position,
            createFlags,
            questChecklistIdx,
            level,
            health,
            maxHealth);
        entities[guid] = entity;
        return entity;
    }

    public EntityPropertiesUpdate ParsePropertiesUpdate(byte[] body)
    {
        using var stream = new MemoryStream(body, writable: false);
        using var reader = new GamePacketReader(stream);

        uint guid = reader.ReadUInt();
        uint? maxHealth = null;
        byte propertyCount = reader.ReadByte();
        for (int i = 0; i < propertyCount; i++)
        {
            Property property = reader.ReadEnum<Property>(8);
            _ = reader.ReadSingle();
            float propertyValue = reader.ReadSingle();
            if (property == Property.BaseHealth)
                maxHealth = ToUnsignedProperty(propertyValue);
        }

        if (maxHealth is uint maxHealthValue)
            UpdateMaxHealth(guid, maxHealthValue);
        return new EntityPropertiesUpdate(guid, maxHealth);
    }

    public (uint Guid, Vector3? Position) ParseCommands(byte[] body)
    {
        using var stream = new MemoryStream(body, writable: false);
        using var reader = new GamePacketReader(stream);

        uint guid = reader.ReadUInt();
        _ = reader.ReadUInt();
        _ = reader.ReadBit();
        _ = reader.ReadBit();

        byte commandCount = reader.ReadByte(5);
        Vector3? position = null;
        for (int i = 0; i < commandCount; i++)
        {
            EntityCommand command = reader.ReadEnum<EntityCommand>(5);
            if (!CommandTypes.TryGetValue(command, out Type? modelType)
                || Activator.CreateInstance(modelType) is not IEntityCommandModel model)
                throw new InvalidDataException($"Unknown entity command {(int)command}.");

            model.Read(reader);
            if (model is SetPositionCommand setPosition)
            {
                position = setPosition.Position;
                positionPaths.Remove(guid);
            }
            else if (model is SetPositionPathCommand setPositionPath)
            {
                TrackPositionPath(guid, setPositionPath);
                if (positionPaths.TryGetValue(guid, out TrackedPositionPath? path))
                    position = CalculatePathPosition(path);
            }
        }

        if (position is Vector3 value && entities.TryGetValue(guid, out TrackedEntity? entity))
            entities[guid] = entity with { Position = value };

        return (guid, position);
    }

    private void TrackPositionPath(uint guid, SetPositionPathCommand command)
    {
        if (command.Positions.Count < 2 || command.Speed <= 0f)
        {
            positionPaths.Remove(guid);
            return;
        }

        positionPaths[guid] = new TrackedPositionPath(
            command.Positions.ToArray(),
            command.Speed,
            command.Mode,
            command.Offset / 1000f,
            Stopwatch.GetTimestamp());
    }

    private static Vector3 CalculatePathPosition(TrackedPositionPath path)
    {
        float totalDistance = 0f;
        for (int i = 1; i < path.Positions.Count; i++)
            totalDistance += Vector3.Distance(path.Positions[i - 1], path.Positions[i]);
        if (totalDistance <= float.Epsilon)
            return path.Positions[^1];

        float elapsedSeconds = (float)Stopwatch.GetElapsedTime(path.ReceivedTimestamp).TotalSeconds;
        float distance = path.InitialDistance + elapsedSeconds * path.Speed;
        distance = path.Mode switch
        {
            SplineMode.OneShot => Math.Clamp(distance, 0f, totalDistance),
            SplineMode.OneShotReverse => totalDistance - Math.Clamp(distance, 0f, totalDistance),
            SplineMode.Cyclic => PositiveModulo(distance, totalDistance),
            SplineMode.CyclicReverse => totalDistance - PositiveModulo(distance, totalDistance),
            SplineMode.BackAndForth => BackAndForthDistance(distance, totalDistance),
            SplineMode.BackAndForthReverse => totalDistance - BackAndForthDistance(distance, totalDistance),
            _ => Math.Clamp(distance, 0f, totalDistance)
        };

        for (int i = 1; i < path.Positions.Count; i++)
        {
            Vector3 start = path.Positions[i - 1];
            Vector3 end = path.Positions[i];
            float segmentDistance = Vector3.Distance(start, end);
            if (segmentDistance <= float.Epsilon)
                continue;
            if (distance <= segmentDistance)
                return Vector3.Lerp(start, end, distance / segmentDistance);
            distance -= segmentDistance;
        }

        return path.Positions[^1];
    }

    private static float BackAndForthDistance(float distance, float totalDistance)
    {
        float cycleDistance = PositiveModulo(distance, totalDistance * 2f);
        return cycleDistance <= totalDistance ? cycleDistance : totalDistance * 2f - cycleDistance;
    }

    private static float PositiveModulo(float value, float modulus)
    {
        float result = value % modulus;
        return result < 0f ? result + modulus : result;
    }

    private static uint? ToUnsignedProperty(float value)
        => float.IsFinite(value) && value >= 0f && value <= uint.MaxValue
            ? (uint)value
            : null;

    private static (Stat Stat, uint? IntegerValue) ReadStat(GamePacketReader reader)
    {
        Stat stat = reader.ReadEnum<Stat>(5);
        StatType type = reader.ReadEnum<StatType>(2);
        switch (type)
        {
            case StatType.Integer:
                return (stat, reader.ReadUInt());
            case StatType.Float:
                _ = reader.ReadSingle();
                return (stat, null);
            case StatType.Data:
                _ = reader.ReadUInt();
                _ = reader.ReadUInt();
                return (stat, null);
            default:
                throw new InvalidDataException($"Unknown stat type {(int)type}.");
        }
    }

    /// <summary>
    /// Reads the model written by each of the 37 EntityType values. Keeping every model byte-aligned
    /// is required even for the eight models without a CreatureId because CreateFlags, stats and
    /// movement commands follow immediately after it in ServerEntityCreate.
    /// </summary>
    private static (uint? CreatureId, byte? QuestChecklistIdx) ReadEntityModel(
        GamePacketReader reader,
        EntityType type)
    {
        uint? creatureId = null;
        byte? questChecklistIdx = null;

        switch (type)
        {
            case EntityType.NonPlayer:
            case EntityType.CollectableUnit:
            case EntityType.Simple:
            case EntityType.Platform:
            case EntityType.AiTurret:
            case EntityType.SimpleCollidable:
                creatureId = reader.ReadUInt(18);
                questChecklistIdx = reader.ReadByte();
                break;

            case EntityType.Chest:
            case EntityType.Destructible:
            case EntityType.Door:
            case EntityType.HarvestUnit:
            case EntityType.CorpseUnit:
            case EntityType.MailBox:
            case EntityType.Residence:
            case EntityType.BindPoint:
            case EntityType.Hidden:
            case EntityType.DestructibleDoor:
            case EntityType.HousingMannequin:
                creatureId = reader.ReadUInt(18);
                break;

            case EntityType.Vehicle:
                creatureId = reader.ReadUInt(18);
                _ = reader.ReadUShort(14);
                ReadPassengers(reader);
                break;
            case EntityType.Mount:
                creatureId = reader.ReadUInt(18);
                _ = reader.ReadUShort(14);
                _ = reader.ReadUInt();
                ReadPassengers(reader);
                break;
            case EntityType.Taxi:
                creatureId = reader.ReadUInt(18);
                _ = reader.ReadUShort(14);
                _ = reader.ReadUInt();
                ReadPassengers(reader);
                break;
            case EntityType.InstancePortal:
                creatureId = reader.ReadUInt(18);
                _ = reader.ReadUInt();
                break;
            case EntityType.Plug:
                _ = reader.ReadUShort(14);
                _ = reader.ReadUShort(15);
                _ = reader.ReadByte(6);
                break;
            case EntityType.StructuredPlug:
                creatureId = reader.ReadUInt(18);
                _ = reader.ReadByte(2);
                _ = reader.ReadUShort(14);
                break;
            case EntityType.PinataLoot:
                creatureId = reader.ReadUInt(18);
                _ = reader.ReadUInt();
                _ = reader.ReadUInt();
                _ = reader.ReadByte(4);
                break;
            case EntityType.Player:
                ReadPlayerModel(reader);
                break;
            case EntityType.Trigger:
                ReadTriggerModel(reader);
                break;
            case EntityType.Ghost:
                ReadGhostModel(reader);
                break;
            case EntityType.Pet:
                creatureId = reader.ReadUInt(18);
                _ = reader.ReadUInt();
                _ = reader.ReadUShort(15);
                _ = reader.ReadWideString();
                break;
            case EntityType.EsperPet:
                creatureId = reader.ReadUInt(18);
                _ = reader.ReadUInt();
                _ = reader.ReadUShort(15);
                break;
            case EntityType.WorldUnit:
            case EntityType.Camera:
                _ = reader.ReadBit();
                break;
            case EntityType.ScannerUnit:
                creatureId = reader.ReadUInt(18);
                _ = reader.ReadUInt();
                _ = reader.ReadWideString();
                break;
            case EntityType.Trap:
                creatureId = reader.ReadUInt(18);
                _ = reader.ReadUInt();
                break;
            case EntityType.Pickup:
                creatureId = reader.ReadUInt(18);
                _ = reader.ReadUInt();
                _ = reader.ReadUInt();
                break;
            case EntityType.HousingHarvestPlug:
            case EntityType.HousingPlant:
                _ = reader.ReadUShort(15);
                _ = reader.ReadUShort(14);
                break;
            case EntityType.Lockbox:
                creatureId = reader.ReadUInt(18);
                ushort structureCount = reader.ReadUShort(16);
                for (int i = 0; i < structureCount; i++)
                    _ = reader.ReadUInt();
                break;
            default:
                throw new InvalidDataException($"Unknown entity type {(int)type}.");
        }

        return (creatureId, questChecklistIdx);
    }

    private static void ReadPassengers(GamePacketReader reader)
    {
        byte passengerCount = reader.ReadByte(3);
        for (int i = 0; i < passengerCount; i++)
        {
            _ = reader.ReadByte(2);
            _ = reader.ReadByte(3);
            _ = reader.ReadUInt();
        }
    }

    private static void ReadTriggerModel(GamePacketReader reader)
    {
        _ = reader.ReadWideString();
        byte boundCount = reader.ReadByte();
        for (int i = 0; i < boundCount; i++)
        {
            _ = reader.ReadByte(2);
            for (int coordinate = 0; coordinate < 6; coordinate++)
                _ = reader.ReadSingle();
            _ = reader.ReadSingle();
        }
    }

    private static void ReadGhostModel(GamePacketReader reader)
    {
        _ = reader.ReadULong();
        _ = reader.ReadUShort(14);
        _ = reader.ReadWideString();
        _ = reader.ReadByte(5);
        _ = reader.ReadByte(5);
        _ = reader.ReadByte(2);
        _ = reader.ReadULong();
        _ = reader.ReadWideString();
        _ = reader.ReadByte(4);

        byte guildCount = reader.ReadByte(5);
        for (int i = 0; i < guildCount; i++)
            _ = reader.ReadULong();

        byte boneCount = reader.ReadByte(6);
        for (int i = 0; i < boneCount; i++)
            _ = reader.ReadSingle();

        _ = reader.ReadUShort(14);
    }

    private static void ReadPlayerModel(GamePacketReader reader)
    {
        _ = reader.ReadULong();
        _ = reader.ReadUShort(14);
        _ = reader.ReadWideString();
        _ = reader.ReadByte(5);
        _ = reader.ReadByte(5);
        _ = reader.ReadByte(2);
        _ = reader.ReadULong();

        byte petCount = reader.ReadByte();
        for (int i = 0; i < petCount; i++)
            _ = reader.ReadUInt();

        _ = reader.ReadWideString();
        _ = reader.ReadByte(4);

        byte guildCount = reader.ReadByte(5);
        for (int i = 0; i < guildCount; i++)
            _ = reader.ReadULong();

        byte boneCount = reader.ReadByte(6);
        for (int i = 0; i < boneCount; i++)
            _ = reader.ReadSingle();

        _ = reader.ReadByte(3);
        _ = reader.ReadByte();
        _ = reader.ReadUShort(14);
    }
}
