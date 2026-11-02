using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Static.Quest;
using NexusForever.Script.Template;
using NexusForever.Script.Template.Filter;

namespace NexusForever.Script.Main.Tutorial.Script.Part2
{
    /// <summary>
    /// Bon Voyage (10528 Exile / 10530 Dominion): "Travel to Nexus from the Arkship Docking Bay" through a Departures Terminal.
    /// The terminals have no activation spell and retail's shuttle script is lost. Stand-in: using the Northern Wilds terminal
    /// credits the travel objective and sends the player to the Northern Wilds crash site, next to Scout Frostfield (53532),
    /// who receives Bon Voyage and gives Welcome to Northern Wilds. SEMANTICS: "Departures Terminal travels to Nexus" (2026-11-01).
    /// </summary>
    [ScriptFilterCreatureId(73606)]
    public class NorthernWildsDeparturesTerminalEntityScript : IWorldEntityScript, IOwnedScript<IWorldEntity>
    {
        private const ushort NorthernWilds = 426;
        private static readonly uint[] TravelTargetGroups = [14367u, 14387u]; // Exile 10528 / Dominion 10530

        public void OnLoad(IWorldEntity owner)
        {
        }

        public void OnActivate(IPlayer activator)
        {
            foreach (uint targetGroupId in TravelTargetGroups)
                activator.QuestManager.ObjectiveUpdate(QuestObjectiveType.ActivateTargetGroup, targetGroupId, 1u);
            activator.TeleportTo(NorthernWilds, 4098.16f, -665.978f, -5165.1f);
        }
    }
}
