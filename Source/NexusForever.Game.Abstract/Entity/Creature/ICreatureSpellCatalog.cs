namespace NexusForever.Game.Abstract.Entity.Creature
{
    /// <summary>
    /// Combat spells observed for creatures on retail, loaded from the file named by Realm:CreatureSpellsPath.
    /// </summary>
    public interface ICreatureSpellCatalog
    {
        /// <summary>
        /// Instant spells the creature uses as auto attacks, empty when unknown.
        /// </summary>
        IReadOnlyList<uint> GetAutoAttacks(uint creatureId);

        /// <summary>
        /// Spells with a cast time the creature uses on a timer, empty when unknown.
        /// </summary>
        IReadOnlyList<uint> GetSpecials(uint creatureId);
    }
}
