using NexusForever.Database;
using NexusForever.Database.Character;
using NexusForever.Game.Static.Entity;
using NexusForever.GameTable.Model;
using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model.Shared;

namespace NexusForever.Game.Abstract.Entity
{
    public interface IItem : IDatabaseCharacter, IDatabaseState, INetworkBuildable<Item>
    {
        uint Id { get; }
        IItemInfo Info { get; }
        Spell4BaseEntry SpellEntry { get; }
        ulong Guid { get; }
        ulong? CharacterId { get; set; }
        InventoryLocation Location { get; set; }
        InventoryLocation PreviousLocation { get; set; }
        uint BagIndex { get; set; }
        uint PreviousBagIndex { get; set; }
        uint StackCount { get; set; }
        uint Charges { get; set; }
        float Durability { get; set; }
        uint ExpirationTimeLeft { get; set; }

        // <summary>
        /// Returns the <see cref="CurrencyType"/> this <see cref="IItem"/> sells for at a vendor.
        /// </summary>
        CurrencyType GetVendorSellCurrency(byte index);

        /// <summary>
        /// Credits a vendor charges to repair this <see cref="IItem"/> to full durability.
        /// </summary>
        ulong GetRepairCost();

        /// <summary>
        /// Packed rune slot elements, sent to the client as RandomGlyphData.
        /// </summary>
        uint RuneData { get; }

        /// <summary>
        /// Installed rune item ids by slot, 0 for an empty slot.
        /// </summary>
        IReadOnlyList<uint> Runes { get; }

        int RuneSlotCount { get; }

        /// <summary>
        /// Maximum number of rune slots the item can have.
        /// </summary>
        int GetMaxRuneSlots();

        /// <summary>
        /// Element of a rune slot, 1 Air, 2 Water, 3 Earth, 4 Fire, 5 Logic, 6 Life, 7 Fusion.
        /// </summary>
        uint GetRuneSlotType(int index);

        void SetRune(int index, uint runeItemId);

        /// <summary>
        /// Add a rune slot of the supplied element (1 to 7) or a random one (0).
        /// </summary>
        bool AddRuneSlot(uint type);

        /// <summary>
        /// Change the element of a rune slot to the supplied one (1 to 7) or a random different one (0).
        /// </summary>
        void SetRuneSlotType(int index, uint type);

        /// <summary>
        /// Returns the amount of <see cref="CurrencyType"/> this <see cref="IItem"/> sells for at a vendor.
        /// </summary>
        uint GetVendorSellAmount(byte index);
    }
}