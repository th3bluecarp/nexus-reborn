using System.Numerics;
using NexusForever.Database.Auth;
using NexusForever.Database.Character;
using NexusForever.Database.Character.Model;
using NexusForever.Game.Abstract.Account;
using NexusForever.Game.Abstract.Achievement;
using NexusForever.Game.Abstract.Guild;
using NexusForever.Game.Abstract.Housing;
using NexusForever.Game.Abstract.Map;
using NexusForever.Game.Abstract.Map.Lock;
using NexusForever.Game.Abstract.Pvp;
using NexusForever.Game.Abstract.Trade;
using NexusForever.Game.Abstract.Reputation;
using NexusForever.Game.Static.Entity;
using NexusForever.Game.Static.PVP;
using NexusForever.Game.Static.Setting;
using NexusForever.Game.Static.Option;
using NexusForever.Network.World.Message.Model;
using NexusForever.GameTable.Model;
using NexusForever.Network.Session;
using NexusForever.Network.World.Message.Static;

namespace NexusForever.Game.Abstract.Entity
{
    public interface IPlayer : IUnitEntity, IDatabaseAuth, IDatabaseCharacter
    {
        IAccount Account { get; }

        Identity Identity { get; }
        ulong CharacterId { get; }
        string Name { get; }
        Sex Sex { get; set; }
        Race Race { get; set; }
        Class Class { get; }
        CharacterFlag Flags { get; set; }
        Static.PlayerPath.Path Path { get; set; }
        DateTime PathActivatedTime { get; }
        InputSets InputKeySet { get; set; }

        DateTime CreateTime { get; }
        double TimePlayedTotal { get; }
        double TimePlayedLevel { get; }
        double TimePlayedSession { get; }

        void Initialise(IGameSession session, IAccount account, CharacterModel model);

        /// <summary>
        /// Guid of the <see cref="IWorldEntity"/> that currently being controlled by the <see cref="IPlayer"/>.
        /// </summary>
        uint? ControlGuid { get; }

        /// <summary>
        /// Guid of the <see cref="IPetEntity"/> currently summoned by the <see cref="IPlayer"/>.
        /// </summary>
        uint? VanityPetGuid { get; set; }

        /// <summary>
        /// Id of the primary group that <see cref="IPlayer"/> is associated with.
        /// </summary>
        ulong GroupAssociation { get; set; }

        bool IsSitting { get; }

        /// <summary>
        /// Returns if <see cref="IPlayer"/> has premium signature subscription.
        /// </summary>
        bool SignatureEnabled { get; }

        PvPFlag PvPFlags { get; set; }

        IGameSession Session { get; }

        /// <summary>
        /// Returns if <see cref="IPlayer"/>'s client is currently in a loading screen.
        /// </summary>
        bool IsLoading { get; set; }

        IInventory Inventory { get; }
        ICurrencyManager CurrencyManager { get; }
        IPathManager PathManager { get; }
        ITitleManager TitleManager { get; }
        ISpellManager SpellManager { get; }
        ICostumeManager CostumeManager { get; }
        IPetCustomisationManager PetCustomisationManager { get; }
        ICharacterKeybindingManager KeybindingManager { get; }
        IDatacubeManager DatacubeManager { get; }
        IPathMissionManager PathMissionManager { get; }

        /// <summary>
        /// Send every visible world entity again so the client re-evaluates state it only checks on unit creation.
        /// </summary>
        void ResendVisibleEntities();
        IChallengeManager ChallengeManager { get; }
        ITradeskillManager TradeskillManager { get; }
        IArchiveManager ArchiveManager { get; }
        IMailManager MailManager { get; }
        IZoneMapManager ZoneMapManager { get; }
        IQuestManager QuestManager { get; }
        ICharacterAchievementManager AchievementManager { get; }
        ISupplySatchelManager SupplySatchelManager { get; }
        IXpManager XpManager { get; }
        IReputationManager ReputationManager { get; }
        IGuildManager GuildManager { get; }
        IResidenceManager ResidenceManager { get; }
        ICinematicManager CinematicManager { get; }
        ICharacterEntitlementManager EntitlementManager { get; }
        ILogoutManager LogoutManager { get; }
        IAppearanceManager AppearanceManager { get; }
        IResurrectionManager ResurrectionManager { get; }

        IVendorInfo SelectedVendorInfo { get; set; }

        /// <summary>
        /// Duel this <see cref="IPlayer"/> is in or has been challenged to.
        /// </summary>
        IDuel Duel { get; set; }

        /// <summary>
        /// Duel challenges are refused while set (Ignore Duel Requests option).
        /// </summary>
        bool IgnoreDuelRequests { get; set; }

        /// <summary>
        /// Trade this <see cref="IPlayer"/> is in or has been invited to.
        /// </summary>
        IPlayerTrade Trade { get; set; }

        /// <summary>
        /// Multiplier on vendor purchase prices from VendorPriceModifier spell effects, 1 without any.
        /// </summary>
        float VendorBuyMultiplier { get; set; }

        /// <summary>
        /// Multiplier on what vendors pay from VendorPriceModifier spell effects, 1 without any.
        /// </summary>
        float VendorSellMultiplier { get; set; }

        /// <summary>
        /// Extra fraction of earned XP from active XP boost spells (RewardPropertyModifier), 0 without any.
        /// </summary>
        float XpBoost { get; set; }

        /// <summary>
        /// Combat log entry kinds the client asked not to be sent.
        /// </summary>
        CombatLogOptions CombatLogDisables { get; set; }

        /// <summary>
        /// Combat log entries from other players are not sent while set.
        /// </summary>
        bool CombatLogDisableOthers { get; set; }

        /// <summary>
        /// Returns if the supplied combat log entry broadcast by the supplied entity is filtered out by this player's combat log options.
        /// </summary>
        bool IsCombatLogFiltered(ServerCombatLog combatLog, IWorldEntity source);

        /// <summary>
        /// Save <see cref="IPlayer"/> to database, invoke supplied <see cref="Action"/> once save is complete.
        /// </summary>
        /// <remarks>
        /// This is a delayed save, <see cref="AuthContext"/> changes are saved first followed by <see cref="CharacterContext"/> changes.
        /// Packets for session will not be handled until save is complete.
        /// </remarks>
        void Save(Action callback = null);

        /// <summary>
        /// Track a summoned combat pet so the client pet frame is cleared when it goes away.
        /// </summary>
        void AddCombatPet(uint guid);

        /// <summary>
        /// Bind the player to the transmat terminal at the supplied position, Recall returns here.
        /// </summary>
        void SetBindPoint(ushort bindPointId, ushort worldId, System.Numerics.Vector3 position);

        /// <summary>
        /// Return the bound world and position, false when the player never bound.
        /// </summary>
        bool TryGetBindPoint(out ushort worldId, out System.Numerics.Vector3 position);

        /// <summary>
        /// Guids of the combat pets currently summoned by the player.
        /// </summary>
        IReadOnlyCollection<uint> GetCombatPets();

        /// <summary>
        /// Save <see cref="IPlayer"/> to database.
        /// </summary>
        /// <remarks>
        /// This is an instant save, <see cref="AuthContext"/> changes are saved first followed by <see cref="CharacterContext"/> changes.
        /// This will block the calling thread until the database save is complete. 
        /// </remarks>
        void SaveDirect();

        ItemProficiency GetItemProficiencies();

        /// <summary>
        /// Set the <see cref="IWorldEntity"/> that currently being controlled by the <see cref="IPlayer"/>.
        /// </summary>
        void SetControl(IWorldEntity entity);

        /// <summary>
        /// Returns if <see cref="IPlayer"/> can teleport.
        /// </summary>
        bool CanTeleport();

        /// <summary>
        /// Teleport <see cref="IPlayer"/> to supplied location.
        /// </summary>
        void TeleportTo(ushort worldId, float x, float y, float z, IMapLock mapLock = null, TeleportReason reason = TeleportReason.Relocate);

        /// <summary>
        /// Teleport <see cref="IPlayer"/> to supplied location.
        /// </summary>
        void TeleportTo(WorldEntry entry, Vector3 position, IMapLock mapLock = null, TeleportReason reason = TeleportReason.Relocate);

        /// <summary>
        /// Teleport <see cref="IPlayer"/> to supplied location.
        /// </summary>
        void TeleportTo(IMapPosition mapPosition, TeleportReason reason = TeleportReason.Relocate);

        /// <summary>
        /// Show loading screen for supplied <see cref="IMapPosition"/>.
        /// </summary>
        void ShowLoadingScreen(IMapPosition position);

        /// <summary>
        /// Teleport <see cref="IPlayer"/> to supplied location.
        /// </summary>
        void TeleportToLocal(Vector3 position, bool showLoadingScreen = true, Action<Vector3> callback = null);

        /// <summary>
        /// Invoked when <see cref="IPlayer"/> has finished loading and is ready to enter world.
        /// </summary>
        void OnEnteredWorld();

        /// <summary>
        /// Make <see cref="IPlayer"/> sit on provided <see cref="IWorldEntity"/>.
        /// </summary>
        void Sit(IWorldEntity chair);

        /// <summary>
        /// Remove <see cref="IPlayer"/> from the <see cref="IWorldEntity"/> it is sitting on.
        /// </summary>
        void Unsit();

        void SendGenericError(GenericError error);
        void SendSystemMessage(string text);

        /// <summary>
        /// Returns whether this <see cref="IPlayer"/> is allowed to summon or be added to a vehicle.
        /// </summary>
        bool CanMount();

        /// <summary>
        /// Dismounts this <see cref="IPlayer"/> from a vehicle that it's attached to
        /// </summary>
        void Dismount();

        /// <summary>
        /// Returns the time in seconds that has past since the last <see cref="IPlayer"/> save.
        /// </summary>
        double GetTimeSinceLastSave();

        /// <summary>
        /// Add a new <see cref="CharacterFlag"/>.
        /// </summary>
        void SetFlag(CharacterFlag flag);

        /// <summary>
        /// Remove an existing <see cref="CharacterFlag"/>.
        /// </summary>
        void RemoveFlag(CharacterFlag flag);

        /// <summary>
        /// Returns if supplied <see cref="CharacterFlag"/> exists.
        /// </summary>
        bool HasFlag(CharacterFlag flag);

        void SendCharacterFlagsUpdated();

        /// <summary>
        /// Add a <see cref="Property"/> modifier given a <see cref="ItemSlot"/> and value.
        /// </summary>
        void AddItemProperty(Property property, ItemSlot itemSlot, float value);

        /// <summary>
        /// Remove a <see cref="Property"/> modifier by a item that is currently affecting this <see cref="IPlayer"/>.
        /// </summary>
        void RemoveItemProperty(Property property, ItemSlot itemSlot);
    }
}
