using NexusForever.Game.Static.Spell;
using NexusForever.GameTable;
using NexusForever.GameTable.Model;

namespace NexusForever.Game.Spell
{
    /// <summary>
    /// Rules for the ModifySpell and ModifySpellEffect effects (AMPs and talents that change other abilities), decoded from the
    /// Spell4Modification and Spell4EffectModification tables and the tooltips that quote them.
    /// </summary>
    /// <remarks>
    /// A modification row belongs to the modifying effect (Spell4EffectsId is the ModifySpell or ModifySpellEffect effect itself).
    /// The effect picks its targets: DataBits00 one spell (ModifySpell, Spell4 or Spell4Base id) or one effect (ModifySpellEffect),
    /// DataBits01 and DataBits03 a spell group (Spell4GroupList), DataBits04 the effect type for ModifySpellEffect.
    /// ModificationTypeEnum 0 gives value * Data00 + Data01, capped at Data02 when it is set; 1 gives value * Data00; 2 sets the value to Data00.
    /// </remarks>
    public static class SpellModification
    {
        /// <summary>
        /// ModificationParameterEnum values with a known meaning.
        /// </summary>
        public const uint CastTime        = 0u;
        public const uint Cooldown        = 3u;
        public const uint Duration        = 4u;
        public const uint Damage          = 6u;
        public const uint Cost            = 8u;

        private static ILookup<uint, Spell4ModificationEntry> spellRows;
        private static ILookup<uint, Spell4EffectModificationEntry> effectRows;

        private static ILookup<uint, Spell4ModificationEntry> SpellRows => spellRows ??= GameTableManager.Instance.Spell4Modification.Entries
            .ToLookup(e => e.Spell4EffectsId);
        private static ILookup<uint, Spell4EffectModificationEntry> EffectRows => effectRows ??= GameTableManager.Instance.Spell4EffectModification.Entries
            .ToLookup(e => e.Spell4EffectsId);

        /// <summary>
        /// Apply the ModifySpell effects in <paramref name="modifiers"/> that target supplied spell to a value of supplied parameter.
        /// </summary>
        public static float ApplySpell(IEnumerable<Spell4EffectsEntry> modifiers, Spell4Entry spell, uint parameter, float value)
        {
            foreach (Spell4EffectsEntry modifier in modifiers)
            {
                if (modifier.EffectType != SpellEffectType.ModifySpell || !TargetsSpell(modifier, spell))
                    continue;

                foreach (Spell4ModificationEntry row in SpellRows[modifier.Id].OrderBy(r => r.Priority))
                    if (row.ModificationParameterEnum == parameter)
                        value = Apply(row.ModificationTypeEnum, row.Data00, row.Data01, row.Data02, value);
            }

            return value;
        }

        /// <summary>
        /// Apply the ModifySpellEffect effects in <paramref name="modifiers"/> that target supplied effect to a value of supplied parameter.
        /// </summary>
        /// <remarks>
        /// For <see cref="Damage"/> the value is a multiplier and only rows that read as one are used (Data01 below 1), rows adding whole
        /// numbers are skipped until their meaning is known.
        /// </remarks>
        public static float ApplyEffect(IEnumerable<Spell4EffectsEntry> modifiers, Spell4Entry spell, Spell4EffectsEntry effect, uint parameter, float value)
        {
            foreach (Spell4EffectsEntry modifier in modifiers)
            {
                if (modifier.EffectType != SpellEffectType.ModifySpellEffect)
                    continue;

                if (modifier.DataBits04 != 0u && modifier.DataBits04 != (uint)effect.EffectType)
                    continue;

                bool targeted = modifier.DataBits00 != 0u
                    ? modifier.DataBits00 == effect.Id
                    : InGroup(spell, modifier.DataBits01) || InGroup(spell, modifier.DataBits03);
                if (!targeted)
                    continue;

                foreach (Spell4EffectModificationEntry row in EffectRows[modifier.Id].OrderBy(r => r.Priority))
                {
                    if (row.ModificationParameterEnum != parameter)
                        continue;
                    if (row.EffectTypeEnum != 0u && row.EffectTypeEnum != (uint)effect.EffectType)
                        continue;
                    if (parameter == Damage && row.Data01 >= 1f)
                        continue;

                    value = Apply(row.ModificationTypeEnum, row.Data00, row.Data01, row.Data02, value);
                }
            }

            return value;
        }

        private static float Apply(uint type, float data00, float data01, float data02, float value)
        {
            switch (type)
            {
                case 0u:
                    value = value * data00 + data01;
                    if (data02 > 0f)
                        value = Math.Min(value, data02);
                    return Math.Max(0f, value);
                // Efficiency (0.98, 1, 1) is a plain 2 percent cut, Data01 and Data02 are not an add and a cap here
                case 1u:
                    return Math.Max(0f, value * data00);
                case 2u:
                    return Math.Max(0f, data00);
                default:
                    return value;
            }
        }

        private static bool TargetsSpell(Spell4EffectsEntry modifier, Spell4Entry spell)
        {
            if (modifier.DataBits00 != 0u)
                return spell.Id == modifier.DataBits00 || spell.Spell4BaseIdBaseSpell == modifier.DataBits00;

            return InGroup(spell, modifier.DataBits01) || InGroup(spell, modifier.DataBits03);
        }

        private static bool InGroup(Spell4Entry spell, uint group)
        {
            if (group == 0u || spell.Spell4GroupListId == 0u)
                return false;

            Spell4GroupListEntry groupList = GameTableManager.Instance.Spell4GroupList.GetEntry(spell.Spell4GroupListId);
            return groupList != null && groupList.SpellGroupIds.Contains(group);
        }
    }
}
