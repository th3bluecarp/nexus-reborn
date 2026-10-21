using NexusForever.Network.Message;

namespace NexusForever.Network.World.Message.Model
{
    /// <summary>
    /// Sets an item's durability at the client, layout read from WildStar64.exe (reader 0x14008ef80, handler 0x1403b9690 fires ItemDurabilityUpdate).
    /// </summary>
    [Message(GameMessageOpcode.ServerItemDurabilityUpdate)]
    public class ServerItemDurabilityUpdate : IWritable
    {
        public ulong ItemGuid { get; set; }

        /// <summary>
        /// Fraction of the maximum durability, 0 to 1.
        /// </summary>
        public float Durability { get; set; }

        public void Write(GamePacketWriter writer)
        {
            writer.Write(ItemGuid);
            writer.Write(Durability);
        }
    }
}
