using NexusForever.GameTable.Model;

namespace NexusForever.Game.Abstract.Entity.Creature
{
    public interface ICreatureInfoOverride : ICreatureInfo
    {
        /// <summary>
        /// Set the <see cref="ICreatureInfo"/> to override.
        /// </summary>
        ICreatureInfoOverride SetCreatureInfoOverride(ICreatureInfo creatureInfo);

        /// <summary>
        /// Set the level override for <see cref="ICreatureInfo"/>.
        /// </summary>
        ICreatureInfoOverride SetLevelOverride(uint? level);

        /// <summary>
        /// Set the <see cref="Creature2DifficultyEntry"/> override for <see cref="ICreatureInfo"/>.
        /// </summary>
        ICreatureInfoOverride SetDifficultyEntryOverride(Creature2DifficultyEntry difficultyEntry);

        /// <summary>
        /// Set the <see cref="Creature2DisplayInfoEntry"/> override for <see cref="ICreatureInfo"/>.
        /// </summary>
        ICreatureInfoOverride SetDisplayInfoEntryOverride(Creature2DisplayInfoEntry displayInfoEntry);

        /// <summary>
        /// Set the <see cref="Creature2OutfitInfoEntry"/> override for <see cref="ICreatureInfo"/>.
        /// </summary>
        ICreatureInfoOverride SetOutfitInfoEntryOverride(Creature2OutfitInfoEntry outfitInfoEntry);
    }
}
