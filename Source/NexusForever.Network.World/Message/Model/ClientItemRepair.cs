using NexusForever.Network.Message;

namespace NexusForever.Network.World.Message.Model
{
    /// <summary>
    /// Repair at a vendor, layout read from WildStar64.exe (writer 0x140088e60, 192 bits).
    /// </summary>
    [Message(GameMessageOpcode.ClientItemRepair)]
    public class ClientItemRepair : IReadable
    {
        /// <summary>
        /// Item to repair, 0 repairs every damaged item.
        /// </summary>
        public ulong ItemGuid { get; private set; }

        /// <summary>
        /// Guild paying for the repair, 0 for a normal repair.
        /// </summary>
        public ulong GuildId { get; private set; }

        /// <summary>
        /// Guild repair limit sent with a guild repair all.
        /// </summary>
        public ulong GuildRepairLimit { get; private set; }

        public void Read(GamePacketReader reader)
        {
            ItemGuid         = reader.ReadULong();
            GuildId          = reader.ReadULong();
            GuildRepairLimit = reader.ReadULong();
        }
    }
}
