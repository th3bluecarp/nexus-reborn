using Microsoft.EntityFrameworkCore.ChangeTracking;
using NexusForever.Database.Character;
using NexusForever.Database.Character.Model;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Static.Entity;
using NexusForever.Game.Static.Quest;
using NexusForever.Game.Static.Reputation;
using NexusForever.Game.Static.Challenges;
using NexusForever.GameTable;
using NexusForever.GameTable.Model;
using NexusForever.Network.World.Message.Model.Challenges;
using NexusForever.Network.World.Message.Static;
using NetworkChallenge = NexusForever.Network.World.Message.Model.Challenges.ServerChallengeUpdate.Challenge;

namespace NexusForever.Game.Entity
{
    /// <summary>
    /// Zone challenges for a player: unlock in their area, count qualifying kills, tiers, timer, leave area fail and cooldown.
    /// </summary>
    /// <remarks>
    /// Combat challenges count kills of a target group, collect challenges kills or activations, checklist challenges activations. Challenges with flag 0x20 start on the first qualifying kill,
    /// the others from the challenge window. Timer 5 minutes, leave area grace 10 seconds and cooldown 30 minutes are the
    /// common retail values, the per challenge values are not in the client data. Completing a challenge earns points on its zone
    /// reward track (Challenge.RewardTrackId), each RewardTrack point cost reached gives a piece of challenge gear, see <see cref="ChallengeGear"/>.
    /// Reward track progress is saved, challenge state is not.
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
            public Dictionary<uint, DateTime> Touched { get; } = [];

            public uint Goal => Tiers.Length > 0 ? Tiers[^1] : 0u;
        }

        private class RewardTrack
        {
            public uint Points { get; set; }
            public uint Claimed { get; set; }
            public bool IsNew { get; set; }
            public bool IsDirty { get; set; }
        }

        // reward points for finishing on bronze, silver and gold, retail values unknown
        private static readonly uint[] TierPoints = [100u, 150u, 200u];

        private readonly IPlayer player;
        private readonly Dictionary<uint, Challenge> unlocked = [];
        private readonly Dictionary<ushort, RewardTrack> rewardTracks = [];
        private HashSet<uint> zoneIds = [];
        private double updateTimer;

        public ChallengeManager(IPlayer owner, CharacterModel model)
        {
            player = owner;
            foreach (CharacterRewardTrackModel trackModel in model.RewardTrack)
                rewardTracks.Add(trackModel.RewardTrackId, new RewardTrack { Points = trackModel.Points, Claimed = trackModel.Claimed });
        }

        public void Save(CharacterContext context)
        {
            foreach ((ushort id, RewardTrack track) in rewardTracks)
            {
                if (!track.IsDirty)
                    continue;

                var model = new CharacterRewardTrackModel
                {
                    Id            = player.CharacterId,
                    RewardTrackId = id,
                    Points        = track.Points,
                    Claimed       = track.Claimed
                };

                if (track.IsNew)
                    context.Add(model);
                else
                {
                    EntityEntry<CharacterRewardTrackModel> entity = context.Attach(model);
                    entity.Property(p => p.Points).IsModified  = true;
                    entity.Property(p => p.Claimed).IsModified = true;
                }

                track.IsNew   = false;
                track.IsDirty = false;
            }
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
                if (!IsSupported(entry) || unlocked.ContainsKey(entry.Id))
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
            {
                SendUpdate();
                // challenge objects shown by unlock state (ChallengeLocked) appear
                player.VisibilityUpdate();
            }
        }

        private static bool IsSupported(ChallengeEntry entry)
        {
            return (ChallengeType)entry.ChallengeTypeEnum is ChallengeType.Combat or ChallengeType.Collect or ChallengeType.ChecklistActivate or ChallengeType.Item or ChallengeType.Ability
                || ((ChallengeType)entry.ChallengeTypeEnum == ChallengeType.General && (TargetGroup(entry) != 0u || TargetCreatures.ContainsKey(entry.Id)
                    || TouchCreatures.ContainsKey(entry.Id) || IsReach(entry)));
        }

        /// <summary>
        /// General challenges without a target or reward pane group whose objects are creatures named in their text, counted on kill or activation.
        /// </summary>
        private static readonly Dictionary<uint, uint[]> TargetCreatures = new()
        {
            [620u] = [37391u],          // Seek and Destroy: Alchemical Supplies in Grimbone Tomb (Algoroc)
            [493u] = [30974u],          // Chasing Fireflies: Fireflies near Celestial Falls (Celestion)
            [185u] = [21185u, 21187u],  // Green Thumb: the healthy Galettuce and Galaraddish of Clearfield Farm (Galeras)
            [236u] = [23151u],          // Defusing the Situation: Sunderstone Proximity Mines (Whitevale), taking blast damage is not checked
            [505u] = [33152u],          // Lowland Lavender Eggs (Ellevar)
            [710u] = [41173u],          // Capital Trash: Illium Market Litter (Illium)
            [424u] = [29638u],          // Speeder Beater: Darkspur Speeders (Auroria)
            [467u] = [31600u],          // Hot Hand: Stray Flames (Auroria)
            [490u] = [32060u],          // Here Comes the Boom: Mining Burrows (Auroria), dropping the explosive is the activation
        };

        /// <summary>
        /// General challenges counted by passing through creatures ("Jump through as many flying whimfibers as you can"): each unit
        /// of them coming within <see cref="TouchRange"/> of the player counts, and counts again <see cref="TouchRespawn"/> later
        /// (retail despawned touched objects and brought them back, Uplifting Updrafts asks for 15 jumps through 10 known clouds).
        /// </summary>
        private static readonly Dictionary<uint, uint[]> TouchCreatures = new()
        {
            [172u] = [9374u, 20791u, 20870u, 20871u, 20875u], // Whimsical Whimfibers: Flying Whimfibers (Galeras)
            [410u] = [28977u],                                // Flitterfly Chaser: Everstar Flitterflies (Everstar Grove)
            [191u] = [18481u],                                // Uplifting Updrafts: the Primal Air jump clouds of Skywind Breach (Galeras), shared with quest 4908
        };

        private const float TouchRange = 4f;
        private static readonly TimeSpan TouchRespawn = TimeSpan.FromSeconds(20);

        private void UpdateTouch()
        {
            foreach (Challenge challenge in unlocked.Values.Where(c => c.Activated && TouchCreatures.ContainsKey(c.Entry.Id)).ToList())
            {
                uint[] creatures = TouchCreatures[challenge.Entry.Id];
                List<IWorldEntity> touched = player.GetVisible()
                    .OfType<IWorldEntity>()
                    .Where(e => creatures.Contains(e.CreatureId)
                        && (!challenge.Touched.TryGetValue(e.Guid, out DateTime at) || DateTime.UtcNow - at >= TouchRespawn)
                        && System.Numerics.Vector3.Distance(e.Position, player.Position) <= TouchRange)
                    .ToList();
                foreach (IWorldEntity entity in touched)
                    challenge.Touched[entity.Guid] = DateTime.UtcNow;

                if (touched.Count > 0)
                    Count([challenge], (uint)touched.Count);
            }
        }

        /// <summary>
        /// The target group a challenge counts: its target, or for General challenges without one the reward pane group
        /// (the mines, eggs, bodies or flowers its text asks for).
        /// </summary>
        private static uint TargetGroup(ChallengeEntry entry)
        {
            if (entry.Target != 0u)
                return entry.Target;

            return (ChallengeType)entry.ChallengeTypeEnum == ChallengeType.General ? entry.TargetGroupIdRewardPane : 0u;
        }

        /// <summary>
        /// General challenges without a target whose text asks to reach, race or deliver to a spot (scale a cliff, vista and race challenges): the
        /// indicator location is the goal, the start location (when there is one) starts the challenge on arrival. Tiers of
        /// thousands are time limits in milliseconds, finishing under more of them gives a better tier.
        /// </summary>
        private static bool IsReach(ChallengeEntry entry)
        {
            if ((ChallengeType)entry.ChallengeTypeEnum != ChallengeType.General || entry.Target != 0u || entry.WorldLocation2IdIndicator == 0u)
                return false;

            string progress = GameTableManager.Instance.TextEnglish?.GetEntry(entry.LocalizedTextIdProgress);
            // "Reach the top of ...", "Race to ...", "Deliver ... to ..." (a run from the start spot to the indicator), "Navigate the
            // lasers ..." (the end of the grid, touching a beam is not tracked), "Land in the center of the pond ..." (the pond)
            return progress != null && (progress.StartsWith("Reach", StringComparison.OrdinalIgnoreCase)
                || progress.StartsWith("Race", StringComparison.OrdinalIgnoreCase)
                || progress.StartsWith("Navigate", StringComparison.OrdinalIgnoreCase)
                || progress.StartsWith("Land in", StringComparison.OrdinalIgnoreCase)
                || progress.StartsWith("Leap", StringComparison.OrdinalIgnoreCase)
                || progress.StartsWith("Run to", StringComparison.OrdinalIgnoreCase)
                || progress.StartsWith("Get to", StringComparison.OrdinalIgnoreCase)
                || progress.StartsWith("Make it to", StringComparison.OrdinalIgnoreCase)
                || progress.StartsWith("Climb", StringComparison.OrdinalIgnoreCase)
                || progress.StartsWith("Find the location", StringComparison.OrdinalIgnoreCase)
                || ((progress.StartsWith("Deliver", StringComparison.OrdinalIgnoreCase) || progress.StartsWith("Return", StringComparison.OrdinalIgnoreCase)
                    || progress.StartsWith("Take", StringComparison.OrdinalIgnoreCase))
                    && entry.WorldLocation2IdStartLocation != 0u)
                || IsTimedRun(entry));
        }

        /// <summary>
        /// A General challenge whose start spot differs from its indicator and whose tiers are all time limits (Whirlwind Racer,
        /// Ravenok Racer, Skeech Saboteur): a run from the start spot to the indicator, whatever the text says is done on the way.
        /// </summary>
        private static bool IsTimedRun(ChallengeEntry entry)
        {
            if (entry.WorldLocation2IdStartLocation == 0u || entry.WorldLocation2IdStartLocation == entry.WorldLocation2IdIndicator)
                return false;

            uint[] tiers = new[] { entry.ChallengeTierId00, entry.ChallengeTierId01, entry.ChallengeTierId02 }
                .Select(t => GameTableManager.Instance.ChallengeTier.GetEntry(t)?.Count ?? 0u)
                .Where(c => c > 0u)
                .ToArray();
            return tiers.Length > 0 && tiers.All(t => t >= 1000u);
        }

        private const float ReachMinRadius = 6f;
        private const float ReachMaxHeight = 6f;

        private bool AtLocation(uint worldLocationId, float minRadius, float maxHeight)
        {
            WorldLocation2Entry location = GameTableManager.Instance.WorldLocation2.GetEntry(worldLocationId);
            if (location == null || player.Map == null || location.WorldId != player.Map.Entry.Id)
                return false;

            float dx = player.Position.X - location.Position0;
            float dz = player.Position.Z - location.Position2;
            float radius = Math.Max(location.Radius, minRadius);
            return dx * dx + dz * dz <= radius * radius && Math.Abs(player.Position.Y - location.Position1) <= Math.Max(maxHeight, location.MaxVerticalDistance);
        }

        private void UpdateReach(DateTime now)
        {
            foreach (Challenge challenge in unlocked.Values.Where(c => IsReach(c.Entry)).ToList())
            {
                if (!challenge.Activated)
                {
                    // arriving at the start spot starts the challenge, challenges without one start from the challenge window
                    if (challenge.Entry.WorldLocation2IdStartLocation == 0u || now < challenge.CooldownUntil || !InArea(challenge)
                        || !AtLocation(challenge.Entry.WorldLocation2IdStartLocation, ReachMinRadius, 30f)
                        || unlocked.Values.Any(c => c.Activated && c.Entry.ChallengeTypeEnum == challenge.Entry.ChallengeTypeEnum))
                        continue;

                    Activate(challenge);
                    SendUpdate();
                    continue;
                }

                if (!AtLocation(challenge.Entry.WorldLocation2IdIndicator, ReachMinRadius, ReachMaxHeight))
                    continue;

                if (challenge.Tiers.All(t => t >= 1000u))
                {
                    uint elapsed = (uint)(now - challenge.ActivatedAt).TotalMilliseconds;
                    challenge.CurrentTier = (uint)Math.Max(challenge.Tiers.Count(t => elapsed <= t), 1);
                    challenge.Count       = challenge.Goal;
                }
                else
                {
                    challenge.Count       = challenge.Goal;
                    challenge.CurrentTier = (uint)challenge.Tiers.Length;
                }

                Finish(challenge, true);
            }
        }

        private bool InArea(Challenge challenge)
        {
            uint areaZone = challenge.Entry.WorldZoneIdRestriction != 0u ? challenge.Entry.WorldZoneIdRestriction : challenge.Entry.WorldZoneId;
            return zoneIds.Contains(areaZone);
        }

        /// <summary>
        /// Invoked when the player gets kill credit for a creature.
        /// </summary>
        public void OnKill(uint creatureId, IEnumerable<uint> targetGroupIds)
        {
            OnTarget(creatureId, targetGroupIds, ChallengeType.Combat, ChallengeType.Collect, ChallengeType.General);
        }

        /// <summary>
        /// Invoked when the player successfully activates a creature.
        /// </summary>
        public void OnActivate(uint creatureId, IEnumerable<uint> targetGroupIds)
        {
            OnTarget(creatureId, targetGroupIds, ChallengeType.Collect, ChallengeType.ChecklistActivate, ChallengeType.General);
        }

        public void OnItemLoot(uint itemId, uint count)
        {
            // Item challenges: the target is the Item2 id to collect
            Count(unlocked.Values.Where(c => (ChallengeType)c.Entry.ChallengeTypeEnum == ChallengeType.Item && c.Entry.Target == itemId).ToList(), count);
        }

        public void OnAbilityHit(uint spell4Id)
        {
            // Ability challenges: the target is the Spell4 id (an item ability like the Eldan Combat Arm), any tier of its base counts
            uint baseId = GameTableManager.Instance.Spell4.GetEntry(spell4Id)?.Spell4BaseIdBaseSpell ?? 0u;
            Count(unlocked.Values.Where(c => (ChallengeType)c.Entry.ChallengeTypeEnum == ChallengeType.Ability
                && (c.Entry.Target == spell4Id || (baseId != 0u && GameTableManager.Instance.Spell4.GetEntry(c.Entry.Target)?.Spell4BaseIdBaseSpell == baseId))).ToList(), 1u);
        }

        private void OnTarget(uint creatureId, IEnumerable<uint> targetGroupIds, params ChallengeType[] types)
        {
            var groups = targetGroupIds.ToHashSet();
            Count(unlocked.Values.Where(c => types.Contains((ChallengeType)c.Entry.ChallengeTypeEnum)
                && (groups.Contains(TargetGroup(c.Entry)) || (TargetCreatures.TryGetValue(c.Entry.Id, out uint[] targets) && targets.Contains(creatureId)))).ToList(), 1u);
        }

        private void Count(List<Challenge> challenges, uint amount)
        {
            DateTime now = DateTime.UtcNow;
            foreach (Challenge challenge in challenges)
            {
                if (!challenge.Activated)
                {
                    if ((challenge.Entry.ChallengeFlags & AutoStartFlag) == 0u || now < challenge.CooldownUntil || !InArea(challenge))
                        continue;

                    // auto start skips quietly when another challenge of the type runs, the window path reports it
                    if (unlocked.Values.Any(c => c.Activated && c.Entry.ChallengeTypeEnum == challenge.Entry.ChallengeTypeEnum))
                        continue;

                    if (!Activate(challenge))
                        continue;
                }

                challenge.Count += amount;
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

        public bool IsUnlocked(uint challengeId) => unlocked.ContainsKey(challengeId);

        public bool IsActive(uint challengeId) => unlocked.TryGetValue(challengeId, out Challenge challenge) && challenge.Activated;

        public uint GetCompletionCount(uint challengeId) => unlocked.TryGetValue(challengeId, out Challenge challenge) ? challenge.CompletionCount : 0u;

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
            challenge.Touched.Clear();
            challenge.LeftAreaAt  = null;
            SendResult(challenge.Entry.Id, ChallengeResult.Activate, 0);
            // collect objects shown while the challenge runs (ChallengeRequirement) appear
            player.VisibilityUpdate();
            return true;
        }

        private void Reset(Challenge challenge)
        {
            challenge.Activated  = false;
            challenge.Count      = 0u;
            challenge.LeftAreaAt = null;
            player.VisibilityUpdate();
        }

        private void Finish(Challenge challenge, bool completed)
        {
            if (completed || challenge.CurrentTier > 0u)
            {
                challenge.CompletionCount++;
                SendResult(challenge.Entry.Id, ChallengeResult.Completed, (int)Math.Max(challenge.CurrentTier, 1u) - 1);
                player.QuestManager.ObjectiveUpdate(QuestObjectiveType.CompleteChallenge, 0u, 1u);
                AddRewardPoints(challenge.Entry.RewardTrackId, TierPoints[Math.Clamp((int)challenge.CurrentTier, 1, TierPoints.Length) - 1]);
            }
            else
                SendResult(challenge.Entry.Id, ChallengeResult.TimerExpired, 0);

            challenge.CooldownUntil = DateTime.UtcNow + CooldownTime;
            Reset(challenge);
            SendUpdate();
        }

        private void AddRewardPoints(uint rewardTrackId, uint points)
        {
            RewardTrackEntry entry = GameTableManager.Instance.RewardTrack.GetEntry(rewardTrackId);
            if (entry == null)
                return;

            if (!rewardTracks.TryGetValue((ushort)rewardTrackId, out RewardTrack track))
            {
                track = new RewardTrack { IsNew = true };
                rewardTracks.Add((ushort)rewardTrackId, track);
            }

            uint[] costs = new[] { entry.RewardPointCost00, entry.RewardPointCost01, entry.RewardPointCost02, entry.RewardPointCost03, entry.RewardPointCost04,
                entry.RewardPointCost05, entry.RewardPointCost06, entry.RewardPointCost07, entry.RewardPointCost08, entry.RewardPointCost09 }
                .TakeWhile(c => c != 0u)
                .ToArray();
            if (costs.Length == 0)
                return;

            track.Points  = Math.Min(track.Points + points, costs[^1]);
            track.IsDirty = true;

            string name = GameTableManager.Instance.TextEnglish.GetEntry(entry.LocalizedTextId) ?? "Challenge rewards";
            player.SendSystemMessage($"{name}: {track.Points} of {costs[^1]} reward points.");

            for (int i = 0; i < costs.Length; i++)
            {
                if (track.Points < costs[i] || (track.Claimed & (1u << i)) != 0u)
                    continue;

                // a full bag leaves the reward unclaimed, it is paid when the next challenge finishes
                if (player.Inventory.IsInventoryFull(InventoryLocation.Inventory))
                {
                    player.SendSystemMessage("Your bags are full, challenge gear will be given after your next challenge.");
                    break;
                }

                track.Claimed |= 1u << i;
                uint itemId = ChallengeGear.Pick(player, rewardTrackId, i);
                if (itemId != 0u)
                    player.Inventory.ItemCreate(InventoryLocation.Inventory, itemId, 1u, ItemUpdateReason.RewardTrack);
            }
        }

        public void Update(double lastTick)
        {
            // passing through moving creatures needs every tick, the rest once a second
            UpdateTouch();

            updateTimer += lastTick;
            if (updateTimer < 1d)
                return;
            updateTimer = 0d;

            DateTime now = DateTime.UtcNow;
            UpdateReach(now);
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

        /// <summary>
        /// Milliseconds left until the end time, the client adds its own clock to the timer fields.
        /// </summary>
        private static uint Remaining(DateTime end, DateTime now)
        {
            return end > now ? (uint)(end - now).TotalMilliseconds : 0u;
        }

        /// <remarks>
        /// The client copies each challenge straight into its state and adds its clock to TimeActivatedDt, TimeCooldownDt and TimeAreaFailDt,
        /// so they are time remaining. Combat challenges show a percent worked out from ObjectiveCompletion (0 to 100 of the goal).
        /// </remarks>
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
                    QualifyCount        = c.Count,
                    QualityTotal        = c.Goal,
                    CurrentCount        = c.Count,
                    GoalCount           = c.Goal,
                    ObjectiveCompletion = c.Goal > 0u ? Math.Min(c.Count * 100u / c.Goal, 100u) : 0u,
                    CurrentTier         = c.CurrentTier,
                    LastRewardTier      = c.CurrentTier,
                    CompletionCount     = c.CompletionCount,
                    Unlocked            = true,
                    Activated           = c.Activated,
                    OnCooldown          = now < c.CooldownUntil,
                    LeftArea            = c.LeftAreaAt != null,
                    TimeActivatedDt     = c.Activated ? Remaining(c.ActivatedAt + ActiveTime, now) : 0u,
                    TimeTotalActive     = (uint)ActiveTime.TotalMilliseconds,
                    TimeCooldownDt      = Remaining(c.CooldownUntil, now),
                    TimeTotalCooldown   = (uint)CooldownTime.TotalMilliseconds,
                    TimeAreaFailDt      = c.LeftAreaAt != null ? Remaining(c.LeftAreaAt.Value + AreaFailTime, now) : 0u,
                    TimeTotalAreaFail   = (uint)AreaFailTime.TotalMilliseconds,
                    TierGoalCount       = [c.Tiers.ElementAtOrDefault(0), c.Tiers.ElementAtOrDefault(1), c.Tiers.ElementAtOrDefault(2)]
                }).ToList()
            });
        }
    }
}
