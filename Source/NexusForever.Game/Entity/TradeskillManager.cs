using Microsoft.EntityFrameworkCore.ChangeTracking;
using NexusForever.Database.Character;
using NexusForever.Database.Character.Model;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Static.Crafting;
using NexusForever.Game.Static.Quest;
using NexusForever.GameTable;
using NexusForever.GameTable.Model;
using NexusForever.Network.World.Message.Model.Crafting;
using NexusForever.Network.World.Message.Model.Shared;

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

        public TradeskillManager(IPlayer owner, CharacterModel model)
        {
            player = owner;
            foreach (CharacterTradeskillModel tradeskillModel in model.Tradeskill)
                tradeskills[(TradeskillType)tradeskillModel.TradeskillId] = new Tradeskill { Xp = tradeskillModel.Xp, Active = tradeskillModel.Active };
        }

        public void Save(CharacterContext context)
        {
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

            // "Learn two tradeskills from the trainer" quests (LearnTradeskill, Data 0)
            player.QuestManager.ObjectiveUpdate(QuestObjectiveType.LearnTradeskill, 0u, 1u);
        }

        public bool HasReachedTier(uint tradeskillTierId)
        {
            TradeskillTierEntry tier = GameTableManager.Instance.TradeskillTier.GetEntry(tradeskillTierId);
            if (tier == null || !tradeskills.TryGetValue((TradeskillType)tier.TradeSkillId, out Tradeskill tradeskill))
                return false;

            return tradeskill.Xp >= tier.RequiredXp;
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
            player.Session.EnqueueMessageEncrypted(new ServerProfessionsLoad
            {
                Tradeskills = tradeskills.Select(t => BuildInfo(t.Key, t.Value)).ToList()
            });
        }
    }
}
