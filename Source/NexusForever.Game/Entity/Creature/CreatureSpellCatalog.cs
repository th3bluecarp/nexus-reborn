using System.Text.Json;
using Microsoft.Extensions.Logging;
using NexusForever.Game.Abstract.Entity.Creature;
using NexusForever.Game.Configuration.Model;
using NexusForever.Game.Static.Spell;
using NexusForever.GameTable;
using NexusForever.GameTable.Model;
using NexusForever.Shared.Configuration;

namespace NexusForever.Game.Entity.Creature
{
    /// <summary>
    /// Combat spells observed for creatures on retail, loaded from the file named by Realm:CreatureSpellsPath.
    /// </summary>
    /// <remarks>
    /// The test server builds this file from the ability tabs of archived Jabbithole creature pages, in this format:
    /// { "creatureId": { "name": "...", "auto": [spell4Id], "special": [spell4Id] } }.
    /// The file calls every spell without a cast time an auto attack. Instant abilities among them (a telegraph, or a
    /// multi-phase, channelled or aura cast) are moved to the specials when the file is loaded, see <see cref="SortAbilities"/>.
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
        private readonly IGameTableManager gameTableManager;

        public CreatureSpellCatalog(
            ILogger<CreatureSpellCatalog> log,
            IGameTableManager gameTableManager)
        {
            this.log              = log;
            this.gameTableManager = gameTableManager;
            entries               = new Lazy<Dictionary<uint, Entry>>(Load);
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
                    .Where(p => p.Value != null)
                    .ToDictionary(p => uint.Parse(p.Key), p => p.Value);
                log.LogInformation($"Loaded combat spells for {result.Count} creatures from {path}.");
                SortAbilities(result);
                return result;
            }
            catch (Exception exception)
            {
                log.LogError(exception, $"Failed to load creature spells from {path}!");
                return [];
            }
        }

        /// <summary>
        /// Move instant abilities out of the auto attacks into the specials.
        /// </summary>
        /// <remarks>
        /// The file is sorted by cast time alone, so an instant telegraph such as Crushing Rush (46797, six telegraphs, a 3.8 s
        /// multi-phase charge for 4.331 of assault power against 0.55 for a swing) stood among the swings and came round every
        /// third 1.5 s slot: the Darkspur Lieutenant (16702) cast it every 8.2 s. A spell that draws a telegraph or is not a plain
        /// cast is an ability and joins the specials, one of which is used every 8 to 14 seconds. Creatures left with no swing
        /// at all (mines, hazards, casters seen with one spell) keep their list as it is. The spells keep their table values,
        /// only how often the creature picks them changes; the real rotation is not in the client tables.
        /// </remarks>
        private void SortAbilities(Dictionary<uint, Entry> catalog)
        {
            try
            {
                Spell4TelegraphEntry[] telegraphs = gameTableManager?.Spell4Telegraph?.Entries;
                if (telegraphs == null || gameTableManager.Spell4 == null || gameTableManager.Spell4Base == null)
                    return;

                HashSet<uint> telegraphed = telegraphs
                    .Where(e => e != null)
                    .Select(e => e.Spell4Id)
                    .ToHashSet();

                bool IsAbility(uint spell4Id)
                {
                    Spell4Entry spell = gameTableManager.Spell4.GetEntry(spell4Id);
                    if (spell == null)
                        return false;

                    Spell4BaseEntry spellBase = gameTableManager.Spell4Base.GetEntry(spell.Spell4BaseIdBaseSpell);
                    return telegraphed.Contains(spell4Id) || (spellBase != null && spellBase.CastMethod != CastMethod.Normal);
                }

                int creatures = 0;
                int spells    = 0;
                foreach (Entry entry in catalog.Values)
                {
                    entry.Auto    ??= [];
                    entry.Special ??= [];

                    List<uint> abilities = entry.Auto.Where(IsAbility).ToList();
                    if (abilities.Count == 0 || abilities.Count == entry.Auto.Count)
                        continue;

                    entry.Auto    = entry.Auto.Except(abilities).ToList();
                    entry.Special = entry.Special.Union(abilities).ToList();
                    creatures++;
                    spells += abilities.Count;
                }

                log.LogInformation($"Moved {spells} instant abilities of {creatures} creatures from the auto attacks to the specials.");
            }
            catch (Exception exception)
            {
                log.LogError(exception, "Failed to sort the creature spells, instant abilities stay among the auto attacks!");
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
