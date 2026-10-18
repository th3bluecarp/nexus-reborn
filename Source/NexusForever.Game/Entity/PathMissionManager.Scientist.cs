using Microsoft.Extensions.DependencyInjection;
using System.Numerics;
using System.Text.RegularExpressions;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Entity.Creature;
using NexusForever.Game.Abstract.Map;
using NexusForever.Game.Static.PlayerPath;
using NexusForever.GameTable;
using NexusForever.GameTable.Model;
using NexusForever.Network.World.Message.Model.PlayerPath;
using NexusForever.Shared;
using Path = NexusForever.Game.Static.PlayerPath.Path;

namespace NexusForever.Game.Entity
{
    /// <summary>
    /// Scientist scan missions (mission type 2, and type 14 checklists that need several different units) and the scanbot.
    /// </summary>
    /// <remarks>
    /// Retail decided on the server which creatures belong to a scan mission and told the client with
    /// ServerPathScientistAddCreatureInfoToCreature. That mapping is lost, it is rebuilt here from the mission texts as creature
    /// names (<see cref="ScanRules"/>, zone by zone). Every group of names has a number of scans to make, each scan of a
    /// different unit fills one slot, the mission progress is the bit mask of filled slots.
    /// The client sends 0x180 to summon the scanbot, 0xF0 to dismiss it and 0x15F when the path action button scans.
    /// </remarks>
    public partial class PathMissionManager
    {
        private record ScanRule(string[][] Groups, uint ScansPerGroup);

        /// <summary>
        /// Scan targets by path mission id, creature names (plural tag removed) that count for each group of the mission.
        /// </summary>
        private static readonly Dictionary<uint, ScanRule> ScanRules = new()
        {
            // Northern Wilds
            [42u]  = new([["Dominion Turret", "Inactive Dominion Turret"], ["Dominion Secure Database", "Dominion Secure Database Hologram"]], 1u), // ANALYSIS: Turning the Tide
            [160u] = new([["Vitalium Crystal"]], 3u),                                                     // CHEMISTRY: Vitalium Crystals
            [648u] = new([["Skeech Diviner", "Skeech Scratcher", "Skeech Shaman", "Cleaved Skeech Shaman"]], 3u), // BIOLOGY: Skeech Physiology
            // Algoroc (Grimstone Girrok 264, Gronyx 826 and Stumpkins 827 have no spawns anywhere in Algoroc, no rule)
            [201u]  = new([["Irradiated Icetail Leaper", "Irradiated Icetail Pouncer", "Irradiated Icetail Slayer", "Irradiated Icetail Stalker", "Irradiated Snoglug"]], 3u), // BIOLOGY: Irradiated Wildlife
            [218u]  = new([["Large Loftite Formation"]], 3u),       // CHEMISTRY: Loftite Crystals
            [266u]  = new([["Fiera Flower"]], 3u),                  // BOTANY: Fiera Flowers
            [1978u] = new([["Augmented XAS Scientist"]], 3u),       // BIOLOGY: Augmented Scientists
            [2139u] = new([["Eldan Repair Construct"]], 3u),        // ANALYSIS: Eldan Constructs
            [262u]  = new([["Darkspur Cartel Terminal"]], 5u),      // ANALYSIS: Darkspur Cartel Terminals (checklist of 5)
            [701u]  = new([["Eldan Relic", "Eldan Relics"]], 8u),   // ARCHAEOLOGY: Excavated Artifacts (checklist of 8)
            [2132u] = new([["DREDplex Data Terminal"]], 10u),       // ANALYSIS: DREDplex Weapon Designs (checklist of 10)
            [2133u] = new([["Deadstar Data Terminal", "Deadstar Footlocker", "Deadstar Clothesline"]], 3u), // ANALYSIS: Deadeye and the Deadstar (checklist of 3)
            // Celestion
            [1714u] = new([["Hoogle Guardian Totem", "Transmorphing Flight Hoogle Totem"]], 3u), // ANALYSIS: Hoogle Totems
            [1719u] = new([["Draken Garden Gnome"], ["Draken Backscratcher"], ["Draken Chamberpot"]], 1u), // ANALYSIS: Draken Implements
            [1724u] = new([["Weave Portal"]], 3u),                  // BOTANY: Weave Portals (checklist of 3)
            [1725u] = new([["Moodie Mojo"]], 3u),                   // ARCHAEOLOGY: Moodie Mojo
            [1828u] = new([["Augmented Celestial Razortusk", "Augmented Nightfang Creeper", "Augmented Nightfang Weaver", "Augmented Nightfang Widow", "Augmented Buzzbing", "Augmented Nightfang Flayer"]], 3u), // BIOLOGY: Augmented Creatures
            [1830u] = new([["Amorous Rowsdower"]], 10u),            // CHEMISTRY: Lessons in Love (checklist of 10)
            [1840u] = new([["Rowsdower's Corpse", "Sunhorn Roamer's Corpse", "Ravenous Corpse"]], 3u), // BIOLOGY: That Special Glow
            [1841u] = new([["Mordesh Vitality Extractor"]], 3u),    // DIAGNOSTICS: Mordesh Machinery
            [1845u] = new([["Water of Life"]], 5u),                 // CHEMISTRY: Water of Life (checklist of 5)
            [1847u] = new([["Ravenous Ripper", "Ravenous Ghoul", "Ravenous Horror", "Ravenous Fiend", "Ravenous Reaver", "Ravenous Malignant", "Ravenous Mordesh"]], 3u), // BIOLOGY: Ravenous Ruminations
            [1854u] = new([["Communication Controls", "Communication Dish"]], 4u), // ANALYSIS: Eldan Communications (checklist of 4)
            [1859u] = new([["Shield Generator", "Bio-Containment Shield"]], 3u), // DIAGNOSTICS: Switching Safeguards (checklist of 3)
            // Galeras (dead Buzzbings 707, Crimson Legion uplink stations 712 and sparking wires 3156 have no spawns, no rule)
            [425u]  = new([["Razorbeak Riptalon", "Razorbeak Render", "Razorbeak Chick", "Razorbeak Nester", "Razorbeak Forager", "Razorbeak Ripper"]], 3u), // BIOLOGY: Razorbeak Gribbons
            [426u]  = new([["Thundercall Guardian", "Thundercall Arcanist", "Thundercall Wind-Speaker", "Thundercall Warrior", "Thundercall Wind-Witch", "Thundercall Wind-Striker"]], 3u), // BIOLOGY: Thundercall Pell
            [427u]  = new([["Sandstorm Elemental", "Wind Elemental", "Gust Elemental", "Sandsquall Elemental", "Angry Air Elemental"]], 3u), // BIOLOGY: Air Elementals
            [696u]  = new([["Windyburrow Biter", "Windyburrow Nibbler", "Windyburrow Nipper", "Windyburrow Gnawer"]], 3u), // BIOLOGY: Chompacabra
            [697u]  = new([["Pollen"]], 3u),                        // BOTANY: Pollen Clouds
            [698u]  = new([["Falkrin Feather Pile", "Falkrin Feather"]], 3u), // BIOLOGY: Falkrin Feathers
            [699u]  = new([["Primal Osun Brazier", "Osun Chains"]], 3u), // ARCHAEOLOGY: Osun Relics
            [702u]  = new([["Augmented Gustclaw Burrower", "Augmented Sickleswarm Hivedrone", "Augmented Spiderling", "Augmented Recluse", "Augmented Sickleswarm Divebomber", "Augmented Gustclaw Shredder"]], 3u), // BIOLOGY: Augmented Organisms
            [706u]  = new([["Eldan Scrap"]], 3u),                   // ARCHAEOLOGY: Eldan Scrap Yard
            [708u]  = new([["Starstem Plant"]], 3u),                // BOTANY: The Growling Forest
            [709u]  = new([["Eldan Switch"]], 4u),                  // ANALYSIS: Eldan Primal Lock (checklist of 4)
            [710u]  = new([["Galeras Sprout"]], 3u),                // BOTANY: Resilient Sprouts
            [711u]  = new([["Toxic Material"]], 6u),                // CHEMISTRY: Toxic Wasteland (checklist of 6)
            [1251u] = new([["Eldan Fuel Rod"]], 6u),                // ARCHAEOLOGY: Eldan Fuel Rods (checklist of 6)
            [3159u] = new([["Osiric Totem"]], 3u),                  // ANALYSIS: Totems of Osiric
            // Everstar Grove
            [1516u] = new([["Thrasher Fern"]], 3u), // BOTANY: Thrasher Ferns
            [1517u] = new([["Barkblight Egg"]], 3u), // BIOLOGY: Barkblight Eggs
            [1518u] = new([["Firestorm Fuel"]], 3u), // ANALYSIS: Firestorm Fuel
            // Whitevale (the two augmented creatures behind force fields, 1033, have no clear name, no rule)
            [777u] = new([["Silversnout Mauler", "Silversnout Crusher", "Silversnout Girrok", "Hibernating Silversnout", "Evolved Silversnout"]], 3u), // BIOLOGY: Silversnout Girrok
            [873u] = new([["Silversnout Mauler", "Silversnout Crusher", "Silversnout Girrok", "Hibernating Silversnout", "Evolved Silversnout"]], 3u), // BIOLOGY: Silversnout Girrok
            [778u] = new([["Territorial Icetail", "Wired Icetail", "Wired Icetail Screecher"]], 3u), // BIOLOGY: Aggressive Icetails
            [871u] = new([["Territorial Icetail", "Wired Icetail", "Wired Icetail Screecher"]], 3u), // BIOLOGY: Aggressive Icetails
            [779u] = new([["Ice-Crusted Boulderback"]], 3u), // BIOLOGY: Ice-Crusted Boulderbacks
            [874u] = new([["Ice-Crusted Boulderback"]], 3u), // BIOLOGY: Ice-Crusted Boulderbacks
            [780u] = new([["Frost-Striped Pumera"]], 3u), // BIOLOGY: Frost-Striped Pumera
            [872u] = new([["Frost-Striped Pumera"]], 3u), // BIOLOGY: Frost-Striped Pumera
            [781u] = new([["Lumbering Walatusk", "Walatusk Bull"]], 3u), // BIOLOGY: Walatusk
            [782u] = new([["Tundra Jabbit"]], 3u), // BIOLOGY: Tundra Jabbit
            [875u] = new([["Tundra Jabbit"]], 3u), // BIOLOGY: Tundra Jabbit
            [814u] = new([["Gigantic Ancient Skull"], ["Gigantic Bone Claw"]], 1u), // ARCHAEOLOGY: Gigantic Ancient Remains (checklist of 2)
            [877u] = new([["Territorial Icebay Shellark"]], 3u), // BIOLOGY: Territorial Icebay Shellark
            [1103u] = new([["Lopp Treasure Hunter"]], 3u), // BIOLOGY: Lopp Treasure Hunters
            [1106u] = new([["Rimerazor Nomad", "Rimerazor Brute", "Rimerazor Howler", "Rimerazor Bruiser"]], 3u), // BIOLOGY: Rimerazor Yeti
            [1107u] = new([["Coldroot Icecracker"]], 3u), // BIOLOGY: Rootbrute Icecrackers
            [1293u] = new([["Soulcore Evocator", "Soulcore Vindicator", "Soulcore Defender", "Soulcore Disciple", "Soulcore Interrogator", "Soulcore Fanatic", "Soulcore Zealot"]], 3u), // BIOLOGY: Soulcore Ascendancy
            [1294u] = new([["Modified X426 Probe"]], 3u), // BIOLOGY: Soulcore Monstrosities
            [1302u] = new([["Ikthian Data Terminal"]], 6u), // ANALYSIS: Ikthian Data Terminals (checklist of 6)
            [1303u] = new([["Ikthian Data Terminal"]], 6u), // ANALYSIS: Ikthian Data Terminals (checklist of 6)
            [1305u] = new([["Silverscale Hydrocrawler"]], 3u), // BIOLOGY: Silverscale Hydrocrawlers
            [1307u] = new([["Silverscale Hydrocrawler"]], 3u), // BIOLOGY: Silverscale Hydrocrawlers
            [1331u] = new([["Calmwater Rain-Warrior", "Calmwater Tide-Bringer", "Calmwater Rain-Catcher"]], 8u), // BIOLOGY: Calmwater Pell (checklist of 8)
            [1334u] = new([["Calmwater Rain-Warrior", "Calmwater Tide-Bringer", "Calmwater Rain-Catcher"]], 8u), // BIOLOGY: Calmwater Pell (checklist of 8)
            [3058u] = new([["Terros Medusa"]], 3u), // BIOLOGY: Terros Medusas
            // Crimson Isle
            [1048u] = new([["Bloodpetal"]], 3u), // BOTANY: Bloodpetal Properties
            [1056u] = new([["Dreg Experiment", "Dreg Abomination"]], 3u), // BIOLOGY: Dreg Decryption
            [1057u] = new([["Deactivated Battlebot"]], 3u), // ANALYSIS: Repurposed Bots
            // Levian Bay (the Eldan signal relays of 1365 have no spawned creature, no rule)
            [1262u] = new([["Gloomshroud"]], 3u), // BOTANY: Gloomshroud Plants
            [1370u] = new([["Inactive Exile Turret"]], 3u), // ANALYSIS: Reversal of Fortune
            // Deradune (supersized flora 1298 and the risen dead 1345 have no clear creatures, no rule)
            [66u] = new([["Cinderbloom"]], 3u), // BOTANY: Cinderbloom
            [67u] = new([["Deadwind Maniac", "Deadwind Witchdoctor", "Deadwind Lunatic", "Deadwind Caster", "Deadwind Sleeper"]], 3u), // BIOLOGY: Deadwind Moodies
            [68u] = new([["Gnarlfoot Climber", "Gnarlfoot Slasher"]], 3u), // BIOLOGY: Gnarlfoot Razortail
            [70u] = new([["Bloodfire Sawgrass"]], 3u), // BOTANY: Bloodfire Sawgrass
            [71u] = new([["Accelerite Crystal"]], 3u), // CHEMISTRY: Accelerite Crystals
            [167u] = new([["Grove Strangler"]], 3u), // BOTANY: Grove Strangler
            [1153u] = new([["Feralplain Data Station"]], 15u), // DIAGNOSTICS: Feralplain Security Checkpoints (checklist of 15)
            [1182u] = new([["Larval Terminite", "Terminite Egg"]], 5u), // ANALYSIS: Terminite Mutations (checklist of 5)
            [1366u] = new([["Oozing Sludgeon", "Putrid Oozing Sludgeon", "Viscous Oozing Sludgeon"]], 3u), // BIOLOGY: Oozing Sludgeons
            [1887u] = new([["Bloodtalon Warden", "Bloodtalon Scrapper", "Bloodtalon Horncaller", "Bloodtalon Slicer"]], 3u), // BIOLOGY: Falkrin and Primal Air
        };

        /// <summary>
        /// A scan reaches the player's target up to this distance, without a target the nearest scan target within <see cref="ScanSearchRange"/>.
        /// </summary>
        private const float ScanTargetRange = 30f;
        private const float ScanSearchRange = 15f;

        private uint? scanbotGuid;
        private readonly Dictionary<ushort, HashSet<uint>> scannedUnits = [];

        private static PathMission CreateScanMission(PathMissionEntry entry)
        {
            if (!ScanRules.TryGetValue(entry.Id, out ScanRule rule))
                return null;

            uint slots = Math.Min((uint)rule.Groups.Length * rule.ScansPerGroup, 32u);
            return new PathMission { Entry = entry, Kind = MissionKind.Scan, Objects = [entry.ObjectId], Count = slots };
        }

        private static string CreatureName(IWorldEntity entity)
        {
            if (entity.CreatureId == 0u)
                return null;

            Creature2Entry creature = GameTableManager.Instance.Creature2.GetEntry(entity.CreatureId);
            if (creature == null)
                return null;

            string name = GameTableManager.Instance.TextEnglish?.GetEntry(creature.LocalizedTextIdName);
            return name == null ? null : Regex.Replace(name, @"\{p:[^}]*\}", "");
        }

        /// <summary>
        /// Return the group of the scan rule the entity belongs to, -1 when it is not a target of the mission.
        /// </summary>
        private static int ScanGroup(PathMission mission, string name)
        {
            if (name == null || !ScanRules.TryGetValue(mission.Entry.Id, out ScanRule rule))
                return -1;

            for (int i = 0; i < rule.Groups.Length; i++)
                if (rule.Groups[i].Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)))
                    return i;

            return -1;
        }

        /// <summary>
        /// Return the first free slot of the group, null when the group has all its scans.
        /// </summary>
        private static int? FreeScanSlot(PathMission mission, int group)
        {
            uint perGroup = ScanRules[mission.Entry.Id].ScansPerGroup;
            for (int i = 0; i < perGroup; i++)
            {
                int slot = group * (int)perGroup + i;
                if (slot < 32 && (mission.Progress & (1u << slot)) == 0u)
                    return slot;
            }

            return null;
        }

        private IEnumerable<PathMission> ActiveScanMissions()
        {
            return missions.Values.Where(m => m.Kind == MissionKind.Scan && !m.IsComplete && m.Entry.PathTypeEnum == (uint)player.Path);
        }

        /// <summary>
        /// The scan mission the entity can still count for, with the group it belongs to.
        /// </summary>
        private (PathMission Mission, int Group)? FindScanMission(IWorldEntity entity)
        {
            string name = CreatureName(entity);
            if (name == null)
                return null;

            foreach (PathMission mission in ActiveScanMissions())
            {
                if (scannedUnits.TryGetValue((ushort)mission.Entry.Id, out HashSet<uint> scanned) && scanned.Contains(entity.Guid))
                    continue;

                int group = ScanGroup(mission, name);
                if (group >= 0 && FreeScanSlot(mission, group) != null)
                    return (mission, group);
            }

            return null;
        }

        private void MarkScanTarget(IWorldEntity entity)
        {
            if (player.Path != Path.Scientist)
                return;

            (PathMission Mission, int Group)? target = FindScanMission(entity);
            if (target == null)
                return;

            player.Session.EnqueueMessageEncrypted(new ServerPathScientistAddCreatureInfoToCreature
            {
                UnitId                      = entity.Guid,
                PathScientistCreatureInfoId = target.Value.Mission.Entry.ObjectId
            });
            player.Session.EnqueueMessageEncrypted(new ServerPathScientistUnitScanParameters
            {
                UnitId          = entity.Guid,
                ScanRewardFlags = ScanReward.RewardForScan,
                IsScannable     = true
            });
        }

        public void SummonScanbot(uint profileId)
        {
            if (player.Path != Path.Scientist || player.Map == null)
                return;

            if (scanbotGuid != null && player.Map.GetEntity<IPetEntity>(scanbotGuid.Value) != null)
                return;

            PathScientistScanBotProfileEntry profile = GameTableManager.Instance.PathScientistScanBotProfile.GetEntry(profileId == 0u ? 1u : profileId)
                ?? GameTableManager.Instance.PathScientistScanBotProfile.GetEntry(1u);
            if (profile == null)
                return;

            ICreatureInfo creatureInfo = LegacyServiceProvider.Provider.GetService<ICreatureInfoManager>().GetCreatureInfo(profile.Creature2Id);
            if (creatureInfo == null)
                return;

            var scanbot = LegacyServiceProvider.Provider.GetService<IEntityFactory>().CreateEntity<IPetEntity>();
            scanbot.Initialise(player, creatureInfo, true);
            scanbot.AddToMap(player.Map, player.Position + new Vector3(1.5f, 0f, 1.5f), (map, guid, vector) =>
            {
                scanbotGuid = guid;
                player.Session.EnqueueMessageEncrypted(new ServerPathScientistScanbotState
                {
                    ScanbotUnitId = guid
                });
            });
        }

        public void DismissScanbot()
        {
            if (scanbotGuid == null)
                return;

            player.Map?.GetEntity<IPetEntity>(scanbotGuid.Value)?.RemoveFromMap();
            scanbotGuid = null;

            player.Session.EnqueueMessageEncrypted(new ServerPathScientistScanbotState
            {
                ScanbotUnitId     = 0u,
                ScanbotCooldownMS = 0u
            });
        }

        public void Scan()
        {
            if (player.Path != Path.Scientist || player.Map == null)
                return;

            // the path action scans only with the scanbot out, without one the client asks for it instead (0x180)
            if (scanbotGuid == null)
            {
                SummonScanbot(0u);
                return;
            }

            IWorldEntity target = null;
            if (player.TargetGuid != null)
            {
                IWorldEntity selected = player.GetVisible<IWorldEntity>(player.TargetGuid.Value);
                if (selected != null && Vector3.Distance(selected.Position, player.Position) <= ScanTargetRange && FindScanMission(selected) != null)
                    target = selected;
            }

            target ??= player.GetVisible()
                .OfType<IWorldEntity>()
                .Where(e => e != player && Vector3.Distance(e.Position, player.Position) <= ScanSearchRange)
                .OrderBy(e => Vector3.DistanceSquared(e.Position, player.Position))
                .FirstOrDefault(e => FindScanMission(e) != null);

            if (target == null)
            {
                // a specimen survey takes a sample from the target, or the nearest creature, at its locations
                IWorldEntity specimen = player.TargetGuid != null ? player.GetVisible<IWorldEntity>(player.TargetGuid.Value) : null;
                specimen ??= player.GetVisible()
                    .OfType<IUnitEntity>()
                    .Where(e => e is not IPlayer && e.IsAlive && Vector3.Distance(e.Position, player.Position) <= ScanSearchRange)
                    .OrderBy(e => Vector3.DistanceSquared(e.Position, player.Position))
                    .FirstOrDefault();
                if (specimen != null && Vector3.Distance(specimen.Position, player.Position) <= ScanTargetRange)
                    ScanSpecimen(specimen);
                return;
            }

            (PathMission mission, int group) = FindScanMission(target).Value;
            int slot = FreeScanSlot(mission, group).Value;

            if (!scannedUnits.TryGetValue((ushort)mission.Entry.Id, out HashSet<uint> scanned))
                scannedUnits.Add((ushort)mission.Entry.Id, scanned = []);
            scanned.Add(target.Guid);

            // the scanbot goes to the target
            IPetEntity scanbot = player.Map.GetEntity<IPetEntity>(scanbotGuid.Value);
            scanbot?.MovementManager.Follow(target, 2f);

            player.Session.EnqueueMessageEncrypted(new ServerPathScientistUnitScanParameters
            {
                UnitId          = target.Guid,
                ScanRewardFlags = 0,
                IsScannable     = false
            });

            Advance(mission, mission.Progress | (1u << slot));
        }
    }
}
