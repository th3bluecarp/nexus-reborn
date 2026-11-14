using NexusForever.GameTable.Model;

namespace NexusForever.Game.Abstract.Entity.Creature
{
    public interface ICreatureInfoOverride : ICreatureInfo
    {
        /// <summary>
        /// The rank a creature raised above its own rank deals damage at (a holdout boss keeps Prime health and title but hits like its own rank), null for any other creature.
        /// </summary>
        Creature2DifficultyEntry DamageDifficultyEntry { get; }

        /// <summary>
        /// Multiplier for the caster health terms of the spell damage the creature deals: its health at <see cref="DamageDifficultyEntry"/> over its actual health, 1 for any other creature.
        /// </summary>
        float DamageHealthScale { get; }

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
        /// Set the rank the creature deals damage at and the multiplier for the caster health terms of its damage, see <see cref="DamageDifficultyEntry"/>.
        /// </summary>
        ICreatureInfoOverride SetDamageRank(Creature2DifficultyEntry difficultyEntry, float healthScale);

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
