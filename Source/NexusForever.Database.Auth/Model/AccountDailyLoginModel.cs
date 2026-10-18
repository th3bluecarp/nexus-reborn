using System;

namespace NexusForever.Database.Auth.Model
{
    public class AccountDailyLoginModel
    {
        public uint Id { get; set; }
        public uint LoginDays { get; set; }
        public DateTime LastLoginDay { get; set; }

        public AccountModel Account { get; set; }
    }
}
