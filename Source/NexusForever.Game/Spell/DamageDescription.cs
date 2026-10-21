using NexusForever.Game.Abstract.Spell;
using NexusForever.Game.Static.Spell;
using NetworkDamageDescription = NexusForever.Network.World.Message.Model.Spell.SpellEffectResult;

namespace NexusForever.Game.Spell
{
    public class DamageDescription : IDamageDescription
    {
        public uint RawDamage { get; set; }
        public uint RawScaledDamage { get; set; }
        public uint AbsorbedAmount { get; set; }
        public uint ShieldAbsorbAmount { get; set; }
        public uint AdjustedDamage { get; set; }
        public uint OverkillAmount { get; set; }
        public uint GlanceAmount { get; set; }
        public bool KilledTarget { get; set; }
        public CombatResult CombatResult { get; set; }
        public DamageType DamageType { get; set; }

        public NetworkDamageDescription Build()
        {
            return new NetworkDamageDescription
            {
                RawDamage          = RawDamage,
                RawScaledDamage    = RawScaledDamage,
                AbsorbedAmount     = AbsorbedAmount,
                ShieldAbsorbAmount = ShieldAbsorbAmount,
                AdjustedDamage     = AdjustedDamage,
                OverkillAmount     = OverkillAmount,
                Glance             = GlanceAmount,
                KilledTarget       = KilledTarget,
                CombatResult       = CombatResult,
                DamageType         = DamageType
            };
        }
    }
}
