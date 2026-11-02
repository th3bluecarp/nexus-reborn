using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Static.Reputation;
using NexusForever.Script.Template;
using NexusForever.Script.Template.Filter;

namespace NexusForever.Script.Main.Tutorial.Script.Part2
{
    /// <summary>
    /// Simulation enemies whose creature data says FriendlyToAll, so no ability could target them (retail made them hostile
    /// inside the simulation). They spawn with the Virtual Battle Beasts' hostile faction. SEMANTICS: "Sim-Turrets are hostile".
    /// </summary>
    public abstract class HostileSimEntityScript : IWorldEntityScript, IOwnedScript<IWorldEntity>
    {
        private const Faction VirtualBattleBeastFaction = (Faction)1441;

        public void OnLoad(IWorldEntity owner)
        {
            owner.Faction1 = VirtualBattleBeastFaction;
            owner.Faction2 = VirtualBattleBeastFaction;
        }
    }

    /// <summary>"Destroy the Virtual Eldan Defense Construct" (Deconstruct the Construct 10520/10523).</summary>
    [ScriptFilterCreatureId(73499)]
    public class VirtualEldanDefenseConstructEntityScript : HostileSimEntityScript
    {
    }
}
