using NexusForever.Game.Abstract.Entity;

namespace NexusForever.Game.Abstract.Prerequisite
{
    public interface IPrerequisiteManager
    {
        /// <summary>
        /// Checks if <see cref="IPlayer"/> meets supplied prerequisite.
        /// </summary>
        bool Meets(IPlayer player, uint prerequisiteId);

        /// <summary>
        /// Checks if <see cref="IPlayer"/> meets supplied prerequisite.
        /// </summary>
        bool Meets(IPlayer player, uint prerequisiteId, IPrerequisiteParameters parameters);

        /// <summary>
        /// Checks if a creature (non player) caster meets a spell effect's caster prerequisite. Only the checks that mean
        /// something for a creature are evaluated (its difficulty rank), the rest pass.
        /// </summary>
        bool MeetsCreature(IWorldEntity caster, uint prerequisiteId);
    }
}
