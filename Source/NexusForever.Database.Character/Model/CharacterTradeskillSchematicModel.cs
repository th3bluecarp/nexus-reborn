namespace NexusForever.Database.Character.Model
{
    public class CharacterTradeskillSchematicModel
    {
        public ulong Id { get; set; }
        public uint SchematicId { get; set; }

        public CharacterModel Character { get; set; }
    }
}
