using NexusForever.Game.Spell;
using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model.PlayerPath;

namespace NexusForever.WorldServer.Network.Message.Handler.Path
{
    /// <summary>
    /// The Explorer's Searching ability: the client sends this instead of a normal cast (WildStar64.exe 0x14039bf20) with the distance
    /// band to the nearest scavenger hunt clue, and the matching Searching spell is cast for the hot or cold feedback. Clues are still
    /// found by reaching them (PathMissionManager).
    /// </summary>
    public class ClientPathExplorerCastSearchingHandler : IMessageHandler<IWorldSession, ClientPathExplorerCastSearching>
    {
        private static readonly uint[] searchingSpells = [24953u, 27712u, 27713u, 27714u];

        public void HandleMessage(IWorldSession session, ClientPathExplorerCastSearching searching)
        {
            if (session.Player?.Map == null || searching.SearchRadiusBand >= searchingSpells.Length)
                return;

            session.Player.CastSpell(searchingSpells[searching.SearchRadiusBand], new SpellParameters());
        }
    }
}
