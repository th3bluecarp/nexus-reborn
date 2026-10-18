-- Path mission objects missing from the world database (scan, diagnostics and similar targets), hand placed.
-- Positions from archived Jabbithole pins where they exist, otherwise around the mission's own map marker; heights from the server map files.
-- Remove with: DELETE FROM entity WHERE Id BETWEEN 1080000 AND 1089999;
DELETE FROM `entity_stats` WHERE `Id` BETWEEN 1080000 AND 1089999;
DELETE FROM `entity` WHERE `Id` BETWEEN 1080000 AND 1089999;
-- Sparking Wire (M3156 DIAGNOSTICS: Power Outage, archived pins)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080001,0,53027,51,1002,4646.0,-795.000,-2688.0,0,0,0,24324,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080001,10,1),(1080001,15,0);
-- Sparking Wire (M3156 DIAGNOSTICS: Power Outage, archived pins)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080002,0,53027,51,1020,4715.0,-752.000,-2845.0,0,0,0,24324,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080002,10,1),(1080002,15,0);
-- Sparking Wire (M3156 DIAGNOSTICS: Power Outage, archived pins)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080003,0,53027,51,1020,4706.0,-756.000,-2812.0,0,0,0,24324,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080003,10,1),(1080003,15,0);
-- Sparking Wire (M3156 DIAGNOSTICS: Power Outage, archived pins)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080004,0,53027,51,1020,4683.0,-746.750,-2828.0,0,0,0,24324,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080004,10,1),(1080004,15,0);
-- Sparking Wire (M3156 DIAGNOSTICS: Power Outage, archived pins)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080005,0,53027,51,1020,4688.0,-774.000,-2765.0,0,0,0,24324,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080005,10,1),(1080005,15,0);
-- Sparking Wire (M3156 DIAGNOSTICS: Power Outage, archived pins)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080006,0,53027,51,1002,4671.0,-785.000,-2740.0,0,0,0,24324,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080006,10,1),(1080006,15,0);
-- Sparking Wire (M3156 DIAGNOSTICS: Power Outage, archived pins)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080007,0,53027,51,1020,4688.0,-750.750,-2815.0,0,0,0,24324,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080007,10,1),(1080007,15,0);
-- Sparking Wire (M3156 DIAGNOSTICS: Power Outage, archived pins)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080008,0,53027,51,1020,4711.0,-754.750,-2830.0,0,0,0,24324,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080008,10,1),(1080008,15,0);
-- Crimson Legion Uplink Station (M712 ANALYSIS: Stolen Science, around the mission marker, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080009,0,19637,51,1014,5204.9,-893.500,-3159.4,0,0,0,24059,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080009,10,1),(1080009,15,0);
-- Crimson Legion Uplink Station (M712 ANALYSIS: Stolen Science, around the mission marker, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080010,0,19637,51,1014,5217.8,-893.500,-3133.6,0,0,0,24059,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080010,10,1),(1080010,15,0);
-- Crimson Legion Uplink Station (M712 ANALYSIS: Stolen Science, around the mission marker, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080011,0,19637,51,1014,5169.9,-893.500,-3146.0,0,0,0,24059,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080011,10,1),(1080011,15,0);
-- Crimson Legion Uplink Station (M712 ANALYSIS: Stolen Science, around the mission marker, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080012,0,19637,51,1014,5166.7,-893.500,-3154.7,0,0,0,24059,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080012,10,1),(1080012,15,0);
-- Buzzbing Remains (M707 BIOLOGY: Buzzing Bodies, around the mission marker, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080013,0,14845,51,1054,5213.8,-898.000,-1878.6,0,0,0,21780,0,0,0);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080013,10,1),(1080013,15,0);
-- Buzzbing Remains (M707 BIOLOGY: Buzzing Bodies, around the mission marker, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080014,0,14845,51,1054,5220.9,-896.275,-1902.4,0,0,0,21780,0,0,0);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080014,10,1),(1080014,15,0);
-- Buzzbing Remains (M707 BIOLOGY: Buzzing Bodies, around the mission marker, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080015,0,14846,51,1054,5184.8,-902.100,-1919.6,0,0,0,21780,0,0,0);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080015,10,1),(1080015,15,0);
-- Buzzbing Remains (M707 BIOLOGY: Buzzing Bodies, around the mission marker, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080016,0,14846,51,1054,5188.8,-901.300,-1897.4,0,0,0,21780,0,0,0);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080016,10,1),(1080016,15,0);
-- Hoogle Statue (M3184 ANALYSIS: Hoogle Stones, Arborian Gardens in Thayd, around the mission marker, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080017,0,53582,51,2545,3610.6,-838.498,-1943.0,0,0,0,24690,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080017,10,1),(1080017,15,0);
-- Hoogle Statue (M3184 ANALYSIS: Hoogle Stones, Arborian Gardens in Thayd, around the mission marker, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080018,0,54358,51,2545,3572.5,-839.31,-1942.2,0,0,0,26741,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080018,10,1),(1080018,15,0);
-- Hoogle Statue (M3184 ANALYSIS: Hoogle Stones, Arborian Gardens in Thayd, around the mission marker, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080019,0,54359,51,2545,3591.1,-839.269,-1975.5,0,0,0,27786,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080019,10,1),(1080019,15,0);
