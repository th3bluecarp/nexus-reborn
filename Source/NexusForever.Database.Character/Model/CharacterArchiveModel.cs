namespace NexusForever.Database.Character.Model
{
    public class CharacterArchiveModel
    {
        public ulong Id { get; set; }
        public ushort ArticleId { get; set; }
        public uint UnlockedFlags { get; set; }
        public uint ViewedFlags { get; set; }

        public CharacterModel Character { get; set; }
    }
}
