using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Microsoft.Extensions.Logging;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Spell;
using NexusForever.Game.Static.Reputation;
using NexusForever.GameTable;
using NexusForever.GameTable.Model;
using NexusForever.Network;
using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model;

namespace NexusForever.WorldServer.Network.Message.Handler.Entity.Player
{
    /// <summary>
    /// Taxi kiosk flights and shuttles: the player is taken to the last node of the route with the rapid transport spell.
    /// </summary>
    /// <remarks>
    /// Retail flew the player along the TaxiRoute splines, here the trip is a rapid transport to the destination (same cost and
    /// cooldown handling). The payload layout is decoded by trying the likely count and id bit widths and keeping the first that
    /// reads as a short list of known TaxiNode ids filling the message.
    /// </remarks>
    public class ClientTaxiFlightHandler : IMessageHandler<IWorldSession, ClientTaxiFlight>
    {
        private const float KioskRange = 150f;

        private static readonly (uint Count, uint Id)[] Layouts = [(32u, 14u), (32u, 32u), (32u, 16u), (16u, 16u), (16u, 14u), (8u, 16u), (8u, 14u), (32u, 15u), (8u, 15u), (5u, 15u), (32u, 12u), (8u, 8u)];

        #region Dependency Injection

        private readonly ILogger<ClientTaxiFlightHandler> log;
        private readonly IGameTableManager gameTableManager;

        public ClientTaxiFlightHandler(
            ILogger<ClientTaxiFlightHandler> log,
            IGameTableManager gameTableManager)
        {
            this.log              = log;
            this.gameTableManager = gameTableManager;
        }

        #endregion

        public void HandleMessage(IWorldSession session, ClientTaxiFlight taxiFlight)
        {
            log.LogInformation($"Taxi request payload {Convert.ToHexString(taxiFlight.Payload)}");

            uint[] route = Decode(taxiFlight.Payload);
            if (route == null || route.Length == 0)
            {
                log.LogWarning("Taxi request could not be decoded.");
                return;
            }

            if (!CanFly(session, route))
                return;

            GameFormulaEntry gameFormulaEntry = gameTableManager.GameFormula.GetEntry(1307);
            if (gameFormulaEntry == null)
                return;

            uint spell4Id = gameFormulaEntry.Dataint0;
            if (session.Player.SpellManager.GetSpellCooldown(gameFormulaEntry.Dataint0) > 0d)
                spell4Id = gameFormulaEntry.Dataint01;

            log.LogInformation($"Taxi route {string.Join(",", route)}, going to node {route[^1]}.");
            session.Player.CastSpell(spell4Id, new SpellParameters
            {
                TaxiNode = (ushort)route[^1]
            });
        }

        /// <summary>
        /// The player has to be at a taxi kiosk (near a taxi node of their world) and every node of the route has to be neutral or of their faction.
        /// </summary>
        private bool CanFly(IWorldSession session, uint[] route)
        {
            IPlayer player = session.Player;
            if (player.Map == null)
                return false;

            uint faction = player.Faction1 switch
            {
                Faction.Exile    => 1u,
                Faction.Dominion => 2u,
                _                => 0u
            };

            uint badNode = route.FirstOrDefault(id =>
            {
                uint nodeFaction = gameTableManager.TaxiNode.GetEntry(id).TaxiNodeFactionEnum;
                return nodeFaction != 0u && faction != 0u && nodeFaction != faction;
            });
            if (badNode != 0u)
            {
                log.LogWarning($"Taxi request refused, node {badNode} belongs to the other faction.");
                return false;
            }

            bool atKiosk = gameTableManager.TaxiNode.Entries.Any(n =>
            {
                WorldLocation2Entry location = gameTableManager.WorldLocation2.GetEntry(n.WorldLocation2Id);
                return location != null
                    && location.WorldId == player.Map.Entry.Id
                    && Vector3.Distance(new Vector3(location.Position0, location.Position1, location.Position2), player.Position) <= KioskRange;
            });
            if (!atKiosk)
            {
                log.LogWarning($"Taxi request refused, no taxi node within {KioskRange} of {player.Position} on world {player.Map.Entry.Id}.");
                return false;
            }

            return true;
        }

        private uint[] Decode(byte[] payload)
        {
            uint totalBits = (uint)payload.Length * 8u;
            foreach ((uint countBits, uint idBits) in Layouts)
            {
                using var stream = new MemoryStream(payload);
                using var reader = new GamePacketReader(stream);

                if (countBits > totalBits)
                    continue;

                uint count = reader.ReadUInt(countBits);
                if (count == 0u || count > 30u || countBits + count * idBits > totalBits)
                    continue;

                var ids = new uint[count];
                bool valid = true;
                for (uint i = 0u; i < count; i++)
                {
                    ids[i] = reader.ReadUInt(idBits);
                    if (gameTableManager.TaxiNode.GetEntry(ids[i]) == null)
                    {
                        valid = false;
                        break;
                    }
                }

                // the rest of the message is padding to the next byte
                if (valid && totalBits - (countBits + count * idBits) < 8u)
                    return ids;
            }

            return null;
        }
    }
}
