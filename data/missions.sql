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
