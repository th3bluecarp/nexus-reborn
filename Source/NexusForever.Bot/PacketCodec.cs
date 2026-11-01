using System.Globalization;
using System.Numerics;
using NexusForever.Game.Static;
using NexusForever.Game.Static.Chat;
using NexusForever.Game.Static.Entity;
using NexusForever.Game.Static.Entity.Movement.Command;
using NexusForever.Game.Static.Loot;
using NexusForever.Game.Static.PlayerPath;
using NexusForever.Game.Static.Quest;
using NexusForever.Game.Static.Spell;
using NexusForever.GameTable.Model;
using NexusForever.Network;
using NexusForever.Network.World.Message.Static;
using NexusForever.Network.World.Entity.Command;

namespace NexusForever.Bot;

internal sealed record RealmInfo(uint AccountId, byte[] SessionKey, ushort Port, string RealmName);
internal sealed record CharacterCreateResult(ulong CharacterId, uint WorldId, byte Result);
internal sealed record QuestStateUpdate(ushort QuestId, QuestState State, uint QuestObjectiveId);
internal sealed record QuestObjectiveUpdate(ushort QuestId, uint Index, uint Completed);
internal sealed record SpellCastStart(uint ServerUniqueId, uint CasterId, uint PrimaryTargetId, uint? ClientUniqueId);
internal sealed record SpellCastResult(uint ClientUniqueId, uint Spell4Id, CastResult Result);
internal sealed record ActionSetSlot(ushort BagIndex, ShortcutType ShortcutType, uint ObjectId);
internal sealed record ActionSetInfo(byte SpecIndex, bool Unlocked, IReadOnlyList<ActionSetSlot> Slots);
internal sealed record QuestInitInfo(
    IReadOnlySet<ushort> CompletedQuestIds,
    IReadOnlyDictionary<ushort, QuestState> InactiveQuestStates);
internal sealed record ResurrectionShow(
    uint GhostId,
    uint RezCost,
    uint TimeUntilRezMs,
    bool Dead,
    ResurrectionType Flags,
    bool HasCasterRezRequest,
    uint TimeUntilWakeHereMs,
    uint TimeUntilForceRezMs);
internal sealed record EntityStatInteger(uint UnitId, Stat Stat, uint Value);
internal sealed record UnitCombatState(uint UnitId, bool InCombat);
internal sealed record InventoryItemInfo(
    ulong Guid,
    uint ItemId,
    InventoryLocation Location,
    uint BagIndex,
    uint StackCount);
internal sealed record PlayerCreateInfo(byte ActiveSpec, IReadOnlyList<InventoryItemInfo> Inventory);
internal sealed record LootItemInfo(
    uint LootUnitId,
    LootItemType Type,
    uint StaticId,
    uint Amount,
    bool CanLoot,
    bool Delivered);
internal sealed record LootNotification(uint OwnerUnitId, IReadOnlyList<LootItemInfo> Items);
internal sealed record LootGrant(uint OwnerUnitId, uint LooterUnitId, LootItemInfo Item);
internal sealed record PathInitialiseInfo(
    NexusForever.Game.Static.PlayerPath.Path ActivePath,
    IReadOnlyList<uint> Progress,
    PathUnlockedMask UnlockedMask);
internal sealed record PathMissionStatus(
    ushort MissionId,
    bool Completed,
    uint Progress,
    uint StateFlags,
    PathMissionState? State,
    uint GiverUnitId);
internal sealed record PathEpisodeProgress(ushort EpisodeId, IReadOnlyList<PathMissionStatus> Missions);
internal sealed record PathSoldierHoldoutWave(ushort EventId, uint WaveIndex, bool IsBoss);
internal sealed record PathSoldierHoldoutStatus(
    ushort EventId,
    uint UnitCount,
    bool IsBoss,
    PlayerPathSoldierEventMode Mode,
    int DelayTime,
    int WaveIndex,
    int StartTimeOffset);

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

    public static byte[] ClientQuestAccept(ushort questId, InventoryLocation location, uint bagIndex) => Write(writer =>
    {
        writer.Write(location, 9);
        writer.Write(bagIndex);
        writer.Write(questId, 15);
    });

    public static byte[] ClientQuestComplete(ushort questId) => Write(writer =>
    {
        writer.Write(questId, 15);
        writer.Write((ushort)0, 15);
        writer.Write(false);
    });

    public static byte[] ClientQuestAbandon(ushort questId) => Write(writer => writer.Write(questId, 15));

    public static byte[] ClientResurrectAccept(uint unitId, ResurrectionType type) => Write(writer =>
    {
        writer.Write(unitId);
        writer.Write(type, 32);
    });

    public static byte[] ClientActivateUnit(uint guid) => Write(writer => writer.Write(guid));
    public static byte[] ClientEntitySelect(uint guid) => Write(writer => writer.Write(guid));
    public static byte[] ClientItemDelete(InventoryLocation location, uint bagIndex) => Write(writer =>
    {
        writer.Write(location, 9);
        writer.Write(bagIndex);
    });
    public static byte[] ClientLootItem(uint ownerUnitId, uint lootUnitId) => Write(writer =>
    {
        writer.Write(ownerUnitId);
        writer.Write(lootUnitId);
        writer.Write(false); // ClientLootItem.Request: false collects the item.
    });

    public static byte[] ClientEntityCommand(Vector3 position, Vector3 rotation) => Write(writer =>
    {
        writer.Write(unchecked((uint)Environment.TickCount64));
        writer.Write(2u);

        writer.Write(EntityCommand.SetPosition, 5);
        new SetPositionCommand
        {
            Position = position,
            Blend = false
        }.Write(writer);

        writer.Write(EntityCommand.SetRotation, 5);
        new SetRotationCommand
        {
            Rotation = rotation,
            Blend = false
        }.Write(writer);
    });

    public static byte[] ClientActivateUnitCast(uint uniqueId, uint guid) => Write(writer =>
    {
        writer.Write(uniqueId);
        writer.Write(guid);
    });

    public static byte[] ClientSpellClientSideInteraction(uint serverUniqueId) => Write(writer =>
    {
        writer.Write(serverUniqueId);
        writer.Write((byte)1, 3);
        writer.Write(0u);
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

    public static PlayerCreateInfo ParsePlayerCreate(byte[] body) => Read(body, reader =>
    {
        uint inventoryCount = reader.ReadUInt();
        var inventory = new List<InventoryItemInfo>(checked((int)inventoryCount));
        for (uint i = 0; i < inventoryCount; i++)
            inventory.Add(ReadInventoryItem(reader));
        for (int i = 0; i < 16; i++)
            _ = reader.ReadULong();
        _ = reader.ReadUInt();
        _ = reader.ReadUInt();
        _ = reader.ReadUInt();
        _ = reader.ReadUInt();
        _ = reader.ReadUInt();
        return new PlayerCreateInfo(reader.ReadByte(3), inventory);
    });

    public static ActionSetInfo ParseActionSet(byte[] body) => Read(body, reader =>
    {
        byte specIndex = reader.ReadByte(3);
        bool unlocked = reader.ReadByte(2) != 0;
        _ = reader.ReadByte(6);
        byte count = reader.ReadByte(6);
        var slots = new List<ActionSetSlot>(count);
        for (int i = 0; i < count; i++)
        {
            ShortcutType shortcutType = reader.ReadEnum<ShortcutType>(4);
            _ = reader.ReadEnum<InventoryLocation>(9);
            uint bagIndex = reader.ReadUInt();
            uint objectId = reader.ReadUInt();
            if (bagIndex <= ushort.MaxValue)
                slots.Add(new ActionSetSlot((ushort)bagIndex, shortcutType, objectId));
        }
        return new ActionSetInfo(specIndex, unlocked, slots);
    });

    public static uint ParseEntityDestroy(byte[] body) => Read(body, reader => reader.ReadUInt());

    public static QuestStateUpdate ParseQuestState(byte[] body) => Read(body, reader =>
        new QuestStateUpdate(reader.ReadUShort(15), reader.ReadEnum<QuestState>(32), reader.ReadUInt()));

    public static QuestObjectiveUpdate ParseQuestObjective(byte[] body) => Read(body, reader =>
        new QuestObjectiveUpdate(reader.ReadUShort(15), reader.ReadUInt(), reader.ReadUInt()));

    public static GenericError ParseGenericError(byte[] body) => Read(body, reader => reader.ReadEnum<GenericError>(8));

    public static SpellCastStart ParseSpellStart(byte[] body) => Read(body, reader =>
    {
        uint serverUniqueId = reader.ReadUInt();
        _ = reader.ReadUInt(18);
        _ = reader.ReadUInt(18);
        _ = reader.ReadUInt(18);
        uint casterId = reader.ReadUInt();
        _ = reader.ReadUShort();
        uint primaryTargetId = reader.ReadUInt();
        return new SpellCastStart(serverUniqueId, casterId, primaryTargetId, null);
    });

    public static SpellCastStart ParseSpellConfirmCast(byte[] body) => Read(body, reader =>
    {
        uint clientUniqueId = reader.ReadUInt();
        uint serverUniqueId = reader.ReadUInt();
        uint casterId = reader.ReadUInt();
        return new SpellCastStart(serverUniqueId, casterId, 0u, clientUniqueId);
    });

    public static SpellCastResult ParseSpellCastResult(byte[] body) => Read(body, reader =>
        new SpellCastResult(reader.ReadUInt(), reader.ReadUInt(18), reader.ReadEnum<CastResult>(9)));

    public static uint ParseSpellFinish(byte[] body) => Read(body, reader => reader.ReadUInt());

    public static (uint UnitId, bool Dead) ParseEntityDeathState(byte[] body) => Read(body, reader =>
        (reader.ReadUInt(), reader.ReadBit()));

    public static ResurrectionShow ParseResurrectionShow(byte[] body) => Read(body, reader =>
    {
        uint ghostId = reader.ReadUInt();
        uint rezCost = reader.ReadUInt();
        uint timeUntilRezMs = reader.ReadUInt();
        bool dead = reader.ReadBit();
        ResurrectionType flags = reader.ReadEnum<ResurrectionType>(8);
        bool hasCasterRezRequest = reader.ReadBit();
        uint timeUntilWakeHereMs = reader.ReadUInt();
        uint timeUntilForceRezMs = reader.ReadUInt();
        return new ResurrectionShow(
            ghostId,
            rezCost,
            timeUntilRezMs,
            dead,
            flags,
            hasCasterRezRequest,
            timeUntilWakeHereMs,
            timeUntilForceRezMs);
    });

    public static (uint UnitId, uint Health) ParseEntityHealthUpdate(byte[] body) => Read(body, reader =>
        (reader.ReadUInt(), reader.ReadUInt()));

    public static UnitCombatState ParseUnitCombatState(byte[] body) => Read(body, reader =>
        new UnitCombatState(reader.ReadUInt(), reader.ReadBit()));

    public static EntityStatInteger ParseEntityStatInteger(byte[] body) => Read(body, reader =>
        new EntityStatInteger(reader.ReadUInt(), reader.ReadEnum<Stat>(5), reader.ReadUInt()));

    public static LootNotification ParseLootNotify(byte[] body) => Read(body, reader =>
    {
        uint ownerUnitId = reader.ReadUInt();
        _ = reader.ReadUInt(); // ParentUnitId
        _ = reader.ReadBit(); // Explosion
        uint count = reader.ReadUInt();
        var items = new List<LootItemInfo>(checked((int)count));
        for (uint i = 0; i < count; i++)
            items.Add(ReadLootItem(reader));
        return new LootNotification(ownerUnitId, items);
    });

    public static LootGrant ParseLootGrant(byte[] body) => Read(body, reader =>
        new LootGrant(reader.ReadUInt(), reader.ReadUInt(), ReadLootItem(reader)));

    public static uint ParseLootRemove(byte[] body) => Read(body, reader => reader.ReadUInt());

    public static InventoryItemInfo ParseItemAdd(byte[] body) => Read(body, ReadInventoryItem);

    public static (ulong Guid, uint StackCount) ParseItemStackCountUpdate(byte[] body) => Read(body, reader =>
    {
        ulong guid = reader.ReadULong();
        uint stackCount = reader.ReadUInt();
        _ = reader.ReadByte(6); // ItemUpdateReason
        return (guid, stackCount);
    });

    public static ulong ParseItemDelete(byte[] body) => Read(body, reader =>
    {
        ulong guid = reader.ReadULong();
        _ = reader.ReadByte(6); // ItemUpdateReason
        return guid;
    });

    public static QuestInitInfo ParseQuestInit(byte[] body) => Read(body, reader =>
    {
        var completed = new HashSet<ushort>();
        var inactive = new Dictionary<ushort, QuestState>();

        uint completedCount = reader.ReadUInt();
        for (uint i = 0; i < completedCount; i++)
        {
            completed.Add(reader.ReadUShort(15));
            _ = reader.ReadBit();
        }

        uint inactiveCount = reader.ReadUInt();
        for (uint i = 0; i < inactiveCount; i++)
        {
            ushort questId = reader.ReadUShort(15);
            inactive[questId] = reader.ReadEnum<QuestState>(4);
        }

        uint activeCount = reader.ReadUInt();
        for (uint i = 0; i < activeCount; i++)
        {
            _ = reader.ReadUShort(15);
            _ = reader.ReadByte(4);
            _ = reader.ReadUInt();
            _ = reader.ReadUInt();
            uint objectiveCount = reader.ReadUInt();
            for (uint objective = 0; objective < objectiveCount; objective++)
                _ = reader.ReadUInt();
            _ = reader.ReadUInt();
            for (uint objective = 0; objective < objectiveCount; objective++)
                _ = reader.ReadUInt();
        }

        _ = reader.ReadULong();
        return new QuestInitInfo(completed, inactive);
    });

    public static PathInitialiseInfo ParsePathInitialise(byte[] body) => Read(body, reader =>
    {
        NexusForever.Game.Static.PlayerPath.Path activePath = reader.ReadEnum<NexusForever.Game.Static.PlayerPath.Path>(3);
        uint[] progress = Enumerable.Range(0, 4).Select(_ => reader.ReadUInt()).ToArray();
        PathUnlockedMask mask = reader.ReadEnum<PathUnlockedMask>(4);
        _ = reader.ReadSingle();
        return new PathInitialiseInfo(activePath, progress, mask);
    });

    public static ushort ParsePathCurrentEpisode(byte[] body) => Read(body, reader =>
    {
        _ = reader.ReadUShort(15);
        return reader.ReadUShort(14);
    });

    public static PathEpisodeProgress ParsePathEpisodeProgress(byte[] body) => Read(body, reader =>
    {
        ushort episodeId = reader.ReadUShort(14);
        ushort count = reader.ReadUShort(16);
        var missions = new List<PathMissionStatus>(count);
        for (int i = 0; i < count; i++)
            missions.Add(ReadPathMission(reader, includeState: false));
        return new PathEpisodeProgress(episodeId, missions);
    });

    public static IReadOnlyList<PathMissionStatus> ParsePathMissionActivate(byte[] body) => Read(body, reader =>
    {
        uint count = reader.ReadUInt();
        var missions = new List<PathMissionStatus>(checked((int)count));
        for (uint i = 0; i < count; i++)
            missions.Add(ReadPathMission(reader, includeState: true));
        return missions;
    });

    public static PathMissionStatus ParsePathMissionUpdate(byte[] body)
        => Read(body, reader => ReadPathMission(reader, includeState: false));

    public static ushort ParsePathMissionAdvanced(byte[] body)
        => Read(body, reader => reader.ReadUShort(14));

    public static PathSoldierHoldoutWave ParsePathSoldierHoldoutNextWave(byte[] body) => Read(body, reader =>
        new PathSoldierHoldoutWave(reader.ReadUShort(14), reader.ReadUInt(), reader.ReadBit()));

    public static PathSoldierHoldoutStatus ParsePathSoldierHoldoutStatus(byte[] body) => Read(body, reader =>
    {
        ushort eventId = reader.ReadUShort(14);
        uint unitCount = reader.ReadUInt();
        for (uint i = 0; i < unitCount; i++)
        {
            _ = reader.ReadUInt();
            _ = reader.ReadEnum<TowerDefenseUnitType>(32);
        }
        _ = reader.ReadUInt();
        bool isBoss = reader.ReadBit();
        PlayerPathSoldierEventMode mode = reader.ReadEnum<PlayerPathSoldierEventMode>(32);
        int delayTime = reader.ReadInt();
        int waveIndex = reader.ReadInt();
        _ = reader.ReadSingle();
        _ = reader.ReadSingle();
        int startTimeOffset = reader.ReadInt();
        return new PathSoldierHoldoutStatus(
            eventId,
            unitCount,
            isBoss,
            mode,
            delayTime,
            waveIndex,
            startTimeOffset);
    });

    public static ushort ParsePathSoldierHoldoutDeath(byte[] body)
        => Read(body, reader => reader.ReadUShort(14));

    public static (ushort EventId, PlayerPathSoldierResult Result) ParsePathSoldierHoldoutEnd(byte[] body)
        => Read(body, reader => (reader.ReadUShort(14), reader.ReadEnum<PlayerPathSoldierResult>(32)));

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

    public static string TeleportCommand(Vector3 position)
        => string.Create(CultureInfo.InvariantCulture, $"!tele coordinates {position.X + 2f:R} {position.Y:R} {position.Z + 2f:R}");

    public static string TeleportCommand(WorldLocation2Entry location, bool includeWorld, float offsetX = 0f)
    {
        string coordinates = string.Create(CultureInfo.InvariantCulture, $"!tele coordinates {location.Position0 + offsetX:R} {location.Position1:R} {location.Position2:R}");
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

    private static PathMissionStatus ReadPathMission(GamePacketReader reader, bool includeState)
    {
        ushort missionId = reader.ReadUShort(15);
        bool completed = reader.ReadBit();
        uint progress = reader.ReadUInt();
        uint stateFlags = reader.ReadUInt();
        PathMissionState? state = includeState ? reader.ReadEnum<PathMissionState>(3) : null;
        uint giverUnitId = includeState ? reader.ReadUInt() : 0u;
        return new PathMissionStatus(missionId, completed, progress, stateFlags, state, giverUnitId);
    }

    private static InventoryItemInfo ReadInventoryItem(GamePacketReader reader)
    {
        ulong guid = reader.ReadULong();
        _ = reader.ReadULong();
        uint itemId = reader.ReadUInt(18);
        InventoryLocation location = reader.ReadEnum<InventoryLocation>(9);
        uint bagIndex = reader.ReadUInt();
        uint stackCount = reader.ReadUInt();
        _ = reader.ReadUInt();
        _ = reader.ReadULong();
        _ = reader.ReadUInt();
        _ = reader.ReadULong();
        _ = reader.ReadSingle();
        _ = reader.ReadUInt();
        _ = reader.ReadByte();
        _ = reader.ReadUInt();
        _ = reader.ReadUInt();
        _ = reader.ReadUInt();
        for (int i = 0; i < 2; i++)
        {
            _ = reader.ReadByte(3);
            _ = reader.ReadUInt();
            _ = reader.ReadUInt();
        }
        _ = reader.ReadUInt(18);
        byte microchipCount = reader.ReadByte(3);
        for (int i = 0; i < microchipCount; i++)
            _ = reader.ReadUInt();
        byte glyphCount = reader.ReadByte(4);
        for (int i = 0; i < glyphCount; i++)
            _ = reader.ReadUInt();
        byte unknownCount = reader.ReadByte(6);
        for (int i = 0; i < unknownCount; i++)
        {
            _ = reader.ReadUShort(14);
            _ = reader.ReadULong();
        }
        _ = reader.ReadUInt();
        _ = reader.ReadByte(6);
        return new InventoryItemInfo(guid, itemId, location, bagIndex, stackCount);
    }

    private static LootItemInfo ReadLootItem(GamePacketReader reader)
    {
        uint lootUnitId = reader.ReadUInt();
        LootItemType type = reader.ReadEnum<LootItemType>(32);
        uint staticId = reader.ReadUInt();
        uint amount = reader.ReadUInt();
        bool canLoot = reader.ReadBit();
        _ = reader.ReadBit(); // RequiresRoll
        _ = reader.ReadBit(); // OnlyMasterLootable
        bool delivered = reader.ReadBit();
        _ = reader.ReadUInt(); // RollTime
        _ = reader.ReadULong(); // RandomCircuitData
        _ = reader.ReadUInt(); // RandomGlyphData
        _ = reader.ReadUInt(); // ItemQuality2Id
        uint masterCount = reader.ReadUInt();
        for (uint i = 0; i < masterCount; i++)
        {
            _ = reader.ReadUShort(14); // Identity.RealmId
            _ = reader.ReadULong(); // Identity.Id
        }
        return new LootItemInfo(lootUnitId, type, staticId, amount, canLoot, delivered);
    }
}
