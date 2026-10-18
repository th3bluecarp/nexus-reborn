using Microsoft.EntityFrameworkCore.ChangeTracking;
using NexusForever.Database.Character;
using NexusForever.Database.Character.Model;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Static.Crafting;
using NexusForever.Game.Static.Entity;
using NexusForever.Game.Static.Quest;
using NexusForever.GameTable;
using NexusForever.GameTable.Model;
using NexusForever.Network.World.Message.Model.Crafting;
using NexusForever.Network.World.Message.Model.Shared;
using NexusForever.Network.World.Message.Static;

namespace NexusForever.Game.Entity
{
    /// <summary>
    /// The player's tradeskills: which are learned and active, and their xp. Saved in character_tradeskill.
    /// </summary>
    /// <remarks>
    /// Tradeskill.Flags: 2 gathering, 4 equipment crafting, 8 other crafting (Technologist, Architect, Cooking), 1 on Cooking,
    /// Fishing and Farmer which never had the crafting limit, 16 Runecrafting. As late retail, at most two crafting
    /// tradeskills are active at once; gathering and the flag 1 tradeskills do not count.
    /// </remarks>
    public class TradeskillManager : ITradeskillManager
    {
        private const int MaxActiveCrafting = 2;

        private class Tradeskill
        {
            public uint Xp { get; set; }
            public bool Active { get; set; }
            public bool IsNew { get; set; }
            public bool IsDirty { get; set; }
        }

        private readonly IPlayer player;
        private readonly Dictionary<TradeskillType, Tradeskill> tradeskills = [];
        private readonly HashSet<uint> learnedSchematics = [];
        private readonly HashSet<uint> newSchematics = [];

        public TradeskillManager(IPlayer owner, CharacterModel model)
        {
            player = owner;
            foreach (CharacterTradeskillModel tradeskillModel in model.Tradeskill)
                tradeskills[(TradeskillType)tradeskillModel.TradeskillId] = new Tradeskill { Xp = tradeskillModel.Xp, Active = tradeskillModel.Active };
            foreach (CharacterTradeskillSchematicModel schematicModel in model.TradeskillSchematic)
                learnedSchematics.Add(schematicModel.SchematicId);
        }

        public void Save(CharacterContext context)
        {
            foreach (uint schematicId in newSchematics)
                context.Add(new CharacterTradeskillSchematicModel { Id = player.CharacterId, SchematicId = schematicId });
            newSchematics.Clear();

            foreach ((TradeskillType type, Tradeskill tradeskill) in tradeskills)
            {
                if (!tradeskill.IsDirty)
                    continue;

                var model = new CharacterTradeskillModel
                {
                    Id           = player.CharacterId,
                    TradeskillId = (uint)type,
                    Xp           = tradeskill.Xp,
                    Active       = tradeskill.Active
                };

                if (tradeskill.IsNew)
                    context.Add(model);
                else
                {
                    EntityEntry<CharacterTradeskillModel> entity = context.Attach(model);
                    entity.Property(p => p.Xp).IsModified     = true;
                    entity.Property(p => p.Active).IsModified = true;
                }

                tradeskill.IsNew   = false;
                tradeskill.IsDirty = false;
            }
        }

        private static bool IsLimited(TradeskillType type)
        {
            TradeskillEntry entry = GameTableManager.Instance.Tradeskill.GetEntry((uint)type);
            return entry != null && (entry.Flags & (4u | 8u)) != 0u && (entry.Flags & 1u) == 0u;
        }

        public void Learn(TradeskillType toLearn, TradeskillType toDrop)
        {
            if (GameTableManager.Instance.Tradeskill.GetEntry((uint)toLearn) == null)
                return;

            if (tradeskills.TryGetValue(toLearn, out Tradeskill existing) && existing.Active)
                return;

            if (toDrop != 0 && tradeskills.TryGetValue(toDrop, out Tradeskill dropped) && dropped.Active)
            {
                dropped.Active  = false;
                dropped.IsDirty = true;
                SendUpdate(toDrop, dropped);
            }

            if (IsLimited(toLearn) && tradeskills.Count(t => t.Value.Active && IsLimited(t.Key)) >= MaxActiveCrafting)
            {
                player.SendSystemMessage("You already have two crafting tradeskills, drop one to learn another.");
                return;
            }

            if (existing == null)
            {
                existing = new Tradeskill { IsNew = true };
                tradeskills.Add(toLearn, existing);
            }

            existing.Active  = true;
            existing.IsDirty = true;
            SendUpdate(toLearn, existing);

            GrantHarvestSpells(toLearn);

            // "Learn two tradeskills from the trainer" quests (LearnTradeskill, Data 0)
            player.QuestManager.ObjectiveUpdate(QuestObjectiveType.LearnTradeskill, 0u, 1u);
        }

        // harvesting abilities by tradeskill, Spell4Base ids: Mining (24762, spell 40546 does 100000 damage to the node),
        // Survivalist (49162 Harvesting, 25204), Relic Hunter (54180); the pairing is read from the spell names
        private static readonly Dictionary<TradeskillType, uint[]> HarvestSpells = new()
        {
            { TradeskillType.Mining,       [24762u] },
            { TradeskillType.Survivalist,  [49162u, 25204u] },
            { TradeskillType.Relic_Hunter, [54180u] }
        };

        private void GrantHarvestSpells(TradeskillType type)
        {
            if (!HarvestSpells.TryGetValue(type, out uint[] spellBaseIds))
                return;

            foreach (uint spellBaseId in spellBaseIds)
            {
                try
                {
                    player.SpellManager.AddSpell(spellBaseId);
                }
                catch (InvalidOperationException)
                {
                    // already known
                }
                catch (ArgumentOutOfRangeException)
                {
                    // no spell info for this base
                }
            }
        }

        public bool HasReachedTier(uint tradeskillTierId)
        {
            TradeskillTierEntry tier = GameTableManager.Instance.TradeskillTier.GetEntry(tradeskillTierId);
            if (tier == null || !tradeskills.TryGetValue((TradeskillType)tier.TradeSkillId, out Tradeskill tradeskill))
                return false;

            return tradeskill.Xp >= tier.RequiredXp;
        }

        /// <summary>
        /// Highest TradeskillTier.Tier reached by xp, 0 when the tradeskill is not known.
        /// </summary>
        private uint CurrentTier(TradeskillType type)
        {
            if (!tradeskills.TryGetValue(type, out Tradeskill tradeskill))
                return 0u;

            return GameTableManager.Instance.TradeskillTier.Entries
                .Where(t => t.TradeSkillId == (uint)type && tradeskill.Xp >= t.RequiredXp)
                .Select(t => t.Tier)
                .DefaultIfEmpty(1u)
                .Max();
        }

        private TradeskillTierEntry CurrentTierEntry(TradeskillType type)
        {
            uint tier = CurrentTier(type);
            return GameTableManager.Instance.TradeskillTier.Entries.FirstOrDefault(t => t.TradeSkillId == (uint)type && t.Tier == tier);
        }

        /// <summary>
        /// Schematics known without an item: flag 1 (1,253 schematics, none of them taught by a GiveSchematic item) of an
        /// active tradeskill at or below its current tier.
        /// </summary>
        private bool KnowsSchematic(TradeskillSchematic2Entry schematic)
        {
            var type = (TradeskillType)schematic.TradeSkillId;
            if (!tradeskills.TryGetValue(type, out Tradeskill tradeskill) || !tradeskill.Active)
                return false;

            return learnedSchematics.Contains(schematic.Id) || ((schematic.Flags & 1u) != 0u && schematic.Tier <= CurrentTier(type));
        }

        private IEnumerable<uint> KnownSchematics()
        {
            return GameTableManager.Instance.TradeskillSchematic2.Entries
                .Where(KnowsSchematic)
                .Select(s => s.Id);
        }

        public void LearnSchematic(uint tradeskillSchematic2Id)
        {
            TradeskillSchematic2Entry schematic = GameTableManager.Instance.TradeskillSchematic2.GetEntry(tradeskillSchematic2Id);
            if (schematic == null || !learnedSchematics.Add(tradeskillSchematic2Id))
                return;

            newSchematics.Add(tradeskillSchematic2Id);
            player.Session.EnqueueMessageEncrypted(new ServerSchematicAddLearned
            {
                TradeskillId           = (TradeskillType)schematic.TradeSkillId,
                TradeskillSchematic2Id = tradeskillSchematic2Id
            });
        }

        /// <remarks>
        /// TradeskillTier has no harvest xp column, the tier CraftXp stands in for it.
        /// </remarks>
        public void OnKill(uint creatureId)
        {
            Creature2Entry creature = GameTableManager.Instance.Creature2.GetEntry(creatureId);
            if (creature == null || creature.TradeskillHarvestingInfoId == 0u)
                return;

            TradeskillHarvestingInfoEntry harvest = GameTableManager.Instance.TradeskillHarvestingInfo.GetEntry(creature.TradeskillHarvestingInfoId);
            TradeskillTierEntry nodeTier = harvest != null ? GameTableManager.Instance.TradeskillTier.GetEntry(harvest.TradeSkillTierId) : null;
            if (nodeTier == null)
                return;

            var type = (TradeskillType)nodeTier.TradeSkillId;
            if (!tradeskills.TryGetValue(type, out Tradeskill tradeskill) || !tradeskill.Active)
                return;

            uint xp = CurrentTierEntry(type)?.CraftXp ?? 0u;
            if (xp == 0u)
                return;

            tradeskill.Xp     += xp;
            tradeskill.IsDirty = true;
            SendUpdate(type, tradeskill);
        }

        public void Craft(uint tradeskillSchematic2Id, uint count)
        {
            TradeskillSchematic2Entry schematic = GameTableManager.Instance.TradeskillSchematic2.GetEntry(tradeskillSchematic2Id);
            if (schematic == null || !KnowsSchematic(schematic))
            {
                SendCraftFinish(tradeskillSchematic2Id, 0u, 0u, false);
                return;
            }

            var materials = new[]
            {
                (schematic.Item2IdMaterial00, schematic.MaterialCost00), (schematic.Item2IdMaterial01, schematic.MaterialCost01),
                (schematic.Item2IdMaterial02, schematic.MaterialCost02), (schematic.Item2IdMaterial03, schematic.MaterialCost03),
                (schematic.Item2IdMaterial04, schematic.MaterialCost04)
            }.Where(m => m.Item1 != 0u && m.Item2 != 0u).ToArray();

            for (uint i = 0u; i < Math.Max(count, 1u); i++)
            {
                if (!materials.All(m => HasMaterial(m.Item1, m.Item2)))
                {
                    if (i == 0u)
                        SendCraftFinish(tradeskillSchematic2Id, 0u, 0u, false);
                    break;
                }

                foreach ((uint itemId, uint cost) in materials)
                    TakeMaterial(itemId, cost);

                var type = (TradeskillType)schematic.TradeSkillId;
                TradeskillTierEntry tier = CurrentTierEntry(type);
                uint xp = tier?.CraftXp ?? 0u;
                Tradeskill tradeskill = tradeskills[type];
                tradeskill.Xp    += xp;
                tradeskill.IsDirty = true;

                if (schematic.Item2IdOutput != 0u)
                    player.Inventory.ItemCreate(InventoryLocation.Inventory, schematic.Item2IdOutput, Math.Max(schematic.OutputCount, 1u), ItemUpdateReason.Crafting);

                SendCraftFinish(tradeskillSchematic2Id, schematic.Item2IdOutput, xp, true);
                SendUpdate(type, tradeskill);

                // work orders and crafting quests name the schematic
                player.QuestManager.ObjectiveUpdate(QuestObjectiveType.CraftSchematic, tradeskillSchematic2Id, 1u);
                player.QuestManager.ObjectiveUpdate(QuestObjectiveType.ObtainSchematic, tradeskillSchematic2Id, 1u);
                // work order and crafting delivery objectives (type 31) point at a table that is not in the client data,
                // any successful craft counts for them
                player.QuestManager.ObjectiveUpdateAny(QuestObjectiveType.Unknown31, 1u);
            }
        }

        private static ushort? MaterialId(uint itemId)
        {
            TradeskillMaterialEntry entry = GameTableManager.Instance.TradeskillMaterial.Entries.FirstOrDefault(m => m.Item2IdStatRevolution == itemId);
            return entry != null ? (ushort)entry.Id : null;
        }

        private bool HasMaterial(uint itemId, uint cost)
        {
            ushort? materialId = MaterialId(itemId);
            return (materialId != null && player.SupplySatchelManager.CanAfford(materialId.Value, cost))
                || player.Inventory.GetItemCount(itemId) >= cost;
        }

        private void TakeMaterial(uint itemId, uint cost)
        {
            ushort? materialId = MaterialId(itemId);
            if (materialId != null && player.SupplySatchelManager.CanAfford(materialId.Value, cost))
                player.SupplySatchelManager.RemoveAmount(materialId.Value, cost);
            else
                player.Inventory.ItemDelete(itemId, cost, ItemUpdateReason.Crafting);
        }

        private void SendCraftFinish(uint schematicId, uint itemId, uint xp, bool pass)
        {
            player.Session.EnqueueMessageEncrypted(new ServerCraftingFinish
            {
                Pass                          = pass,
                TradeskillSchematic2IdCrafted = schematicId,
                Item2IdCrafted                = itemId,
                EarnedXp                      = xp
            });
        }

        private static TradeskillInfo BuildInfo(TradeskillType type, Tradeskill tradeskill)
        {
            return new TradeskillInfo
            {
                TradeskillId = type,
                TradeskillXp = tradeskill.Xp,
                IsActive     = tradeskill.Active ? 1u : 0u
            };
        }

        private void SendUpdate(TradeskillType type, Tradeskill tradeskill)
        {
            player.Session.EnqueueMessageEncrypted(new ServerProfessionUpdate
            {
                Tradeskill = BuildInfo(type, tradeskill)
            });
        }

        public void SendInitialPackets()
        {
            // the message layout comes from upstream and is untested against the client, it is only sent once there is
            // something to send so characters without tradeskills log in exactly as before
            if (tradeskills.Count == 0)
                return;

            player.Session.EnqueueMessageEncrypted(new ServerProfessionsLoad
            {
                Tradeskills       = tradeskills.Select(t => BuildInfo(t.Key, t.Value)).ToList(),
                LearnedSchematics = KnownSchematics().ToList()
            });
        }
    }
}
