using System.Numerics;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Trade;
using NexusForever.Game.Static.Entity;
using NexusForever.Network.World.Message.Model;
using NexusForever.Network.World.Message.Static;
using P2PTradeResult = NexusForever.Network.World.Message.Model.ServerP2PTradeResult.P2PTradeResult;

namespace NexusForever.Game.Trade
{
    /// <summary>
    /// Trade window between two players. Layouts and client behaviour read from WildStar64.exe: the initiator's client opens
    /// its side itself, result 3 opens the window on both, results 6 to 9 set the commit flags, any result with Cancelled
    /// set closes it. Items and money change hands when both have committed.
    /// </summary>
    /// <remarks>
    /// The 8 item limit, 30 s invite timeout and 30 m range are invented (see docs/reborn/SEMANTICS.md). The initiator's update drives the trade.
    /// </remarks>
    public class PlayerTrade : IPlayerTrade
    {
        public const int MaxItems          = 8;
        public const double InviteTimeout  = 30d;
        public const float MaxDistance     = 30f;

        private class Offer
        {
            public List<ulong> Items { get; } = [];
            public Dictionary<ulong, uint> Counts { get; } = [];
            public ulong Credits { get; set; }
            public bool Committed { get; set; }
        }

        public IPlayer Initiator { get; }
        public IPlayer Target { get; }
        public bool IsActive { get; private set; }

        private readonly Dictionary<ulong, Offer> offers = [];
        private double inviteTime;
        private bool finished;

        public PlayerTrade(IPlayer initiator, IPlayer target)
        {
            Initiator = initiator;
            Target    = target;
            offers[initiator.CharacterId] = new Offer();
            offers[target.CharacterId]    = new Offer();

            initiator.Trade = this;
            target.Trade    = this;

            target.Session.EnqueueMessageEncrypted(new ServerP2PTradeInvite
            {
                TradeInviterUnitId = initiator.Guid
            });
        }

        public void Accept(IPlayer player)
        {
            if (finished || IsActive || player != Target)
                return;

            IsActive = true;
            SendResult(P2PTradeResult.PlayerAcceptedInvite, false);
        }

        public void Decline(IPlayer player)
        {
            if (finished || IsActive || player != Target)
                return;

            Finish(P2PTradeResult.PlayerDeclinedInvite);
        }

        public void Cancel(IPlayer player)
        {
            if (finished || !IsParticipant(player))
                return;

            Finish(P2PTradeResult.PlayerCanceled);
        }

        public void Abort()
        {
            if (!finished)
                Finish(P2PTradeResult.MissingPlayer);
        }

        public void AddItem(IPlayer player, ulong itemGuid)
        {
            if (!IsActive || finished || !IsParticipant(player))
                return;

            Offer offer = offers[player.CharacterId];
            IItem item = player.Inventory.GetItem(itemGuid);
            if (item == null || item.Location != InventoryLocation.Inventory || offer.Items.Contains(itemGuid) || offer.Items.Count >= MaxItems)
            {
                player.Session.EnqueueMessageEncrypted(new ServerP2PTradeResult { Result = P2PTradeResult.ErrorAddingItem });
                return;
            }

            offer.Items.Add(itemGuid);
            offer.Counts[itemGuid] = item.StackCount;
            ClearCommits();

            var update = new ServerP2PTradeUpdateItem
            {
                TradeIndex  = (uint)(offer.Items.Count - 1),
                OwnerUnitId = player.Guid,
                Item2Id     = item.Id,
                ItemGuid    = item.Guid,
                Quantity    = Math.Max(item.StackCount, 1u)
            };
            Initiator.Session.EnqueueMessageEncrypted(update);
            Target.Session.EnqueueMessageEncrypted(update);
        }

        public void RemoveItem(IPlayer player, ulong itemGuid)
        {
            if (!IsActive || finished || !IsParticipant(player))
                return;

            if (!offers[player.CharacterId].Items.Remove(itemGuid))
            {
                player.Session.EnqueueMessageEncrypted(new ServerP2PTradeResult { Result = P2PTradeResult.ErrorRemovingItem });
                return;
            }

            ClearCommits();

            var removed = new ServerPTPTradeItemRemoved
            {
                ItemGuid = itemGuid
            };
            Initiator.Session.EnqueueMessageEncrypted(removed);
            Target.Session.EnqueueMessageEncrypted(removed);
        }

        public void SetMoney(IPlayer player, ulong credits)
        {
            if (!IsActive || finished || !IsParticipant(player))
                return;

            if (!player.CurrencyManager.CanAfford(CurrencyType.Credits, credits))
                credits = 0ul;

            offers[player.CharacterId].Credits = credits;
            ClearCommits();

            var update = new ServerP2PTradeUpdateMoney
            {
                UnitId  = player.Guid,
                Credits = credits
            };
            Initiator.Session.EnqueueMessageEncrypted(update);
            Target.Session.EnqueueMessageEncrypted(update);
        }

        public void Commit(IPlayer player)
        {
            if (!IsActive || finished || !IsParticipant(player))
                return;

            offers[player.CharacterId].Committed = true;
            SendResult(player == Initiator ? P2PTradeResult.InitiatorCommitted : P2PTradeResult.TargetCommitted, false);

            if (offers.Values.All(o => o.Committed))
                Execute();
        }

        public void Update(double lastTick)
        {
            if (finished)
                return;

            if (Initiator.Map == null || Target.Map == null || Initiator.Map != Target.Map
                || Vector3.Distance(Initiator.Position, Target.Position) > MaxDistance)
            {
                Abort();
                return;
            }

            if (!IsActive)
            {
                inviteTime += lastTick;
                if (inviteTime >= InviteTimeout)
                    Finish(P2PTradeResult.PlayerDeclinedInvite);
            }
        }

        private bool IsParticipant(IPlayer player)
        {
            return player != null && (player == Initiator || player == Target);
        }

        private void ClearCommits()
        {
            foreach (Offer offer in offers.Values)
                offer.Committed = false;
        }

        private void Execute()
        {
            Offer initiatorOffer = offers[Initiator.CharacterId];
            Offer targetOffer    = offers[Target.CharacterId];

            List<IItem> initiatorItems = initiatorOffer.Items.Select(g => Initiator.Inventory.GetItem(g)).ToList();
            List<IItem> targetItems    = targetOffer.Items.Select(g => Target.Inventory.GetItem(g)).ToList();

            if (initiatorOffer.Items.Count + targetOffer.Items.Count == 0 && initiatorOffer.Credits == 0ul && targetOffer.Credits == 0ul)
            {
                Finish(P2PTradeResult.NothingToTrade);
                return;
            }

            // everything offered must still be there, paid for and fit in the other's bags
            // an offered stack must still be the size that was shown (split after offering)
            if (initiatorItems.Any(i => i == null || i.Location != InventoryLocation.Inventory || i.StackCount != initiatorOffer.Counts[i.Guid])
                || targetItems.Any(i => i == null || i.Location != InventoryLocation.Inventory || i.StackCount != targetOffer.Counts[i.Guid])
                || !Initiator.CurrencyManager.CanAfford(CurrencyType.Credits, initiatorOffer.Credits)
                || !Target.CurrencyManager.CanAfford(CurrencyType.Credits, targetOffer.Credits)
                || Target.Inventory.GetInventorySlotsRemaining(InventoryLocation.Inventory) + (uint)targetItems.Count < (uint)initiatorItems.Count
                || Initiator.Inventory.GetInventorySlotsRemaining(InventoryLocation.Inventory) + (uint)initiatorItems.Count < (uint)targetItems.Count)
            {
                Finish(P2PTradeResult.ErrorAddingItem);
                return;
            }

            foreach (IItem item in initiatorItems)
                Initiator.Inventory.ItemRemove(item, ItemUpdateReason.Trade);
            foreach (IItem item in targetItems)
                Target.Inventory.ItemRemove(item, ItemUpdateReason.Trade);

            foreach (IItem item in initiatorItems)
            {
                item.CharacterId = Target.CharacterId;
                Target.Inventory.AddItem(item, InventoryLocation.Inventory, ItemUpdateReason.Trade);
            }
            foreach (IItem item in targetItems)
            {
                item.CharacterId = Initiator.CharacterId;
                Initiator.Inventory.AddItem(item, InventoryLocation.Inventory, ItemUpdateReason.Trade);
            }

            if (initiatorOffer.Credits > 0ul)
            {
                Initiator.CurrencyManager.CurrencySubtractAmount(CurrencyType.Credits, initiatorOffer.Credits);
                Target.CurrencyManager.CurrencyAddAmount(CurrencyType.Credits, initiatorOffer.Credits);
            }
            if (targetOffer.Credits > 0ul)
            {
                Target.CurrencyManager.CurrencySubtractAmount(CurrencyType.Credits, targetOffer.Credits);
                Initiator.CurrencyManager.CurrencyAddAmount(CurrencyType.Credits, targetOffer.Credits);
            }

            Finish(P2PTradeResult.FinishedSuccess);
        }

        private void SendResult(P2PTradeResult result, bool cancelled)
        {
            var message = new ServerP2PTradeResult
            {
                Result    = result,
                Cancelled = cancelled
            };
            Initiator.Session?.EnqueueMessageEncrypted(message);
            Target.Session?.EnqueueMessageEncrypted(message);
        }

        private void Finish(P2PTradeResult result)
        {
            finished = true;
            SendResult(result, true);

            if (Initiator.Trade == this)
                Initiator.Trade = null;
            if (Target.Trade == this)
                Target.Trade = null;
        }
    }
}
