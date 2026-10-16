namespace NexusForever.Database.Character.Model
{
    public class CharacterPathMissionModel
    {
        public ulong Id { get; set; }
        public ushort MissionId { get; set; }
        public byte State { get; set; }
        public uint Progress { get; set; }

        public CharacterModel Character { get; set; }
    }
}
