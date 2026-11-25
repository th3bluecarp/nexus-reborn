using System.Numerics;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Static.PlayerPath;
using NexusForever.GameTable;
using NexusForever.GameTable.Model;
using Path = NexusForever.Game.Static.PlayerPath.Path;

namespace NexusForever.Game.Entity
{
    /// <summary>
    /// Scientist datacube discovery (mission type 24) and specimen survey (mission type 23) missions.
    /// </summary>
    /// <remarks>
    /// Datacube discovery asks for all the Eldan datacubes of a zone, PathScientistDatacubeDiscovery only names the zone: the
    /// datacubes are the DATACUBE creatures spawned in it (<see cref="DatacubeRules"/>, built from the world database spawns),
    /// opening each one counts. Specimen surveys list up to 10 locations: scanning a creature within
    /// <see cref="SpecimenRange"/> of a location takes its sample.
    /// </remarks>
    public partial class PathMissionManager
    {
        private const float SpecimenRange = 30f;

        /// <summary>
        /// Datacube creatures by datacube discovery mission, the DATACUBE creatures spawned in the mission's zone.
        /// </summary>
        private static IReadOnlyDictionary<uint, uint[]> DatacubeRules => ScientistMissionRules.Datacubes;

        private static PathMission CreateDiscoveryMission(PathMissionEntry entry)
        {
            if (entry.PathMissionTypeEnum == 24u)
            {
                if (!DatacubeRules.TryGetValue(entry.Id, out uint[] datacubes))
                    return null;
                return new PathMission { Entry = entry, Kind = MissionKind.Datacube, Objects = datacubes };
            }

            PathScientistSpecimenSurveyEntry survey = GameTableManager.Instance.PathScientistSpecimenSurvey.GetEntry(entry.ObjectId);
            if (survey == null)
                return null;

            uint[] locations = new[] { survey.WorldLocation2Id00, survey.WorldLocation2Id01, survey.WorldLocation2Id02, survey.WorldLocation2Id03, survey.WorldLocation2Id04,
                    survey.WorldLocation2Id05, survey.WorldLocation2Id06, survey.WorldLocation2Id07, survey.WorldLocation2Id08, survey.WorldLocation2Id09 }
                .Where(l => l != 0u)
                .ToArray();
            if (locations.Length == 0)
                return null;
            return new PathMission { Entry = entry, Kind = MissionKind.SpecimenSurvey, Objects = locations };
        }

        private void OpenDatacube(uint creatureId)
        {
            if (player.Path != Path.Scientist)
                return;

            foreach (PathMission mission in missions.Values.Where(m => m.Kind == MissionKind.Datacube && !m.IsComplete).ToList())
            {
                int index = Array.IndexOf(mission.Objects, creatureId);
                if (index >= 0 && index < 32 && (mission.Progress & (1u << index)) == 0u)
                    Advance(mission, mission.Progress | (1u << index));
            }
        }

        /// <summary>
        /// Take a specimen survey sample from the scanned unit, returns true when one was taken.
        /// </summary>
        private bool ScanSpecimen(IWorldEntity target)
        {
            if (target == null || target is IPlayer)
                return false;

            foreach (PathMission mission in missions.Values.Where(m => m.Kind == MissionKind.SpecimenSurvey && !m.IsComplete).ToList())
            {
                for (int i = 0; i < mission.Objects.Length; i++)
                {
                    uint bit = 1u << i;
                    if ((mission.Progress & bit) != 0u)
                        continue;

                    WorldLocation2Entry location = GameTableManager.Instance.WorldLocation2.GetEntry(mission.Objects[i]);
                    if (location == null || location.WorldId != player.Map.Entry.Id)
                        continue;

                    float range = Math.Max(location.Radius, SpecimenRange);
                    if (Vector3.Distance(new Vector3(location.Position0, location.Position1, location.Position2), target.Position) > range)
                        continue;

                    Advance(mission, mission.Progress | bit);
                    return true;
                }
            }

            return false;
        }
    }
}
