using System.Collections.Generic;
using NexusForever.Network.Message;

namespace NexusForever.Network.World.Message.Model
{
    /// <summary>
    /// Sets an item's rune slots and installed runes at the client, layout read from WildStar64.exe (reader 0x1400a3e40, handler
    /// 0x14056aba0 fires ItemModified).
    /// </summary>
    [Message(GameMessageOpcode.ServerItemRunesUpdate)]
    public class ServerItemRunesUpdate : IWritable
    {
        public ulong ItemGuid { get; set; }

        /// <summary>
        /// Packed rune slot elements, the item's RandomGlyphData.
        /// </summary>
        public uint RuneData { get; set; }

        /// <summary>
        /// Installed rune item ids by slot, 0 for an empty slot.
        /// </summary>
        public List<uint> Runes { get; set; } = [];

        public void Write(GamePacketWriter writer)
        {
            writer.Write(ItemGuid);
            writer.Write(RuneData);
            writer.Write(Runes.Count, 4u);
            Runes.ForEach(r => writer.Write(r));
        }
    }
}
