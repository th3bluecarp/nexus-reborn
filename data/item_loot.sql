-- Contents of loot bag items whose server loot tables are lost (daily login reward boxes). Loot group ids 2000000+.
-- The contents are invented: retail's are not in the client data or any archive found (see docs/reborn/SEMANTICS.md).
DELETE FROM `item_loot` WHERE `lootGroupId` >= 2000000;
DELETE FROM `loot_item` WHERE `id` >= 2000000;
DELETE FROM `loot_group` WHERE `id` >= 2000000;
-- Nexus Survival Kit (daily login reward): basic medishots and rations
INSERT INTO `loot_group` (`id`,`parentId`,`probability`,`minCount`,`maxCount`,`conditionType`,`condition`,`comment`) VALUES (2000001,NULL,100,0,0,0,0,'Nexus Survival Kit');
INSERT INTO `loot_item` (`id`,`type`,`staticId`,`probability`,`minCount`,`maxCount`,`comment`) VALUES (2000001,0,14838,100,3,3,'Basic Medishot: Instant Healing'),(2000001,0,4977,100,2,2,'Field Rations');
INSERT INTO `item_loot` (`id`,`lootGroupId`,`comment`) VALUES (83615,2000001,'Nexus Survival Kit');
