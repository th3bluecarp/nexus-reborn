namespace NexusForever.Game.Static.PlayerPath
{
    /// <summary>
    /// The targets of a scientist scan mission: creature names (plural tag removed) by group, and the scans each group needs.
    /// </summary>
    public sealed record ScientistScanRule(string[][] Groups, uint ScansPerGroup);

    /// <summary>
    /// Which creatures belong to a scientist scan, checklist or datacube mission.
    /// </summary>
    /// <remarks>
    /// Retail decided this on the server and the mapping is lost, it is rebuilt here zone by zone from the mission texts and
    /// the world database spawns. The world server (PathMissionManager) credits scans from these tables and the test bot
    /// (NexusForever.Bot) reads the same tables to find its targets: one list, so a zone added here is played by the bot too.
    /// </remarks>
    public static class ScientistMissionRules
    {
        /// <summary>
        /// Scan targets by path mission id, creature names (plural tag removed) that count for each group of the mission.
        /// </summary>
        public static IReadOnlyDictionary<uint, ScientistScanRule> Scan { get; } = new Dictionary<uint, ScientistScanRule>
        {
            // Northern Wilds
            [42u]  = new([["Dominion Turret", "Inactive Dominion Turret"], ["Dominion Secure Database", "Dominion Secure Database Hologram"]], 1u), // ANALYSIS: Turning the Tide
            [160u] = new([["Vitalium Crystal"]], 3u),                                                     // CHEMISTRY: Vitalium Crystals
            [648u] = new([["Skeech Diviner", "Skeech Scratcher", "Skeech Shaman", "Cleaved Skeech Shaman"]], 3u), // BIOLOGY: Skeech Physiology
            // Algoroc
            [264u]  = new([["Grimstone Digger", "Grimstone Ruffian", "Grimstone Mangler", "Grimstone Savage", "Grimstone Ravager"]], 3u), // BIOLOGY: Grimstone Girrok
            [826u]  = new([["Augmented Slateform Slammer", "Augmented Slateform Charger", "Augmented Slateform Demolisher", "Hulking Gronyx", "Gronyx"]], 3u), // BIOLOGY: Gronyx
            [827u]  = new([["Pinewrath Barktooth", "Pinewrath Trunksplitter", "Pinewrath Stumpmaker", "Pinewrath Logchomper", "Pinewrath Branchsnapper", "Barktooth Stumpkin", "Trunksplitter Stumpkin"]], 3u), // BIOLOGY: Stumpkins
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
            // Whitevale
            [1033u] = new([["Augmented Canimid"], ["Augmented Buzzbing"]], 1u),       // ANALYSIS: Augmented Monstrosities
            // Thayd (the Hoogle Statues are placed by data/missions.sql)
            [3184u] = new([["Floating Hoogle Statue", "Hoogle Statue"]], 3u),          // ANALYSIS: Hoogle Stones
            // Galeras (dead Buzzbings, uplink stations and sparking wires are placed by data/missions.sql)
            [707u]  = new([["Buzzbing Remains 1", "Buzzbing Remains 2"]], 3u),          // BIOLOGY: Buzzing Bodies
            [712u]  = new([["Crimson Legion Uplink Station"]], 3u),                  // ANALYSIS: Stolen Science
            [3156u] = new([["Sparking Wire"]], 8u),                                  // DIAGNOSTICS: Power Outage (checklist of 8)
            [425u]  = new([["Razorbeak Riptalon", "Razorbeak Render", "Razorbeak Chick", "Razorbeak Nester", "Razorbeak Forager", "Razorbeak Ripper"]], 3u), // BIOLOGY: Razorbeak Gribbons
            [426u]  = new([["Thundercall Guardian", "Thundercall Arcanist", "Thundercall Wind-Speaker", "Thundercall Warrior", "Thundercall Wind-Witch", "Thundercall Wind-Striker"]], 3u), // BIOLOGY: Thundercall Pell
            [427u]  = new([["Sandstorm Elemental", "Wind Elemental", "Gust Elemental", "Sandsquall Elemental", "Angry Air Elemental"]], 3u), // BIOLOGY: Air Elementals
            [696u]  = new([["Windyburrow Biter", "Windyburrow Nibbler", "Windyburrow Nipper", "Windyburrow Gnawer"]], 3u), // BIOLOGY: Chompacabra
            [697u]  = new([["Pollen"]], 3u),                        // BOTANY: Pollen Clouds
            [698u]  = new([["Falkrin Feather Pile", "Falkrin Feather"]], 3u), // BIOLOGY: Falkrin Feathers
            [699u]  = new([["Primal Osun Brazier", "Osun Chains"]], 3u), // ARCHAEOLOGY: Osun Relics
            [702u]  = new([["Augmented Gustclaw Burrower", "Augmented Sickleswarm Hivedrone", "Augmented Spiderling", "Augmented Recluse", "Augmented Sickleswarm Divebomber", "Augmented Gustclaw Shredder"]], 3u), // BIOLOGY: Augmented Organisms
            [706u]  = new([["Eldan Scrap"]], 3u),                   // ARCHAEOLOGY: Eldan Scrap Yard
            [708u]  = new([["Whimflower", "Starstem Plant"]], 3u),  // BOTANY: The Growling Forest (Creature2 describes the six Whimflowers as "Scientist m708 Scan Target")
            [709u]  = new([["Eldan Button", "Eldan Switch"]], 4u),  // ANALYSIS: Eldan Primal Lock (checklist of 4; Creature2 describes the four Eldan Buttons as "Scientist m709 Puzzle Button")
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
            // Levian Bay
            [1365u] = new([["Eldan Signal Relay", "Eldan Control Panel", "Eldan Security Node", "Eldan Primary Access Hub"]], 6u), // ARCHAEOLOGY: Eldan Enigma (checklist of 6)
            [1262u] = new([["Gloomshroud"]], 3u), // BOTANY: Gloomshroud Plants
            [1370u] = new([["Inactive Exile Turret"]], 3u), // ANALYSIS: Reversal of Fortune
            // Deradune
            [1298u] = new([["Giant Papyrus"]], 15u),                                // BOTANY: Supersized Flora (checklist of 15)
            [1345u] = new([["Risen Girrok", "Risen Girrok Skullchewer", "Risen Draken Warrior", "Risen Draken Huntress", "Newly Risen Draken", "Vengeful Risen Draken", "Witchwake Risen"]], 3u), // BIOLOGY: The Risen Dead
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
            // Ellevar (the Weeping Bluepines are placed by data/missions.sql)
            [1693u] = new([["Rusty Frame", "Rusty Valve"]], 1u),                   // ARCHAEOLOGY: Forgotten Filtration
            [1702u] = new([["Fine Leather Couch"], ["Lightreach Well"], ["Icon of Virtue"], ["Forgotten Jar"], ["Forgotten Sigil"], ["Forgotten Shrine"]], 1u), // ARCHAEOLOGY: Icons of Faith (checklist of 6)
            [1806u] = new([["Weeping Bluepine"]], 3u),                             // BOTANY: Weeping Bluepine
            [1692u] = new([["Ravenok Nest"]], 3u), // BIOLOGY: Ravenok Nesting Grounds
            [1694u] = new([["Stoneridge Cragfist", "Stoneridge Smasher"]], 3u), // CHEMISTRY: Stoneridge Gronyx
            [1695u] = new([["Highland Aloe"]], 3u), // BOTANY: Highland Aloe
            [1707u] = new([["Copperworth Sprinkler"]], 3u), // DIAGNOSTICS: Copperworth Irrigation
            [1738u] = new([["Augmentation Apparatus Component", "Ikthian Stasis Generator", "Ikthian Transport Controls", "Ikthian Biological Experiment", "Ikthian Scanning Device", "Ikthian Data Panel"]], 8u), // ANALYSIS: Ikthian Technology (checklist of 8)
            [1748u] = new([["Ferocious Darkspire Slank", "Bloated Darkspire Slank", "Darkspire Slank", "Darkspire Tormentor", "Darkspire Strangler", "Darkspire Hoarder Slank"]], 3u), // BIOLOGY: Mutated Wildlife
            [1807u] = new([["AH47 Augmentor", "Augmentor Core"]], 3u), // ARCHAEOLOGY: Eldan Answers
            [2007u] = new([["Circuitry"]], 4u), // ARCHAEOLOGY: Eldan Circuitry (checklist of 4)
            // Auroria
            [1612u] = new([["Collegium Corpse"]], 3u),                             // BIOLOGY: Victims of Augmentation
            [1615u] = new([["Larval Terminite", "Terminite Egg"]], 3u),          // BIOLOGY: Terminite Studies
            [1745u] = new([["Shrine to Ugreth Foe-Breaker"], ["Shrine to Morgra the Subjugator"], ["Shrine to Kozek the Strong"], ["Shrine to Vrogh the Hound-Striker"], ["Shrine to Torug the Bladeforger"]], 1u), // ARCHAEOLOGY: Heroes of the Osun (checklist of 5)
            // Illium
            [2373u] = new([["Indominar"], ["Mechari Head"], ["Helm of Azrion"], ["Exanite Ore"], ["The Cozmotron"], ["The Illium Scroll"], ["The Nomad Beacon"], ["The God-Bone"]], 1u), // ANALYSIS: Eldan Treasure (checklist of 8)
            [183u] = new([["Plagued Strawgrazer Bull", "Plagued Fields Hunter"]], 3u), // BIOLOGY: Plagued Citizens
            [187u] = new([["Greystone Rumbler", "Greystone Slasher", "Greystone Demolisher", "Greystone Earthrager", "Greystone Masher", "Greystone Burrower", "Greystone Pummeler"]], 3u), // BIOLOGY: Greystone Canimid
            [188u] = new([["Nova Blossom"]], 3u), // BOTANY: Volatile Nova Blossom
            [189u] = new([["Eldan Technology"]], 3u), // ARCHAEOLOGY: Augmentation Relics
            [190u] = new([["Plague Canister"]], 3u), // ANALYSIS: Plague Canisters
            [235u] = new([["Cuboar Abomination", "Expired Cubig"]], 3u), // BIOLOGY: Cuboar Rejects
            [237u] = new([["Honeyhive Guardian", "Honeyhive Stinger", "Honeyhive Sentinel", "Honeyhive Swarmer", "Honeyhive Warden", "Honeyhive Harvester", "Honeyhive Drone"]], 3u), // BIOLOGY: Honeyhive Buzzbings
            [301u] = new([["Blooming Gildgrass"]], 3u), // BOTANY: Blooming Gildgrass
            [1322u] = new([["Biting Boulder"]], 3u), // BIOLOGY: Biting Boulder
            [1362u] = new([["Terrenite"]], 3u), // CHEMISTRY: Terrenite
            [1616u] = new([["Blackheart Spellbinder"], ["Blackheart Summoner"], ["Blackheart Enchanter"]], 1u), // ANALYSIS: Channelers of the Blackheart
            [1750u] = new([["Soulrot Elixir", "Soulrotted Minion", "Soulrotted Digger", "Soulrotted Dark Mender"]], 3u), // BIOLOGY: Slaves of the Osun
            [1751u] = new([["Lancer Drop Pod"]], 6u), // ANALYSIS: Dropping Like Granok (checklist of 6)
            [1752u] = new([["Voreth Bone Totem"]], 3u), // ANALYSIS: Grim Foci
            [1755u] = new([["Sky Cutter", "Fringe Pack Rover", "Fringe Scavenger", "Fringe Pack Alpha", "Fringe Pack Stalker", "Fringe Pack Hunter"]], 3u), // BIOLOGY: Scavenger Studies
            // Farside (asteroids, medusa eggs, foxfire, fiera flowers, toys, agave, drums and the third construct and hookfoot are placed by data/missions.sql)
            [1534u] = new([["Farside Scavenger", "Farside Scrounger"]], 3u),                                // BIOLOGY: Alien Inhabitants (hookfoots)
            [1535u] = new([["Unusual Asteroid"]], 5u),                                                     // ARCHAEOLOGY: Strange Rocks (checklist of 5)
            [1558u] = new([["Derelict Silo Maintenance Construct", "Derelict Silo Command Construct", "Damaged Maintenance Construct", "Damaged Command Construct"]], 3u), // ANALYSIS: Eldan Maintenance
            [1559u] = new([["Ravenous Frizlet"]], 3u),                                                     // BIOLOGY: Frizlet Necropsy
            [1560u] = new([["Derelict Silo Squirg", "Derelict Silo Squirg Overseer"]], 3u),                // BIOLOGY: Derelict Squirg
            [1561u] = new([["Darktide Engulfer", "Darktide Obstructer", "Darktide Destructor", "Darktide Champion", "Darktide Dragoon", "Darktide Finripper",
                            "Darktide Enforcer", "Darktide Cryomancer", "Darktide Hydroprober", "Darktide Pincher", "Darktide Physicist", "Darktide Stifler"]], 3u), // BIOLOGY: Darktide Neural System
            [1562u] = new([["Plasmic Equalizer"]], 1u),                                                    // ANALYSIS: Plasmic Equalizer (checklist of 1)
            [1585u] = new([["Medusa Eggs"]], 3u),                                                          // BIOLOGY: Medusa Eggs
            [1586u] = new([["Glowing Foxfire"]], 15u),                                                     // ANALYSIS: Unnatural Technologies (checklist of 15)
            [2216u] = new([["Farside Fiera Flower"]], 3u),                                                 // BOTANY: Farside Fiera Flowers
            [2222u] = new([["Tanglevine Toys"]], 3u),                                                      // ANALYSIS: Tanglevine Toys
            [2226u] = new([["Timber Raven", "Domesticated Timber Raven"]], 3u),                            // BIOLOGY: Timber Raven
            [2246u] = new([["Aggressive Agave"]], 3u),                                                     // BOTANY: Aggressive Agave
            [2254u] = new([["Primal Vent"]], 1u),                                                          // CHEMISTRY: Primal Vents (one vent at the mission spot)
            [2255u] = new([["Primal Vent"]], 1u),                                                          // CHEMISTRY: Primal Vents (one vent at the mission spot)
            [2262u] = new([["Flamewalker Drum"]], 3u),                                                     // ANALYSIS: Flamewalker Drums
            [2292u] = new([["Tanglevine Shaman", "Tanglevine Bruiser", "Tanglevine Brawler", "Tanglevine Slasher", "Tanglevine Matriarch", "Tanglevine Pummeler",
                            "Tanglevine Menace", "Tanglevine Banshee", "Adolescent Tanglevine"]], 3u),     // BIOLOGY: Tanglevine Skeech
            // Wilderrun
            [1567u] = new([["Swarming Splorg", "Wilderrun Splorg", "Scampering Splorg"]], 3u), // BIOLOGY: Splorg Toxins
            [1576u] = new([["Eldan Hologram", "Eldan Focal Monitor", "Eldan Holoprojector", "Eldan Augmentor"]], 3u), // ANALYSIS: The Truth is Somewhere (checklist of 3)
            [1628u] = new([["Primal Life Essence"]], 5u), // ANALYSIS: Elemental Life Spring (checklist of 5)
            [1632u] = new([["Undergrove Sporeling", "Undergrove Grimspore", "Undergrove Spore Herder", "Undergrove Direspore", "Corrupted Rootbrute"]], 3u), // BOTANY: Undergrove Rootbrutes
            [1663u] = new([["Osun Chain", "Osun Weapon", "Ruined Osun Banner"]], 5u), // ARCHAEOLOGY: The Fall of the Osun (checklist of 5)
            [1665u] = new([["Eldan Information Construct", "Eldan Hologram", "Eldan Augmentor", "Eldan Focal Monitor", "Eldan Holoprojector"]], 3u), // ANALYSIS: Secrets of the Jungle (checklist of 3)
            [1666u] = new([["Aerophyll Bulb", "Aerophyll Drifter"]], 3u), // BOTANY: Aerophyll Bulbs
            [2247u] = new([["Strain Corruptor", "Greater Strain Corruptor"]], 3u), // BIOLOGY: Strain Corruptors
            [2249u] = new([["Corrupted Sporeling", "Corrupted Padfoot", "Corrupted Sister", "Corrupted Flora"]], 3u), // BIOLOGY: Corrupted Wilderness
            [2289u] = new([["Megadroid Interface", "Megadroid Head", "Megadroid Leg Circuitry", "Megadroid Arm Circuitry", "Megadroid Hand Circuitry", "Megadroid Chest Circuitry"]], 5u), // ANALYSIS: Megadroid Remnants (checklist of 5)
            [2329u] = new([["Kel Ulgar Forge", "Kel Ulgar Weapon Rack", "Osun Weapon"]], 3u), // ANALYSIS: Osun Forgemastery
            [2340u] = new([["Spirevale Scorcher", "Spirevale Firenova"]], 3u), // BIOLOGY: Elemental Fallout
            [2350u] = new([["Statue of Korol the Devout", "Torine Statue", "Ritual Statue"]], 4u), // ARCHAEOLOGY: Eldan Surveillance (checklist of 4)
            [3060u] = new([["Tanglecrash Slashtail", "Tanglecrash Clipclaw", "Tanglecrash Fastclaw", "Tanglecrash Curltail"]], 3u), // BIOLOGY: The Servants of Toria
            [3067u] = new([["Scattered Wreckage"]], 5u), // ANALYSIS: Wisdom from the Wreckage (checklist of 5)
            // Malgrave (the sand disturbances of 2060 have no clear creature, no rule)
            [1937u] = new([["Robo-Cultist Acolyte", "Robo-Cultist Initiate"]], 3u), // ANALYSIS: Occult Observation
            [1941u] = new([["Dreg Hoversail", "Flammable Dreg Belongings", "Dreg Corpse", "Dreg Brute Corpse"]], 5u), // ANALYSIS: Dregging Up the Past (checklist of 5)
            [1942u] = new([["Massive Skull"]], 10u), // BIOLOGY: Cataloguing the Massive Ones (checklist of 10)
            [1946u] = new([["Spectral Brigand", "Spectral Raider", "Spectral Gunslinger", "Spectral Crackshot", "Spectral Outlaw"]], 3u), // ANALYSIS: We're Going Full Stream
            [1947u] = new([["Blooming Hellrose"]], 3u), // BIOLOGY: Flaming Flora
            [1950u] = new([["Eldan Defense Construct", "Eldan Security Construct", "Cooling Construct", "Sandstone Constructor"]], 3u), // ANALYSIS: Forgotten Science
            [1952u] = new([["Land Mine"]], 3u), // DIAGNOSTICS: Proper Explosive Removal
            [1953u] = new([["Disabled Probe"]], 3u), // DIAGNOSTICS: Back Online
            [2044u] = new([["Skittering Flayer"]], 3u), // BIOLOGY: Skittering Spiders
            [2065u] = new([["Angry Regurgitated Buzzbing"]], 3u), // BIOLOGY: Buzz Off
            [2067u] = new([["Eldan Artifacts", "Eldan Relay"]], 5u), // ANALYSIS: Eldan Remnants (checklist of 5)
            [2068u] = new([["Pure Loftite Crystal"]], 3u), // CHEMISTRY: Loftite Resonance
            [2087u] = new([["Protostar Secure-A-Lock 3000"]], 3u), // ANALYSIS: Safecracking
            [2088u] = new([["Dust Devil"]], 3u), // ANALYSIS: Hyperlocal Weather Systems
            // Grimvault (the Cankertube creature of 3176 and the Strain Maw 3254 have no clear target, no rule)
            [2211u] = new([["Corrupted Warden", "Corrupted Protector", "Corrupted Hatchling", "Corrupted Probe", "Corrupted Stemdragon", "Corrupted Watcher"]], 3u), // BIOLOGY: Corrupted Coterie
            [2391u] = new([["Corrupted Warden", "Corrupted Protector", "Corrupted Hatchling", "Corrupted Probe", "Corrupted Stemdragon", "Corrupted Watcher"]], 3u), // BIOLOGY: Corrupted Coterie
            [2212u] = new([["Mk. III Cryo Pod"]], 3u), // DIAGNOSTICS: Safety Recall
            [2404u] = new([["Mk. III Cryo Pod"]], 3u), // DIAGNOSTICS: Safety Recall
            [2215u] = new([["Oozing Sludge", "Oozing Horror"]], 3u), // BIOLOGY: Oozing Terrors
            [2392u] = new([["Oozing Horror"]], 3u), // BIOLOGY: Oozing Horror
            [2466u] = new([["Exanite Coated Plant"]], 3u), // CHEMISTRY: Exanite Interactions
            [2468u] = new([["Exanite Coated Plant"]], 3u), // CHEMISTRY: Exanite Interactions
            [2590u] = new([["Energy Worm"]], 3u), // BIOLOGY: Logical Constructs
            [3122u] = new([["Eldan Console", "Cage Override Console", "System Core Console", "Genetic Archives Access Panel"]], 4u), // DIAGNOSTICS: Datacube on Lockdown (checklist of 4)
            [3137u] = new([["Enthralled Exterminator", "Repair Construct", "Eldan Construct Fail-Safe"]], 3u), // ANALYSIS: Long-Term Effects
            [3178u] = new([["Malleable Exanite", "Exanite Shard"]], 12u), // CHEMISTRY: Exanite Extraction (checklist of 12)
            [3227u] = new([["Malleable Exanite", "Exanite Shard"]], 12u), // CHEMISTRY: Exanite Extraction (checklist of 12)
            [3218u] = new([["Ship Debris"]], 5u), // ANALYSIS: The X-Periment (checklist of 5)
            [3225u] = new([["Ship Debris"]], 5u), // ANALYSIS: Big Baby (checklist of 5)
            [3242u] = new([["Eldan Control Panel", "Corrupted Generator"]], 7u), // ANALYSIS: Power of the Resonator (checklist of 7)
            [3269u] = new([["Eldan Information Construct"]], 3u), // DIAGNOSTICS: Sentient Creations (checklist of 3)
        };

        /// <summary>
        /// Datacube creatures by datacube discovery mission, the DATACUBE creatures spawned in the mission's zone.
        /// </summary>
        public static IReadOnlyDictionary<uint, uint[]> Datacubes { get; } = new Dictionary<uint, uint[]>
        {
            [1877u] = [24858u, 26379u, 26380u, 26381u, 26382u, 26383u, 26384u, 32729u, 32753u, 32785u, 32848u, 32997u, 40185u, 40186u, 40187u, 40188u, 40189u], // Deradune
            [1936u] = [57151u, 57152u, 57154u, 57155u, 57156u, 57157u, 57158u, 57159u, 57160u, 57161u, 57162u, 57163u, 57164u, 58303u], // Malgrave
            [2136u] = [21894u, 21896u, 21897u, 21898u, 21899u, 21900u, 21901u, 21902u, 21903u, 21904u, 21905u, 21906u, 21907u], // Algoroc
            [2167u] = [19672u, 20907u, 21750u, 22109u, 22136u, 22141u, 22142u, 22143u, 22144u, 22145u, 22146u, 22147u, 22148u, 22149u, 22150u, 22151u, 22152u, 22153u, 22154u, 22155u, 37629u, 54204u], // Galeras
            [2244u] = [37629u, 39475u, 39477u, 39478u, 39479u, 39480u, 39481u, 39517u, 39518u, 56824u, 56825u, 56833u, 56834u, 56835u, 56836u, 56837u], // Wilderrun
            [2375u] = [25461u, 25462u, 25463u, 25464u, 25465u, 25466u, 25467u, 25468u, 25469u, 25470u, 25471u, 25472u, 25473u, 25474u, 25475u, 25476u, 25477u, 25478u, 25479u, 25480u, 48660u], // Auroria
            [2421u] = [37629u, 40696u, 40697u, 40698u], // Illium
            [2470u] = [37629u, 48599u, 48602u, 48603u, 48604u, 48605u, 48606u, 48607u, 52721u, 52723u, 52724u, 52725u, 52727u, 56766u, 56767u, 56768u, 56769u, 56770u, 56771u, 56772u, 65268u, 70011u, 70012u], // Southern Grimvault
            [2525u] = [37629u, 48599u, 48602u, 48603u, 48604u, 48605u, 48606u, 48607u, 52721u, 52723u, 52724u, 52725u, 52727u, 56766u, 56767u, 56768u, 56769u, 56770u, 56771u, 56772u, 65268u, 70011u, 70012u], // Western Grimvault
            [3103u] = [21694u, 21695u, 21696u, 21697u, 21698u, 21699u, 21700u, 21701u, 21702u, 21703u, 23257u, 23258u, 25842u, 25843u, 25844u, 25845u, 25846u, 25847u, 25848u, 25849u, 25850u, 34887u, 34888u, 37629u], // Whitevale
            [3104u] = [30418u, 30419u, 30420u, 30421u, 30422u, 30423u, 30424u, 30425u, 30426u, 30427u, 30428u, 30429u, 30430u, 30431u, 30432u, 34503u], // Celestion
            [3105u] = [30342u, 30353u, 30354u, 30355u, 30357u, 30358u, 30361u, 30363u, 30364u, 30365u, 30367u, 30368u, 30370u, 30371u], // Ellevar
            [3189u] = [54226u, 54227u, 54228u], // Thayd
            [3212u] = [37629u, 48599u, 48602u, 48603u, 48604u, 48605u, 48606u, 48607u, 52721u, 52723u, 52724u, 52725u, 52727u, 56766u, 56767u, 56768u, 56769u, 56770u, 56771u, 56772u, 65268u, 70011u, 70012u], // Blighthaven
            [3268u] = [37629u, 48599u, 48602u, 48603u, 48604u, 48605u, 48606u, 48607u, 52721u, 52723u, 52724u, 52725u, 52727u, 56766u, 56767u, 56768u, 56769u, 56770u, 56771u, 56772u, 65268u, 70011u, 70012u], // The Defile
        };
    }
}
