using NexusForever.Network.Message;

namespace NexusForever.Network.World.Message.Model.PlayerPath
{
    [Message(GameMessageOpcode.ClientPathScientistScan)]
    public class ClientPathScientistScan : IReadable
    {
        public void Read(GamePacketReader reader)
        {
            // zero byte message, the server picks the target
        }
    }
}
