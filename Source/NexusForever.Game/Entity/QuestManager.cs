using Microsoft.Extensions.Logging;
using NexusForever.Database.Character;
using NexusForever.Database.Character.Model;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Quest;
using NexusForever.Game.Prerequisite;
using NexusForever.Game.Quest;
using NexusForever.Game.Static;
using NexusForever.Game.Static.Entity;
using NexusForever.Game.Static.Quest;
using NexusForever.Game.Static.Reputation;
using NexusForever.GameTable;
using NexusForever.GameTable.Model;
using NexusForever.Network.World.Message.Model;
using NexusForever.Network.World.Message.Static;
using NexusForever.Shared;
using NLog;

namespace NexusForever.Game.Entity
{
    public class QuestManager : IQuestManager
    {
        [Flags]
        private enum GetQuestFlags
        {
            Completed = 0x01,
            Inactive  = 0x02,
            Active    = 0x04,
            All       = Completed | Inactive | Active
        }

        private IPlayer player;

        private readonly Dictionary<ushort, IQuest> completedQuests = [];
        private readonly Dictionary<ushort, IQuest> inactiveQuests = [];
        private readonly Dictionary<ushort, IQuest> activeQuests = [];

        #region Dependency Injection

        private readonly ILogger<QuestManager> log;
        private readonly IFactory<IQuest> questFactory;

        public QuestManager(
            ILogger<QuestManager> log,
            IFactory<IQuest> questFactory)
        {
            this.log          = log;
            this.questFactory = questFactory;
        }

        #endregion

        /// <summary>
        /// Initialise a new <see cref="IQuestManager"/> from existing <see cref="CharacterModel"/> database model.
        /// </summary>
        public void Initialise(IPlayer owner, CharacterModel model)
        {
            if (player != null)
                throw new InvalidOperationException("QuestManager has already been initialised!");

            player = owner;

            foreach (CharacterQuestModel questModel in model.Quest)
            {
                IQuestInfo info = GlobalQuestManager.Instance.GetQuestInfo(questModel.QuestId);
                if (info == null)
                {
                    log.LogError($"Player {player.CharacterId} has an invalid quest {questModel.QuestId}!");
                    continue;
                }

                IQuest quest = questFactory.Resolve();
                quest.Initialise(player, info, questModel);

                switch (quest.State)
                {
                    case QuestState.Completed:
                        completedQuests.Add(quest.Id, quest);
                        break;
                    case QuestState.Botched:
                    case QuestState.Ignored:
                    case QuestState.Mentioned:
                        inactiveQuests.Add(quest.Id, quest);
                        break;
                    case QuestState.Accepted:
                    case QuestState.Achieved:
                        activeQuests.Add(quest.Id, quest);
                        break;
                }
            }
        }

        public void Dispose()
        {
            foreach (IQuest quest in completedQuests.Values
                .Concat(inactiveQuests.Values)
                .Concat(activeQuests.Values))
                quest.Dispose();
        }

        public void Save(CharacterContext context)
        {
            foreach (IQuest quest in completedQuests.Values)
                quest.Save(context);

            foreach (IQuest quest in inactiveQuests.Values.ToList())
            {
                if (quest.PendingDelete)
                    inactiveQuests.Remove(quest.Id);

                quest.Save(context);
            }

            foreach (IQuest quest in activeQuests.Values.ToList())
            {
                if (quest.PendingDelete)
                    activeQuests.Remove(quest.Id);

                quest.Save(context);
            }
        }

        /// <summary>
        /// Invoked each world tick with the delta since the previous tick occurred.
        /// </summary>
        public void Update(double lastTick)
        {
            var botchedQuests = new List<IQuest>();
            foreach (IQuest quest in activeQuests.Values)
            {
                quest.Update(lastTick);
                if (quest.State == QuestState.Botched)
                    botchedQuests.Add(quest);
            }

            foreach (IQuest quest in botchedQuests)
            {
                activeQuests.Remove(quest.Id);
                inactiveQuests.Add(quest.Id, quest);

                log.LogTrace($"Failed to complete quest {quest.Id} before the timer expired!");
            }
        }

        public void SendInitialPackets()
        {
            DateTime now = DateTime.UtcNow;
            player.Session.EnqueueMessageEncrypted(new ServerQuestInit
            {
                Completed = completedQuests.Values
                    .Select(q => new ServerQuestInit.QuestComplete
                    {
                        QuestId        = q.Id,
                        CompletedToday = now < q.Reset
                    }).ToList(),
                Inactive = inactiveQuests.Values
                    .Select(q => new ServerQuestInit.QuestInactive
                    {
                        QuestId = q.Id,
                        State   = q.State
                    }).ToList(),
                Active = activeQuests.Values
                    .Select(q => new ServerQuestInit.QuestActive
                    {
                        QuestId    = q.Id,
                        State      = q.State,
                        Flags      = q.Flags,
                        QuestTimeElapsed      = q.Timer ?? 0u,
                        Objectives = q.Select(o => new ServerQuestInit.QuestActive.Objective
                        {
                            Progress = o.Progress,
                            TimeElapsed    = 0u
                        }).ToList()
                    }).ToList()
            });
        }

        /// <summary>
        /// Get an active <see cref="IQuest"/> from supplied quest id.
        /// </summary>
        public IQuest GetActiveQuest<T>(T questId) where T : Enum
        {
            return GetActiveQuest(questId.As<T, ushort>());
        }

        /// <summary>
        /// Get an active <see cref="IQuest"/> from supplied quest id.
        /// </summary>
        public IQuest GetActiveQuest(ushort questId)
        {
            return GetQuest(questId, GetQuestFlags.Active);
        }

        /// <summary>
        /// Returns a collection of all active quests.
        /// </summary>
        public IEnumerable<IQuest> GetActiveQuests()
        {
            return activeQuests.Values;
        }

        /// <summary>
        /// Return <see cref="QuestState"/> for supplied quest.
        /// </summary>
        public QuestState? GetQuestState<T>(T questId) where T : Enum
        {
            return GetQuestState(questId.As<T, ushort>());
        }

        /// <summary>
        /// Return <see cref="QuestState"/> for supplied quest.
        /// </summary>
        public QuestState? GetQuestState(ushort questId)
        {
            return GetQuest(questId)?.State;
        }

        private IQuest GetQuest(ushort questId, GetQuestFlags flags = GetQuestFlags.All)
        {
            if ((flags & GetQuestFlags.Active) != 0
                && activeQuests.TryGetValue(questId, out IQuest quest))
                return quest;

            if ((flags & GetQuestFlags.Inactive) != 0
                && inactiveQuests.TryGetValue(questId, out quest))
                return quest;

            if ((flags & GetQuestFlags.Completed) != 0
                && completedQuests.TryGetValue(questId, out quest))
                return quest;

            return null;
        }

        /// <summary>
        /// Mention a quest from supplied quest id, skipping any prerequisites checks.
        /// </summary>
        public void QuestMention(ushort questId)
        {
            IQuestInfo info = GlobalQuestManager.Instance.GetQuestInfo(questId);
            if (info == null)
                throw new ArgumentException($"Invalid quest {questId}!");

            if (DisableManager.Instance.IsDisabled(DisableType.Quest, questId))
            {
                player.SendSystemMessage($"Unable to add quest {questId} because it is disabled.");
                return;
            }

            if (GetQuest(questId) != null)
                return;

            QuestMention(info);
        }

        /// <summary>
        /// Mention a quest from supplied <see cref="IQuestInfo"/>, skipping any prerequisites checks.
        /// </summary>
        public void QuestMention(IQuestInfo info)
        {
            IQuest quest = GetQuest((ushort)info.Entry.Id);
            if (quest == null)
            {
                quest = questFactory.Resolve();
                quest.Initialise(player, info);
            }
            else
                QuestReset(quest);

            quest.State = QuestState.Mentioned;
            inactiveQuests.Add((ushort)info.Entry.Id, quest);

            log.LogTrace($"Mentioned new quest {info.Entry.Id}.");
        }

        /// <summary>
        /// Add a quest from supplied id, optionally supplying <see cref="IItem"/> which was used to start the quest.
        /// </summary>
        public void QuestAdd(ushort questId, IItem item)
        {
            IQuestInfo info = GlobalQuestManager.Instance.GetQuestInfo(questId);
            if (info == null)
                throw new ArgumentException($"Invalid quest {questId}!");

            if (DisableManager.Instance.IsDisabled(DisableType.Quest, questId))
            {
                player.SendSystemMessage($"Unable to add quest {questId} because it is disabled.");
                return;
            }

            IQuest quest = GetQuest(questId);
            QuestAdd(info, quest, item);
        }

        private void QuestAdd(IQuestInfo info, IQuest quest, IItem item)
        {
            if (quest?.State is QuestState.Accepted or QuestState.Achieved)
                throw new QuestException($"Player {player.CharacterId} tried to start quest {info.Entry.Id} which is already in progress!");

            // if quest has already been completed make sure it's repeatable and the reset period has elapsed
            if (quest?.State == QuestState.Completed)
            {
                if (info.Entry.QuestRepeatPeriodEnum == 0u)
                    throw new QuestException($"Player {player.CharacterId} tried to start quest {info.Entry.Id} which they have already completed!");

                DateTime? resetTime = GetQuest((ushort)info.Entry.Id, GetQuestFlags.Completed).Reset;
                if (DateTime.UtcNow < resetTime)
                    throw new QuestException($"Player {player.CharacterId} tried to start quest {info.Entry.Id} which hasn't reset yet!");
            }

            if (item != null)
            {
                if (info.Entry.Id != item.Info.Entry.Quest2IdActivation)
                    throw new QuestException($"Player {player.CharacterId} tried to start quest {info.Entry.Id} from invalid item {item.Info.Entry.Id}!");

                // TODO: consume charge
            }
            else
            {
                // make sure the player is in range of a quest giver or they are eligible for a communicator message that starts the quest
                if (!GlobalQuestManager.Instance.GetQuestGivers((ushort)info.Entry.Id)
                        .Any(c => player.GetVisibleCreature<WorldEntity>(c).Any())
                    && !GlobalQuestManager.Instance.GetQuestCommunicatorMessages((ushort)info.Entry.Id)
                        .Any(m => m.Meets(player)))
                    throw new QuestException($"Player {player.CharacterId} tried to start quest {info.Entry.Id} without quest giver!");
            }

            // server doesn't send an error message for prerequisites since the client does the exact same checks
            // it's assumed that a player could never get here without cheating in some way
            if (!MeetsPrerequisites(info))
                throw new QuestException($"Player {player.CharacterId} tried to start quest {info.Entry.Id} without meeting the prerequisites!");

            QuestAdd(info);
        }

        private bool MeetsPrerequisites(IQuestInfo info)
        {
            if (info.Entry.QuestPlayerFactionEnum == 0u && player.Faction1 != Faction.Exile)
                return false;
            if (info.Entry.QuestPlayerFactionEnum == 1u && player.Faction1 != Faction.Dominion)
                return false;
            if (info.Entry.PrerequisiteRace != 0u && player.Race != (Race)info.Entry.PrerequisiteRace)
                return false;
            if (info.Entry.PrerequisiteClass != 0u && player.Class != (Class)info.Entry.PrerequisiteClass)
                return false;
            if (player.Level < info.Entry.PrerequisiteLevel)
                return false;

            if (!info.PrerequisiteQuests.IsEmpty)
            {
                bool preReqQuestsCompleted;
                if ((info.Entry.PrerequisiteFlags & 1) != 0u)
                    preReqQuestsCompleted = info.PrerequisiteQuests.Any(q => GetQuestState((ushort)q.Id) == QuestState.Completed);
                else
                    preReqQuestsCompleted = info.PrerequisiteQuests.All(q => GetQuestState((ushort)q.Id) == QuestState.Completed);

                if (!preReqQuestsCompleted)
                    return false;
            }

            if (info.Entry.PrerequisiteId != 0u && !PrerequisiteManager.Instance.Meets(player, info.Entry.PrerequisiteId))
                return false;

            if (!info.IsContract())
            {
                GameFormulaEntry entry = GameTableManager.Instance.GameFormula.GetEntry(655);
                // client also hard codes 40 if entry doesn't exist
                if (activeQuests.Count > (entry?.Dataint0 ?? 40u))
                    return false;
            }
            else
            {
                // TODO: contracts use reward property for max slots, RewardProperty.ActiveContractSlots
            }

            return true;
        }

        /// <summary>
        /// Add a quest from supplied quest id.
        /// </summary>
        public void QuestAdd<T>(T questId) where T : Enum
        {
            QuestAdd(questId.As<T, ushort>());
        }

        /// <summary>
        /// Add a quest from supplied quest id.
        /// </summary>
        public void QuestAdd(ushort questId)
        {
            IQuestInfo info = GlobalQuestManager.Instance.GetQuestInfo(questId);
            if (info == null)
                throw new ArgumentException($"Invalid quest {questId}!");

            QuestAdd(info);
        }

        /// <summary>
        /// Add a quest from supplied <see cref="IQuestInfo"/>, skipping any prerequisites checks.
        /// </summary>
        public void QuestAdd(IQuestInfo info)
        {
            // make sure player has room for all pushed items
            if (player.Inventory.GetInventorySlotsRemaining(InventoryLocation.Inventory)
                < info.Entry.PushedItemIds.Count(i => i != 0u))
            {
                player.SendGenericError(GenericError.ItemInventoryFull);
                return;
            }

            IQuest quest = GetQuest((ushort)info.Entry.Id);
            if (quest == null)
                quest = questFactory.Resolve();
            else
                QuestReset(quest);

            quest.Initialise(player, info);
            activeQuests.Add((ushort)info.Entry.Id, quest);

            // collect objectives count items the player already carries
            foreach (IQuestObjective objective in quest.Where(o => o.ObjectiveInfo.Entry.Type == (uint)QuestObjectiveType.CollectItem))
                quest.ObjectiveSet(QuestObjectiveType.CollectItem, objective.ObjectiveInfo.Entry.Data, player.Inventory.GetItemCount(objective.ObjectiveInfo.Entry.Data));
            
            log.LogTrace($"Accepted new quest {info.Entry.Id}.");
        }

        private void QuestReset(IQuest quest)
        {
            // remove existing quest from its current home before
            switch (quest.State)
            {
                case QuestState.Abandoned:
                    activeQuests.Remove(quest.Id);
                    break;
                case QuestState.Completed:
                    completedQuests.Remove(quest.Id);
                    break;
                case QuestState.Botched:
                case QuestState.Ignored:
                case QuestState.Mentioned:
                    inactiveQuests.Remove(quest.Id);
                    break;
            }

            if (quest.PendingDelete)
                quest.EnqueueDelete(false);

            // reset previous objective progress
            foreach (IQuestObjective objective in quest)
                objective.Progress = 0u;
        }

        /// <summary>
        /// Retry an inactive quest id that was previously failed.
        /// </summary>
        public void QuestRetry(ushort questId)
        {
            IQuestInfo info = GlobalQuestManager.Instance.GetQuestInfo(questId);
            if (info == null)
                throw new ArgumentException($"Invalid quest {questId}!");

            IQuest quest = GetQuest(questId, GetQuestFlags.Inactive);
            if (quest == null)
                throw new QuestException($"Player {player.CharacterId} tried to restart quest {questId} which they don't have!");

            if (quest.State != QuestState.Botched)
                throw new QuestException($"Player {player.CharacterId} tried to restart quest {questId} which hasn't been failed!");

            QuestAdd(info, quest, null);
        }

        /// <summary>
        /// Abandon an active quest.
        /// </summary>
        public void QuestAbandon(ushort questId)
        {
            if (GlobalQuestManager.Instance.GetQuestInfo(questId) == null)
                throw new ArgumentException($"Invalid quest {questId}!");

            IQuest quest = GetQuest(questId, GetQuestFlags.Active | GetQuestFlags.Inactive);
            if (quest == null || quest.PendingDelete)
                throw new QuestException($"Player {player.CharacterId} tried to abandon quest {questId} which they don't have!");

            if (!quest.CanAbandon())
                throw new QuestException($"Player {player.CharacterId} tried to abandon quest {questId} which can't be abandoned!");

            // don't delete quests that have been mentioned, they may not be able to be re-collected.
            if (!quest.PendingCreate && quest.CanDelete())
                quest.EnqueueDelete(true);
            else
            {
                switch (quest.State)
                {
                    case QuestState.Accepted:
                    case QuestState.Achieved:
                        activeQuests.Remove(questId);
                        break;
                    case QuestState.Botched:
                        inactiveQuests.Remove(quest.Id);
                        break;
                }
            }

            foreach (IQuestObjective objective in quest)
                objective.Progress = 0u;

            if (quest.Info.IsQuestMentioned)
            {
                quest.State = QuestState.Mentioned;
                inactiveQuests.Add(quest.Id, quest);
            }
            else
                quest.State = QuestState.Abandoned;

            log.LogTrace($"Abandoned quest {questId}.");
        }

        /// <summary>
        /// Complete all <see cref="IQuestObjective"/>'s for supplied active quest id.
        /// </summary>
        public void QuestAchieve(ushort questId)
        {
            if (GlobalQuestManager.Instance.GetQuestInfo(questId) == null)
                throw new ArgumentException($"Invalid quest {questId}!");

            IQuest quest = GetQuest(questId);
            if (quest == null || quest.PendingDelete)
                throw new QuestException($"Player {player.CharacterId} tried to achieve quest {questId} which they don't have!");

            if (quest.State != QuestState.Accepted)
                throw new QuestException($"Player {player.CharacterId} tried to achieve quest {questId} with invalid state!");

            foreach (IQuestObjectiveInfo info in quest.Info.Objectives)
                quest.ObjectiveUpdate(info.Type, info.Entry.Data, info.Entry.Count);
        }

        /// <summary>
        /// Complete single <see cref="IQuestObjective"/> for supplied active quest id.
        /// </summary>
        public void QuestAchieveObjective(ushort questId, byte index)
        {
            if (GlobalQuestManager.Instance.GetQuestInfo(questId) == null)
                throw new ArgumentException($"Invalid quest {questId}!");

            IQuest quest = GetQuest(questId);
            if (quest == null || quest.PendingDelete)
                throw new QuestException();

            if (quest.State != QuestState.Accepted)
                throw new QuestException();

            IQuestObjective objective = quest.SingleOrDefault(o => o.Index == index);
            if (objective == null)
                throw new QuestException();

            quest.ObjectiveUpdate(objective.ObjectiveInfo.Type, objective.ObjectiveInfo.Entry.Data, objective.ObjectiveInfo.Entry.Count);
        }

        /// <summary>
        /// Complete an achieved quest supplying an optional reward and whether the quest was completed from the communicator.
        /// </summary>
        public void QuestComplete(ushort questId, ushort reward, bool communicator)
        {
            IQuestInfo info = GlobalQuestManager.Instance.GetQuestInfo(questId);
            if (info == null)
                throw new ArgumentException($"Invalid quest {questId}!");

            if (DisableManager.Instance.IsDisabled(DisableType.Quest, questId))
            {
                player.SendSystemMessage($"Unable to complete quest {questId} because it is disabled.");
                return;
            }

            IQuest quest = GetQuest(questId, GetQuestFlags.Active);
            if (quest == null)
            {
                if (!info.IsAutoComplete())
                    throw new QuestException($"Player {player.CharacterId} tried to complete quest {questId} which they don't have!");

                QuestAdd(questId, null);
                quest = GetQuest(questId);
                quest.State = QuestState.Achieved;
            }

            if (quest.State != QuestState.Achieved)
                throw new QuestException($"Player {player.CharacterId} tried to complete quest {questId} which wasn't complete!");

            if (communicator)
            {
                // TODO: check if this is complete, client seems to also refer to contact info
                // for more see QuestTracker:HelperShowQuestCallbackBtn in LUA which contains the logic to show the complete button in the quest tracker
                if (!quest.Info.IsCommunicatorReceived())
                    throw new QuestException($"Player {player.CharacterId} tried to complete quest {questId} without communicator message!");
            }
            else
            {
                if (!GlobalQuestManager.Instance.GetQuestReceivers(questId).Any(c => player.GetVisibleCreature<WorldEntity>(c).Any()))
                    throw new QuestException($"Player {player.CharacterId} tried to complete quest {questId} without any quest receiver!");
            }

            quest.RewardQuest(reward);

            activeQuests.Remove(questId);
            completedQuests.Add(questId, quest);

            // collected items are handed in with the quest
            foreach (IQuestObjective objective in quest.Where(o => o.ObjectiveInfo.Entry.Type == (uint)QuestObjectiveType.CollectItem))
            {
                uint count = Math.Min(objective.ObjectiveInfo.Entry.Count, player.Inventory.GetItemCount(objective.ObjectiveInfo.Entry.Data));
                if (count > 0u)
                    player.Inventory.ItemDelete(objective.ObjectiveInfo.Entry.Data, count, ItemUpdateReason.Quest);
            }

            // entities with a visibility prerequisite on this quest may need to appear or disappear
            player.VisibilityUpdate();
        }

        /// <summary>
        /// Complete an achieved <see cref="IQuest"/> from supplied quest id.
        /// </summary>
        public void QuestComplete<T>(T questId) where T : Enum
        {
            QuestComplete(questId.As<T, ushort>());
        }

        /// <summary>
        /// Complete an achieved <see cref="IQuest"/> from supplied quest id.
        /// </summary>
        public void QuestComplete(ushort questId)
        {
            IQuest quest = GetQuest(questId, GetQuestFlags.Active);
            if (quest == null)
                return;

            quest.CompleteQuest();

            activeQuests.Remove(questId);
            completedQuests.Add(questId, quest);

            // entities with a visibility prerequisite on this quest may need to appear or disappear
            player.VisibilityUpdate();
        }

        /// <summary>
        /// Ignore or acknowledge an inactive quest.
        /// </summary>
        public void QuestIgnore(ushort questId, bool ignored)
        {
            IQuestInfo questInfo = GlobalQuestManager.Instance.GetQuestInfo(questId);
            if (questInfo == null)
                throw new ArgumentException($"Invalid quest {questId}!");

            IQuest quest = GetQuest((ushort)questInfo.Entry.Id);
            if (quest == null)
            {
                quest = questFactory.Resolve(); // Add quest so we can set it to ignored.
                quest.Initialise(player, questInfo);
            }
            else
                QuestReset(quest); // Removes from quest log. Might not be the cleanest way to do this?

            quest.State = ignored ? QuestState.Ignored : QuestState.Mentioned;

            inactiveQuests.Add(questId, quest);
        }

        /// <summary>
        /// Track or hide an active quest.
        /// </summary>
        public void QuestTrack(ushort questId, bool tracked)
        {
            if (GlobalQuestManager.Instance.GetQuestInfo(questId) == null)
                throw new ArgumentException($"Invalid quest {questId}!");

            IQuest quest = GetQuest(questId, GetQuestFlags.Active);
            if (quest == null)
                throw new QuestException($"Player {player.CharacterId} tried to track quest {questId} which they don't have!");

            if (quest.State != QuestState.Accepted && quest.State != QuestState.Achieved)
                throw new QuestException($"Player {player.CharacterId} tried to track quest {questId} with invalid state!");

            if (tracked)
                quest.Flags |= QuestStateFlags.Tracked;
            else
                quest.Flags &= ~QuestStateFlags.Tracked;

            log.LogTrace($"Updated tracked state of quest {questId} to {tracked}.");
        }

        /// <summary>
        /// Share supplied quest with another <see cref="IPlayer"/>.
        /// </summary>
        public void QuestShare(ushort questId)
        {
            IQuestInfo info = GlobalQuestManager.Instance.GetQuestInfo(questId);
            if (info == null)
                throw new ArgumentException($"Invalid quest {questId}!");

            IQuest quest = GetQuest(questId);
            if (quest == null)
                throw new QuestException($"Player {player.CharacterId} tried to share quest {questId} which they don't have!");

            if (!quest.CanShare())
                throw new QuestException($"Player {player.CharacterId} tried to share quest {questId} which can't be shared!");

            if (player.TargetGuid == null)
                throw new QuestException($"Player {player.CharacterId} tried to share quest {questId} without a target!");

            IPlayer recipient = player.GetVisible<IPlayer>(player.TargetGuid.Value);
            if (recipient == null)
                throw new QuestException($"Player {player.CharacterId} tried to share quest {questId} to an invalid player!");

            // TODO

            log.LogTrace($"Shared quest {questId} with player {recipient.Name}.");
        }

        /// <summary>
        /// Accept or deny a shared quest from another <see cref="IPlayer"/>.
        /// </summary>
        public void QuestShareResult(ushort questId, bool result)
        {
            // TODO
        }

        /// <summary>
        /// Update any active quest <see cref="IQuestObjective"/>'s with supplied <see cref="QuestObjectiveType"/> and data with progress.
        /// </summary>
        public void ObjectiveUpdate(QuestObjectiveType type, uint data, uint progress)
        {
            // it is possible for an objective update to achieve a quest which has auto complete
            // this may start a new quest right away
            foreach (IQuest quest in activeQuests.Values.ToList())
                quest.ObjectiveUpdate(type, data, progress);
        }

        /// <summary>
        /// Update any active quest <see cref="IQuestObjective"/>'s with supplied <see cref="QuestObjectiveType"/> whose reward pane target group matches with progress.
        /// </summary>
        public void RewardPaneObjectiveUpdate(QuestObjectiveType type, uint targetGroupId, uint progress)
        {
            foreach (IQuest quest in activeQuests.Values.ToList())
                quest.RewardPaneObjectiveUpdate(type, targetGroupId, progress);
        }

        /// <summary>
        /// Invoked when the number of an item in the player's bags changes, updates <see cref="QuestObjectiveType.CollectItem"/> objectives.
        /// </summary>
        public void ItemCountUpdate(uint itemId, uint count)
        {
            foreach (IQuest quest in activeQuests.Values.ToList())
                quest.ObjectiveSet(QuestObjectiveType.CollectItem, itemId, count);
        }

        /// <summary>
        /// Update any active quest <see cref="IQuestObjective"/>'s with supplied ID with progress.
        /// </summary>
        public void ObjectiveUpdate(uint id, uint progress)
        {
            foreach (IQuest quest in activeQuests.Values.ToList())
                quest.ObjectiveUpdate(id, progress);
        }
    }
}
