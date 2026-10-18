using NexusForever.Game.Abstract.Entity;
using NexusForever.GameTable;
using NexusForever.GameTable.Model;
using NexusForever.Network.World.Message.Model.PlayerPath;
using Path = NexusForever.Game.Static.PlayerPath.Path;

namespace NexusForever.Game.Entity
{
    /// <summary>
    /// Scientist experimentation missions (mission type 22): a code breaking game at an experiment console.
    /// </summary>
    /// <remarks>
    /// The console creature names the experimentation (Creature2.PathScientistExperimentationId, the mission's ObjectId) and the
    /// client sends a ScientistActivate interaction for it. The server answers with ServerPathScientistExperimentationInvoke,
    /// the unit and a secret code of 4 pattern ids. The client (WildStar64.exe 0x140618ed0) keeps the code, offers its distinct
    /// patterns topped up with the other patterns of the experimentation, scores every attempt itself (2 exact, 1 right pattern
    /// in the wrong slot, 0 wrong) and counts the attempts against PathScientistExperimentation.NumAttempts. Only a fully
    /// correct attempt is sent (ClientPathMissionAttemptScientistExperimentation, 0x140619230), which completes the mission.
    /// The code is drawn at random from the experimentation's 4 patterns, repeats allowed, how retail picked it is not known.
    /// </remarks>
    public partial class PathMissionManager
    {
        private const int ExperimentationSlots = 4;

        private PathMission openExperimentation;
        private uint[] experimentationCode;

        public bool OpenExperimentation(IWorldEntity entity)
        {
            if (entity == null || player.Path != Path.Scientist)
                return false;

            Creature2Entry creature = GameTableManager.Instance.Creature2.GetEntry(entity.CreatureId);
            if (creature == null || creature.PathScientistExperimentationId == 0u)
                return false;

            PathMission mission = missions.Values.FirstOrDefault(m => m.Kind == MissionKind.Experimentation && !m.IsComplete
                && m.Objects[0] == creature.PathScientistExperimentationId);
            if (mission == null)
                return false;

            uint[] patterns = GameTableManager.Instance.PathScientistExperimentationPattern.Entries
                .Where(p => p.PathScientistExperimentationId == creature.PathScientistExperimentationId)
                .Select(p => p.Id)
                .ToArray();
            if (patterns.Length == 0)
                return false;

            // every opening is a fresh experiment, so running out of attempts is retried by using the console again
            openExperimentation = mission;
            experimentationCode = Enumerable.Range(0, ExperimentationSlots)
                .Select(_ => patterns[Random.Shared.Next(patterns.Length)])
                .ToArray();

            var invoke = new ServerPathScientistExperimentationInvoke
            {
                UnitId = entity.Guid
            };
            experimentationCode.CopyTo(invoke.CurrentPattern, 0);
            player.Session.EnqueueMessageEncrypted(invoke);

            if (mission.State == Static.PlayerPath.PathMissionState.Unlocked)
            {
                mission.State   = Static.PlayerPath.PathMissionState.Started;
                mission.IsDirty = true;
            }

            return true;
        }

        public void AttemptExperimentation(IReadOnlyList<uint> choices)
        {
            if (openExperimentation == null || experimentationCode == null || choices.Count != ExperimentationSlots)
                return;

            if (!choices.SequenceEqual(experimentationCode))
                return;

            PathMission mission = openExperimentation;
            openExperimentation = null;
            experimentationCode = null;

            if (!mission.IsComplete)
                Advance(mission, mission.Count);
        }
    }
}
