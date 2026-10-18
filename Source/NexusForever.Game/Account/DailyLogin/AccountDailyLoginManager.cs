using Microsoft.EntityFrameworkCore.ChangeTracking;
using NexusForever.Database.Auth;
using NexusForever.Database.Auth.Model;
using NexusForever.Game.Abstract.Account;
using NexusForever.Game.Abstract.Account.DailyLogin;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Static.Entity;
using NexusForever.GameTable;
using NexusForever.GameTable.Model;
using NexusForever.Network.World.Message.Model.AccountInventory;
using NexusForever.Network.World.Message.Static;
using NLog;

namespace NexusForever.Game.Account.DailyLogin
{
    /// <summary>
    /// Daily login rewards: the first character to enter the world on a new day (UTC) moves the account one day along the
    /// DailyLoginReward calendar and gets that day's rewards.
    /// </summary>
    /// <remarks>
    /// Retail put the rewards (AccountItem) in the account inventory to claim, which is not implemented here: the AccountItem's
    /// Item2Id is put in the character's bags straight away. Only the basic tier is granted (DailyLoginRewardTierEnum 0).
    /// </remarks>
    public class AccountDailyLoginManager : IAccountDailyLoginManager
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private readonly IAccount account;
        private DateTime lastLoginDay;
        private bool isNew;
        private bool isDirty;

        public uint LoginDays { get; private set; }

        public AccountDailyLoginManager(IAccount account, AccountModel model)
        {
            this.account = account;

            AccountDailyLoginModel dailyLogin = model.AccountDailyLogin.FirstOrDefault();
            if (dailyLogin == null)
            {
                isNew = true;
                return;
            }

            LoginDays    = dailyLogin.LoginDays;
            lastLoginDay = dailyLogin.LastLoginDay;
        }

        public void Save(AuthContext context)
        {
            if (!isDirty)
                return;

            var model = new AccountDailyLoginModel
            {
                Id           = account.Id,
                LoginDays    = LoginDays,
                LastLoginDay = lastLoginDay
            };

            if (isNew)
                context.AccountDailyLogin.Add(model);
            else
            {
                EntityEntry<AccountDailyLoginModel> entity = context.AccountDailyLogin.Attach(model);
                entity.Property(p => p.LoginDays).IsModified    = true;
                entity.Property(p => p.LastLoginDay).IsModified = true;
            }

            isNew   = false;
            isDirty = false;
        }

        public void OnEnteredWorld(IPlayer player)
        {
            DateTime today = DateTime.UtcNow.Date;
            if (lastLoginDay.Date < today)
            {
                lastLoginDay = today;
                LoginDays++;
                isDirty = true;
                GrantRewards(player, LoginDays);
            }

            SendUpdate();
        }

        private void GrantRewards(IPlayer player, uint day)
        {
            // the calendar has 365 days, after that it starts over
            uint calendarDay = ((day - 1u) % 365u) + 1u;
            foreach (DailyLoginRewardEntry reward in GameTableManager.Instance.DailyLoginReward.Entries
                .Where(r => r.LoginDay == calendarDay && r.DailyLoginRewardTierEnum == 0u))
            {
                if (reward.DailyLoginRewardTypeEnum != 0u)
                {
                    log.Warn($"Daily login reward {reward.Id} has unhandled type {reward.DailyLoginRewardTypeEnum}.");
                    continue;
                }

                AccountItemEntry accountItem = GameTableManager.Instance.AccountItem.GetEntry(reward.RewardObjectValue);
                if (accountItem == null || accountItem.Item2Id == 0u)
                {
                    log.Warn($"Daily login reward {reward.Id} account item {reward.RewardObjectValue} has no item.");
                    continue;
                }

                player.Inventory.ItemCreate(InventoryLocation.Inventory, accountItem.Item2Id, Math.Max(accountItem.EntitlementCount, 1u), ItemUpdateReason.NoReason);
                log.Info($"Daily login day {day}: {player.Name} got item {accountItem.Item2Id}.");
            }
        }

        public void SendUpdate()
        {
            DateTime now = DateTime.UtcNow;
            account.Session.EnqueueMessageEncrypted(new ServerDailyLoginUpdate
            {
                LoginDaysTotal            = LoginDays,
                CurrentLoginDays          = LoginDays,
                NextDailyRewardNotifyTime = (uint)(now.Date.AddDays(1d) - now).TotalMilliseconds
            });
        }
    }
}
