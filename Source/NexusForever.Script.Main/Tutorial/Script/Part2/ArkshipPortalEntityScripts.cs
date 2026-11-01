using NexusForever.Game.Abstract.Entity;
using NexusForever.Script.Template;
using NexusForever.Script.Template.Filter;

namespace NexusForever.Script.Main.Tutorial.Script.Part2
{
    /// <summary>
    /// The arkship simulation portals have no activation spell in the game data; retail moved the player with a script that
    /// is lost. Stand-in: activating the portal teleports the player to the first objective location of the stage it leads to.
    /// SEMANTICS: "arkship portals teleport" (2026-11-01, quest bot: A Claim to Stake stalled at the Protostar Model Skyplot portal).
    /// </summary>
    public abstract class ArkshipPortalEntityScript : IWorldEntityScript, IOwnedScript<IWorldEntity>
    {
        protected abstract (ushort WorldId, float X, float Y, float Z) Destination { get; }

        public void OnLoad(IWorldEntity owner)
        {
        }

        public void OnActivate(IPlayer activator)
        {
            (ushort worldId, float x, float y, float z) = Destination;
            // same world: relocate in place (a TeleportTo into the same world runs a full map transfer and reload)
            if (activator.Map?.Entry.Id == worldId)
                activator.TeleportToLocal(new System.Numerics.Vector3(x, y, z));
            else
                activator.TeleportTo(worldId, x, y, z);
        }
    }

    /// <summary>
    /// "Right-click the Skyplot Portal to continue to the Protostar Model Skyplot" (A Claim to Stake 10525 / 10526): zone 4965,
    /// next to the Sky Specialist (world location 51711).
    /// </summary>
    [ScriptFilterCreatureId(73741)]
    public class SkyplotPortalEntityScript : ArkshipPortalEntityScript
    {
        protected override (ushort WorldId, float X, float Y, float Z) Destination => (3460, 1230f, -494.2f, 116f);
    }

    /// <summary>
    /// "Right-click ... to end the simulation and wake from cryosleep" (Deconstruct the Construct 10523): zone 5999, the
    /// Arkship Docking Bay next to the Chancellor (world location 52750).
    /// </summary>
    [ScriptFilterCreatureId(73745)]
    public class CryosleepExitPortalEntityScript : ArkshipPortalEntityScript
    {
        protected override (ushort WorldId, float X, float Y, float Z) Destination => (3460, -3634f, -437.9f, -4354f);
    }
}
