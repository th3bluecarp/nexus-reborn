using NexusForever.Database.Character;
using NexusForever.Shared;

namespace NexusForever.Game.Abstract.Quest
{
    public interface IQuestObjective : IUpdate, IDatabaseCharacter
    {
        IQuestInfo QuestInfo { get; }
        IQuestObjectiveInfo ObjectiveInfo { get; }
        byte Index { get; }
        uint Progress { get; set; }

        /// <summary>
        /// Set objective progress to an absolute value, capped at the objective maximum.
        /// </summary>
        /// <remarks>
        /// Used for objectives such as <see cref="QuestObjectiveType.CollectItem"/> where progress mirrors a count that can also go down.
        /// </remarks>
        void ObjectiveSet(uint value);
        uint? Timer { get; set; }

        /// <summary>
        /// Return if the objective has been completed.
        /// </summary>
        bool IsComplete();

        /// <summary>
        /// Update object progress with supplied update.
        /// </summary>
        void ObjectiveUpdate(uint update);

        /// <summary>
        /// Complete this <see cref="IQuestObjective"/>.
        /// </summary>
        void Complete();
    }
}