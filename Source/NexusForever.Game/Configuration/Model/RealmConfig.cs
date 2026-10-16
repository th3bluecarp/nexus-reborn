using NexusForever.Game.Static.RBAC;
using NexusForever.Shared.Configuration;

namespace NexusForever.Game.Configuration.Model
{
    [ConfigurationBind]
    public class RealmConfig
    {
        public MapConfig Map { get; set; }
        public KillXpConfig KillXp { get; set; } = new();
        public RespawnConfig Respawn { get; set; } = new();
        public PathMissionXpConfig PathMissionXp { get; set; } = new();

        /// <summary>
        /// JSON file of observed creature combat spells (see CreatureSpellCatalog for the format), optional.
        /// </summary>
        public string CreatureSpellsPath { get; set; }
        public ushort RealmId { get; set; }
        public string MessageOfTheDay { get; set; }
        public uint LengthOfInGameDay { get; set; }
        public bool CrossFactionChat { get; set; } = true;
        public uint MaxPlayers { get; set; } = 50u;
        public Role? DefaultRole { get; set; } = Role.Player;
    }
}