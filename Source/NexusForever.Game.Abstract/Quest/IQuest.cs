using NexusForever.Database;
using NexusForever.Database.Character;
using NexusForever.Database.Character.Model;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Static.Quest;
using NexusForever.Shared;

namespace NexusForever.Game.Abstract.Quest
{
    public interface IQuest : IDisposable, IUpdate, IDatabaseCharacter, IDatabaseState, IEnumerable<IQuestObjective>
    {
        ushort Id { get; }
        IQuestInfo Info { get; }
        QuestState State { get; set; }
        QuestStateFlags Flags { get; set; }
        uint? Timer { get; set; }
        DateTime? Reset { get; set; }

        /// <summary>
        /// Create a new <see cref="IQuest"/> from an existing database model.
        /// </summary>
        void Initialise(IPlayer owner, IQuestInfo info, CharacterQuestModel model);

        /// <summary>
        /// Create a new <see cref="IQuest"/> from supplied <see cref="IQuestInfo"/>.
        /// </summary>
        void Initialise(IPlayer owner, IQuestInfo info);

        /// <summary>
        /// Returns if <see cref="IQuest"/> can be deleted.
        /// </summary>
        bool CanDelete();

        /// <summary>
        /// Returns if <see cref="IQuest"/> can be abandoned.
        /// </summary>
        bool CanAbandon();

        /// <summary>
        /// Returns if <see cref="IQuest"/> can be shared with another <see cref="IPlayer"/>.
        /// </summary>
        bool CanShare();

        /// <summary>
        /// Returns the owner <see cref="IPlayer"/> of the <see cref="IQuest"/>.
        /// </summary>
        IPlayer GetOwner();

        /// <summary>
        /// Return the <see cref="IQuestObjective"/> with the supplied id.
        /// </summary>
        IQuestObjective GetQuestObjective(uint id);

        /// <summary>
        /// Return the <see cref="IQuestObjective"/> with the supplied index.
        /// </summary>
        IQuestObjective GetQuestObjectiveByIndex(byte index);

        /// <summary>
        /// Return if the <see cref="IQuestObjective"/> with the supplied index can currently be progressed.
        /// </summary>
        /// <remarks>
        /// The quest must be accepted, the objective incomplete and, for sequential objectives, every earlier objective complete.
        /// </remarks>
        bool IsObjectiveActive(byte index);

        /// <summary>
        /// Complete the <see cref="IQuest"/> without rewards.
        /// </summary>
        void CompleteQuest();

        /// <summary>
        /// Complete the <see cref="IQuest"/> with the specified reward.
        /// </summary>
        void RewardQuest(ushort reward);

        /// <summary>
        /// Update any <see cref="IQuestObjective"/>'s with supplied <see cref="QuestObjectiveType"/> and data with progress.
        /// </summary>
        void ObjectiveUpdate(QuestObjectiveType type, uint data, uint progress);

        /// <summary>
        /// Update any <see cref="IQuestObjective"/> of the supplied <see cref="QuestObjectiveType"/> whatever its data.
        /// </summary>
        void ObjectiveUpdateAny(QuestObjectiveType type, uint progress);

        /// <summary>
        /// Update any <see cref="IQuestObjective"/>'s with supplied <see cref="QuestObjectiveType"/> whose reward pane target group matches with progress.
        /// </summary>
        /// <remarks>
        /// Used for objectives such as <see cref="QuestObjectiveType.VirtualCollect"/> where data is not the source of progress,
        /// the reward pane target group lists the creatures that grant progress.
        /// </remarks>
        void RewardPaneObjectiveUpdate(QuestObjectiveType type, uint targetGroupId, uint progress);

        /// <summary>
        /// Update objectives that ask the player to equip the supplied item.
        /// </summary>
        void EquipObjectiveUpdate(uint itemId);

        /// <summary>
        /// Set any <see cref="IQuestObjective"/>'s with supplied <see cref="QuestObjectiveType"/> and data to an absolute progress value.
        /// </summary>
        /// <remarks>
        /// Unlike an update, progress can go down, which moves an achieved quest back to accepted.
        /// </remarks>
        void ObjectiveSet(QuestObjectiveType type, uint data, uint value);

        /// <summary>
        /// Update SpellSuccess objectives for a successful cast of supplied spell on supplied targets.
        /// </summary>
        void SpellSuccessUpdate(uint spell4Id, IReadOnlyList<IUnitEntity> targets);

        /// <summary>
        /// Update any <see cref="IQuestObjective"/>'s with supplied ID with progress.
        /// </summary>
        void ObjectiveUpdate(uint id, uint progress);
    }
}