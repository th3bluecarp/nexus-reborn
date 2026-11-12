using Microsoft.EntityFrameworkCore.ChangeTracking;
using NexusForever.Database.Character;
using NexusForever.Database.Character.Model;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Quest;
using NexusForever.Game.Static.Quest;
using NexusForever.GameTable.Model;

namespace NexusForever.Game.Quest
{
    public class QuestObjective : IQuestObjective
    {
        [Flags]
        public enum QuestObjectiveSaveMask
        {
            None     = 0x00,
            Create   = 0x01,
            Progress = 0x02,
            Timer    = 0x04
        }

        public IQuestInfo QuestInfo { get; }
        public IQuestObjectiveInfo ObjectiveInfo { get; }

        public byte Index { get; }

        public uint Progress
        {
            get => progress;
            set
            {
                saveMask |= QuestObjectiveSaveMask.Progress;
                progress = value;
            }
        }

        private uint progress;

        /// <summary>
        /// Progress represented in the count format expected by the client. Some server-side objective state uses a bitmask.
        /// </summary>
        public uint ClientProgress => IsKillRewardTargetGroup()
            ? (IsComplete() ? ObjectiveInfo.Entry.Count : 0u)
            : Progress;

        public uint? Timer
        {
            get => timer;
            set
            {
                saveMask |= QuestObjectiveSaveMask.Timer;
                timer = value;
            }
        }

        private uint? timer;

        private QuestObjectiveSaveMask saveMask;

        private readonly IPlayer player;

        /// <summary>
        /// Create a new <see cref="IQuestObjective"/> from an existing database model.
        /// </summary>
        public QuestObjective(IPlayer owner, IQuestInfo questInfo, IQuestObjectiveInfo objectiveInfo, CharacterQuestObjectiveModel model)
        {
            player        = owner;

            QuestInfo     = questInfo;
            ObjectiveInfo = objectiveInfo;
            Index         = model.Index;
            progress      = model.Progress;
            timer         = model.Timer;
        }

        /// <summary>
        /// Create a new <see cref="IQuestObjective"/> from supplied <see cref="QuestObjectiveEntry"/>.
        /// </summary>
        public QuestObjective(IPlayer owner, IQuestInfo questInfo, IQuestObjectiveInfo objectiveInfo, byte index)
        {
            player        = owner;

            QuestInfo     = questInfo;
            ObjectiveInfo = objectiveInfo;
            Index         = index;

            if (objectiveInfo.Entry.MaxTimeAllowedMS != 0u)
            {
                // TODO
            }

            saveMask = QuestObjectiveSaveMask.Create;
        }

        public void Save(CharacterContext context)
        {
            if (saveMask == QuestObjectiveSaveMask.None)
                return;

            if ((saveMask & QuestObjectiveSaveMask.Create) != 0)
            {
                context.Add(new CharacterQuestObjectiveModel
                {
                    Id       = player.CharacterId,
                    QuestId  = (ushort)QuestInfo.Entry.Id,
                    Index    = Index,
                    Progress = Progress
                });
            }
            else
            {
                var model = new CharacterQuestObjectiveModel
                {
                    Id      = player.CharacterId,
                    QuestId = (ushort)QuestInfo.Entry.Id,
                    Index   = Index
                };

                EntityEntry<CharacterQuestObjectiveModel> entity = context.Entry(model);
                if ((saveMask & QuestObjectiveSaveMask.Progress) != 0)
                {
                    model.Progress = Progress;
                    entity.Property(p => p.Progress).IsModified = true;
                }

                if ((saveMask & QuestObjectiveSaveMask.Timer) != 0)
                {
                    // TODO
                }
            }

            saveMask = QuestObjectiveSaveMask.None;
        }

        public void Update(double lastTick)
        {
            // TODO: update timer
        }

        private bool IsDynamic()
        {
            // dynamic objectives have their progress based on percentage rather than count
            return ObjectiveInfo.Type is QuestObjectiveType.KillCreature
                    or QuestObjectiveType.KillTargetGroups
                    or QuestObjectiveType.KillTargetGroup
                    or QuestObjectiveType.KillCreature2
                && ObjectiveInfo.Entry.Count > 1u
                && !ObjectiveInfo.HasUnknown0200();
        }

        /// <summary>
        /// Checklist objectives (Burn Skeech Huts, Overload the Dominion Cannons) keep one bit per unit's QuestChecklistIdx as their
        /// progress, the client reads type 10 and 14 progress as that bitmask (WildStar64.exe 0x1405517a0) and gates each unit on its bit.
        /// </summary>
        private bool IsChecklist()
        {
            return ObjectiveInfo.Type is QuestObjectiveType.ActivateTargetGroupChecklist or QuestObjectiveType.Unknown10;
        }

        private bool IsKillRewardTargetGroup()
        {
            return ObjectiveInfo.Type == QuestObjectiveType.KillRewardTargetGroup;
        }

        private System.Collections.Immutable.ImmutableList<uint> GetKillRewardTargets()
        {
            return AssetManager.Instance.GetTargetGroupCreatureIds(ObjectiveInfo.Entry.TargetGroupIdRewardPane);
        }

        /// <summary>
        /// Return if the <see cref="QuestObjective"/> has been completed.
        /// </summary>
        public bool IsComplete()
        {
            if (IsChecklist())
                return System.Numerics.BitOperations.PopCount(progress) >= ObjectiveInfo.Entry.Count;

            if (IsKillRewardTargetGroup())
            {
                int targetCount = Math.Min(GetKillRewardTargets().Count, 32);
                return targetCount == 0
                    ? progress >= ObjectiveInfo.Entry.Count
                    : System.Numerics.BitOperations.PopCount(progress) >= targetCount;
            }

            return progress >= GetMaxValue();
        }

        private uint GetMaxValue()
        {
            return IsDynamic() ? 1000u : ObjectiveInfo.Entry.Count;
        }

        /// <summary>
        /// Update object progress with supplied update.
        /// </summary>
        public void ObjectiveUpdate(uint update)
        {
            // Type 15's Count is one even when its reward-pane group names several required creatures. Keep one bit per
            // distinct creature entry so killing one member repeatedly cannot complete the group.
            if (IsKillRewardTargetGroup())
            {
                System.Collections.Immutable.ImmutableList<uint> targets = GetKillRewardTargets();
                int index = targets.IndexOf(update);
                if (index >= 0 && index < 32)
                    Progress = progress | (1u << index);
                return;
            }

            // checklist updates carry the unit's checklist index
            if (IsChecklist())
            {
                if (update < 32u)
                {
                    uint bit = 1u << (int)update;
                    // a unit whose bit is already set was used again after its respawn: our world often has fewer units than
                    // the count asks (Algoroc has 3 Pure Loftite Veins for "Mark 4"), so it fills the next free bit instead
                    if ((progress & bit) != 0u)
                    {
                        int free = System.Numerics.BitOperations.TrailingZeroCount(~progress);
                        if (free < 32)
                            bit = 1u << free;
                    }
                    Progress = progress | bit;
                }
                return;
            }

            if (IsDynamic())
                update = (uint)(((float)update / ObjectiveInfo.Entry.Count) * 1000f);

            Progress = Math.Min(progress + update, GetMaxValue());
        }

        /// <summary>
        /// Set objective progress to an absolute value, capped at the objective maximum.
        /// </summary>
        public void ObjectiveSet(uint value)
        {
            if (IsKillRewardTargetGroup())
            {
                if (value >= ObjectiveInfo.Entry.Count)
                    Complete();
                else
                    Progress = 0u;
                return;
            }

            Progress = Math.Min(value, GetMaxValue());
        }

        public void Complete()
        {
            if (IsKillRewardTargetGroup())
            {
                int targetCount = Math.Min(GetKillRewardTargets().Count, 32);
                Progress = targetCount == 0
                    ? ObjectiveInfo.Entry.Count
                    : targetCount == 32 ? uint.MaxValue : (1u << targetCount) - 1u;
                return;
            }

            if (IsChecklist())
            {
                uint bits = progress;
                for (int i = 0; i < 32 && System.Numerics.BitOperations.PopCount(bits) < ObjectiveInfo.Entry.Count; i++)
                    bits |= 1u << i;
                Progress = bits;
                return;
            }

            Progress = GetMaxValue();
        }
    }
}
