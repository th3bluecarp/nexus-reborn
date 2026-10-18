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
-- Unusual Asteroid (M1535 ARCHAEOLOGY: Strange Rocks, in the Tract 2 crater of the Loftite crystals, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080020,0,28759,1421,1505,3481.5,-734.500,-5769.6,0,0,0,22472,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080020,10,1),(1080020,15,0);
-- Unusual Asteroid (M1535 ARCHAEOLOGY: Strange Rocks, in the Tract 2 crater of the Loftite crystals, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080021,0,28759,1421,1476,3435.0,-723.000,-5736.1,0,0,0,22472,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080021,10,1),(1080021,15,0);
-- Unusual Asteroid (M1535 ARCHAEOLOGY: Strange Rocks, in the Tract 2 crater of the Loftite crystals, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080022,0,28759,1421,1476,3413.4,-723.000,-5767.8,0,0,0,22472,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080022,10,1),(1080022,15,0);
-- Unusual Asteroid (M1535 ARCHAEOLOGY: Strange Rocks, in the Tract 2 crater of the Loftite crystals, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080023,0,28759,1421,1505,3427.6,-734.500,-5796.1,0,0,0,22472,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080023,10,1),(1080023,15,0);
-- Unusual Asteroid (M1535 ARCHAEOLOGY: Strange Rocks, in the Tract 2 crater of the Loftite crystals, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080024,0,28759,1421,1505,3471.7,-754.350,-5800.4,0,0,0,22472,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080024,10,1),(1080024,15,0);
-- Medusa Eggs (M1585 BIOLOGY: Medusa Eggs, around the mission marker, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080025,0,29030,1421,4944,3766.1,-718.775,-5873.0,0,0,0,26447,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080025,10,29),(1080025,15,0);
-- Medusa Eggs (M1585 BIOLOGY: Medusa Eggs, around the mission marker, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080026,0,29030,1421,4944,3721.0,-719.500,-5879.4,0,0,0,26447,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080026,10,29),(1080026,15,0);
-- Medusa Eggs (M1585 BIOLOGY: Medusa Eggs, around the mission marker, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080027,0,29030,1421,4944,3733.2,-721.500,-5912.0,0,0,0,26447,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080027,10,29),(1080027,15,0);
-- Glowing Foxfire (M1586 ANALYSIS: Unnatural Technologies, spread around the mission marker, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080028,0,29040,1421,4944,3821.6,-718.000,-5861.2,0,0,0,27962,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080028,10,1),(1080028,15,0);
-- Glowing Foxfire (M1586 ANALYSIS: Unnatural Technologies, spread around the mission marker, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080029,0,29040,1421,4944,3805.9,-718.000,-5831.7,0,0,0,27962,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080029,10,1),(1080029,15,0);
-- Glowing Foxfire (M1586 ANALYSIS: Unnatural Technologies, spread around the mission marker, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080030,0,29040,1421,4944,3802.1,-718.000,-5787.5,0,0,0,27962,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080030,10,1),(1080030,15,0);
-- Glowing Foxfire (M1586 ANALYSIS: Unnatural Technologies, spread around the mission marker, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080031,0,29040,1421,4944,3770.8,-717.000,-5747.4,0,0,0,27962,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080031,10,1),(1080031,15,0);
-- Glowing Foxfire (M1586 ANALYSIS: Unnatural Technologies, spread around the mission marker, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080032,0,29040,1421,4944,3687.0,-720.025,-5838.1,0,0,0,27962,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080032,10,1),(1080032,15,0);
-- Glowing Foxfire (M1586 ANALYSIS: Unnatural Technologies, spread around the mission marker, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080033,0,29040,1421,4944,3670.9,-734.000,-5808.3,0,0,0,27962,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080033,10,1),(1080033,15,0);
-- Glowing Foxfire (M1586 ANALYSIS: Unnatural Technologies, spread around the mission marker, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080034,0,29040,1421,1476,3624.5,-734.000,-5820.9,0,0,0,27962,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080034,10,1),(1080034,15,0);
-- Glowing Foxfire (M1586 ANALYSIS: Unnatural Technologies, spread around the mission marker, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080035,0,29040,1421,1476,3617.0,-725.300,-5884.2,0,0,0,27962,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080035,10,1),(1080035,15,0);
-- Glowing Foxfire (M1586 ANALYSIS: Unnatural Technologies, spread around the mission marker, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080036,0,29040,1421,1476,3609.9,-735.000,-5923.9,0,0,0,27962,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080036,10,1),(1080036,15,0);
-- Glowing Foxfire (M1586 ANALYSIS: Unnatural Technologies, spread around the mission marker, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080037,0,29040,1421,1476,3668.5,-734.351,-5992.2,0,0,0,27962,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080037,10,1),(1080037,15,0);
-- Glowing Foxfire (M1586 ANALYSIS: Unnatural Technologies, spread around the mission marker, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080038,0,29040,1421,1476,3733.8,-731.225,-6006.7,0,0,0,27962,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080038,10,1),(1080038,15,0);
-- Glowing Foxfire (M1586 ANALYSIS: Unnatural Technologies, spread around the mission marker, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080039,0,29040,1421,1476,3791.6,-731.299,-6033.4,0,0,0,27962,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080039,10,1),(1080039,15,0);
-- Glowing Foxfire (M1586 ANALYSIS: Unnatural Technologies, spread around the mission marker, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080040,0,29040,1421,1476,3844.0,-733.900,-6005.6,0,0,0,27962,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080040,10,1),(1080040,15,0);
-- Glowing Foxfire (M1586 ANALYSIS: Unnatural Technologies, spread around the mission marker, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080041,0,29040,1421,1476,3857.2,-723.500,-5968.4,0,0,0,27962,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080041,10,1),(1080041,15,0);
-- Glowing Foxfire (M1586 ANALYSIS: Unnatural Technologies, spread around the mission marker, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080042,0,29040,1421,1476,3888.1,-725.525,-5923.8,0,0,0,27962,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080042,10,1),(1080042,15,0);
-- Farside Fiera Flower (M2216 BOTANY: Farside Fiera Flowers, around the mission marker, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080043,0,39200,1421,2034,1994.2,-918.000,-1355.5,0,0,0,24503,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080043,10,1),(1080043,15,0);
-- Farside Fiera Flower (M2216 BOTANY: Farside Fiera Flowers, around the mission marker, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080044,0,39200,1421,2034,1965.2,-921.000,-1343.8,0,0,0,24503,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080044,10,1),(1080044,15,0);
-- Farside Fiera Flower (M2216 BOTANY: Farside Fiera Flowers, around the mission marker, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080045,0,39200,1421,2034,1924.6,-925.025,-1373.9,0,0,0,24503,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080045,10,1),(1080045,15,0);
-- Farside Fiera Flower (M2216 BOTANY: Farside Fiera Flowers, around the mission marker, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080046,0,39200,1421,2034,1979.8,-917.950,-1392.4,0,0,0,24503,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080046,10,1),(1080046,15,0);
-- Tanglevine Toys (M2222 ANALYSIS: Tanglevine Toys, around the mission marker, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080047,0,39217,1421,2037,1179.3,-950.650,-1335.0,0,0,0,28060,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080047,10,1),(1080047,15,0);
-- Tanglevine Toys (M2222 ANALYSIS: Tanglevine Toys, around the mission marker, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080048,0,39217,1421,2037,1150.1,-919.275,-1295.1,0,0,0,28060,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080048,10,1),(1080048,15,0);
-- Tanglevine Toys (M2222 ANALYSIS: Tanglevine Toys, around the mission marker, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080049,0,39217,1421,2037,1127.2,-917.000,-1344.9,0,0,0,28060,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080049,10,1),(1080049,15,0);
-- Tanglevine Toys (M2222 ANALYSIS: Tanglevine Toys, around the mission marker, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080050,0,39217,1421,2037,1172.6,-958.300,-1359.2,0,0,0,28060,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080050,10,1),(1080050,15,0);
-- Aggressive Agave (M2246 BOTANY: Aggressive Agave, around the mission marker in the bio-dome, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080051,0,39326,1421,2029,4094.5,-936.700,-1509.3,0,0,0,26399,0,218,218);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080051,10,30),(1080051,15,0);
-- Aggressive Agave (M2246 BOTANY: Aggressive Agave, around the mission marker in the bio-dome, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080052,0,39326,1421,2029,4059.7,-941.075,-1499.2,0,0,0,26399,0,218,218);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080052,10,30),(1080052,15,0);
-- Aggressive Agave (M2246 BOTANY: Aggressive Agave, around the mission marker in the bio-dome, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080053,0,39326,1421,2029,4051.1,-948.024,-1540.8,0,0,0,26399,0,218,218);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080053,10,30),(1080053,15,0);
-- Aggressive Agave (M2246 BOTANY: Aggressive Agave, around the mission marker in the bio-dome, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080054,0,39326,1421,2029,4077.7,-939.200,-1545.5,0,0,0,26399,0,218,218);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080054,10,30),(1080054,15,0);
-- Flamewalker Drum (M2262 ANALYSIS: Flamewalker Drums, around the mission marker, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080055,0,39380,1421,2032,4786.6,-983.150,-1324.6,0,0,0,26703,0,988,988);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080055,10,1),(1080055,15,0);
-- Flamewalker Drum (M2262 ANALYSIS: Flamewalker Drums, around the mission marker, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080056,0,39380,1421,2032,4755.4,-981.000,-1303.8,0,0,0,26703,0,988,988);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080056,10,1),(1080056,15,0);
-- Flamewalker Drum (M2262 ANALYSIS: Flamewalker Drums, around the mission marker, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080057,0,39380,1421,2032,4746.2,-978.800,-1355.0,0,0,0,26703,0,988,988);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080057,10,1),(1080057,15,0);
-- Flamewalker Drum (M2262 ANALYSIS: Flamewalker Drums, around the mission marker, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080058,0,39380,1421,2032,4792.4,-987.199,-1375.4,0,0,0,26703,0,988,988);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080058,10,1),(1080058,15,0);
-- Derelict Silo Maintenance Construct (M1558 ANALYSIS: Eldan Maintenance, a third construct next to the spawned two, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080059,0,27773,1421,1430,5583.0,-459.250,-11525.0,0,0,0,23881,0,233,233);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080059,10,36),(1080059,15,0);
-- Farside Scavenger (M1534 BIOLOGY: Alien Inhabitants, a third hookfoot next to the spawned two, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080060,0,28211,1421,4944,3792.0,-718.225,-5838.0,0,0,0,23148,0,1084,1084);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080060,10,31),(1080060,15,0);
-- Eldan Research Samples (Q6152/Q6456 Secondhand Research, at the Derelict Silo objective marker like its other targets, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080061,0,27947,1421,1430,5574,-459.25,-11526,0,0,0,24340,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080061,10,1),(1080061,15,0);
-- Eldan Research Samples (Q6152/Q6456 Secondhand Research, at the Derelict Silo objective marker like its other targets, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080062,0,27947,1421,1430,5580,-459.25,-11526,0,0,0,24340,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080062,10,1),(1080062,15,0);
-- Eldan Research Samples (Q6152/Q6456 Secondhand Research, at the Derelict Silo objective marker like its other targets, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080063,0,27947,1421,1430,5574,-459.25,-11532,0,0,0,24340,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080063,10,1),(1080063,15,0);
-- Eldan Research Samples (Q6152/Q6456 Secondhand Research, at the Derelict Silo objective marker like its other targets, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080064,0,27947,1421,1430,5580,-459.25,-11532,0,0,0,24340,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080064,10,1),(1080064,15,0);
-- Exo-Lab 13 defenders and records (Q7748/Q7749, around the Exo-Lab 13 location link, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080065,0,38881,1421,2026,4538,-1384,-1473,0,0,0,31123,0,233,233);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080065,10,31),(1080065,15,0);
-- Exo-Lab 13 defenders and records (Q7748/Q7749, around the Exo-Lab 13 location link, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080066,0,38881,1421,2026,4546,-1384,-1481,0,0,0,31123,0,233,233);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080066,10,31),(1080066,15,0);
-- Exo-Lab 13 defenders and records (Q7748/Q7749, around the Exo-Lab 13 location link, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080067,0,38882,1421,2026,4546,-1384,-1473,0,0,0,31094,0,233,233);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080067,10,32),(1080067,15,0);
-- Exo-Lab 13 defenders and records (Q7748/Q7749, around the Exo-Lab 13 location link, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080068,0,38882,1421,2026,4538,-1384,-1481,0,0,0,31094,0,233,233);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080068,10,32),(1080068,15,0);
-- Exo-Lab 13 defenders and records (Q7748/Q7749, around the Exo-Lab 13 location link, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080069,0,50148,1421,2026,4550,-1384,-1477,0,0,0,31123,0,233,233);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080069,10,31),(1080069,15,0);
-- Exo-Lab 13 defenders and records (Q7748/Q7749, around the Exo-Lab 13 location link, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080070,0,38884,1421,2026,4542,-1384,-1471,0,0,0,23102,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080070,10,1),(1080070,15,0);
-- Matrix Defense Console D (Q7997 Face the Custodian, beside the Bio-Dome 3 Custodian, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080071,0,45365,1421,2035,1310,-935.625,-1190,0,0,0,23102,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080071,10,1),(1080071,15,0);
-- Keyuri's Kurg (Q8778 Buried Shinies, at the Keyuri Catacomb location link, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080072,0,45662,1421,2026,3948,-947,-1567,0,0,0,21763,0,416,416);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080072,10,30),(1080072,15,0);
-- The Alternator (Q8173 Solid Gold Gangster, at the Lady Lugwrench location link, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080073,0,42437,51,863,2467,-898,3431,0,0,0,23464,0,233,233);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080073,10,35),(1080073,15,0);
-- Propaganda Control Station (Q8173, beside The Alternator, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080074,0,42439,51,863,2472,-898,3431,0,0,0,28514,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080074,10,1),(1080074,15,0);
-- Enter the Transport Ship (Q8173 exit, beside The Alternator, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080075,0,42433,51,863,2462,-898,3431,0,0,0,28170,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080075,10,1),(1080075,15,0);
-- Thought-Speaker (Q8177 Chompacabra Champ turn-in, beside the cage area marker, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080076,0,42465,51,863,2634,-952,3062,0,0,0,21321,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080076,10,36),(1080076,15,0);
-- Mindbreak Disciple (Q8217 Back to the Village, at the Integral Function location link, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080077,0,42477,51,2251,-40.0,-860,1835.0,0,0,0,22432,0,1140,1140);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080077,10,37),(1080077,15,0);
-- Mindbreak Disciple (Q8217 Back to the Village, at the Integral Function location link, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080078,0,42477,51,2251,-44.0,-860,1839.0,0,0,0,22432,0,1140,1140);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080078,10,37),(1080078,15,0);
-- Mindbreak Disciple (Q8217 Back to the Village, at the Integral Function location link, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080079,0,42477,51,2251,-48.0,-860,1835.0,0,0,0,22432,0,1140,1140);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080079,10,37),(1080079,15,0);
-- Mindbreak Disciple (Q8217 Back to the Village, at the Integral Function location link, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080080,0,42477,51,2251,-44.0,-860,1831.0,0,0,0,22432,0,1140,1140);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080080,10,37),(1080080,15,0);
-- Corruption Source (Q8218 Structural Integrity, around the Integral Function location link, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080081,0,42509,51,2251,-35.0,-860,1835.0,0,0,0,24324,0,0,0);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080081,10,1),(1080081,15,0);
-- Corruption Source (Q8218 Structural Integrity, around the Integral Function location link, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080082,0,42509,51,2251,-44.0,-860,1844.0,0,0,0,24324,0,0,0);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080082,10,1),(1080082,15,0);
-- Corruption Source (Q8218 Structural Integrity, around the Integral Function location link, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080083,0,42509,51,2251,-53.0,-860,1835.0,0,0,0,24324,0,0,0);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080083,10,1),(1080083,15,0);
-- Corruption Source (Q8218 Structural Integrity, around the Integral Function location link, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080084,0,42509,51,2251,-44.0,-860,1826.0,0,0,0,24324,0,0,0);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080084,10,1),(1080084,15,0);
-- Pitchsoul Queen (Q8219 Unexpected Royalty, at the Integral Function location link, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080085,0,41527,51,2251,-44,-860,1847,0,0,0,21787,0,233,233);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080085,10,37),(1080085,15,0);
-- Hexcomb hive buzzbings (Q8435 Buzzkill, at the Hexcomb Hive location link, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080086,0,41578,51,4192,2886,-929,2218,0,0,0,24713,0,257,257);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080086,10,35),(1080086,15,0);
-- Hexcomb hive buzzbings (Q8435 Buzzkill, at the Hexcomb Hive location link, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080087,0,41587,51,4192,2894,-929,2218,0,0,0,24713,0,257,257);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080087,10,35),(1080087,15,0);
-- Hexcomb hive buzzbings (Q8435 Buzzkill, at the Hexcomb Hive location link, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080088,0,41589,51,4192,2890,-929,2214,0,0,0,21473,0,257,257);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080088,10,35),(1080088,15,0);
-- Hexcomb hive buzzbings (Q8435 Buzzkill, at the Hexcomb Hive location link, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080089,0,41589,51,4192,2890,-929,2222,0,0,0,21473,0,257,257);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080089,10,35),(1080089,15,0);
-- Banner Placement (Q8438 Technological Advancement, chambers of the Hexcomb Hive Cavern, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080090,0,42261,51,4192,3277,-956,2023,0,0,0,27794,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080090,10,1),(1080090,15,0);
-- Banner Placement (Q8438 Technological Advancement, chambers of the Hexcomb Hive Cavern, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080091,0,42265,51,4192,3265,-956,2010,0,0,0,27794,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080091,10,1),(1080091,15,0);
-- Hexcomb Augmentor (Q8439 The Source of the Problem, at the Hexcomb Hive Cavern location link, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080092,0,41753,51,4192,3282.0,-956,2023.0,0,0,0,23868,0,233,233);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080092,10,35),(1080092,15,0);
-- Hexcomb Augmentor (Q8439 The Source of the Problem, at the Hexcomb Hive Cavern location link, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080093,0,41753,51,4192,3277.0,-956,2028.0,0,0,0,23868,0,233,233);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080093,10,35),(1080093,15,0);
-- Hexcomb Augmentor (Q8439 The Source of the Problem, at the Hexcomb Hive Cavern location link, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080094,0,41753,51,4192,3272.0,-956,2023.0,0,0,0,23868,0,233,233);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080094,10,35),(1080094,15,0);
-- Hexcomb Augmentor (Q8439 The Source of the Problem, at the Hexcomb Hive Cavern location link, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080095,0,41753,51,4192,3277.0,-956,2018.0,0,0,0,23868,0,233,233);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080095,10,35),(1080095,15,0);
-- Geometric Mapper Unit ONE (Q8440 Cave Cartography, between mapper two and the cavern link, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080096,0,42419,51,4192,3200,-956,1980,0,0,0,22904,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080096,10,1),(1080096,15,0);
-- Power Generator (Q8607 Order of Snowflakes, around Pattern-Seeker's Recluse, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080097,0,43327,51,1337,2226.0,-951,2533.0,0,0,0,26172,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080097,10,1),(1080097,15,0);
-- Power Generator (Q8607 Order of Snowflakes, around Pattern-Seeker's Recluse, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080098,0,43327,51,1337,2217.7,-951,2544.4,0,0,0,26172,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080098,10,1),(1080098,15,0);
-- Power Generator (Q8607 Order of Snowflakes, around Pattern-Seeker's Recluse, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080099,0,43327,51,1337,2204.3,-951,2540.1,0,0,0,26172,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080099,10,1),(1080099,15,0);
-- Power Generator (Q8607 Order of Snowflakes, around Pattern-Seeker's Recluse, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080100,0,43327,51,1337,2204.3,-951,2525.9,0,0,0,26172,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080100,10,1),(1080100,15,0);
-- Power Generator (Q8607 Order of Snowflakes, around Pattern-Seeker's Recluse, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080101,0,43327,51,1337,2217.7,-951,2521.6,0,0,0,26172,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080101,10,1),(1080101,15,0);
-- Exo-Lab XC42 constructs (Q7384 Defenders of the Cube, at the Exo-Lab XC42 location link, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080102,0,35924,1061,1948,2226,-868,3768,0,0,0,30426,0,1115,1115);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080102,10,46),(1080102,15,0);
-- Exo-Lab XC42 constructs (Q7384 Defenders of the Cube, at the Exo-Lab XC42 location link, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080103,0,35925,1061,1948,2234,-868,3768,0,0,0,30426,0,233,233);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080103,10,46),(1080103,15,0);
-- Exo-Lab XC42 constructs (Q7384 Defenders of the Cube, at the Exo-Lab XC42 location link, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080104,0,35926,1061,1948,2230,-868,3764,0,0,0,30425,0,233,233);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080104,10,45),(1080104,15,0);
-- Exo-Lab XC42 constructs (Q7384 Defenders of the Cube, at the Exo-Lab XC42 location link, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080105,0,35927,1061,1948,2230,-868,3772,0,0,0,30425,0,233,233);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080105,10,45),(1080105,15,0);
-- Exo-Lab XC42 constructs (Q7384 Defenders of the Cube, at the Exo-Lab XC42 location link, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080106,0,35928,1061,1948,2236,-868,3774,0,0,0,30421,0,233,233);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080106,10,46),(1080106,15,0);
-- Defiled Console (Q8356 Breaching the Core, around the Techno-Core location link, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080107,0,42552,1061,2841,-1018.0,-1200,-4096.0,0,0,0,28550,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080107,10,1),(1080107,15,0);
-- Defiled Console (Q8356 Breaching the Core, around the Techno-Core location link, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080108,0,42552,1061,2841,-1024.0,-1200,-4090.0,0,0,0,28550,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080108,10,1),(1080108,15,0);
-- Defiled Console (Q8356 Breaching the Core, around the Techno-Core location link, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080109,0,42552,1061,2841,-1030.0,-1200,-4096.0,0,0,0,28550,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080109,10,1),(1080109,15,0);
-- Defiled Console (Q8356 Breaching the Core, around the Techno-Core location link, guessed)
INSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES (1080110,0,42552,1061,2841,-1024.0,-1200,-4102.0,0,0,0,28550,0,219,219);
INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES (1080110,10,1),(1080110,15,0);
