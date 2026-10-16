using NexusForever.Game.Static.Spell;

namespace NexusForever.Game.Spell.Effect.Data
{
    /// <summary>
    /// DistanceDependentDamage uses the Damage data layout (DataBits00 multiplier, DataBits01 base damage).
    /// </summary>
    [SpellEffectData(SpellEffectType.DistanceDependentDamage)]
    public class SpellEffectDistanceDependentDamageData : SpellEffectDamageData
    {
    }
}
