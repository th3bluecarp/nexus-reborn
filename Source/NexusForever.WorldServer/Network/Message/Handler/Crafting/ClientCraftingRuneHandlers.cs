using System.Linq;
using NexusForever.Game;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Static.Crafting;
using NexusForever.Game.Static.Entity;
using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model.Crafting;
using NexusForever.Network.World.Message.Static;

namespace NexusForever.WorldServer.Network.Message.Handler.Crafting
{
    /// <summary>
    /// Shared rules for the Runecrafting window. Layouts and meanings read from WildStar64.exe and the Runecrafting addon.
    /// </summary>
    /// <remarks>
    /// Slot elements are 1 Air, 2 Water, 3 Earth, 4 Fire, 5 Logic, 6 Life, 7 Fusion. Rune items are family 33, Item2Type 352 to 358
    /// (basic) and 419 to 425 (set) in the order Fire, Water, Earth, Air, Life, Logic, Fusion. A rune fits a slot of its element,
    /// a Fusion rune fits any slot. Adding, clearing and rerolling slots are free (retail charged credits or service tokens).
    /// </remarks>
    internal static class Runecrafting
    {
        private static readonly uint[] typeElements = [4u, 2u, 3u, 1u, 6u, 5u, 7u];

        /// <summary>
        /// Element of a rune item (1 to 7), 0 if the item is not a rune.
        /// </summary>
        public static uint GetRuneElement(uint runeItemId)
        {
            IItemInfo info = ItemManager.Instance.GetItemInfo(runeItemId);
            if (info?.Entry == null || info.Entry.Item2FamilyId != 33u)
                return 0u;

            uint type = info.Entry.Item2TypeId;
            if (type is >= 352u and <= 358u)
                return typeElements[type - 352u];
            if (type is >= 419u and <= 425u)
                return typeElements[type - 419u];
            return 0u;
        }

        /// <summary>
        /// Slot element from the window's rune type (7 Air to 13 Fusion), 0 for random.
        /// </summary>
        public static uint FromRuneType(RuneType type)
        {
            return (uint)type is >= 7u and <= 13u ? (uint)type - 6u : 0u;
        }

        public static IItem GetItem(IPlayer player, ulong guid)
        {
            IItem item = player.Inventory.GetItem(guid);
            return item != null && item.Location is InventoryLocation.Inventory or InventoryLocation.Equipped ? item : null;
        }

        public static void SendResult(IWorldSession session, TradeskillResult result)
        {
            session.EnqueueMessageEncrypted(new ServerTradeskillSigilResult
            {
                TradeskillSigilResult = result
            });
        }
    }

    /// <summary>
    /// Install runes: the client sends the rune for every slot, unchanged slots carry their current rune or 0.
    /// </summary>
    public class ClientCraftingRuneInstallHandler : IMessageHandler<IWorldSession, ClientCraftingRuneInstall>
    {
        public void HandleMessage(IWorldSession session, ClientCraftingRuneInstall install)
        {
            IItem item = Runecrafting.GetItem(session.Player, install.ItemGuid);
            if (item == null)
            {
                Runecrafting.SendResult(session, TradeskillResult.InvalidItem);
                return;
            }

            if (install.RuneSlotItem2Id.Length != item.RuneSlotCount)
            {
                Runecrafting.SendResult(session, TradeskillResult.InvalidSlot);
                return;
            }

            var changes = Enumerable.Range(0, item.RuneSlotCount)
                .Where(i => install.RuneSlotItem2Id[i] != 0u && install.RuneSlotItem2Id[i] != item.Runes[i])
                .ToList();
            if (changes.Count == 0)
            {
                Runecrafting.SendResult(session, TradeskillResult.MissingRune);
                return;
            }

            foreach (int slot in changes)
            {
                uint runeId = install.RuneSlotItem2Id[slot];
                if (item.Runes[slot] != 0u)
                {
                    Runecrafting.SendResult(session, TradeskillResult.RuneExists);
                    return;
                }

                uint element = Runecrafting.GetRuneElement(runeId);
                if (element == 0u || (element != 7u && element != item.GetRuneSlotType(slot)))
                {
                    Runecrafting.SendResult(session, TradeskillResult.InvalidSlot);
                    return;
                }
            }

            foreach (var group in changes.GroupBy(s => install.RuneSlotItem2Id[s]))
            {
                if (session.Player.Inventory.GetItemCount(group.Key) < (uint)group.Count())
                {
                    Runecrafting.SendResult(session, TradeskillResult.MissingRune);
                    return;
                }
            }

            foreach (int slot in changes)
                session.Player.Inventory.ItemDelete(install.RuneSlotItem2Id[slot], 1u, ItemUpdateReason.Crafting);

            session.Player.Inventory.ItemRunesUpdate(item, i =>
            {
                foreach (int slot in changes)
                    i.SetRune(slot, install.RuneSlotItem2Id[slot]);
            });
            Runecrafting.SendResult(session, TradeskillResult.Success);
        }
    }

    public class ClientCraftingRuneSlotAddHandler : IMessageHandler<IWorldSession, ClientCraftingRuneSlotAdd>
    {
        public void HandleMessage(IWorldSession session, ClientCraftingRuneSlotAdd add)
        {
            IItem item = Runecrafting.GetItem(session.Player, add.ItemGuid);
            if (item == null)
            {
                Runecrafting.SendResult(session, TradeskillResult.InvalidItem);
                return;
            }

            if (item.RuneSlotCount >= item.GetMaxRuneSlots())
            {
                Runecrafting.SendResult(session, TradeskillResult.RuneSlotLimit);
                return;
            }

            session.Player.Inventory.ItemRunesUpdate(item, i => i.AddRuneSlot(Runecrafting.FromRuneType(add.Type)));
            Runecrafting.SendResult(session, TradeskillResult.Success);
        }
    }

    /// <summary>
    /// Clear a rune slot, putting the rune back in the bags when asked to recover it.
    /// </summary>
    public class ClientCraftingRuneSlotClearHandler : IMessageHandler<IWorldSession, ClientCraftingRuneSlotClear>
    {
        public void HandleMessage(IWorldSession session, ClientCraftingRuneSlotClear clear)
        {
            IItem item = Runecrafting.GetItem(session.Player, clear.ItemGuid);
            if (item == null)
            {
                Runecrafting.SendResult(session, TradeskillResult.InvalidItem);
                return;
            }

            int slot = (int)clear.RuneSlotIndex;
            if (slot >= item.RuneSlotCount || item.Runes[slot] == 0u)
            {
                Runecrafting.SendResult(session, TradeskillResult.InvalidSlot);
                return;
            }

            uint runeId = item.Runes[slot];
            if (clear.RecoverRune && session.Player.Inventory.IsInventoryFull(InventoryLocation.Inventory))
            {
                session.Player.SendGenericError(GenericError.ItemInventoryFull);
                return;
            }

            session.Player.Inventory.ItemRunesUpdate(item, i => i.SetRune(slot, 0u));
            if (clear.RecoverRune)
                session.Player.Inventory.ItemCreate(InventoryLocation.Inventory, runeId, 1u, ItemUpdateReason.Crafting);

            Runecrafting.SendResult(session, TradeskillResult.Success);
        }
    }

    /// <summary>
    /// Change an empty slot's element, to the chosen one or a random different one.
    /// </summary>
    public class ClientCraftingRuneSlotRerollHandler : IMessageHandler<IWorldSession, ClientCraftingRuneSlotReroll>
    {
        public void HandleMessage(IWorldSession session, ClientCraftingRuneSlotReroll reroll)
        {
            IItem item = Runecrafting.GetItem(session.Player, reroll.ItemGuid);
            if (item == null)
            {
                Runecrafting.SendResult(session, TradeskillResult.InvalidItem);
                return;
            }

            int slot = (int)reroll.SlotIndex;
            if (slot >= item.RuneSlotCount)
            {
                Runecrafting.SendResult(session, TradeskillResult.InvalidSlot);
                return;
            }

            if (item.Runes[slot] != 0u)
            {
                Runecrafting.SendResult(session, TradeskillResult.RuneExists);
                return;
            }

            session.Player.Inventory.ItemRunesUpdate(item, i => i.SetRuneSlotType(slot, Runecrafting.FromRuneType(reroll.Type)));
            Runecrafting.SendResult(session, TradeskillResult.Success);
        }
    }
}
