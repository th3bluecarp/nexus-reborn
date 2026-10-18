using System.Collections;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging;
using NexusForever.Database.Character;
using NexusForever.Database.Character.Model;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Quest;
using NexusForever.Game.Static.Achievement;
using NexusForever.Game.Static.Entity;
using NexusForever.Game.Static.Quest;
using NexusForever.GameTable.Model;
using NexusForever.Network.World.Message.Model;
using NexusForever.Network.World.Message.Static;
using NexusForever.Script;
using NexusForever.Script.Template;
using NexusForever.Script.Template.Collection;
using NexusForever.Shared;
using NexusForever.Shared.Game;

namespace NexusForever.Game.Quest
{
    public class Quest : IQuest
    {
        [Flags]
        private enum QuestSaveMask
        {
            None   = 0x00,
            Create = 0x01,
            State  = 0x02,
            Flags  = 0x04,
            Reset  = 0x08,
            Delete = 0x10,
            Timer  = 0x20
        }

        public ushort Id => (ushort)Info.Entry.Id;
        public IQuestInfo Info { get; private set; }

        public QuestState State
        {
            get => state;
            set
            {
                QuestState oldState = state;

                state = value;
                saveMask |= QuestSaveMask.State;

                OnStateChange(oldState);
            }
        }

        private QuestState state;

        /// <remarks>
        /// The Objective0Complete to Objective5Complete bits always follow the objectives: the client keeps them as its record of which
        /// objectives are done (WildStar64.exe 0x1405510d0) and only updates them itself on objective updates, so after a relog a
        /// sequential objective whose earlier objectives lack their bit is never available and its objects cannot be used
        /// (0x140553790, unit activation 0x1405a8b30).
        /// </remarks>
        public QuestStateFlags Flags
        {
            get
            {
                QuestStateFlags objectiveFlags = QuestStateFlags.None;
                foreach (IQuestObjective objective in objectives.Where(o => o.Index < 6 && o.IsComplete()))
                    objectiveFlags |= (QuestStateFlags)((uint)QuestStateFlags.Objective0Complete << (int)objective.Index);

                const QuestStateFlags allObjectives = QuestStateFlags.Objective0Complete | QuestStateFlags.Objective1Complete
                    | QuestStateFlags.Objective2Complete | QuestStateFlags.Objective3Complete | QuestStateFlags.Objective4Complete
                    | QuestStateFlags.Objective5Complete;
                return (flags & ~allObjectives) | objectiveFlags;
            }
            set
            {
                flags = value;
                saveMask |= QuestSaveMask.Flags;
            }
        }

        private QuestStateFlags flags;

        public uint? Timer
        {
            get => timer;
            set
            {
                timer = value;
                saveMask |= QuestSaveMask.Timer;
            }
        }

        private uint? timer;

        public DateTime? Reset
        {
            get => reset;
            set
            {
                reset = value;
                saveMask |= QuestSaveMask.Reset;
            }
        }

        private DateTime? reset;

        /// <summary>
        /// Returns if <see cref="IQuest"/> is enqueued to be saved to the database.
        /// </summary>
        public bool PendingCreate => (saveMask & QuestSaveMask.Create) != 0;

        /// <summary>
        /// Returns if <see cref="IQuest"/> is enqueued to be deleted from the database.
        /// </summary>
        public bool PendingDelete => (saveMask & QuestSaveMask.Delete) != 0;

        private QuestSaveMask saveMask;

        private IPlayer player;
        private readonly List<IQuestObjective> objectives = [];

        private UpdateTimer questTimer;

        private IScriptCollection scriptCollection;


        #region Dependency Injection

        private readonly ILogger<Quest> log;
        private readonly IScriptManager scriptManager;

        public Quest(
            ILogger<Quest> log,
            IScriptManager scriptManager)
        {
            this.log           = log;
            this.scriptManager = scriptManager;
        }

        #endregion

        /// <summary>
        /// Create a new <see cref="IQuest"/> from an existing database model.
        /// </summary>
        public void Initialise(IPlayer owner, IQuestInfo info, CharacterQuestModel model)
        {
            player = owner;
            Info   = info;
            // a stored state is loaded, not changed: no state change packet (the client is not in the world yet) and no communicator trigger
            state  = (QuestState)model.State;
            flags  = (QuestStateFlags)model.Flags;
            timer  = model.Timer;
            reset  = model.Reset;

            if (timer != null)
                questTimer = new UpdateTimer(timer.Value);

            foreach (CharacterQuestObjectiveModel objectiveModel in model.QuestObjective)
                objectives.Add(new QuestObjective(player, info, info.Objectives[objectiveModel.Index], objectiveModel));

            scriptCollection = scriptManager.InitialiseOwnedCollection<IQuest>(this);
            scriptManager.InitialiseOwnedScripts<IQuest>(scriptCollection, info.Entry.Id);
        }

        /// <summary>
        /// Create a new <see cref="IQuest"/> from supplied <see cref="IQuestInfo"/>.
        /// </summary>
        public void Initialise(IPlayer owner, IQuestInfo info)
        {
            player = owner;
            Info   = info;
            flags |= QuestStateFlags.Tracked;
            State  = QuestState.Accepted;

            // a mentioned or ignored quest that is accepted again is initialised a second time, its old objectives go
            objectives.Clear();
            for (byte i = 0; i < info.Objectives.Count; i++)
                objectives.Add(new QuestObjective(player, info, info.Objectives[i], i));

            if (objectives.Count == 0)
                State = QuestState.Achieved;

            InitialiseItems();
            InitialiseTimers();

            player.VisibilityUpdate();

            saveMask = QuestSaveMask.Create;

            scriptCollection = scriptManager.InitialiseOwnedCollection<IQuest>(this);
            scriptManager.InitialiseOwnedScripts<IQuest>(scriptCollection, info.Entry.Id);
        }

        private void InitialiseItems()
        {
            for (int i = 0; i < Info.Entry.PushedItemIds.Length; i++)
            {
                uint itemId = Info.Entry.PushedItemIds[i];
                if (itemId != 0u)
                    player.Inventory.ItemCreate(InventoryLocation.Inventory, itemId, Info.Entry.PushedItemCounts[i]);
            }

            // TODO: virtual items
        }

        private void InitialiseTimers()
        {
            if (Info.Entry.MaxTimeAllowedMS != 0u)
            {
                questTimer = new UpdateTimer(Info.Entry.MaxTimeAllowedMS / 1000d);
                Timer = (uint)(questTimer.Time * 1000d);
            }

            // TODO: objective timers
        }

        public void Dispose()
        {
            if (scriptCollection != null)
                scriptManager.Unload(scriptCollection);

            scriptCollection = null;
        }

        public void Save(CharacterContext context)
        {
            if (saveMask != QuestSaveMask.None)
            {
                if ((saveMask & QuestSaveMask.Create) != 0)
                {
                    context.Add(new CharacterQuestModel
                    {
                        Id      = player.CharacterId,
                        QuestId = Id,
                        State   = (byte)State,
                        Flags   = (byte)Flags,
                        Timer   = Timer,
                        Reset   = Reset
                    });
                }
                else if ((saveMask & QuestSaveMask.Delete) != 0)
                {
                    var model = new CharacterQuestModel
                    {
                        Id      = player.CharacterId,
                        QuestId = Id
                    };

                    context.Entry(model).State = EntityState.Deleted;
                }
                else
                {
                    var model = new CharacterQuestModel
                    {
                        Id      = player.CharacterId,
                        QuestId = Id
                    };

                    EntityEntry<CharacterQuestModel> entity = context.Attach(model);
                    if ((saveMask & QuestSaveMask.State) != 0)
                    {
                        model.State = (byte)State;
                        entity.Property(p => p.State).IsModified = true;
                    }

                    if ((saveMask & QuestSaveMask.Flags) != 0)
                    {
                        model.Flags = (byte)Flags;
                        entity.Property(p => p.Flags).IsModified = true;
                    }

                    if ((saveMask & QuestSaveMask.Reset) != 0)
                    {
                        model.Reset = Reset;
                        entity.Property(p => p.Reset).IsModified = true;
                    }

                    if ((saveMask & QuestSaveMask.Timer) != 0)
                    {
                        model.Timer = Timer;
                        entity.Property(p => p.Timer).IsModified = true;
                    }
                }

                saveMask = QuestSaveMask.None;
            }

            foreach (IQuestObjective objective in objectives)
                objective.Save(context);
        }

        public void Update(double lastTick)
        {
            scriptCollection?.Invoke<IUpdate>(s => s.Update(lastTick));

            if (questTimer != null)
            {
                questTimer.Update(lastTick);
                Timer = (uint)(questTimer.Time * 1000d);

                if (questTimer.HasElapsed)
                {
                    // ran out of time to complete quest
                    State = QuestState.Botched;
                    questTimer = null;
                }
            }
        }

        /// <summary>
        /// Enqueue <see cref="IQuest"/> to be deleted from the database.
        /// </summary>
        public void EnqueueDelete(bool set)
        {
            if (set)
                saveMask |= QuestSaveMask.Delete;
            else
                saveMask &= ~QuestSaveMask.Delete;
        }

        /// <summary>
        /// Returns if <see cref="IQuest"/> can be deleted.
        /// </summary>
        public bool CanDelete()
        {
            return Info.IsQuestMentioned != true;
        }

        /// <summary>
        /// Returns if <see cref="IQuest"/> can be abandoned.
        /// </summary>
        public bool CanAbandon()
        {
            if (State != QuestState.Botched && Info.CannotAbandon())
                return false;

            if (State == QuestState.Achieved && Info.CannotAbandonWhenAchieved())
                return false;

            return true;
        }

        /// <summary>
        /// Returns if <see cref="IQuest"/> can be shared with another <see cref="IPlayer"/>.
        /// </summary>
        public bool CanShare()
        {
            if (Info.Entry.QuestShareEnum == 0u)
                return false;

            return State is QuestState.Accepted or QuestState.Achieved or QuestState.Completed;
        }

        /// <summary>
        /// Returns the owner <see cref="IPlayer"/> of the <see cref="IQuest"/>.
        /// </summary>
        public IPlayer GetOwner()
        {
            return player;
        }

        /// <summary>
        /// Return the <see cref="IQuestObjective"/> with the supplied id.
        /// </summary>
        public IQuestObjective GetQuestObjective(uint id)
        {
            return objectives.FirstOrDefault(o => o.ObjectiveInfo.Id == id);
        }

        /// <summary>
        /// Return the <see cref="IQuestObjective"/> with the supplied index.
        /// </summary>
        public IQuestObjective GetQuestObjectiveByIndex(byte index)
        {
            return objectives.FirstOrDefault(o => o.Index == index);
        }

        /// <summary>
        /// Return if the <see cref="IQuestObjective"/> with the supplied index can currently be progressed.
        /// </summary>
        public bool IsObjectiveActive(byte index)
        {
            if (State != QuestState.Accepted)
                return false;

            IQuestObjective objective = GetQuestObjectiveByIndex(index);
            if (objective == null || objective.IsComplete())
                return false;

            return CanUpdateObjective(objective);
        }

        /// <summary>
        /// Complete the <see cref="IQuest"/> without rewards.
        /// </summary>
        public void CompleteQuest()
        {
            if (State != QuestState.Achieved)
                return;

            // reclaim any quest specific items
            for (int i = 0; i < Info.Entry.PushedItemIds.Length; i++)
            {
                uint itemId = Info.Entry.PushedItemIds[i];
                if (itemId != 0u)
                    player.Inventory.ItemDelete(itemId, Info.Entry.PushedItemCounts[i]);
            }

            State = QuestState.Completed;

            // mark repeatable quests for reset
            switch ((QuestRepeatPeriod)Info.Entry.QuestRepeatPeriodEnum)
            {
                case QuestRepeatPeriod.Daily:
                    Reset = GlobalQuestManager.Instance.NextDailyReset;
                    break;
                case QuestRepeatPeriod.Weekly:
                    Reset = GlobalQuestManager.Instance.NextWeeklyReset;
                    break;
            }

            player.AchievementManager.CheckAchievements(player, AchievementType.QuestComplete, Info.Entry.Id);
        }

        /// <summary>
        /// Complete the <see cref="IQuest"/> with the specified reward.
        /// </summary>
        public void RewardQuest(ushort reward)
        {
            if (State != QuestState.Achieved)
                return;

            // Handle all Rewards that are not chosen
            foreach (Quest2RewardEntry rewardEntry in Info.Rewards.Values.Where(x => x.Flags == 0))
                RewardQuest(rewardEntry);

            // Handle any chosen rewards
            if (reward != 0)
            {
                if (!Info.Rewards.TryGetValue(reward, out Quest2RewardEntry entry))
                    throw new QuestException($"Player {player.CharacterId} tried to complete quest {Info.Entry.Id} with invalid reward!");

                // TODO: make sure reward is valid for player, some rewards are conditional

                RewardQuest(entry);
            }

            // TODO: fixed rewards

            uint experience = Info.GetRewardExperience();
            if (experience != 0u)
                player.XpManager.GrantXp(experience, ExpReason.Quest);

            uint money = Info.GetRewardMoney();
            if (money != 0u)
                player.CurrencyManager.CurrencyAddAmount(CurrencyType.Credits, money);

            CompleteQuest();
        }

        private void RewardQuest(Quest2RewardEntry entry)
        {
            switch ((QuestRewardType)entry.Quest2RewardTypeId)
            {
                case QuestRewardType.Item:
                    player.Inventory.ItemCreate(InventoryLocation.Inventory, entry.ObjectId, entry.ObjectAmount);
                    break;
                case QuestRewardType.Money:
                    player.CurrencyManager.CurrencyAddAmount((CurrencyType)entry.ObjectId, entry.ObjectAmount);
                    break;
                case QuestRewardType.Reputation:
                    player.ReputationManager.UpdateReputation((NexusForever.Game.Static.Reputation.Faction)entry.ObjectId, entry.ObjectAmount);
                    break;
                case QuestRewardType.AccountCurrency:
                    player.Account.CurrencyManager.CurrencyAddAmount((NexusForever.Game.Static.AccountInventory.AccountCurrencyType)entry.ObjectId, entry.ObjectAmount);
                    break;
                default:
                {
                    log.LogWarning($"Unhandled quest reward type {entry.Quest2RewardTypeId}!");
                    break;
                }
            }

            log.LogTrace($"Recieved quest reward, type: {(QuestRewardType)entry.Quest2RewardTypeId}, objectId: {entry.ObjectId}, amount: {entry.ObjectAmount}.");
        }

        /// <summary>
        /// Update any <see cref="IQuestObjective"/>'s with supplied <see cref="QuestObjectiveType"/> and data with progress.
        /// </summary>
        public void ObjectiveUpdateAny(QuestObjectiveType type, uint progress)
        {
            ObjectiveUpdate(o => o.ObjectiveInfo.Entry.Type == (uint)type, progress);
        }

        public void ObjectiveUpdate(QuestObjectiveType type, uint data, uint progress)
        {
            ObjectiveUpdate(o => o.ObjectiveInfo.Entry.Type == (uint)type
                && (o.ObjectiveInfo.Data == data || NamesTargetGroupOf(type, o, data)), progress);
        }

        /// <summary>
        /// Returns if a creature objective names a target group holding the supplied creature instead of a creature.
        /// </summary>
        /// <remarks>
        /// About 80 kill, talk, activate and interaction objectives carry a TargetGroup id in Data that is no Creature2 id (Kill
        /// Moodie Hunters, Talk to the Survivors). Objectives whose Data is their reward pane group are counted by
        /// <see cref="RewardPaneObjectiveUpdate"/> and left out here so they do not count twice.
        /// </remarks>
        private static bool NamesTargetGroupOf(QuestObjectiveType type, IQuestObjective objective, uint creatureId)
        {
            if (type is not (QuestObjectiveType.KillCreature or QuestObjectiveType.TalkTo or QuestObjectiveType.ActivateEntity
                or QuestObjectiveType.ActivateEntity2 or QuestObjectiveType.SucceedCSI or QuestObjectiveType.GatheResource))
                return false;

            uint targetGroupId = objective.ObjectiveInfo.Data;
            if (targetGroupId == 0u || targetGroupId == objective.ObjectiveInfo.Entry.TargetGroupIdRewardPane
                || GameTable.GameTableManager.Instance.Creature2.GetEntry(targetGroupId) != null)
                return false;

            return Game.AssetManager.Instance.GetTargetGroupsForCreatureId(creatureId).Contains(targetGroupId);
        }

        /// <summary>
        /// Update any <see cref="IQuestObjective"/>'s with supplied <see cref="QuestObjectiveType"/> whose reward pane target group matches with progress.
        /// </summary>
        public void RewardPaneObjectiveUpdate(QuestObjectiveType type, uint targetGroupId, uint progress)
        {
            if (targetGroupId == 0u)
                return;

            // ActivateEntity objectives with a creature target progress through it, those without one (or naming the reward pane group
            // itself, Captives of the Dominion) use the reward pane group
            ObjectiveUpdate(o => o.ObjectiveInfo.Entry.Type == (uint)type && o.ObjectiveInfo.Entry.TargetGroupIdRewardPane == targetGroupId
                && (type != QuestObjectiveType.ActivateEntity || o.ObjectiveInfo.Data == 0u || o.ObjectiveInfo.Data == targetGroupId), progress);
        }

        /// <summary>
        /// Set any <see cref="IQuestObjective"/>'s with supplied <see cref="QuestObjectiveType"/> and data to an absolute progress value.
        /// </summary>
        public void ObjectiveSet(QuestObjectiveType type, uint data, uint value)
        {
            if (PendingDelete)
                return;

            if (State is not (QuestState.Accepted or QuestState.Achieved))
                return;

            bool changed = false;
            foreach (IQuestObjective objective in objectives
                .Where(o => o.ObjectiveInfo.Entry.Type == (uint)type && o.ObjectiveInfo.Data == data))
            {
                if (!CanUpdateObjective(objective))
                    continue;

                uint oldProgress = objective.Progress;
                objective.ObjectiveSet(value);
                if (objective.Progress == oldProgress)
                    continue;

                changed = true;
                SendQuestObjectiveUpdate(objective);
                scriptCollection?.Invoke<IQuestScript>(s => s.OnObjectiveUpdate(objective));
            }

            if (!changed)
                return;

            player.VisibilityUpdate();

            if (RequiredObjectivesComplete())
                CompleteOptionalObjectives();

            if (objectives.All(o => o.IsComplete()))
            {
                if (State == QuestState.Accepted)
                    State = QuestState.Achieved;
            }
            else if (State == QuestState.Achieved)
                State = QuestState.Accepted;
        }

        /// <summary>
        /// Update SpellSuccess objectives for a successful cast of supplied spell on supplied targets.
        /// </summary>
        /// <remarks>
        /// Objectives with a reward pane target group only count targets from that group, one per target hit (for example "use the Glop Gun on creatures").
        /// Objectives without one count the cast itself.
        /// </remarks>
        public void SpellSuccessUpdate(uint spell4Id, IReadOnlyList<IUnitEntity> targets)
        {
            foreach (IQuestObjective objective in objectives
                .Where(o => o.ObjectiveInfo.Entry.Data == spell4Id
                    && (QuestObjectiveType)o.ObjectiveInfo.Entry.Type is QuestObjectiveType.SpellSuccess
                        or QuestObjectiveType.SpellSuccess2
                        or QuestObjectiveType.SpellSuccess3
                        or QuestObjectiveType.SpellSuccess4)
                .ToList())
            {
                uint progress = 1u;
                uint targetGroupId = objective.ObjectiveInfo.Entry.TargetGroupIdRewardPane;
                if (targetGroupId != 0u)
                    progress = (uint)targets.Count(t => AssetManager.Instance.GetTargetGroupsForCreatureId(t.CreatureId).Contains(targetGroupId));

                if (progress > 0u)
                    ObjectiveUpdate(objective.ObjectiveInfo.Id, progress);
            }
        }

        /// <summary>
        /// Update objectives that ask the player to equip the supplied item.
        /// </summary>
        public void EquipObjectiveUpdate(uint itemId)
        {
            if (itemId == 0u)
                return;

            ObjectiveUpdate(o => o.ObjectiveInfo.EquipItemId == itemId, 1u);
        }

        private void ObjectiveUpdate(Func<IQuestObjective, bool> predicate, uint progress)
        {
            if (PendingDelete)
                return;

            if (State == QuestState.Achieved)
                return;

            // Order in reverse Index so that sequential steps don't completed by the same action
            foreach (IQuestObjective objective in objectives
                .Where(predicate)
                .OrderByDescending(o => o.Index))
            {
                if (objective.IsComplete())
                    continue;

                if (!CanUpdateObjective(objective))
                    continue;

                uint oldProgress = objective.Progress;
                objective.ObjectiveUpdate(progress);

                if (objective.Progress != oldProgress)
                    SendQuestObjectiveUpdate(objective);

                scriptCollection?.Invoke<IQuestScript>(s => s.OnObjectiveUpdate(objective));
            }

            player.VisibilityUpdate();

            // TODO: Should you be able to complete optional objectives after required are completed?
            if (RequiredObjectivesComplete())
                CompleteOptionalObjectives();

            if (objectives.All(o => o.IsComplete()))
                State = QuestState.Achieved;
        }

        /// <summary>
        /// Update any <see cref="IQuestObjective"/>'s with supplied ID with progress.
        /// </summary>
        public void ObjectiveUpdate(uint id, uint progress)
        {
            if (PendingDelete)
                return;

            if (State == QuestState.Achieved)
                return;

            IQuestObjective objective = GetQuestObjective(id);
            if (objective == null)
                return;

            if (objective.IsComplete())
                return;

            if (!CanUpdateObjective(objective))
                return;

            uint oldProgress = objective.Progress;
            objective.ObjectiveUpdate(progress);

            if (objective.Progress != oldProgress)
                SendQuestObjectiveUpdate(objective);

            scriptCollection?.Invoke<IQuestScript>(s => s.OnObjectiveUpdate(objective));

            player.VisibilityUpdate();

            // TODO: Should you be able to complete optional objectives after required are completed?
            if (RequiredObjectivesComplete())
                CompleteOptionalObjectives();

            if (objectives.All(o => o.IsComplete()))
                State = QuestState.Achieved;
        }

        private bool CanUpdateObjective(IQuestObjective objective)
        {
            if (objective.ObjectiveInfo.IsSequential())
            {
                for (int i = 0; i < objective.Index; i++)
                    if (!objectives[i].IsComplete())
                        return false;
            }

            // TODO: client also checks objective flags 1 and 8 in the same function
            return true;
        }

        private bool RequiredObjectivesComplete()
        {
            return objectives
                .Where(o => !o.ObjectiveInfo.IsOptional())
                .All(o => o.IsComplete());
        }

        private void CompleteOptionalObjectives()
        {
            foreach (IQuestObjective objective in objectives
                .Where(o => o.ObjectiveInfo.IsOptional() && !o.IsComplete()))
            {
                objective.Complete();
                SendQuestObjectiveUpdate(objective);
            }
        }

        private void SendQuestObjectiveUpdate(IQuestObjective objective)
        {
            // Only update objectives if the state isn't complete. Some scripts will complete quest without objective update.
            if (State == QuestState.Completed)
                return;

            player.Session.EnqueueMessageEncrypted(new ServerQuestObjectiveUpdate
            {
                QuestId   = Id,
                QuestObjectiveIndex     = objective.Index,
                Completed = objective.Progress
            });
        }

        /// <summary>
        /// Invoked when <see cref="QuestState"/> for <see cref="IQuest"/> is updated.
        /// </summary>
        private void OnStateChange(QuestState oldState)
        {
            player.Session.EnqueueMessageEncrypted(new ServerQuestStateChange
            {
                QuestId    = Id,
                QuestState = State
            });

            // check if this quest and state is a trigger for a new communicator message
            foreach (ICommunicatorMessage message in GlobalQuestManager.Instance.GetQuestCommunicatorQuestStateTriggers(Id, state))
                if (message.Meets(player))
                    player.QuestManager.QuestMention(message.QuestId);

            scriptCollection?.Invoke<IQuestScript>(s => s.OnQuestStateChange(State, oldState));
        }

        public IEnumerator<IQuestObjective> GetEnumerator()
        {
            return objectives.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
