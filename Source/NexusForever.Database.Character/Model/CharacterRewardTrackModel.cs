namespace NexusForever.Database.Character.Model
{
    public class CharacterRewardTrackModel
    {
        public ulong Id { get; set; }
        public ushort RewardTrackId { get; set; }
        public uint Points { get; set; }
        public uint Claimed { get; set; }

        public CharacterModel Character { get; set; }
    }
}
