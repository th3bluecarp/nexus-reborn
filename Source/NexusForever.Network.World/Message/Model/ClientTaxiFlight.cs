using NexusForever.Network.Message;

namespace NexusForever.Network.World.Message.Model
{
    /// <summary>
    /// Sent by a taxi kiosk (PurchaseFlightPath, the route of TaxiNode ids) or a shuttle (TakeShuttle, one TaxiNode id).
    /// </summary>
    /// <remarks>
    /// Read from WildStar64.exe 16042: the client builds a count and an array of TaxiNode ids. The bit widths are not known yet,
    /// so the body is kept raw and decoded by the handler.
    /// </remarks>
    [Message(GameMessageOpcode.ClientTaxiFlight)]
    public class ClientTaxiFlight : IReadable
    {
        public byte[] Payload { get; private set; }

        public void Read(GamePacketReader reader)
        {
            Payload = reader.ReadBytes(reader.BytesRemaining);
        }
    }
}
