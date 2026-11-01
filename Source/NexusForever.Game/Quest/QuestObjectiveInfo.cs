using System.Text.RegularExpressions;
using NexusForever.Game.Abstract.Quest;
using NexusForever.Game.Static.Quest;
using NexusForever.GameTable.Model;
using NexusForever.GameTable;

namespace NexusForever.Game.Quest
{
    public class QuestObjectiveInfo : IQuestObjectiveInfo
    {
        public uint Id => Entry.Id;
        public QuestObjectiveType Type => (QuestObjectiveType)Entry.Type;

        public QuestObjectiveEntry Entry { get; }

        /// <summary>
        /// Create a new <see cref="QuestObjectiveInfo"/> with supplied <see cref="QuestObjectiveEntry"/>.
        /// </summary>
        public uint Data { get; }
        public uint EquipItemId { get; }

        public QuestObjectiveInfo(QuestObjectiveEntry entry)
        {
            Entry = entry;
            // Data 0, or a Data that is neither a creature nor a target group (The Face of the Enemy objective 21318: Data 5968,
            // text "Detonate the $(creature=73668)"): the creature named in the text is the one the player activates
            Data  = Type is QuestObjectiveType.ActivateEntity or QuestObjectiveType.SucceedCSI or QuestObjectiveType.KillCreature
                && (entry.Data == 0u || IsUnknownData(entry.Data))
                && CreatureFromText(entry.LocalizedTextIdFull) is uint textCreature and not 0u
                ? textCreature
                : entry.Data;
            if (Type == QuestObjectiveType.ActivateEntity && entry.Data == 0u)
                EquipItemId = EquipItemFromText(entry.LocalizedTextIdFull);
        }

        // objectives with Data 0 that name exactly one creature in their text, as $(creature=27196): 274 ActivateEntity, 15 SucceedCSI, 1 KillCreature
        // 14 ActivateEntity objectives with Data 0 read Equip the $(item=N)
        private static uint EquipItemFromText(uint textId)
        {
            string text = GameTableManager.Instance.TextEnglish?.GetEntry(textId);
            if (string.IsNullOrEmpty(text) || !text.TrimStart().StartsWith("Equip"))
                return 0u;

            uint[] ids = Regex.Matches(text, @"item=([0-9]{1,9})")
                .Select(m => uint.Parse(m.Groups[1].Value))
                .Distinct()
                .ToArray();
            return ids.Length == 1 ? ids[0] : 0u;
        }

        /// <summary>
        /// Returns if objective data is neither a creature nor a target group.
        /// </summary>
        private static bool IsUnknownData(uint data)
        {
            return GameTableManager.Instance.Creature2.GetEntry(data) == null
                && GameTableManager.Instance.TargetGroup.GetEntry(data) == null;
        }

        private static uint CreatureFromText(uint textId)
        {
            string text = GameTableManager.Instance.TextEnglish?.GetEntry(textId);
            if (string.IsNullOrEmpty(text))
                return 0u;

            uint[] ids = Regex.Matches(text, @"creature=([0-9]{1,9})")
                .Select(m => uint.Parse(m.Groups[1].Value))
                .Distinct()
                .ToArray();
            return ids.Length == 1 ? ids[0] : 0u;
        }

        /// <summary>
        /// Quest objective is sequential, previous quest objective must be complete.
        /// </summary>
        public bool IsSequential()
        {
            return ((QuestObjectiveFlags)Entry.Flags & QuestObjectiveFlags.Sequential) != 0;
        }

        /// <summary>
        /// Quest objective is hidden until previous objective is complete.
        /// </summary>
        public bool IsHidden()
        {
            return ((QuestObjectiveFlags)Entry.Flags & QuestObjectiveFlags.Hidden) != 0;
        }

        /// <summary>
        /// Quest objective is optional, doesn't need to be complete for quest to be achieved.
        /// </summary>
        public bool IsOptional()
        {
            return ((QuestObjectiveFlags)Entry.Flags & QuestObjectiveFlags.Optional) != 0;
        }

        public bool HasUnknown0200()
        {
            return ((QuestObjectiveFlags)Entry.Flags & QuestObjectiveFlags.Unknown0200) != 0;
        }
    }
}
