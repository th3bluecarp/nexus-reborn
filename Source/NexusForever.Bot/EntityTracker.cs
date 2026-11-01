using System.Numerics;
using System.Reflection;
using NexusForever.Game.Static.Entity;
using NexusForever.Game.Static.Entity.Movement.Command;
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
    uint? Health);

internal sealed class EntityTracker
{
    private static readonly IReadOnlyDictionary<EntityCommand, Type> CommandTypes = typeof(SetPositionCommand).Assembly
        .GetTypes()
        .Select(type => (Type: type, Attribute: type.GetCustomAttribute<EntityCommandAttribute>()))
        .Where(pair => pair.Attribute is not null && typeof(IEntityCommandModel).IsAssignableFrom(pair.Type))
        .ToDictionary(pair => pair.Attribute!.Command, pair => pair.Type);

    private readonly Dictionary<uint, TrackedEntity> entities = [];

    public void Clear() => entities.Clear();
    public void Remove(uint guid) => entities.Remove(guid);

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

    public TrackedEntity? FindCreature(uint creatureId, Spawn? expected = null)
    {
        IEnumerable<TrackedEntity> matches = entities.Values.Where(entity => entity.CreatureId == creatureId);
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
            .Where(entity => entity.Type == EntityType.NonPlayer
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
            && entity.Type == EntityType.NonPlayer
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
                position = setPosition.Position;
        }

        var entity = new TrackedEntity(
            guid,
            type,
            creatureId,
            position,
            createFlags,
            questChecklistIdx,
            level,
            health);
        entities[guid] = entity;
        return entity;
    }

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
