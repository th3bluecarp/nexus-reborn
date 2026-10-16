using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Static.Challenges;
using NexusForever.GameTable;
using NexusForever.GameTable.Model;
using NexusForever.Network.World.Message.Model.Challenges;
using NetworkChallenge = NexusForever.Network.World.Message.Model.Challenges.ServerChallengeUpdate.Challenge;

namespace NexusForever.Game.Entity
{
    /// <summary>
    /// Zone challenges for a player: unlock in their area, count qualifying kills, tiers, timer, leave area fail and cooldown.
    /// </summary>
    /// <remarks>
    /// Combat challenges count kills of a target group, collect challenges kills or activations, checklist challenges activations. Challenges with flag 0x20 start on the first qualifying kill,
    /// the others from the challenge window. Timer 5 minutes, leave area grace 10 seconds and cooldown 30 minutes are the
    /// common retail values, the per challenge values are not in the client data. Rewards are not given yet. State is not saved.
    /// </remarks>
    public class ChallengeManager : IChallengeManager
    {
        private const uint AutoStartFlag = 0x20u;
        private static readonly TimeSpan ActiveTime   = TimeSpan.FromMinutes(5d);
        private static readonly TimeSpan CooldownTime = TimeSpan.FromMinutes(30d);
        private static readonly TimeSpan AreaFailTime = TimeSpan.FromSeconds(10d);

        private class Challenge
        {
            public ChallengeEntry Entry { get; init; }
            public uint[] Tiers { get; init; }

            public bool Activated { get; set; }
            public uint Count { get; set; }
            public uint CurrentTier { get; set; }
            public uint CompletionCount { get; set; }
            public DateTime ActivatedAt { get; set; }
            public DateTime CooldownUntil { get; set; }
            public DateTime? LeftAreaAt { get; set; }

            public uint Goal => Tiers.Length > 0 ? Tiers[^1] : 0u;
        }

        private readonly IPlayer player;
        private readonly Dictionary<uint, Challenge> unlocked = [];
        private HashSet<uint> zoneIds = [];
        private double updateTimer;

        public ChallengeManager(IPlayer owner)
        {
            player = owner;
        }

        /// <summary>
        /// Invoked when the player enters a new zone, unlocks the combat challenges of the area.
        /// </summary>
        public void OnZoneUpdate(WorldZoneEntry zone)
        {
            zoneIds = [];
            for (WorldZoneEntry z = zone; z != null && zoneIds.Add(z.Id); z = z.ParentZoneId != 0u ? GameTableManager.Instance.WorldZone.GetEntry(z.ParentZoneId) : null)
            {
            }

            bool changed = false;
            foreach (ChallengeEntry entry in GameTableManager.Instance.Challenge.Entries)
            {
                if (!IsSupported(entry) || entry.Target == 0u || unlocked.ContainsKey(entry.Id))
                    continue;

                uint areaZone = entry.WorldZoneIdRestriction != 0u ? entry.WorldZoneIdRestriction : entry.WorldZoneId;
                if (!zoneIds.Contains(areaZone))
                    continue;

                uint[] tiers = new[] { entry.ChallengeTierId00, entry.ChallengeTierId01, entry.ChallengeTierId02 }
                    .Select(t => GameTableManager.Instance.ChallengeTier.GetEntry(t)?.Count ?? 0u)
                    .Where(c => c > 0u)
                    .ToArray();
                if (tiers.Length == 0)
                    continue;

                unlocked.Add(entry.Id, new Challenge { Entry = entry, Tiers = tiers });
                SendResult(entry.Id, ChallengeResult.Unlock, 0);
                changed = true;
            }

            if (changed)
                SendUpdate();
        }

        private static bool IsSupported(ChallengeEntry entry)
        {
            return (ChallengeType)entry.ChallengeTypeEnum is ChallengeType.Combat or ChallengeType.Collect or ChallengeType.ChecklistActivate;
        }

        private bool InArea(Challenge challenge)
        {
            uint areaZone = challenge.Entry.WorldZoneIdRestriction != 0u ? challenge.Entry.WorldZoneIdRestriction : challenge.Entry.WorldZoneId;
            return zoneIds.Contains(areaZone);
        }

        /// <summary>
        /// Invoked when the player gets kill credit for a creature.
        /// </summary>
        public void OnKill(IEnumerable<uint> targetGroupIds)
        {
            OnTarget(targetGroupIds, ChallengeType.Combat, ChallengeType.Collect);
        }

        /// <summary>
        /// Invoked when the player successfully activates a creature.
        /// </summary>
        public void OnActivate(IEnumerable<uint> targetGroupIds)
        {
            OnTarget(targetGroupIds, ChallengeType.Collect, ChallengeType.ChecklistActivate);
        }

        private void OnTarget(IEnumerable<uint> targetGroupIds, params ChallengeType[] types)
        {
            var groups = targetGroupIds.ToHashSet();
            if (groups.Count == 0)
                return;

            DateTime now = DateTime.UtcNow;
            foreach (Challenge challenge in unlocked.Values.Where(c => groups.Contains(c.Entry.Target) && types.Contains((ChallengeType)c.Entry.ChallengeTypeEnum)).ToList())
            {
                if (!challenge.Activated)
                {
                    if ((challenge.Entry.ChallengeFlags & AutoStartFlag) == 0u || now < challenge.CooldownUntil || !InArea(challenge))
                        continue;

                    if (!Activate(challenge))
                        continue;
                }

                challenge.Count++;
                while (challenge.CurrentTier < challenge.Tiers.Length && challenge.Count >= challenge.Tiers[challenge.CurrentTier])
                {
                    SendResult(challenge.Entry.Id, ChallengeResult.TierAchieved, (int)challenge.CurrentTier);
                    challenge.CurrentTier++;
                }

                if (challenge.Count >= challenge.Goal)
                    Finish(challenge, true);
                else
                    SendUpdate();
            }
        }

        /// <summary>
        /// Handle the challenge window buttons.
        /// </summary>
        public void HandleChoice(uint challengeId, ChallengeChoice choice)
        {
            if (!unlocked.TryGetValue(challengeId, out Challenge challenge))
                return;

            switch (choice)
            {
                case ChallengeChoice.Activate:
                    if (challenge.Activated)
                        return;
                    if (DateTime.UtcNow < challenge.CooldownUntil)
                    {
                        SendResult(challengeId, ChallengeResult.CooldownActive, 0);
                        return;
                    }
                    if (!InArea(challenge))
                    {
                        SendResult(challengeId, ChallengeResult.AreaRestriction, 0);
                        return;
                    }
                    Activate(challenge);
                    SendUpdate();
                    break;
                case ChallengeChoice.Abandon:
                    if (!challenge.Activated)
                        return;
                    Reset(challenge);
                    SendResult(challengeId, ChallengeResult.AbandonRemove, 0);
                    SendUpdate();
                    break;
            }
        }

        private bool Activate(Challenge challenge)
        {
            // only one challenge of a type can run at a time
            if (unlocked.Values.Any(c => c.Activated && c.Entry.ChallengeTypeEnum == challenge.Entry.ChallengeTypeEnum))
            {
                SendResult(challenge.Entry.Id, ChallengeResult.TypeAlreadyActive, 0);
                return false;
            }

            challenge.Activated   = true;
            challenge.Count       = 0u;
            challenge.CurrentTier = 0u;
            challenge.ActivatedAt = DateTime.UtcNow;
            challenge.LeftAreaAt  = null;
            SendResult(challenge.Entry.Id, ChallengeResult.Activate, 0);
            return true;
        }

        private void Reset(Challenge challenge)
        {
            challenge.Activated  = false;
            challenge.Count      = 0u;
            challenge.LeftAreaAt = null;
        }

        private void Finish(Challenge challenge, bool completed)
        {
            if (completed || challenge.CurrentTier > 0u)
            {
                challenge.CompletionCount++;
                SendResult(challenge.Entry.Id, ChallengeResult.Completed, (int)Math.Max(challenge.CurrentTier, 1u) - 1);
            }
            else
                SendResult(challenge.Entry.Id, ChallengeResult.TimerExpired, 0);

            challenge.CooldownUntil = DateTime.UtcNow + CooldownTime;
            Reset(challenge);
            SendUpdate();
        }

        public void Update(double lastTick)
        {
            updateTimer += lastTick;
            if (updateTimer < 1d)
                return;
            updateTimer = 0d;

            DateTime now = DateTime.UtcNow;
            foreach (Challenge challenge in unlocked.Values.Where(c => c.Activated).ToList())
            {
                if (now - challenge.ActivatedAt >= ActiveTime)
                {
                    Finish(challenge, false);
                    continue;
                }

                if (InArea(challenge))
                {
                    challenge.LeftAreaAt = null;
                    continue;
                }

                if (challenge.LeftAreaAt == null)
                {
                    challenge.LeftAreaAt = now;
                    SendResult(challenge.Entry.Id, ChallengeResult.LeftAreaWarning, 0);
                    SendUpdate();
                }
                else if (now - challenge.LeftAreaAt.Value >= AreaFailTime)
                {
                    SendResult(challenge.Entry.Id, ChallengeResult.LeftArea, 0);
                    challenge.CooldownUntil = now + CooldownTime;
                    Reset(challenge);
                    SendUpdate();
                }
            }
        }

        public void SendInitialPackets()
        {
            OnZoneUpdate(player.Zone);
            SendUpdate();
        }

        private void SendResult(uint challengeId, ChallengeResult result, int data)
        {
            player.Session.EnqueueMessageEncrypted(new ServerChallengeResult
            {
                ChallengeId = (ushort)challengeId,
                Result      = result,
                Data        = data
            });
        }

        private void SendUpdate()
        {
            DateTime now = DateTime.UtcNow;
            player.Session.EnqueueMessageEncrypted(new ServerChallengeUpdate
            {
                ActiveChallenges = unlocked.Values.Select(c => new NetworkChallenge
                {
                    ChallengeId         = c.Entry.Id,
                    Type                = (ChallengeType)c.Entry.ChallengeTypeEnum,
                    TargetGroupId       = c.Entry.Target,
                    CurrentCount        = c.Count,
                    GoalCount           = c.Goal,
                    ObjectiveCompletion = c.Count,
                    CurrentTier         = c.CurrentTier,
                    LastRewardTier      = c.CurrentTier,
                    CompletionCount     = c.CompletionCount,
                    Unlocked            = true,
                    Activated           = c.Activated,
                    OnCooldown          = now < c.CooldownUntil,
                    LeftArea            = c.LeftAreaAt != null,
                    TimeActivatedDt     = c.Activated ? (uint)(now - c.ActivatedAt).TotalMilliseconds : 0u,
                    TimeTotalActive     = (uint)ActiveTime.TotalMilliseconds,
                    TimeCooldownDt      = now < c.CooldownUntil ? (uint)(CooldownTime - (c.CooldownUntil - now)).TotalMilliseconds : 0u,
                    TimeTotalCooldown   = (uint)CooldownTime.TotalMilliseconds,
                    TimeAreaFailDt      = c.LeftAreaAt != null ? (uint)(now - c.LeftAreaAt.Value).TotalMilliseconds : 0u,
                    TimeTotalAreaFail   = (uint)AreaFailTime.TotalMilliseconds,
                    TierGoalCount       = [c.Tiers.ElementAtOrDefault(0), c.Tiers.ElementAtOrDefault(1), c.Tiers.ElementAtOrDefault(2)]
                }).ToList()
            });
        }
    }
}
