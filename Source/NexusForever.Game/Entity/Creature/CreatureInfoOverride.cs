using NexusForever.Database.World.Model;
using NexusForever.Game.Abstract.Entity.Creature;
using NexusForever.GameTable.Model;

namespace NexusForever.Game.Entity.Creature
{
    public class CreatureInfoOverride : ICreatureInfoOverride
    {
        public Creature2Entry Entry => baseCreatureInfo.Entry;
        public Creature2DifficultyEntry DifficultyEntry => difficultyEntry ?? baseCreatureInfo.DifficultyEntry;
        public Creature2ArcheTypeEntry ArcheTypeEntry => baseCreatureInfo.ArcheTypeEntry;
        public Creature2TierEntry TierEntry => baseCreatureInfo.TierEntry;
        public Creature2ModelInfoEntry ModelEntry => baseCreatureInfo.ModelEntry;
        public UnitVehicleEntry UnitVehicleEntry => baseCreatureInfo.UnitVehicleEntry;
        public PrerequisiteEntry PrerequisiteVisibilityEntry => baseCreatureInfo.PrerequisiteVisibilityEntry;

        private ICreatureInfo baseCreatureInfo;
        private uint? level;
        private Creature2DifficultyEntry difficultyEntry;
        private Creature2DisplayInfoEntry displayInfoEntry;
        private Creature2OutfitInfoEntry outfitInfoEntry;

        /// <summary>
        /// Set the <see cref="ICreatureInfo"/> to override.
        /// </summary>
        public ICreatureInfoOverride SetCreatureInfoOverride(ICreatureInfo creatureInfo)
        {
            baseCreatureInfo = creatureInfo;
            return this;
        }

        /// <summary>
        /// Set the level override for <see cref="ICreatureInfo"/>.
        /// </summary>
        public ICreatureInfoOverride SetLevelOverride(uint? level)
        {
            this.level = level;
            return this;
        }

        /// <summary>
        /// Set the <see cref="Creature2DifficultyEntry"/> override for <see cref="ICreatureInfo"/>.
        /// </summary>
        public ICreatureInfoOverride SetDifficultyEntryOverride(Creature2DifficultyEntry difficultyEntry)
        {
            this.difficultyEntry = difficultyEntry;
            return this;
        }

        /// <summary>
        /// Set the <see cref="Creature2DisplayInfoEntry"/> override for <see cref="ICreatureInfo"/>.
        /// </summary>
        public ICreatureInfoOverride SetDisplayInfoEntryOverride(Creature2DisplayInfoEntry displayInfoEntry)
        {
            this.displayInfoEntry = displayInfoEntry;
            return this;
        }

        /// <summary>
        /// Set the <see cref="Creature2OutfitInfoEntry"/> override for <see cref="ICreatureInfo"/>.
        /// </summary>
        public ICreatureInfoOverride SetOutfitInfoEntryOverride(Creature2OutfitInfoEntry outfitInfoEntry)
        {
            this.outfitInfoEntry = outfitInfoEntry;
            return this;
        }

        /// <summary>
        /// Initialise <see cref="ICreatureInfo"/> with supplied <see cref="Creature2Entry"/>.
        /// </summary>
        public void Initialise(Creature2Entry entry)
        {
            baseCreatureInfo.Initialise(entry);
        }

        /// <summary>
        /// Initialise <see cref="ICreatureInfo"/> with supplied override information.
        /// </summary>
        public void InitialiseOverrides(IEnumerable<CreatureInfoPropertyModel> properties, IEnumerable<CreatureInfoStatModel> stats)
        {
            baseCreatureInfo.InitialiseOverrides(properties, stats);
        }

        /// <summary>
        /// Return all properties that override the default values.
        /// </summary>
        public IEnumerable<ICreatureInfoProperty> GetPropertyOverrides()
        {
            return baseCreatureInfo.GetPropertyOverrides();
        }

        /// <summary>
        /// Return all stats that override the default values.
        /// </summary>
        public IEnumerable<ICreatureInfoStat> GetStatOverrides()
        {
            return baseCreatureInfo.GetStatOverrides();
        }

        /// <summary>
        /// Return a random level between the minimum and maximum level of the creature.
        /// </summary>
        public uint GetLevel()
        {
            if (level != null)
                return level.Value;

            return baseCreatureInfo.GetLevel();
        }

        /// <summary>
        /// Return a random <see cref="Creature2DisplayInfoEntry"/> from the display group.
        /// </summary>
        public Creature2DisplayInfoEntry GetDisplayInfoEntry()
        {
            if (displayInfoEntry != null)
                return displayInfoEntry;

            return baseCreatureInfo.GetDisplayInfoEntry();
        }

        /// <summary>
        /// Return a random <see cref="Creature2OutfitInfoEntry"/> from the outfit group.
        /// </summary>
        public Creature2OutfitInfoEntry GetOutfitInfoEntry()
        {
            if (outfitInfoEntry != null)
                return outfitInfoEntry;

            return baseCreatureInfo.GetOutfitInfoEntry();
        }
    }
}
