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
                    or QuestObjectiveType.Unknown15
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

        /// <summary>
        /// Return if the <see cref="QuestObjective"/> has been completed.
        /// </summary>
        public bool IsComplete()
        {
            if (IsChecklist())
                return System.Numerics.BitOperations.PopCount(progress) >= ObjectiveInfo.Entry.Count;

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
            // checklist updates carry the unit's checklist index
            if (IsChecklist())
            {
                if (update < 32u)
                    Progress = progress | (1u << (int)update);
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
            Progress = Math.Min(value, GetMaxValue());
        }

        public void Complete()
        {
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
