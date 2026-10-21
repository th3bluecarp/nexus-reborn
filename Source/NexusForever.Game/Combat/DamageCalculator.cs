using Microsoft.Extensions.Logging;
using NexusForever.Game.Abstract.Combat;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Spell;
using NexusForever.Game.Abstract.Spell.Target;
using NexusForever.Game.Static.Entity;
using NexusForever.Game.Static.Spell;
using NexusForever.Game.Spell;
using NexusForever.GameTable;
using NexusForever.GameTable.Model;
using NexusForever.Network.World.Combat;

namespace NexusForever.Game.Combat
{
    public sealed class DamageCalculator : IDamageCalculator
    {
        #region Dependency Injection

        private readonly ILogger<DamageCalculator> log;
        private readonly IGameTableManager gameTableManager;
        private readonly IDamageDescription damageDescription;

        public DamageCalculator(
            ILogger<DamageCalculator> log,
            IGameTableManager gameTableManager,
            IDamageDescription damageDescription)
        {
            this.log               = log;
            this.gameTableManager  = gameTableManager;
            this.damageDescription = damageDescription;
        }

        #endregion

        /// <summary>
        /// Returns the calculated damage and updates the referenced <see cref="ISpellTargetEffectInfo"/> appropriately.
        /// </summary>
        /// <remarks>
        /// TODO: This should probably return an instance of a Class which describes all the damage done to both entities. Attackers can have reflected damage from this, etc.
        /// </remarks>
        public void CalculateDamage(ISpellExecutionContext executionContext, IUnitEntity victim, ISpellTargetEffectInfo info)
        {
            damageDescription.DamageType   = info.Entry.DamageType;
            damageDescription.CombatResult = CombatResult.Hit;

            var castData = new CombatLogCastData
            {
                CasterId     = executionContext.Spell.Caster.Guid,
                TargetId     = victim.Guid,
                SpellId      = executionContext.Spell.Parameters.SpellInfo.Entry.Id, // TODO: This was updated in order to use ISpell, check if correct
                CombatResult = damageDescription.CombatResult
            };

            if (CalculateDeflect(executionContext.Spell.Caster, victim))
            {
                damageDescription.CombatResult = CombatResult.Avoid;
                executionContext.AddCombatLog(new CombatLogDeflect
                    {
                        BMultiHit = false,
                        CastData  = castData
                    });
                return;
            }

            uint damage = CalculateBaseDamage(executionContext.Spell.Caster, victim, info.Entry);
            damageDescription.RawDamage       = damage;
            damageDescription.RawScaledDamage = damage;

            damage = CalculateBaseDamageVariance(damage);

            damage = GetDamageAfterPersonalModifiers(executionContext.Spell.Caster, victim, info.Entry.DamageType, damage);

            // ModifySpellEffect AMPs (Atomic Rush, Building Pressure, elemental talents) on the caster
            damage = (uint)Math.Round(damage * SpellModification.ApplyEffect(executionContext.Spell.Caster.SpellModifiers,
                executionContext.Spell.Parameters.SpellInfo.Entry, info.Entry, SpellModification.Damage, 1f));

            damage = GetDamageAfterArmorMitigation(executionContext.Spell.Caster, victim, info.Entry.DamageType, damage);

            // multi-hit: a second strike for a share of the damage, folded into this hit
            float multiHitChance = GetRatingPercentMod(Property.RatingMultiHitChance, executionContext.Spell.Caster);
            if (multiHitChance > 0f && IsSuccessfulChance(multiHitChance))
                damage += (uint)Math.Round(damage * GetRatingPercentMod(Property.RatingMultiHitAmount, executionContext.Spell.Caster));

            if (CalculateCrit(ref damage, executionContext.Spell.Caster, victim))
                damageDescription.CombatResult = CombatResult.Critical;

            uint preGlanceDamage = damage;
            if (CalculateGlance(ref damage, executionContext.Spell.Caster, victim))
            {
                damageDescription.GlanceAmount = preGlanceDamage - damage;
            }

            // absorb shields from spells take damage before the shield does
            uint absorbedAmount = victim.ConsumeAbsorb(info.Entry.DamageType, damage);
            damage -= absorbedAmount;
            damageDescription.AbsorbedAmount = absorbedAmount;

            uint shieldedAmount = CalculateShieldAmount(damage, victim);
            if (shieldedAmount > damage)
            {
                damageDescription.ShieldAbsorbAmount = damage;
                damage = 0;
            }
            else
            {
                damageDescription.ShieldAbsorbAmount = shieldedAmount;
                damage -= shieldedAmount;
            }

            // TODO: Add in other defensive modifiers

            damageDescription.AdjustedDamage = damage;

            info.AddDamage(damageDescription);

            // TODO: Queue Proc Events*/
        }

        /// <summary>
        /// After the victim took the damage: heal the attacker by its lifesteal share and, on a reflect roll, send the victim's reflect share back.
        /// </summary>
        /// <remarks>
        /// Reflected damage goes straight to health, it is not mitigated, absorbed or reflected again.
        /// </remarks>
        public void ApplyDamageReactions(ISpellExecutionContext executionContext, IUnitEntity victim, IDamageDescription damage)
        {
            IUnitEntity attacker = executionContext.Spell.Caster;
            if (damage.AdjustedDamage == 0u || attacker == victim || !attacker.IsAlive)
                return;

            uint stolen = (uint)Math.Round(damage.AdjustedDamage * GetRatingPercentMod(Property.RatingLifesteal, attacker));
            if (stolen > 0u && attacker.Health < attacker.MaxHealth)
            {
                attacker.ModifyHealth(stolen, DamageType.Heal, attacker);
                executionContext.AddCombatLog(new CombatLogLifesteal
                {
                    UnitId       = attacker.Guid,
                    HealthStolen = stolen
                });
            }

            float reflectChance = GetRatingPercentMod(Property.RatingDamageReflectChance, victim);
            if (reflectChance <= 0f || !victim.IsAlive || !IsSuccessfulChance(reflectChance))
                return;

            uint reflected = (uint)Math.Round(damage.AdjustedDamage * GetRatingPercentMod(Property.RatingDamageReflectAmount, victim));
            if (reflected == 0u)
                return;

            attacker.ModifyHealth(reflected, damage.DamageType, victim);
            executionContext.AddCombatLog(new CombatLogReflect
            {
                DamageAmount = reflected,
                RawDamage    = reflected,
                BKilled      = !attacker.IsAlive,
                DamageType   = damage.DamageType,
                EffectType   = SpellEffectType.Damage,
                CastData     = new CombatLogCastData
                {
                    CasterId     = victim.Guid,
                    TargetId     = attacker.Guid,
                    SpellId      = executionContext.Spell.Parameters.SpellInfo.Entry.Id,
                    CombatResult = CombatResult.Hit
                }
            });
        }

        /// <summary>
        /// Returns the calculated heal amount for a Heal or HealShields effect.
        /// </summary>
        /// <remarks>
        /// Uses the same base formula as damage (spell parameters, support power, target or caster max health, per level),
        /// scaled by the caster outgoing and target incoming healing multipliers. Heals can crit, armor and deflect do not apply.
        /// </remarks>
        public uint CalculateHeal(ISpellExecutionContext executionContext, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            IUnitEntity caster = executionContext.Spell.Caster;

            float heal = CalculateEffectAmount(caster, target, info.Entry);
            heal *= caster.GetPropertyValue(Property.HealingMultiplierOutgoing);
            heal *= target.GetPropertyValue(Property.HealingMultiplierIncoming);
            heal *= SpellModification.ApplyEffect(caster.SpellModifiers, executionContext.Spell.Parameters.SpellInfo.Entry, info.Entry, SpellModification.Damage, 1f);
            heal *= target.GetPersonalModifier(info.Entry.EffectType == SpellEffectType.HealShields ? 13u : 12u, caster.Guid);

            uint amount = CalculateBaseDamageVariance((uint)Math.Max(0f, heal));

            damageDescription.DamageType      = info.Entry.EffectType == SpellEffectType.HealShields ? DamageType.HealShields : DamageType.Heal;
            damageDescription.CombatResult    = CalculateCrit(ref amount, caster, target, false) ? CombatResult.Critical : CombatResult.Hit;
            damageDescription.RawDamage       = amount;
            damageDescription.RawScaledDamage = amount;
            damageDescription.AdjustedDamage  = amount;

            info.AddDamage(damageDescription);
            return amount;
        }

        /// <summary>
        /// Returns the base amount of an effect (multiplier, base value and scaling parameters) with no modifiers applied.
        /// </summary>
        /// <remarks>
        /// Used by Heal, HealShields and Absorption, which keep the multiplier in DataBits00 and the base value in DataBits01.
        /// </remarks>
        public uint CalculateEffectAmount(IUnitEntity caster, IUnitEntity target, Spell4EffectsEntry entry)
        {
            float basePropertyDamage = CalculateBasePropertyDamage(caster, entry);
            float baseEntityDamage   = CalculateBaseEntityDamage(caster, target, entry);
            float typeMultiplier     = BitConverter.UInt32BitsToSingle(entry.DataBits00);
            // Absorption stores its base value as a float, Heal and HealShields as an integer
            float typeBaseDamage     = entry.EffectType == SpellEffectType.Absorption ? BitConverter.UInt32BitsToSingle(entry.DataBits01) : entry.DataBits01;

            return (uint)Math.Max(0f, basePropertyDamage + ((typeBaseDamage + baseEntityDamage) * typeMultiplier));
        }

        /// <summary>
        /// Apply PersonalDmgHealMod multipliers: those on the attacker for damage it deals and those on the victim for damage it takes.
        /// </summary>
        private static uint GetDamageAfterPersonalModifiers(IUnitEntity attacker, IUnitEntity victim, DamageType damageType, uint damage)
        {
            if (damageType is < DamageType.Physical or > DamageType.Magic)
                return damage;

            uint offset = (uint)damageType - 1u;
            float multiplier = attacker.GetPersonalModifier(3u + offset, victim.Guid) * victim.GetPersonalModifier(6u + offset, attacker.Guid);
            return (uint)Math.Max(0f, damage * multiplier);
        }

        /// <summary>
        /// Get base damage value for the given <see cref="IUnitEntity"/> with the provided parameter data from the <see cref="Spell4EffectsEntry"/>.
        /// </summary>
        private uint CalculateBaseDamage(IUnitEntity caster, IUnitEntity target, Spell4EffectsEntry entry)
        {
            float basePropertyDamage = CalculateBasePropertyDamage(caster, entry);
            float baseEntityDamage   = CalculateBaseEntityDamage(caster, target, entry);

            float typeMultiplier = 1f;
            float typeBaseDamage = 0;
            switch (entry.EffectType)
            {
                case SpellEffectType.Transference:
                {
                    typeMultiplier = BitConverter.UInt32BitsToSingle(entry.DataBits02);
                    typeBaseDamage = entry.DataBits03;
                    break;
                }
                case SpellEffectType.Damage:
                case SpellEffectType.Heal:
                case SpellEffectType.DistanceDependentDamage:
                case SpellEffectType.DistributedDamage:
                case SpellEffectType.HealShields:
                case SpellEffectType.DamageShields:
                {
                    typeMultiplier = BitConverter.UInt32BitsToSingle(entry.DataBits00);
                    typeBaseDamage = entry.DataBits01;
                    break;
                }
            }

            if (caster.Type is not EntityType.Player and not EntityType.Ghost)
            {
                // TODO: some client code specific to non player entities
            }

            float baseDamage = basePropertyDamage + ((typeBaseDamage + baseEntityDamage) * typeMultiplier);

            float propertyMultiplier = caster.GetProperty((Property)(entry.DamageType + 140)).Value;

            return (uint)(propertyMultiplier * baseDamage);
        }

        private float CalculateBasePropertyDamage(IUnitEntity caster, Spell4EffectsEntry entry)
        {
            float GetProperty(Property property)
            {
                return caster.GetProperty(property)?.Value ?? 0f;
            }

            GameFormulaEntry forumulaEntry = gameTableManager.GameFormula.GetEntry(1266);

            float value = 0f;
            for (int i = 0; i < 4; i++)
            {
                float intermediateValue = 0f;
                switch (entry.ParameterType[i])
                {
                    case SpellEffectParameterType.Brutality:
                        intermediateValue = GetProperty(Property.Strength);
                        break;
                    case SpellEffectParameterType.Finesse:
                        intermediateValue = GetProperty(Property.Dexterity);
                        break;
                    case SpellEffectParameterType.Tech:
                        intermediateValue = GetProperty(Property.Technology);
                        break;
                    case SpellEffectParameterType.Moxie:
                        intermediateValue = GetProperty(Property.Magic);
                        break;
                    case SpellEffectParameterType.Insight:
                        intermediateValue = GetProperty(Property.Wisdom);
                        break;
                    case SpellEffectParameterType.Grit:
                        intermediateValue = GetProperty(Property.Stamina);
                        break;
                    // client defaults to a value of 0.25f if the game table entry is missing
                    case SpellEffectParameterType.AssaultPower:
                        intermediateValue = GetProperty(Property.AssaultRating) * forumulaEntry?.Datafloat0 ?? 0.25f;
                        break;
                    case SpellEffectParameterType.SupportPower:
                        intermediateValue = GetProperty(Property.SupportRating) * forumulaEntry?.Datafloat01 ?? 0.25f;
                        break;
                }

                value += intermediateValue * entry.ParameterValue[i];
            }

            if (value >= 0f)
                return MathF.Ceiling(value);
            else
                return MathF.Floor(value);
        }

        private float CalculateBaseEntityDamage(IUnitEntity caster, IUnitEntity target, Spell4EffectsEntry entry)
        {
            float value = 0f;
            for (int i = 0; i < 4; i++)
            {
                float intermediateValue = 0f;
                switch (entry.ParameterType[i])
                {
                    case SpellEffectParameterType.TargetMaxHealth:
                        intermediateValue = target.MaxHealth;
                        break;
                    case SpellEffectParameterType.CasterMaxHealth:
                        intermediateValue = caster.MaxHealth;
                        break;
                    case SpellEffectParameterType.CasterShieldCapacity:
                        intermediateValue = caster.Shield;
                        break;
                    case SpellEffectParameterType.TargetShieldCapacity:
                        intermediateValue = target.Shield;
                        break;
                    case SpellEffectParameterType.CasterMaxShieldCapacity:
                        intermediateValue = caster.MaxShieldCapacity;
                        break;
                    case SpellEffectParameterType.TargetMaxShieldCapacity:
                        intermediateValue = target.MaxShieldCapacity;
                        break;
                    case SpellEffectParameterType.ItemBudget:
                        intermediateValue = entry.ParameterValue[i];
                        break;
                    case SpellEffectParameterType.TargetCurrentHealth:
                        intermediateValue = target.Health;
                        break;
                    case SpellEffectParameterType.TargetMissingHealth:
                        intermediateValue = (target.MaxHealth - target.Health);
                        break;
                    case SpellEffectParameterType.TargetMissingShields:
                        intermediateValue = (target.MaxShieldCapacity - target.Shield);
                        break;
                    case SpellEffectParameterType.CasterCurrentHealth:
                        intermediateValue = caster.Health;
                        break;
                    case SpellEffectParameterType.CasterMissingHealth:
                        intermediateValue = (caster.MaxHealth - caster.Health);
                        break;
                    case SpellEffectParameterType.CasterMissingShields:
                        intermediateValue = (caster.MaxShieldCapacity - caster.Shield);
                        break;
                    case SpellEffectParameterType.PerLevel:
                        intermediateValue = caster.Level;
                        break;
                }

                value += intermediateValue * entry.ParameterValue[i];
            }

            return value;
        }

        private uint CalculateBaseDamageVariance(uint damage)
        {
            return (uint)(damage * (Random.Shared.Next(95, 103) / 100f));
        }

        private uint GetDamageAfterArmorMitigation(IUnitEntity attacker, IUnitEntity victim, DamageType damageType, uint damage)
        {
            GameFormulaEntry armorFormulaEntry = gameTableManager.GameFormula.GetEntry(1234);
            float maximumArmorMitigation = (float)(armorFormulaEntry.Dataint01 * 0.01);
            float mitigationPct = ConvertRating(armorFormulaEntry, victim.Level, victim.GetPropertyValue(Property.Armor));

            // armor pierce ignores a share of the victim's armor
            mitigationPct *= 1f - Math.Clamp(GetRatingPercentMod(Property.RatingArmorPierce, attacker), 0f, 1f);

            if (damageType == DamageType.Physical)
                mitigationPct += victim.GetPropertyValue(Property.DamageMitigationPctOffsetPhysical);
            else if (damageType == DamageType.Tech)
                mitigationPct += victim.GetPropertyValue(Property.DamageMitigationPctOffsetTech);
            else if (damageType == DamageType.Magic)
                mitigationPct += victim.GetPropertyValue(Property.DamageMitigationPctOffsetMagic);

            if (mitigationPct > 0f)
                damage = (uint)Math.Round(damage * (1f - Math.Clamp(mitigationPct, 0f, maximumArmorMitigation)));

            return damage;
        }

        private bool IsSuccessfulChance(float percentage)
        {
            return new Random().Next(1, 10000) <= percentage * 10000f;
        }

        /// <summary>
        /// Calculates and returns the shielded amount of damage.
        /// </summary>
        private uint CalculateShieldAmount(uint damage, IUnitEntity victim)
        {
            uint maxShieldAmount = (uint)(damage * victim.GetPropertyValue(Property.ShieldMitigationMax));
            uint shieldedAmount = Math.Min(victim.Shield, maxShieldAmount);

            return shieldedAmount;
        }

        /// <summary>
        /// Returns whether this attack was deflected.
        /// </summary>
        /// <remarks>Calculates chance to deflect an attack, avoiding all damage from that attack.</remarks>
        private bool CalculateDeflect(IUnitEntity attacker, IUnitEntity victim)
        {
            // strikethrough takes away from the victim's deflect chance
            float deflectChance = GetRatingPercentMod(Property.RatingAvoidIncrease, victim)
                - GetRatingPercentMod(Property.RatingAvoidReduce, attacker);
            return deflectChance > 0f && IsSuccessfulChance(deflectChance);
        }

        /// <summary>
        /// Returns whether this attack crit, and if so, modifies the referenced damage value appropriately.
        /// </summary>
        private bool CalculateCrit(ref uint damage, IUnitEntity attacker, IUnitEntity victim, bool hostile = true)
        {
            // the victim's crit deflect takes away from the crit chance, its critical mitigation from the bonus damage
            float critRate = GetRatingPercentMod(Property.RatingCritChanceIncrease, attacker)
                - (hostile ? GetRatingPercentMod(Property.RatingCritChanceDecrease, victim) : 0f);
            if (critRate <= 0f)
                return false;

            bool crit = IsSuccessfulChance(critRate);
            if (crit)
            {
                float bonus = GetRatingPercentMod(Property.RatingCritSeverityIncrease, attacker) - 1f;
                if (hostile)
                    bonus *= 1f - Math.Clamp(GetRatingPercentMod(Property.RatingCriticalMitigation, victim), 0f, 1f);
                damage = (uint)Math.Round(damage * (1f + Math.Max(0f, bonus)));
            }

            return crit;
        }

        /// <summary>
        /// Returns whether this attack was glanced, and if so, modifies the referenced damage value appropriately.
        /// </summary>
        private bool CalculateGlance(ref uint damage, IUnitEntity attacker, IUnitEntity victim)
        {
            float glanceChance = GetRatingPercentMod(Property.RatingGlanceChance, victim);
            if (glanceChance <= 0f)
                return false;

            bool glance = IsSuccessfulChance(glanceChance);
            if (glance)
                damage = (uint)Math.Round((float)(damage * (1 - GetRatingPercentMod(Property.RatingGlanceAmount, victim))));

            return glance;
        }

        private float GetRatingPercentMod(Property property, IUnitEntity entity)
        {
            GameFormulaEntry gameFormula = null;

            switch (property)
            {
                case Property.Armor:
                    gameFormula = gameTableManager.GameFormula.GetEntry(1234);
                    break;
                case Property.RatingArmorPierce:
                case Property.IgnoreArmorBase:
                    gameFormula = gameTableManager.GameFormula.GetEntry(1269);
                    break;
                case Property.RatingAvoidReduce: // Strikethrough
                case Property.BaseAvoidReduceChance:
                    gameFormula = gameTableManager.GameFormula.GetEntry(1230);
                    break;
                case Property.RatingAvoidIncrease: // Deflect
                case Property.BaseAvoidChance:
                    gameFormula = gameTableManager.GameFormula.GetEntry(1235);
                    break;
                case Property.RatingCriticalMitigation:
                case Property.BaseCriticalMitigation:
                    gameFormula = gameTableManager.GameFormula.GetEntry(1236);
                    break;
                case Property.RatingCritChanceDecrease: // Deflect Crit Chance
                case Property.BaseAvoidCritChance:
                    gameFormula = gameTableManager.GameFormula.GetEntry(1236);
                    break;
                case Property.RatingCritChanceIncrease:
                    gameFormula = gameTableManager.GameFormula.GetEntry(1231);
                    break;
                case Property.RatingCritSeverityIncrease:
                    gameFormula = gameTableManager.GameFormula.GetEntry(1232);
                    break;
                case Property.CCDurationModifier:
                    gameFormula = gameTableManager.GameFormula.GetEntry(1274);
                    break;
                case Property.RatingDamageReflectAmount:
                case Property.BaseDamageReflectAmount:
                    gameFormula = gameTableManager.GameFormula.GetEntry(1272);
                    break;
                case Property.RatingDamageReflectChance:
                case Property.BaseDamageReflectChance:
                    gameFormula = gameTableManager.GameFormula.GetEntry(1241);
                    break;
                case Property.RatingFocusRecovery:
                case Property.BaseFocusRecoveryInCombat:
                    gameFormula = gameTableManager.GameFormula.GetEntry(1237);
                    break;
                case Property.RatingGlanceAmount:
                case Property.BaseGlanceAmount:
                    gameFormula = gameTableManager.GameFormula.GetEntry(1271);
                    break;
                case Property.RatingGlanceChance:
                case Property.BaseGlanceChance:
                    gameFormula = gameTableManager.GameFormula.GetEntry(1245);
                    break;
                case Property.RatingIntensity:
                case Property.BaseIntensity:
                    gameFormula = gameTableManager.GameFormula.GetEntry(1243);
                    break;
                case Property.RatingLifesteal:
                case Property.BaseLifesteal:
                    gameFormula = gameTableManager.GameFormula.GetEntry(1233);
                    break;
                case Property.RatingMultiHitAmount:
                case Property.BaseMultiHitAmount:
                    gameFormula = gameTableManager.GameFormula.GetEntry(1270);
                    break;
                case Property.RatingMultiHitChance:
                case Property.BaseMultiHitChance:
                    gameFormula = gameTableManager.GameFormula.GetEntry(1240);
                    break;
                case Property.RatingVigor:
                case Property.BaseVigor:
                    gameFormula = gameTableManager.GameFormula.GetEntry(1244);
                    break;
                default:
                    log.LogWarning($"Unhandled Property in calculating Percentage from Rating: {property}");
                    break;
            }

            if (gameFormula == null)
                return GetBasePercentMod(property, entity);

            // Return a decimal representing the % applied by this rating.
            float ratingMod = ConvertRating(gameFormula, entity.Level, entity.GetPropertyValue(property));
            return Math.Min(GetBasePercentMod(property, entity) + ratingMod, gameFormula.Dataint01 * 0.01f);
        }

        /// <summary>
        /// Convert a rating to a fraction the way the client does (WildStar64.exe 0x140453b60, shared by every rating getter).
        /// </summary>
        /// <remarks>
        /// Each point is worth Datafloat0 / level * Datafloat01 percent, plus Dataint0, up to the knee at Datafloat02 percent. Past the knee the
        /// extra rating t adds cos(t / (t + Datafloat03) * pi / 2) * t points' worth, a soft cap. The result is a percentage, so it is scaled by 0.01.
        /// </remarks>
        private static float ConvertRating(GameFormulaEntry formula, uint level, float rating)
        {
            float perPoint = formula.Datafloat0 / Math.Max(1u, level) * formula.Datafloat01;
            if (perPoint <= 0f || rating <= 0f)
                return 0f;

            float knee = (formula.Datafloat02 - formula.Dataint0) / perPoint;
            float percent;
            if (rating < knee)
                percent = perPoint * rating + formula.Dataint0;
            else
            {
                float extra = rating - knee;
                percent = MathF.Cos(extra / (extra + formula.Datafloat03) * MathF.PI * 0.5f) * extra * perPoint + formula.Datafloat02;
            }

            return percent * 0.01f;
        }

        private float GetBasePercentMod(Property property, IUnitEntity entity)
        {
            float baseValue = 0f;
            switch (property)
            {
                case Property.RatingArmorPierce:
                case Property.IgnoreArmorBase:
                    baseValue = entity.GetPropertyValue(Property.IgnoreArmorBase);
                    break;
                case Property.RatingAvoidReduce: // Strikethrough
                case Property.BaseAvoidReduceChance:
                    baseValue = entity.GetPropertyValue(Property.BaseAvoidReduceChance);
                    break;
                case Property.RatingAvoidIncrease: // Deflect
                case Property.BaseAvoidChance:
                    baseValue = entity.GetPropertyValue(Property.BaseAvoidChance);
                    break;
                case Property.RatingCriticalMitigation:
                case Property.BaseCriticalMitigation:
                    baseValue = entity.GetPropertyValue(Property.BaseCriticalMitigation);
                    break;
                case Property.RatingCritChanceDecrease: // Deflect Crit Chance
                case Property.BaseAvoidCritChance:
                    baseValue = entity.GetPropertyValue(Property.BaseAvoidCritChance);
                    break;
                case Property.RatingCritChanceIncrease:
                case Property.BaseCritChance:
                    baseValue = entity.GetPropertyValue(Property.BaseCritChance);
                    break;
                case Property.RatingCritSeverityIncrease:
                    // UnitProperty2 default 1.5, formula 1232 caps the total at 300 percent
                    baseValue = entity.GetPropertyValue(Property.CriticalHitSeverityMultiplier);
                    break;
                case Property.RatingDamageReflectAmount:
                case Property.BaseDamageReflectAmount:
                    baseValue = entity.GetPropertyValue(Property.BaseDamageReflectAmount);
                    break;
                case Property.RatingDamageReflectChance:
                case Property.BaseDamageReflectChance:
                    baseValue = entity.GetPropertyValue(Property.BaseDamageReflectChance);
                    break;
                case Property.RatingFocusRecovery:
                case Property.BaseFocusRecoveryInCombat:
                    baseValue = entity.GetPropertyValue(Property.BaseFocusRecoveryInCombat);
                    break;
                case Property.RatingGlanceAmount:
                case Property.BaseGlanceAmount:
                    baseValue = entity.GetPropertyValue(Property.BaseGlanceAmount);
                    break;
                case Property.RatingGlanceChance:
                case Property.BaseGlanceChance:
                    baseValue = entity.GetPropertyValue(Property.BaseGlanceChance);
                    break;
                case Property.RatingIntensity:
                case Property.BaseIntensity:
                    baseValue = entity.GetPropertyValue(Property.BaseIntensity);
                    break;
                case Property.RatingLifesteal:
                case Property.BaseLifesteal:
                    baseValue = entity.GetPropertyValue(Property.BaseLifesteal);
                    break;
                case Property.RatingMultiHitAmount:
                case Property.BaseMultiHitAmount:
                    baseValue = entity.GetPropertyValue(Property.BaseMultiHitAmount);
                    break;
                case Property.RatingMultiHitChance:
                case Property.BaseMultiHitChance:
                    baseValue = entity.GetPropertyValue(Property.BaseMultiHitChance);
                    break;
                case Property.RatingVigor:
                case Property.BaseVigor:
                    baseValue = entity.GetPropertyValue(Property.BaseVigor);
                    break;
                default:
                    log.LogWarning($"Unhandled Property in calculating Percentage from Base: {property}");
                    break;
            }

            return baseValue;
        }
    }
}
