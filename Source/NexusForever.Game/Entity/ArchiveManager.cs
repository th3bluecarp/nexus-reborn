using Microsoft.EntityFrameworkCore.ChangeTracking;
using NexusForever.Database.Character;
using NexusForever.Database.Character.Model;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Static.Quest;
using NexusForever.GameTable;
using NexusForever.GameTable.Model;
using NexusForever.Network.World.Message.Model.GalacticArchive;

namespace NexusForever.Game.Entity
{
    /// <summary>
    /// The player's Galactic Archive (journal lore): unlocked and viewed entries per article, saved in character_archive.
    /// </summary>
    /// <remarks>
    /// Read from WildStar64.exe: UnlockedFlags bit 31 is the article itself, the low bits are its entries by index in
    /// ArchiveArticle.ArchiveEntryId00-15. The client unlocks articles itself (interaction with a creature whose
    /// Creature2.ArchiveArticleIdInteractUnlock is set, archive links) and reports it; quest completion unlocks come from
    /// ArchiveEntryUnlockRule type 2 (Object00-05 are quest ids). Rule types 0 and 1 are not understood and not applied.
    /// There is no bulk message: a refresh clears the client, then one update per article.
    /// </remarks>
    public class ArchiveManager : IArchiveManager
    {
        private const uint ArticleUnlocked = 0x80000000u;

        private class Article
        {
            public uint Unlocked { get; set; }
            public uint Viewed { get; set; }
            public bool IsNew { get; set; }
            public bool IsDirty { get; set; }
        }

        private static readonly Lazy<Dictionary<uint, List<(ushort ArticleId, int Index)>>> questUnlocks = new(BuildQuestUnlocks);

        private readonly IPlayer player;
        private readonly Dictionary<ushort, Article> articles = [];

        public ArchiveManager(IPlayer owner, CharacterModel model)
        {
            player = owner;
            foreach (CharacterArchiveModel archiveModel in model.Archive)
                articles[archiveModel.ArticleId] = new Article { Unlocked = archiveModel.UnlockedFlags, Viewed = archiveModel.ViewedFlags };
        }

        private static Dictionary<uint, List<(ushort, int)>> BuildQuestUnlocks()
        {
            var entryIndex = new Dictionary<uint, (ushort, int)>();
            foreach (ArchiveArticleEntry article in GameTableManager.Instance.ArchiveArticle.Entries)
            {
                uint[] entries = [article.ArchiveEntryId00, article.ArchiveEntryId01, article.ArchiveEntryId02, article.ArchiveEntryId03,
                    article.ArchiveEntryId04, article.ArchiveEntryId05, article.ArchiveEntryId06, article.ArchiveEntryId07,
                    article.ArchiveEntryId08, article.ArchiveEntryId09, article.ArchiveEntryId10, article.ArchiveEntryId11,
                    article.ArchiveEntryId12, article.ArchiveEntryId13, article.ArchiveEntryId14, article.ArchiveEntryId15];
                for (int i = 0; i < entries.Length; i++)
                    if (entries[i] != 0u)
                        entryIndex.TryAdd(entries[i], ((ushort)article.Id, i));
            }

            var unlocks = new Dictionary<uint, List<(ushort, int)>>();
            foreach (ArchiveEntryUnlockRuleEntry rule in GameTableManager.Instance.ArchiveEntryUnlockRule.Entries.Where(r => r.ArchiveEntryUnlockRuleEnum == 2u))
            {
                if (!entryIndex.TryGetValue(rule.ArchiveEntryId, out (ushort, int) target))
                    continue;

                foreach (uint questId in new[] { rule.Object00, rule.Object01, rule.Object02, rule.Object03, rule.Object04, rule.Object05 }.Where(q => q != 0u))
                {
                    if (!unlocks.TryGetValue(questId, out List<(ushort, int)> list))
                        unlocks[questId] = list = [];
                    list.Add(target);
                }
            }

            return unlocks;
        }

        public void Save(CharacterContext context)
        {
            foreach ((ushort articleId, Article article) in articles)
            {
                if (!article.IsDirty)
                    continue;

                var model = new CharacterArchiveModel
                {
                    Id            = player.CharacterId,
                    ArticleId     = articleId,
                    UnlockedFlags = article.Unlocked,
                    ViewedFlags   = article.Viewed
                };

                if (article.IsNew)
                    context.Add(model);
                else
                {
                    EntityEntry<CharacterArchiveModel> entity = context.Attach(model);
                    entity.Property(p => p.UnlockedFlags).IsModified = true;
                    entity.Property(p => p.ViewedFlags).IsModified   = true;
                }

                article.IsNew   = false;
                article.IsDirty = false;
            }
        }

        public void SendInitialPackets()
        {
            // quests completed before the archive existed still unlock their entries
            foreach ((uint questId, List<(ushort ArticleId, int Index)> targets) in questUnlocks.Value)
                if (player.QuestManager.GetQuestState((ushort)questId) == QuestState.Completed)
                    foreach ((ushort articleId, int index) in targets)
                        Unlock(articleId, 1u << index, false);

            player.Session.EnqueueMessageEncrypted(new ServerGalacticArchiveRefresh());
            foreach (ushort articleId in articles.Keys)
                SendUpdate(articleId);
        }

        public void ClientUnlock(uint articleId)
        {
            ArchiveArticleEntry entry = GameTableManager.Instance.ArchiveArticle.GetEntry(articleId);
            if (entry == null)
                return;

            // the client unlocks the entries flagged 4 along with the article
            uint[] entries = [entry.ArchiveEntryId00, entry.ArchiveEntryId01, entry.ArchiveEntryId02, entry.ArchiveEntryId03,
                entry.ArchiveEntryId04, entry.ArchiveEntryId05, entry.ArchiveEntryId06, entry.ArchiveEntryId07,
                entry.ArchiveEntryId08, entry.ArchiveEntryId09, entry.ArchiveEntryId10, entry.ArchiveEntryId11,
                entry.ArchiveEntryId12, entry.ArchiveEntryId13, entry.ArchiveEntryId14, entry.ArchiveEntryId15];
            uint mask = 0u;
            for (int i = 0; i < entries.Length; i++)
                if (entries[i] != 0u && ((GameTableManager.Instance.ArchiveEntry.GetEntry(entries[i])?.ArchiveEntryFlags ?? 0u) & 4u) != 0u)
                    mask |= 1u << i;

            Unlock((ushort)articleId, mask, true);
        }

        public void Viewed(uint articleId)
        {
            if (!articles.TryGetValue((ushort)articleId, out Article article) || article.Viewed == article.Unlocked)
                return;

            article.Viewed  = article.Unlocked;
            article.IsDirty = true;
        }

        public void OnQuestComplete(uint questId)
        {
            if (!questUnlocks.Value.TryGetValue(questId, out List<(ushort ArticleId, int Index)> targets))
                return;

            foreach ((ushort articleId, int index) in targets)
                Unlock(articleId, 1u << index, true);
        }

        private void Unlock(ushort articleId, uint entryMask, bool send)
        {
            if (GameTableManager.Instance.ArchiveArticle.GetEntry(articleId) == null)
                return;

            if (!articles.TryGetValue(articleId, out Article article))
                articles[articleId] = article = new Article { IsNew = true };

            uint unlocked = article.Unlocked | ArticleUnlocked | entryMask;
            if (unlocked == article.Unlocked)
                return;

            article.Unlocked = unlocked;
            article.IsDirty  = true;

            if (send)
                SendUpdate(articleId);
        }

        private void SendUpdate(ushort articleId)
        {
            Article article = articles[articleId];
            player.Session.EnqueueMessageEncrypted(new ServerGalacticArchiveUpdate
            {
                ArchiveArticleId = articleId,
                UnlockedFlags    = article.Unlocked,
                ViewedFlags      = article.Viewed
            });
        }
    }
}
