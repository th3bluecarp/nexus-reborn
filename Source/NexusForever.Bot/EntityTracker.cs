using System.Numerics;
using System.Reflection;
using NexusForever.Game.Static.Entity;
using NexusForever.Game.Static.Entity.Movement.Command;
using NexusForever.Network;
using NexusForever.Network.World.Entity;
using NexusForever.Network.World.Entity.Command;

namespace NexusForever.Bot;

internal sealed record TrackedEntity(uint Guid, EntityType Type, uint? CreatureId, Vector3? Position);

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

    public TrackedEntity ParseCreate(byte[] body)
    {
        using var stream = new MemoryStream(body, writable: false);
        using var reader = new GamePacketReader(stream);

        uint guid = reader.ReadUInt();
        EntityType type = reader.ReadEnum<EntityType>(6);
        uint? creatureId = null;

        switch (type)
        {
            case EntityType.NonPlayer:
                creatureId = reader.ReadUInt(18);
                _ = reader.ReadByte();
                break;
            case EntityType.Player:
                ReadPlayerModel(reader);
                break;
            default:
            {
                var partial = new TrackedEntity(guid, type, null, null);
                entities[guid] = partial;
                return partial;
            }
        }

        _ = reader.ReadByte(8);
        byte statCount = reader.ReadByte(5);
        for (int i = 0; i < statCount; i++)
            ReadStat(reader);

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

        var entity = new TrackedEntity(guid, type, creatureId, position);
        entities[guid] = entity;
        return entity;
    }

    private static void ReadStat(GamePacketReader reader)
    {
        _ = reader.ReadByte(5);
        StatType type = reader.ReadEnum<StatType>(2);
        switch (type)
        {
            case StatType.Integer:
                _ = reader.ReadUInt();
                break;
            case StatType.Float:
                _ = reader.ReadSingle();
                break;
            case StatType.Data:
                _ = reader.ReadUInt();
                _ = reader.ReadUInt();
                break;
            default:
                throw new InvalidDataException($"Unknown stat type {(int)type}.");
        }
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
