using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Static.Reputation;
using NexusForever.Script.Template;
using NexusForever.Script.Template.Filter;

namespace NexusForever.Script.Main.Tutorial.Script.Part2
{
    /// <summary>
    /// The arkship Sim-Turrets (The Face of the Enemy: "Interrupt the Sim-Turret using one of your abilities, then destroy
    /// it") carry the FriendlyToAll faction in Creature2, so no ability could target them. Retail turned them hostile as part
    /// of the simulation; the simplest stand-in gives them the Virtual Battle Beasts' hostile faction from the start.
    /// SEMANTICS: "Sim-Turrets are hostile" (2026-11-01, found by the quest bot and confirmed in the real client).
    /// </summary>
    [ScriptFilterCreatureId(73494)]
    public class SimTurretEntityScript : IWorldEntityScript, IOwnedScript<IWorldEntity>
    {
        private const Faction VirtualBattleBeastFaction = (Faction)1441;

        public void OnLoad(IWorldEntity owner)
        {
            owner.Faction1 = VirtualBattleBeastFaction;
            owner.Faction2 = VirtualBattleBeastFaction;
        }
    }
}
