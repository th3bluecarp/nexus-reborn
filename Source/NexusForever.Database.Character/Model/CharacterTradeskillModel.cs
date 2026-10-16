namespace NexusForever.Database.Character.Model
{
    public class CharacterTradeskillModel
    {
        public ulong Id { get; set; }
        public uint TradeskillId { get; set; }
        public uint Xp { get; set; }
        public bool Active { get; set; }

        public CharacterModel Character { get; set; }
    }
}
