using System.Globalization;
using System.Numerics;
using NexusForever.Game.Static;
using NexusForever.Game.Static.Chat;
using NexusForever.Game.Static.Entity;
using NexusForever.Game.Static.Quest;
using NexusForever.GameTable.Model;
using NexusForever.Network;

namespace NexusForever.Bot;

internal sealed record RealmInfo(uint AccountId, byte[] SessionKey, ushort Port, string RealmName);
internal sealed record CharacterCreateResult(ulong CharacterId, uint WorldId, byte Result);
internal sealed record QuestStateUpdate(ushort QuestId, QuestState State, uint QuestObjectiveId);
internal sealed record QuestObjectiveUpdate(ushort QuestId, uint Index, uint Completed);

internal static class PacketCodec
{
    public static byte[] ClientHelloAuth(string email, Guid token) => Write(writer =>
    {
        writer.Write(16042u);
        writer.Write(0x1588ul);
        writer.WriteStringFixed(email);
        writer.WriteBytes(Guid.NewGuid().ToByteArray());
        writer.WriteBytes(token.ToByteArray());
        writer.Write(0u);
        writer.Write(Language.English, 32);
        writer.Write(0u);
        writer.Write(0u);

        writer.WriteStringWide("GenuineIntel");
        writer.WriteStringWide("Headless CPU");
        writer.WriteStringWide("NexusForever.Bot");
        writer.Write(6u);
        writer.Write(6u);
        writer.Write(0u);
        writer.Write(3000u);
        writer.Write(4u);
        writer.Write(8192u);
        writer.WriteStringWide("Headless Adapter");
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(512u);
        writer.Write(0x10009u);
        writer.Write(10u << 16);
        writer.Write(0u);
        writer.Write(1u);
        writer.Write(9u);
    });

    public static byte[] ClientHelloRealm(uint accountId, byte[] sessionKey, string email) => Write(writer =>
    {
        writer.Write(accountId);
        writer.WriteBytes(sessionKey, 16);
        writer.Write(0ul);
        writer.WriteStringWide(email);
        writer.Write(3u);
    });

    public static byte[] ClientCharacterCreate(CharacterCreationEntry creation, string name) => Write(writer =>
    {
        writer.Write(creation.Id);
        writer.WriteStringWide(name);
        writer.Write((byte)0, 3);
        writer.Write(0u);
        writer.Write(0u);
    });

    public static byte[] ClientCharacterSelect(ulong characterId) => Write(writer => writer.Write(characterId));
    public static byte[] ClientEnteredWorld(ushort worldZoneId = 0) => Write(writer => writer.Write(worldZoneId, 15));
    public static byte[] ClientCharacterDelete(ulong characterId) => Write(writer => writer.Write(characterId));

    public static byte[] ClientChat(string command, ushort messageId) => Write(writer =>
    {
        writer.Write(ChatChannelType.Command, 14);
        writer.Write(0ul);
        writer.WriteStringWide(command);
        writer.Write((byte)0, 5);
        writer.Write(messageId);
    });

    public static byte[] ClientEntityInteract(uint guid, InteractionType type) => Write(writer =>
    {
        writer.Write(guid);
        writer.Write(type, 7);
    });

    public static byte[] ClientQuestAccept(ushort questId) => Write(writer =>
    {
        writer.Write((ushort)300, 9);
        writer.Write(0u);
        writer.Write(questId, 15);
    });

    public static byte[] ClientQuestComplete(ushort questId) => Write(writer =>
    {
        writer.Write(questId, 15);
        writer.Write((ushort)0, 15);
        writer.Write(false);
    });

    public static byte[] ClientActivateUnit(uint guid) => Write(writer => writer.Write(guid));
    public static byte[] ClientEntitySelect(uint guid) => Write(writer => writer.Write(guid));

    public static byte[] ClientActivateUnitCast(uint uniqueId, uint guid) => Write(writer =>
    {
        writer.Write(uniqueId);
        writer.Write(guid);
    });

    public static byte[] ClientCastSpell(uint uniqueId, ushort bagIndex, uint targetGuid) => Write(writer =>
    {
        writer.Write(uniqueId);
        writer.Write(bagIndex);
        writer.Write(targetGuid);
        writer.Write(true);
    });

    public static uint ParseAuthDenied(byte[] body) => Read(body, reader => reader.ReadUInt());

    public static RealmInfo ParseRealmInfo(byte[] body) => Read(body, reader =>
    {
        _ = reader.ReadUInt();
        ushort port = reader.ReadUShort();
        byte[] sessionKey = reader.ReadBytes(16);
        uint accountId = reader.ReadUInt();
        string realmName = reader.ReadWideString();
        _ = reader.ReadUInt();
        _ = reader.ReadByte(2);
        _ = reader.ReadUInt(21);
        return new RealmInfo(accountId, sessionKey, port, realmName);
    });

    public static CharacterCreateResult ParseCharacterCreate(byte[] body) => Read(body, reader =>
        new CharacterCreateResult(reader.ReadULong(), reader.ReadUInt(), reader.ReadByte(3)));

    public static (ushort WorldId, Vector3 Position) ParseChangeWorld(byte[] body) => Read(body, reader =>
        (reader.ReadUShort(15), reader.ReadVector3()));

    public static uint ParseMovementControl(byte[] body) => Read(body, reader =>
    {
        _ = reader.ReadUInt();
        _ = reader.ReadBit();
        return reader.ReadUInt();
    });

    public static uint ParsePlayerChanged(byte[] body) => Read(body, reader => reader.ReadUInt());

    public static uint ParseEntityDestroy(byte[] body) => Read(body, reader => reader.ReadUInt());

    public static QuestStateUpdate ParseQuestState(byte[] body) => Read(body, reader =>
        new QuestStateUpdate(reader.ReadUShort(15), reader.ReadEnum<QuestState>(32), reader.ReadUInt()));

    public static QuestObjectiveUpdate ParseQuestObjective(byte[] body) => Read(body, reader =>
        new QuestObjectiveUpdate(reader.ReadUShort(15), reader.ReadUInt(), reader.ReadUInt()));

    public static byte ParseCharacterDeleteResult(byte[] body) => Read(body, reader => reader.ReadByte(6));

    public static (uint Position, uint WaitSeconds, bool IsGuest) ParseQueueStatus(byte[] body) => Read(body, reader =>
        (reader.ReadUInt(), reader.ReadUInt(), reader.ReadBit()));

    public static string ParseServerChatText(byte[] body) => Read(body, reader =>
    {
        _ = reader.ReadEnum<ChatChannelType>(14);
        _ = reader.ReadULong();
        _ = reader.ReadBit();
        _ = reader.ReadBit();
        _ = reader.ReadBit();
        _ = reader.ReadUShort(14);
        _ = reader.ReadULong();
        _ = reader.ReadWideString();
        _ = reader.ReadWideString();
        _ = reader.ReadByte(3);
        return reader.ReadWideString();
    });

    public static string TeleportCommand(Spawn spawn, bool includeWorld)
    {
        string coordinates = string.Create(CultureInfo.InvariantCulture, $"!tele coordinates {spawn.X + 2f:R} {spawn.Y:R} {spawn.Z + 2f:R}");
        return includeWorld ? $"{coordinates} {spawn.WorldId}" : coordinates;
    }

    public static string TeleportCommand(WorldLocation2Entry location, bool includeWorld)
    {
        string coordinates = string.Create(CultureInfo.InvariantCulture, $"!tele coordinates {location.Position0:R} {location.Position1:R} {location.Position2:R}");
        return includeWorld ? $"{coordinates} {location.WorldId}" : coordinates;
    }

    private static byte[] Write(Action<GamePacketWriter> action)
    {
        using var stream = new MemoryStream();
        using var writer = new GamePacketWriter(stream);
        action(writer);
        writer.FlushBits();
        return stream.ToArray();
    }

    private static T Read<T>(byte[] body, Func<GamePacketReader, T> action)
    {
        using var stream = new MemoryStream(body, writable: false);
        using var reader = new GamePacketReader(stream);
        return action(reader);
    }
}
