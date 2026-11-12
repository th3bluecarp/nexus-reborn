using System.Collections.Immutable;
using NexusForever.Database.World.Model;
using NexusForever.Game.Abstract.Entity.Creature;
using NexusForever.Game.Static.Entity;
using NexusForever.GameTable;
using NexusForever.GameTable.Model;
using NexusForever.Shared;

namespace NexusForever.Game.Entity.Creature
{
    public class CreatureInfo : ICreatureInfo
    {
        public Creature2Entry Entry { get; private set; }
        public Creature2DifficultyEntry DifficultyEntry { get; private set; }
        public Creature2ArcheTypeEntry ArcheTypeEntry { get; private set; }
        public Creature2TierEntry TierEntry { get; private set; }
        public Creature2ModelInfoEntry ModelEntry { get; private set; }
        public UnitVehicleEntry UnitVehicleEntry { get; private set; }
        public PrerequisiteEntry PrerequisiteVisibilityEntry { get; private set; }

        private ImmutableList<Creature2DisplayGroupEntryEntry> displayGroup;
        private ImmutableList<Creature2OutfitGroupEntryEntry> outfitGroup;
        private ImmutableList<PrerequisiteEntry> prerequisitePriorityEntries;
        private ImmutableDictionary<Property, ICreatureInfoProperty> overrideProperties;
        private ImmutableDictionary<Static.Entity.Stat, ICreatureInfoStat> overrideStats;

        #region Dependency Injection

        private readonly IGameTableManager gameTableManager;
        private readonly IFactory<ICreatureInfoProperty> creatureInfoPropertyFactory;
        private readonly IFactory<ICreatureInfoStat> creatureInfoStatFactory;

        public CreatureInfo(
            IGameTableManager gameTableManager,
            IFactory<ICreatureInfoProperty> creatureInfoPropertyFactory,
            IFactory<ICreatureInfoStat> creatureInfoStatFactory)
        {
            this.gameTableManager            = gameTableManager;
            this.creatureInfoPropertyFactory = creatureInfoPropertyFactory;
            this.creatureInfoStatFactory     = creatureInfoStatFactory;
        }

        #endregion

        /// <summary>
        /// Initialise <see cref="ICreatureInfo"/> with supplied <see cref="Creature2Entry"/>.
        /// </summary>
        public void Initialise(Creature2Entry entry)
        {
            if (Entry != null)
                throw new InvalidOperationException("CreatureInfo has already been initialised.");

            Entry            = entry;
            DifficultyEntry  = gameTableManager.Creature2Difficulty.GetEntry(entry.Creature2DifficultyId);
            ArcheTypeEntry   = gameTableManager.Creature2ArcheType.GetEntry(entry.Creature2ArcheTypeId);
            TierEntry        = gameTableManager.Creature2Tier.GetEntry(entry.Creature2TierId);
            ModelEntry       = gameTableManager.Creature2ModelInfo.GetEntry(entry.Creature2ModelInfoId);
            UnitVehicleEntry = gameTableManager.UnitVehicle.GetEntry(entry.UnitVehicleId);
            PrerequisiteVisibilityEntry = gameTableManager.Prerequisite.GetEntry(entry.PrerequisiteIdVisibility);

            displayGroup = gameTableManager.Creature2DisplayGroupEntry.Entries
                .Where(x => x.Creature2DisplayGroupId == entry.Creature2DisplayGroupId)
                .ToImmutableList();
            outfitGroup  = gameTableManager.Creature2OutfitGroupEntry.Entries
                .Where(x => x.Creature2OutfitGroupId == entry.Creature2OutfitGroupId)
                .ToImmutableList();
        }

        /// <summary>
        /// Initialise <see cref="ICreatureInfo"/> with supplied override information.
        /// </summary>
        public void InitialiseOverrides(IEnumerable<CreatureInfoPropertyModel> properties, IEnumerable<CreatureInfoStatModel> stats)
        {
            var propertyBuilder = ImmutableDictionary.CreateBuilder<Property, ICreatureInfoProperty>();
            foreach (Creature2OverridePropertiesEntry entry in gameTableManager.Creature2OverrideProperties.Entries
                .Where(x => x.Creature2Id == Entry.Id))
            {
                ICreatureInfoProperty templateProperty = creatureInfoPropertyFactory.Resolve();
                templateProperty.Initialise(entry.UnitPropertyIndex, entry.UnitPropertyValue);
                propertyBuilder.Add(entry.UnitPropertyIndex, templateProperty);
            }

            foreach (CreatureInfoPropertyModel propertyModel in properties)
            {
                ICreatureInfoProperty templateProperty = creatureInfoPropertyFactory.Resolve();
                templateProperty.Initialise(propertyModel.Property, propertyModel.Value);
                // deliberately override the property from game table if it exists
                propertyBuilder[templateProperty.Property] = templateProperty;
            }

            overrideProperties = propertyBuilder.ToImmutable();

            var statsBuilder = ImmutableDictionary.CreateBuilder<Static.Entity.Stat, ICreatureInfoStat>();
            foreach (CreatureInfoStatModel statModel in stats)
            {
                ICreatureInfoStat templateStat = creatureInfoStatFactory.Resolve();
                templateStat.Initialise(statModel.Stat, statModel.Value);
                statsBuilder.Add(statModel.Stat, templateStat);
            }

            overrideStats = statsBuilder.ToImmutable();
        }

        /// <summary>
        /// Return all properties that override the default values.
        /// </summary>
        public IEnumerable<ICreatureInfoProperty> GetPropertyOverrides()
        {
            return overrideProperties.Values;
        }

        /// <summary>
        /// Return all stats that override the default values.
        /// </summary>
        public IEnumerable<ICreatureInfoStat> GetStatOverrides()
        {
            return overrideStats.Values;
        }

        /// <summary>
        /// Return a random level between the minimum and maximum level of the creature.
        /// </summary>
        public uint GetLevel()
        {
            // the upper bound of Next is exclusive, MaxLevel is a level the creature can have
            return (uint)Random.Shared.Next((int)Entry.MinLevel, (int)Math.Max(Entry.MinLevel, Entry.MaxLevel) + 1);
        }

        /// <summary>
        /// Return a random <see cref="Creature2DisplayInfoEntry"/> from the display group.
        /// </summary>
        public Creature2DisplayInfoEntry GetDisplayInfoEntry()
        {
            var random = Random.Shared.Next((int)displayGroup.Sum(e => e.Weight));
            foreach (Creature2DisplayGroupEntryEntry entry in displayGroup)
            {
                if (entry.Weight <= random)
                    random -= (int)entry.Weight;
                else
                    return gameTableManager.Creature2DisplayInfo.GetEntry(entry.Creature2DisplayInfoId);
            }

            return null;
        }

        /// <summary>
        /// Return a random <see cref="Creature2OutfitInfoEntry"/> from the outfit group.
        /// </summary>
        public Creature2OutfitInfoEntry GetOutfitInfoEntry()
        {
            var random = Random.Shared.Next((int)outfitGroup.Sum(e => e.Weight));
            foreach (Creature2OutfitGroupEntryEntry entry in outfitGroup)
            {
                if (entry.Weight <= random)
                    random -= (int)entry.Weight;
                else
                    return gameTableManager.Creature2OutfitInfo.GetEntry(entry.Creature2OutfitInfoId);
            }

            return null;
        }
    }
}
