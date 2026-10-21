using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using NexusForever.Database.Character;
using NexusForever.Database.Character.Model;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Static.Entity;
using NexusForever.GameTable;
using NexusForever.GameTable.Model;
using NexusForever.Network.World.Message.Model.Shared;
using NetworkItem = NexusForever.Network.World.Message.Model.Shared.Item;

namespace NexusForever.Game.Entity
{
    public class Item : IItem
    {
        /// <summary>
        /// Determines which fields need saving for <see cref="IItem"/> when being saved to the database.
        /// </summary>
        [Flags]
        public enum ItemSaveMask
        {
            None               = 0x0000,
            Create             = 0x0001,
            Delete             = 0x0002,
            CharacterId        = 0x0004,
            Location           = 0x0008,
            BagIndex           = 0x0010,
            StackCount         = 0x0020,
            Charges            = 0x0040,
            Durability         = 0x0080,
            ExpirationTimeLeft = 0x0100,
            Runes              = 0x0200
        }

        /// <summary>
        /// Rune slot elements as the client reads them from RandomGlyphData: 1 Air, 2 Water, 3 Earth, 4 Fire, 5 Logic, 6 Life, 7 Fusion.
        /// </summary>
        public const int MaxRuneSlots = 8;

        public uint RuneData => runeData;
        public IReadOnlyList<uint> Runes => runes;
        public int RuneSlotCount => runes.Length;

        private uint runeData;
        private uint[] runes = [];

        public uint Id => Info?.Id ?? SpellEntry.Id;
        public IItemInfo Info { get; }
        public Spell4BaseEntry SpellEntry { get; }
        public ulong Guid { get; }

        public ulong? CharacterId
        {
            get => characterId;
            set
            {
                characterId = value;
                saveMask |= ItemSaveMask.CharacterId;
            }
        }

        private ulong? characterId;

        public InventoryLocation Location
        {
            get => location;
            set
            {
                location = value;
                saveMask |= ItemSaveMask.Location;
            }
        }

        private InventoryLocation location;

        public InventoryLocation PreviousLocation { get; set; }

        public uint BagIndex
        {
            get => bagIndex;
            set
            {
                bagIndex = value;
                saveMask |= ItemSaveMask.BagIndex;
            }
        }

        private uint bagIndex;

        public uint PreviousBagIndex { get; set; }

        public uint StackCount
        {
            get => stackCount;
            set
            {
                if (value > Info.Entry.MaxStackCount)
                    throw new ArgumentOutOfRangeException();

                stackCount = value;
                saveMask |= ItemSaveMask.StackCount;
            }
        }

        private uint stackCount;

        public uint Charges
        {
            get => charges;
            set
            {
                if (value > Info.Entry.MaxCharges)
                    throw new ArgumentOutOfRangeException();

                charges = value;
                saveMask |= ItemSaveMask.Charges;
            }
        }

        private uint charges;

        public float Durability
        {
            get => durability;
            set
            {
                if (value < 0f || value > 1.0f)
                    throw new ArgumentOutOfRangeException();

                durability = value;
                saveMask |= ItemSaveMask.Durability;
            }
        }

        private float durability;

        public uint ExpirationTimeLeft
        {
            get => expirationTimeLeft;
            set
            {
                expirationTimeLeft = value;
                saveMask |= ItemSaveMask.ExpirationTimeLeft;
            }
        }

        private uint expirationTimeLeft;

        /// <summary>
        /// Returns if <see cref="IItem"/> is enqueued to be saved to the database.
        /// </summary>
        public bool PendingCreate => (saveMask & ItemSaveMask.Create) != 0;

        public bool PendingDelete => (saveMask & ItemSaveMask.Delete) != 0;

        private ItemSaveMask saveMask;

        public Dictionary<Property, float> InnateProperties { get; private set; } = new Dictionary<Property, float>();

        /// <summary>
        /// Create a new <see cref="IItem"/> from an existing database model.
        /// </summary>
        public Item(ItemModel model)
        {
            Guid             = model.Id;
            characterId      = model.OwnerId;
            location         = InventoryLocation.None;
            PreviousLocation = InventoryLocation.None;
            bagIndex         = 0u;
            PreviousBagIndex = 0u;
            stackCount       = model.StackCount;
            charges          = model.Charges;
            durability       = model.Durability;

            if ((InventoryLocation)model.Location != InventoryLocation.Ability)
                Info = ItemManager.Instance.GetItemInfo(model.ItemId);
            else
                SpellEntry = GameTableManager.Instance.Spell4Base.GetEntry(model.ItemId);

            saveMask = ItemSaveMask.None;

            runeData = model.RuneData;
            runes    = (model.Runes ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(r => uint.TryParse(r, out uint id) ? id : 0u).ToArray();
            if (runes.Length != UnpackRuneSlotCount(runeData))
                Array.Resize(ref runes, (int)UnpackRuneSlotCount(runeData));

            // slots saved with an older count packing are packed again the way the client counts them
            if (runeData != 0u)
            {
                uint repacked = PackRuneSlots(Enumerable.Range(0, runes.Length).Select(i => GetRuneSlotType(i)).ToArray());
                if (repacked != runeData)
                {
                    runeData  = repacked;
                    saveMask |= ItemSaveMask.Runes;
                }
            }

            // gear from before runes existed gets the slots it would have dropped with
            if (runeData == 0u && GenerateRuneSlots())
                saveMask |= ItemSaveMask.Runes;
        }

        /// <summary>
        /// Create a new <see cref="IItem"/> from an <see cref="IItemInfo"/> template.
        /// </summary>
        public Item(ulong? owner, IItemInfo info, uint count = 1u, uint initialCharges = 0)
        {
            Guid             = ItemManager.Instance.NextItemId;
            characterId      = owner;
            location         = InventoryLocation.None;
            PreviousLocation = InventoryLocation.None;
            bagIndex         = 0u;
            stackCount       = count;
            charges          = initialCharges;
            durability       = 1.0f;
            Info             = info;

            saveMask         = ItemSaveMask.Create;

            GenerateRuneSlots();
        }

        /// <summary>
        /// Create a new <see cref="IItem"/> from a <see cref="Spell4BaseEntry"/> template.
        /// </summary>
        public Item(ulong owner, Spell4BaseEntry entry, uint count = 1u)
        {
            Guid             = ItemManager.Instance.NextItemId;
            characterId      = owner;
            location         = InventoryLocation.None;
            PreviousLocation = InventoryLocation.None;
            bagIndex         = 0u;
            PreviousBagIndex = 0u;
            stackCount       = count;
            charges          = 0u;
            durability       = 0.0f;
            SpellEntry       = entry;

            saveMask         = ItemSaveMask.Create;
        }

        /// <summary>
        /// Enqueue <see cref="IItem"/> to be deleted from the database.
        /// </summary>
        public void EnqueueDelete(bool state)
        {
            saveMask = ItemSaveMask.Delete;
        }

        public void Save(CharacterContext context)
        {
            if (saveMask == ItemSaveMask.None)
                return;

            if ((saveMask & ItemSaveMask.Create) != 0)
            {
                // item doesn't exist in database, all information must be saved
                context.Add(new ItemModel
                {
                    Id                 = Guid,
                    OwnerId            = CharacterId,
                    ItemId             = Id,
                    Location           = (ushort)Location,
                    BagIndex           = BagIndex,
                    StackCount         = StackCount,
                    Charges            = Charges,
                    Durability         = Durability,
                    ExpirationTimeLeft = ExpirationTimeLeft,
                    RuneData           = runeData,
                    Runes              = string.Join(',', runes)
                });
            }
            else if ((saveMask & ItemSaveMask.Delete) != 0)
            {
                var model = new ItemModel
                {
                    Id = Guid,
                };

                context.Entry(model).State = EntityState.Deleted;
            }
            else
            {
                // item already exists in database, save only data that has been modified
                var model = new ItemModel
                {
                    Id = Guid
                };

                // could probably clean this up with reflection, works for the time being
                EntityEntry<ItemModel> entity = context.Attach(model);
                if ((saveMask & ItemSaveMask.CharacterId) != 0)
                {
                    model.OwnerId = CharacterId;
                    entity.Property(p => p.OwnerId).IsModified = true;
                }
                if ((saveMask & ItemSaveMask.Location) != 0)
                {
                    model.Location = (ushort)Location;
                    entity.Property(p => p.Location).IsModified = true;
                }
                if ((saveMask & ItemSaveMask.BagIndex) != 0)
                {
                    model.BagIndex = BagIndex;
                    entity.Property(p => p.BagIndex).IsModified = true;
                }
                if ((saveMask & ItemSaveMask.StackCount) != 0)
                {
                    model.StackCount = StackCount;
                    entity.Property(p => p.StackCount).IsModified = true;
                }
                if ((saveMask & ItemSaveMask.Charges) != 0)
                {
                    model.Charges = Charges;
                    entity.Property(p => p.Charges).IsModified = true;
                }
                if ((saveMask & ItemSaveMask.Durability) != 0)
                {
                    model.Durability = Durability;
                    entity.Property(p => p.Durability).IsModified = true;
                }
                if ((saveMask & ItemSaveMask.ExpirationTimeLeft) != 0)
                {
                    model.ExpirationTimeLeft = ExpirationTimeLeft;
                    entity.Property(p => p.ExpirationTimeLeft).IsModified = true;
                }
                if ((saveMask & ItemSaveMask.Runes) != 0)
                {
                    model.RuneData = runeData;
                    entity.Property(p => p.RuneData).IsModified = true;
                    model.Runes = string.Join(',', runes);
                    entity.Property(p => p.Runes).IsModified = true;
                }
            }

            saveMask = ItemSaveMask.None;
        }

        /// <summary>
        /// Return a network representation of the current <see cref="IItem"/> for use in a item related packet.
        /// </summary>
        public NetworkItem Build()
        {
            var networkItem = new NetworkItem
            {
                Guid         = Guid,
                ItemId       = Id,
                LocationData = new ItemLocation
                {
                    Location = Location,
                    BagIndex = BagIndex
                },
                StackCount = StackCount,
                Charges    = Charges,
                Durability = Durability,
                RandomGlyphData = runeData,
                // the client wipes the rune slots of armor and weapons whose circuit data has no version in its top byte
                RandomCircuitData = runeData != 0u ? 1ul << 56 : 0ul,
                Unknown58  = new NetworkItem.UnknownStructure[2]
                {
                    new NetworkItem.UnknownStructure(),
                    new NetworkItem.UnknownStructure()
                }
            };
            networkItem.Glyphs.AddRange(runes);

            return networkItem;
        }

        /// <summary>
        /// Give armor and weapons the rune slots they drop with, following the client (WildStar64.exe 0x14040efb0): items of power
        /// level GameFormula 1024 Dataint01 (5) or more get ItemRuneInstance.DefinedSocketCount slots, or their quality's DefaultRunes.
        /// Defined socket elements are kept, the others are random (Air to Life).
        /// </summary>
        private bool GenerateRuneSlots()
        {
            if (Info?.Entry == null || Info.Entry.Item2FamilyId is not (1u or 2u))
                return false;
            if (Info.Entry.PowerLevel < (GameTableManager.Instance.GameFormula.GetEntry(1024)?.Dataint01 ?? 5u))
                return false;

            ItemRuneInstanceEntry instance = Info.Entry.ItemRuneInstanceId != 0u ? GameTableManager.Instance.ItemRuneInstance.GetEntry(Info.Entry.ItemRuneInstanceId) : null;
            uint count = Math.Min(instance?.DefinedSocketCount ?? Info.QualityEntry?.DefaultRunes ?? 0u, (uint)MaxRuneSlots);
            if (count == 0u)
                return false;

            uint[] defined = instance != null
                ? [instance.DefinedSocketType00, instance.DefinedSocketType01, instance.DefinedSocketType02, instance.DefinedSocketType03,
                    instance.DefinedSocketType04, instance.DefinedSocketType05, instance.DefinedSocketType06, instance.DefinedSocketType07]
                : new uint[MaxRuneSlots];

            var types = new uint[count];
            for (int i = 0; i < count; i++)
                types[i] = defined[i] >= 7u ? defined[i] - 6u : (uint)Random.Shared.Next(1, 7);

            runeData = PackRuneSlots(types);
            runes    = new uint[count];
            return true;
        }

        /// <summary>
        /// Maximum number of rune slots: ItemRuneInstance.SocketCountMax, or the quality's MaxRunes, at most 8.
        /// </summary>
        public int GetMaxRuneSlots()
        {
            if (Info?.Entry == null || Info.Entry.Item2FamilyId is not (1u or 2u))
                return 0;

            ItemRuneInstanceEntry instance = Info.Entry.ItemRuneInstanceId != 0u ? GameTableManager.Instance.ItemRuneInstance.GetEntry(Info.Entry.ItemRuneInstanceId) : null;
            uint max = instance != null && instance.SocketCountMax != 0u ? instance.SocketCountMax : Info.QualityEntry?.MaxRunes ?? 0u;
            return (int)Math.Min(max, (uint)MaxRuneSlots);
        }

        /// <summary>
        /// Element of a rune slot, 1 Air, 2 Water, 3 Earth, 4 Fire, 5 Logic, 6 Life, 7 Fusion.
        /// </summary>
        public uint GetRuneSlotType(int index)
        {
            return index >= 0 && index < MaxRuneSlots ? (runeData >> (7 + 3 * index)) & 7u : 0u;
        }

        public void SetRune(int index, uint runeItemId)
        {
            runes[index] = runeItemId;
            saveMask    |= ItemSaveMask.Runes;
        }

        /// <summary>
        /// Add a rune slot of the supplied element (1 to 7) or a random one (0).
        /// </summary>
        public bool AddRuneSlot(uint type)
        {
            if (runes.Length >= GetMaxRuneSlots())
                return false;

            uint[] types = Enumerable.Range(0, runes.Length).Select(i => GetRuneSlotType(i)).Append(type is >= 1u and <= 7u ? type : (uint)Random.Shared.Next(1, 7)).ToArray();
            runeData = PackRuneSlots(types);
            Array.Resize(ref runes, types.Length);
            saveMask |= ItemSaveMask.Runes;
            return true;
        }

        /// <summary>
        /// Change the element of a rune slot to the supplied one (1 to 7) or a random different one (0).
        /// </summary>
        public void SetRuneSlotType(int index, uint type)
        {
            uint[] types = Enumerable.Range(0, runes.Length).Select(i => GetRuneSlotType(i)).ToArray();
            if (type is < 1u or > 7u)
            {
                uint current = types[index];
                do
                    type = (uint)Random.Shared.Next(1, 7);
                while (type == current);
            }

            types[index] = type;
            runeData = PackRuneSlots(types);
            saveMask |= ItemSaveMask.Runes;
        }

        /// <summary>
        /// Rune slots the item has before any are added: ItemRuneInstance.DefinedSocketCount, or the quality's DefaultRunes.
        /// </summary>
        private uint GetMinimumRuneSlots()
        {
            ItemRuneInstanceEntry instance = Info?.Entry != null && Info.Entry.ItemRuneInstanceId != 0u ? GameTableManager.Instance.ItemRuneInstance.GetEntry(Info.Entry.ItemRuneInstanceId) : null;
            return Math.Min(instance?.DefinedSocketCount ?? Info?.QualityEntry?.DefaultRunes ?? 0u, (uint)MaxRuneSlots);
        }

        /// <summary>
        /// Pack rune slot elements the way the client reads RandomGlyphData: bits 3 to 5 are the slots added beyond the minimum
        /// (the client shows min(minimum + that, maximum)), bits 0 to 2 bonus slots (0), bit 6 a flag (0), then 3 bits per slot.
        /// </summary>
        /// <remarks>
        /// Read from WildStar64.exe 0x14040cf90 and 0x14056a631.
        /// </remarks>
        private uint PackRuneSlots(IReadOnlyList<uint> types)
        {
            uint added = (uint)Math.Max(0, types.Count - (int)GetMinimumRuneSlots());
            uint data = Math.Min(added, 7u) << 3;
            for (int i = 0; i < types.Count && i < MaxRuneSlots; i++)
                data |= (types[i] & 7u) << (7 + 3 * i);
            return data;
        }

        private static uint UnpackRuneSlotCount(uint data)
        {
            uint count = 0u;
            for (int i = 0; i < MaxRuneSlots; i++)
                if (((data >> (7 + 3 * i)) & 7u) != 0u)
                    count = (uint)i + 1u;
            return count;
        }

        /// <summary>
        /// Returns the <see cref="CurrencyType"/> this <see cref="IItem"/> sells for at a vendor.
        /// </summary>
        public CurrencyType GetVendorSellCurrency(byte index)
        {
            return Info.GetVendorSellCurrency(index);
        }

        /// <summary>
        /// Returns the amount of <see cref="CurrencyType"/> this <see cref="IItem"/> sells for at a vendor.
        /// </summary>
        /// <summary>
        /// Credits a vendor charges to repair this item to full, the same amount the client shows.
        /// </summary>
        /// <remarks>
        /// GameFormula 559 Datafloat03 (0.25) times the missing fraction times the item value, from WildStar64.exe 0x1403b54a0.
        /// </remarks>
        public ulong GetRepairCost()
        {
            uint maxDurability = Info.GetMaxDurability();
            if (maxDurability == 0u)
                return 0ul;

            uint current = durability <= 0.000001f ? 0u : Math.Max(1u, (uint)(maxDurability * durability + 0.5f));
            if (current >= maxDurability)
                return 0ul;

            float rate = GameTableManager.Instance.GameFormula.GetEntry(559).Datafloat03;
            return (ulong)(rate * ((maxDurability - current) / (float)maxDurability) * Info.GetItemValue() + 0.5f);
        }

        public uint GetVendorSellAmount(byte index)
        {
            // TODO: Rawaho was lazy and didn't finish this
            // GameFormulaEntry entry = GameTableManager.Instance.GameFormula.GetEntry(559);
            // uint cost = Entry.PowerLevel * entry.Dataint01;

            // TODO: Add calculations for Runes or other things that would increase worth amount.
            return Info.GetVendorSellAmount(index);
        }
    }
}
