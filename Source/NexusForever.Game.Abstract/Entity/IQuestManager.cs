using NexusForever.Database.Character;
using NexusForever.Database.Character.Model;
using NexusForever.Game.Abstract.Quest;
using NexusForever.Game.Static.Quest;
using NexusForever.Shared;

namespace NexusForever.Game.Abstract.Entity
{
    public interface IQuestManager : IDisposable, IDatabaseCharacter, IUpdate
    {
        void SendInitialPackets();

        /// <summary>
        /// Initialise a new <see cref="IQuestManager"/> from existing <see cref="CharacterModel"/> database model.
        /// </summary>
        void Initialise(IPlayer owner, CharacterModel model);

        /// <summary>
        /// Get an active <see cref="IQuest"/> from supplied quest id.
        /// </summary>
        IQuest GetActiveQuest<T>(T questId) where T : Enum;

        /// <summary>
        /// Get an active <see cref="IQuest"/> from supplied quest id.
        /// </summary>
        IQuest GetActiveQuest(ushort questId);

        /// <summary>
        /// Returns a collection of all active quests.
        /// </summary>
        IEnumerable<IQuest> GetActiveQuests();

        /// <summary>
        /// Return <see cref="QuestState"/> for supplied quest.
        /// </summary>
        QuestState? GetQuestState<T>(T questId) where T : Enum;

        /// <summary>
        /// Return <see cref="QuestState"/> for supplied quest.
        /// </summary>
        QuestState? GetQuestState(ushort questId);

        /// <summary>
        /// Mention a quest from supplied quest id, skipping any prerequisites checks.
        /// </summary>
        void QuestMention(ushort questId);

        /// <summary>
        /// Mention a quest from supplied <see cref="IQuestInfo"/>, skipping any prerequisites checks.
        /// </summary>
        void QuestMention(IQuestInfo info);

        /// <summary>
        /// Add a quest from supplied id, optionally supplying <see cref="IItem"/> which was used to start the quest.
        /// </summary>
        void QuestAdd(ushort questId, IItem item);

        /// <summary>
        /// Add a quest from supplied quest id.
        /// </summary>
        void QuestAdd<T>(T questId) where T : Enum;

        /// <summary>
        /// Add a quest from supplied quest id.
        /// </summary>
        void QuestAdd(ushort questId);

        /// <summary>
        /// Add a quest from supplied <see cref="IQuestInfo"/>, skipping any prerequisites checks.
        /// </summary>
        void QuestAdd(IQuestInfo info);

        /// <summary>
        /// Retry an inactive quest id that was previously failed.
        /// </summary>
        void QuestRetry(ushort questId);

        /// <summary>
        /// Abandon an active quest.
        /// </summary>
        void QuestAbandon(ushort questId);

        /// <summary>
        /// Complete all <see cref="IQuestObjective"/>'s for supplied active quest id.
        /// </summary>
        void QuestAchieve(ushort questId);

        /// <summary>
        /// Complete single <see cref="IQuestObjective"/> for supplied active quest id.
        /// </summary>
        void QuestAchieveObjective(ushort questId, byte index);

        /// <summary>
        /// Complete an achieved quest supplying an optional reward and whether the quest was completed from the communicator.
        /// </summary>
        void QuestComplete(ushort questId, ushort reward, bool communicator);

        /// <summary>
        /// Complete an achieved <see cref="IQuest"/> from supplied quest id.
        /// </summary>
        void QuestComplete<T>(T questId) where T : Enum;

        /// <summary>
        /// Complete an achieved <see cref="IQuest"/> from supplied quest id.
        /// </summary>
        void QuestComplete(ushort questId);

        /// <summary>
        /// Ignore or acknowledge an inactive quest.
        /// </summary>
        void QuestIgnore(ushort questId, bool ignored);

        /// <summary>
        /// Track or hide an active quest.
        /// </summary>
        void QuestTrack(ushort questId, bool tracked);

        /// <summary>
        /// Share supplied quest with another <see cref="IPlayer"/>.
        /// </summary>
        void QuestShare(ushort questId);

        /// <summary>
        /// Accept or deny a shared quest from another <see cref="IPlayer"/>.
        /// </summary>
        void QuestShareResult(ushort questId, bool result);

        /// <summary>
        /// Update any active quest <see cref="IQuestObjective"/>'s with supplied <see cref="QuestObjectiveType"/> and data with progress.
        /// </summary>
        void ObjectiveUpdate(QuestObjectiveType type, uint data, uint progress);

        /// <summary>
        /// Update any active quest <see cref="IQuestObjective"/>'s with supplied <see cref="QuestObjectiveType"/> whose reward pane target group matches with progress.
        /// </summary>
        void RewardPaneObjectiveUpdate(QuestObjectiveType type, uint targetGroupId, uint progress);

        /// <summary>
        /// Invoked when the number of an item in the player's bags changes, updates <see cref="QuestObjectiveType.CollectItem"/> objectives.
        /// </summary>
        void ItemCountUpdate(uint itemId, uint count);

        /// <summary>
        /// Invoked when the player successfully casts a spell, updates SpellSuccess objectives.
        /// </summary>
        void SpellSuccessUpdate(uint spell4Id, IEnumerable<IUnitEntity> targets);

        // <summary>
        /// Update any active quest <see cref="IQuestObjective"/>'s with supplied ID with progress.
        /// </summary>
        void ObjectiveUpdate(uint id, uint progress);
    }
}