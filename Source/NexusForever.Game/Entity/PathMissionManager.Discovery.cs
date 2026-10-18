using System.Numerics;
using NexusForever.Game.Abstract.Entity;
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
        private static readonly Dictionary<uint, uint[]> DatacubeRules = new()
        {
            [1877u] = [24858u, 26379u, 26380u, 26381u, 26382u, 26383u, 26384u, 32729u, 32753u, 32785u, 32848u, 32997u, 40185u, 40186u, 40187u, 40188u, 40189u], // Deradune
            [1936u] = [57151u, 57152u, 57154u, 57155u, 57156u, 57157u, 57158u, 57159u, 57160u, 57161u, 57162u, 57163u, 57164u, 58303u], // Malgrave
            [2136u] = [21894u, 21896u, 21897u, 21898u, 21899u, 21900u, 21901u, 21902u, 21903u, 21904u, 21905u, 21906u, 21907u], // Algoroc
            [2167u] = [19672u, 20907u, 21750u, 22109u, 22136u, 22141u, 22142u, 22143u, 22144u, 22145u, 22146u, 22147u, 22148u, 22149u, 22150u, 22151u, 22152u, 22153u, 22154u, 22155u, 37629u, 54204u], // Galeras
            [2244u] = [37629u, 39475u, 39477u, 39478u, 39479u, 39480u, 39481u, 39517u, 39518u, 56824u, 56825u, 56833u, 56834u, 56835u, 56836u, 56837u], // Wilderrun
            [2375u] = [25461u, 25462u, 25463u, 25464u, 25465u, 25466u, 25467u, 25468u, 25469u, 25470u, 25471u, 25472u, 25473u, 25474u, 25475u, 25476u, 25477u, 25478u, 25479u, 25480u, 48660u], // Auroria
            [2421u] = [37629u, 40696u, 40697u, 40698u], // Illium
            [2470u] = [37629u, 48599u, 48602u, 48603u, 48604u, 48605u, 48606u, 48607u, 52721u, 52723u, 52724u, 52725u, 52727u, 56766u, 56767u, 56768u, 56769u, 56770u, 56771u, 56772u, 65268u, 70011u, 70012u], // Southern Grimvault
            [2525u] = [37629u, 48599u, 48602u, 48603u, 48604u, 48605u, 48606u, 48607u, 52721u, 52723u, 52724u, 52725u, 52727u, 56766u, 56767u, 56768u, 56769u, 56770u, 56771u, 56772u, 65268u, 70011u, 70012u], // Western Grimvault
            [3103u] = [21694u, 21695u, 21696u, 21697u, 21698u, 21699u, 21700u, 21701u, 21702u, 21703u, 23257u, 23258u, 25842u, 25843u, 25844u, 25845u, 25846u, 25847u, 25848u, 25849u, 25850u, 34887u, 34888u, 37629u], // Whitevale
            [3104u] = [30418u, 30419u, 30420u, 30421u, 30422u, 30423u, 30424u, 30425u, 30426u, 30427u, 30428u, 30429u, 30430u, 30431u, 30432u, 34503u], // Celestion
            [3105u] = [30342u, 30353u, 30354u, 30355u, 30357u, 30358u, 30361u, 30363u, 30364u, 30365u, 30367u, 30368u, 30370u, 30371u], // Ellevar
            [3189u] = [54226u, 54227u, 54228u], // Thayd
            [3212u] = [37629u, 48599u, 48602u, 48603u, 48604u, 48605u, 48606u, 48607u, 52721u, 52723u, 52724u, 52725u, 52727u, 56766u, 56767u, 56768u, 56769u, 56770u, 56771u, 56772u, 65268u, 70011u, 70012u], // Blighthaven
            [3268u] = [37629u, 48599u, 48602u, 48603u, 48604u, 48605u, 48606u, 48607u, 52721u, 52723u, 52724u, 52725u, 52727u, 56766u, 56767u, 56768u, 56769u, 56770u, 56771u, 56772u, 65268u, 70011u, 70012u], // The Defile
        };

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
