using NexusForever.Network.Message;

namespace NexusForever.Network.World.Message.Model.GalacticArchive
{
    [Message(GameMessageOpcode.ServerGalacticArchiveUpdate)]
    public class ServerGalacticArchiveUpdate : IWritable
    {
        public uint ArchiveArticleId { get; set; }
        public uint UnlockedFlags { get; set; } // Corresponds to the archiveEntry index not archiveEntryId, see archiveArticle tbl
        public uint ViewedFlags { get; set; } // Corresponds to the archiveEntry index not archiveEntryId, see archiveArticle tbl

        public void Write(GamePacketWriter writer)
        {
            writer.Write(ArchiveArticleId, 14u); // 14 bits, read from WildStar64.exe
            writer.Write(UnlockedFlags);
            writer.Write(ViewedFlags);
        }
    }
}
