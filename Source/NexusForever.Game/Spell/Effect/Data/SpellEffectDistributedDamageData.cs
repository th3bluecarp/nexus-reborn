using NexusForever.Game.Static.Spell;

namespace NexusForever.Game.Spell.Effect.Data
{
    /// <summary>
    /// DistributedDamage uses the Damage data layout (DataBits00 multiplier, DataBits01 base damage).
    /// </summary>
    [SpellEffectData(SpellEffectType.DistributedDamage)]
    public class SpellEffectDistributedDamageData : SpellEffectDamageData
    {
    }
}
