using System.Numerics;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Entity.Creature;
using NexusForever.Game.Static.Reputation;
using NexusForever.Script.Template;
using NexusForever.Script.Template.Filter;

namespace NexusForever.Script.Main.Tutorial.Script.Part2
{
    /// <summary>
    /// The arkship Sim-Turrets (The Face of the Enemy: "Interrupt the Sim-Turret using one of your abilities, then destroy
    /// it", then "Kill Virtual Legionnaires"). Creature2 gives the turrets the FriendlyToAll faction, so no ability could
    /// target them, and the Virtual Legionnaires (73492) have no world spawn: retail dropped them in during the simulation.
    /// Stand-in: the turrets spawn with the Virtual Battle Beasts' hostile faction, and each destroyed turret releases two
    /// Virtual Legionnaires next to it, which despawn after ten minutes if nobody kills them.
    /// SEMANTICS: "Sim-Turrets are hostile", "Legionnaires drop in when a Sim-Turret dies" (2026-11-01, quest bot).
    /// </summary>
    [ScriptFilterCreatureId(73494)]
    public class SimTurretEntityScript : IWorldEntityScript, INonPlayerScript, IOwnedScript<IWorldEntity>
    {
        private const Faction VirtualBattleBeastFaction = (Faction)1441;
        private const uint VirtualLegionnaire = 73492u;
        private const int LegionnairesPerTurret = 2;

        private IWorldEntity owner;

        #region Dependency Injection

        private readonly IEntityFactory entityFactory;
        private readonly ICreatureInfoManager creatureInfoManager;

        public SimTurretEntityScript(
            IEntityFactory entityFactory,
            ICreatureInfoManager creatureInfoManager)
        {
            this.entityFactory       = entityFactory;
            this.creatureInfoManager = creatureInfoManager;
        }

        #endregion

        public void OnLoad(IWorldEntity owner)
        {
            this.owner = owner;
            owner.Faction1 = VirtualBattleBeastFaction;
            owner.Faction2 = VirtualBattleBeastFaction;
        }

        public void OnDeath()
        {
            if (owner?.Map == null)
                return;

            ICreatureInfo creatureInfo = creatureInfoManager.GetCreatureInfo(VirtualLegionnaire);
            if (creatureInfo == null)
                return;

            for (int i = 0; i < LegionnairesPerTurret; i++)
            {
                float angle = MathF.PI * 2f * i / LegionnairesPerTurret + MathF.PI / 4f;
                Vector3 position = owner.Position + new Vector3(MathF.Cos(angle) * 4f, 0f, MathF.Sin(angle) * 4f);
                IWorldEntity legionnaire = entityFactory.CreateWorldEntity(creatureInfo.Entry.CreationTypeEnum);
                legionnaire.Initialise(creatureInfo);
                legionnaire.Faction1 = VirtualBattleBeastFaction;
                legionnaire.Faction2 = VirtualBattleBeastFaction;
                legionnaire.Rotation = new Vector3(angle + MathF.PI, 0f, 0f);
                legionnaire.AddToMap(owner.Map, position, (_, _, _) =>
                {
                    if (legionnaire is IUnitEntity unit)
                        unit.DespawnAfter(TimeSpan.FromMinutes(10d));
                }); 
            }
        }
    }
}
