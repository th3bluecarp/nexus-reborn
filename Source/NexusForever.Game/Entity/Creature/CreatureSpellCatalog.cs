using System.Text.Json;
using Microsoft.Extensions.Logging;
using NexusForever.Game.Abstract.Entity.Creature;
using NexusForever.Game.Configuration.Model;
using NexusForever.Shared.Configuration;

namespace NexusForever.Game.Entity.Creature
{
    /// <summary>
    /// Combat spells observed for creatures on retail, loaded from the file named by Realm:CreatureSpellsPath.
    /// </summary>
    /// <remarks>
    /// The test server builds this file from the ability tabs of archived Jabbithole creature pages, in this format:
    /// { "creatureId": { "name": "...", "auto": [spell4Id], "special": [spell4Id] } }.
    /// </remarks>
    public class CreatureSpellCatalog : ICreatureSpellCatalog
    {
        private class Entry
        {
            public string Name { get; set; }
            public List<uint> Auto { get; set; } = [];
            public List<uint> Special { get; set; } = [];
        }

        private readonly Lazy<Dictionary<uint, Entry>> entries;

        #region Dependency Injection

        private readonly ILogger<CreatureSpellCatalog> log;

        public CreatureSpellCatalog(
            ILogger<CreatureSpellCatalog> log)
        {
            this.log = log;
            entries  = new Lazy<Dictionary<uint, Entry>>(Load);
        }

        #endregion

        private Dictionary<uint, Entry> Load()
        {
            string path = SharedConfiguration.Instance.Get<RealmConfig>()?.CreatureSpellsPath;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                log.LogInformation("No creature spell file configured, creatures use the generic auto attacks.");
                return [];
            }

            try
            {
                var raw = JsonSerializer.Deserialize<Dictionary<string, Entry>>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                var result = raw
                    .Where(p => uint.TryParse(p.Key, out _))
                    .ToDictionary(p => uint.Parse(p.Key), p => p.Value);
                log.LogInformation($"Loaded combat spells for {result.Count} creatures from {path}.");
                return result;
            }
            catch (Exception exception)
            {
                log.LogError(exception, $"Failed to load creature spells from {path}!");
                return [];
            }
        }

        public IReadOnlyList<uint> GetAutoAttacks(uint creatureId)
        {
            return entries.Value.TryGetValue(creatureId, out Entry entry) ? entry.Auto : [];
        }

        public IReadOnlyList<uint> GetSpecials(uint creatureId)
        {
            return entries.Value.TryGetValue(creatureId, out Entry entry) ? entry.Special : [];
        }
    }
}
