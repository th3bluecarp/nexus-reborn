"""
Generate PathArrowData.lua: the world positions of every path mission the server supports.

The targets are worked out the same way Source/NexusForever.Game/Entity/PathMissionManager*.cs does it:
scan rules and datacube rules are read straight from the C# source, everything else comes from the
game tables (JSON dumps) and the creature spawns of the world database (a TSV export of the entity table).

Run it in a throwaway container, for example:
  docker run --rm --name patharrow-gen --memory=1g \
    -v <json dir>:/json:ro -v <repo>/Source:/src:ro -v <entities.tsv dir>:/ent:ro \
    -v <repo>/tools/addons/PathArrow:/addon \
    python:3.12-alpine python -I /addon/gen_data.py

entities.tsv columns: id, creature, world, x, y, z (select id, creature, world, x, y, z from entity where creature <> 0).
"""
import json
import math
import os
import re
import sys
from collections import Counter, defaultdict

JSON = os.environ.get("PA_JSON", "/json")
SRC = os.environ.get("PA_SRC", "/src")
ENT = os.environ.get("PA_ENT", "/ent/entities.tsv")
OUT = os.environ.get("PA_OUT", "/addon/PathArrowData.lua")
MAX_POINTS = 25

SOLDIER, SETTLER, SCIENTIST, EXPLORER = 0, 1, 2, 3
PATH_NAMES = {SOLDIER: "Soldier", SETTLER: "Settler", SCIENTIST: "Scientist", EXPLORER: "Explorer"}
PLURAL = re.compile(r"\{p:[^}]*\}")


def load(name):
    with open(os.path.join(JSON, name + ".json"), encoding="utf-8") as f:
        return json.load(f)


def by_id(name):
    return {e["Id"]: e for e in load(name)}


def fields(entry, prefix, count, width=2):
    out = []
    for i in range(count):
        v = entry.get(prefix + str(i).zfill(width), 0)
        if v:
            out.append(v)
    return out


# ---------------------------------------------------------------------------------------------
# tables
# ---------------------------------------------------------------------------------------------
print("loading tables", file=sys.stderr)
text = load("text")
missions = load("PathMission")
episodes = by_id("PathEpisode")
wl2 = by_id("WorldLocation2")
target_groups = by_id("TargetGroup")
field_studies = by_id("PathScientistFieldStudy")
surveys = by_id("PathScientistSpecimenSurvey")
experimentations = by_id("PathScientistExperimentation")
hubs = by_id("PathSettlerHub")
improvement_groups = load("PathSettlerImprovementGroup")
infrastructures = by_id("PathSettlerInfrastructure")
mayors = by_id("PathSettlerMayor")
sheriffs = by_id("PathSettlerSheriff")
soldier_events = by_id("PathSoldierEvent")
assassinates = by_id("PathSoldierAssassinate")
swats = by_id("PathSoldierSWAT")
soldier_activates = by_id("PathSoldierActivate")
explorer_activates = by_id("PathExplorerActivate")
explorer_nodes = load("PathExplorerNode")
hunts = by_id("PathExplorerScavengerHunt")
clues = by_id("PathExplorerScavengerClue")
quest_dirs = by_id("QuestDirection")
quest_dir_entries = by_id("QuestDirectionEntry")
quests = by_id("Quest2")
quest_objectives = by_id("QuestObjective")


def txt(tid):
    return text.get(str(tid), "") if tid else ""


def clean_name(s):
    return PLURAL.sub("", s or "")


# Creature2 is about 200 MB, decode it one object at a time and keep only what is needed.
print("loading Creature2", file=sys.stderr)
creature_name = {}          # creature id -> lower case name, plural tag removed
creature_experiment = defaultdict(list)  # experimentation id -> creature ids
creature_soldier = defaultdict(list)     # soldier mission id -> creature ids (holdout control points)
quest_givers = defaultdict(list)         # quest id -> creature ids
with open(os.path.join(JSON, "Creature2.json"), encoding="utf-8") as f:
    raw = f.read()
dec = json.JSONDecoder()
i = raw.index("[") + 1
n = len(raw)
while True:
    while i < n and raw[i] in " \r\n\t,":
        i += 1
    if i >= n or raw[i] == "]":
        break
    c, i = dec.raw_decode(raw, i)
    cid = c["Id"]
    nm = clean_name(txt(c.get("LocalizedTextIdName", 0)))
    if nm:
        creature_name[cid] = nm.lower()
    if c.get("PathScientistExperimentationId"):
        creature_experiment[c["PathScientistExperimentationId"]].append(cid)
    if c.get("PathMissionIdSoldier"):
        creature_soldier[c["PathMissionIdSoldier"]].append(cid)
    for q in c.get("QuestIdGiven") or []:
        if q:
            quest_givers[q].append(cid)
del raw
names_to_creatures = defaultdict(list)
for cid, nm in creature_name.items():
    names_to_creatures[nm].append(cid)

# ---------------------------------------------------------------------------------------------
# creature spawns from the world database
# ---------------------------------------------------------------------------------------------
print("loading spawns", file=sys.stderr)
spawns = defaultdict(list)  # creature id -> [(world, x, y, z)]
with open(ENT, encoding="ascii", errors="replace") as f:
    for line in f:
        parts = line.split("\t")
        if len(parts) < 6:
            continue
        try:
            cid, world = int(parts[1]), int(parts[2])
            x, y, z = float(parts[3]), float(parts[4]), float(parts[5])
        except ValueError:
            continue
        spawns[cid].append((world, x, y, z))

# ---------------------------------------------------------------------------------------------
# rules written in the C# source
# ---------------------------------------------------------------------------------------------
ENTITY = os.path.join(SRC, "NexusForever.Game", "Entity")
with open(os.path.join(ENTITY, "PathMissionManager.Scientist.cs"), encoding="utf-8-sig") as f:
    sci = f.read()
sci_block = sci[sci.index("ScanRules = new()"):]
sci_block = sci_block[:sci_block.index("\n        };")]
scan_rules = {}
for m in re.finditer(r"\[(\d+)u\]\s*=\s*new\(\[(.*?)\],\s*(\d+)u\)", sci_block, re.S):
    scan_rules[int(m.group(1))] = [s.lower() for s in re.findall(r'"([^"]*)"', m.group(2))]

with open(os.path.join(ENTITY, "PathMissionManager.Discovery.cs"), encoding="utf-8-sig") as f:
    disc = f.read()
datacube_rules = {}
for m in re.finditer(r"\[(\d+)u\]\s*=\s*\[([0-9u,\s]*)\]", disc):
    datacube_rules[int(m.group(1))] = [int(v) for v in re.findall(r"(\d+)u", m.group(2))]
print("scan rules %d, datacube rules %d" % (len(scan_rules), len(datacube_rules)), file=sys.stderr)


# ---------------------------------------------------------------------------------------------
# helpers
# ---------------------------------------------------------------------------------------------
def tg_creatures(tgid, seen=None):
    """Creatures of a target group, the way AssetManager.CacheCreatureTargetGroups resolves them."""
    seen = seen if seen is not None else set()
    if not tgid or tgid in seen or tgid not in target_groups:
        return []
    seen.add(tgid)
    tg = target_groups[tgid]
    data = [d for d in tg.get("DataEntries", []) if d]
    if tg["Type"] == 1:
        return data
    if tg["Type"] in (10, 11):
        out = []
        for d in data:
            out += tg_creatures(d, seen)
        return out
    return []


def creature_points(cids):
    pts = []
    for cid in dict.fromkeys(cids):
        for (w, x, y, z) in spawns.get(cid, []):
            pts.append((w, x, y, z))
    return pts


def wl2_points(ids):
    pts = []
    for lid in ids:
        loc = wl2.get(lid)
        if loc:
            pts.append((loc["WorldId"], loc["Position0"], loc["Position1"], loc["Position2"]))
    return pts


def quest_direction_points(qdid):
    qd = quest_dirs.get(qdid)
    if not qd:
        return []
    ids = []
    for e in fields(qd, "QuestDirectionEntryId", 16):
        qe = quest_dir_entries.get(e)
        if qe and qe.get("WorldLocation2Id"):
            ids.append(qe["WorldLocation2Id"])
    return wl2_points(ids)


def thin(points, keep_first=0):
    """Dedupe to 1 m, then keep at most MAX_POINTS spread evenly over the area (farthest point sampling).
    The first keep_first points (depots and similar) are always kept."""
    uniq = list(dict.fromkeys((round(x), round(y), round(z)) for (x, y, z) in points))
    if len(uniq) <= MAX_POINTS:
        return uniq
    keep = list(dict.fromkeys((round(x), round(y), round(z)) for (x, y, z) in points[:keep_first]))[:MAX_POINTS]
    rest = [p for p in uniq if p not in keep]
    chosen = keep or [rest.pop(0)]
    dist = [min((p[0] - c[0]) ** 2 + (p[2] - c[2]) ** 2 for c in chosen) for p in rest]
    while len(chosen) < MAX_POINTS and rest:
        k = max(range(len(rest)), key=lambda j: dist[j])
        c = rest.pop(k)
        dist.pop(k)
        chosen.append(c)
        dist = [min(d, (p[0] - c[0]) ** 2 + (p[2] - c[2]) ** 2) for d, p in zip(dist, rest)]
    return chosen


# ---------------------------------------------------------------------------------------------
# per mission targets, mirroring PathMissionManager.CreateMission
# ---------------------------------------------------------------------------------------------
def mission_targets(m):
    """Returns (kind, points as (world, x, y, z), priority count, note) or None when the server does not support it."""
    path, mtype, obj = m["PathTypeEnum"], m["PathMissionTypeEnum"], m["ObjectId"]
    t = (path, mtype)
    if t in ((SCIENTIST, 2), (SCIENTIST, 14), (SCIENTIST, 20)):
        names = scan_rules.get(m["Id"])
        extra = []
        if names is None and mtype == 20:
            fs = field_studies.get(obj)
            if not fs:
                return None
            cids = ([fs["Creature2Id"]] if fs["Creature2Id"] else [])
            tg = target_groups.get(fs["TargetGroupId"])
            if tg and tg["Type"] == 1:
                cids += [d for d in tg["DataEntries"] if d]
            names = list(dict.fromkeys(creature_name[c] for c in cids if c in creature_name))
            if not names:
                return None
            extra = wl2_points(fields(fs, "WorldLocation2IdIndicator", 8))
        if names is None:
            return None
        cids = [c for nm in names for c in names_to_creatures.get(nm, [])]
        return "scan", creature_points(cids), 0, extra
    if t in ((SCIENTIST, 23), (SCIENTIST, 24)):
        if mtype == 24:
            if m["Id"] not in datacube_rules:
                return None
            return "datacube", creature_points(datacube_rules[m["Id"]]), 0, []
        sv = surveys.get(obj)
        if not sv:
            return None
        ids = fields(sv, "WorldLocation2Id", 10)
        if not ids:
            return None
        return "survey", wl2_points(ids), 0, []
    if t == (SCIENTIST, 22):
        if obj not in experimentations:
            return None
        return "experiment", creature_points(creature_experiment.get(obj, [])), 0, []
    if t == (SETTLER, 19):
        hub = hubs.get(obj)
        if not hub:
            return None
        depots = wl2_points([g["WorldLocation2IdDisplayPoint"] for g in improvement_groups
                             if g["PathSettlerHubId"] == hub["Id"] and g["WorldLocation2IdDisplayPoint"]])
        res = wl2_points([hub.get("WorldLocation2IdMapResource%02dLoc%02d" % (r, l), 0) for r in range(3) for l in range(4)])
        return "expansion", depots + res, len(depots), []
    if t == (SETTLER, 21):
        inf = infrastructures.get(obj)
        if not inf:
            return None
        depot = creature_points([inf["Creature2IdDepot"]] if inf["Creature2IdDepot"] else [])
        rc = fields(inf, "Creature2IdResource", 3)
        if not rc:
            mt = re.match(r"^Collect (.+?) (?:for|from|to)\b", txt(inf["LocalizedTextIdObjective"]), re.I)
            if mt:
                want = clean_name(mt.group(1)).lower()
                rc = names_to_creatures.get(want, []) + names_to_creatures.get(want[:-1], []) if want.endswith("s") else names_to_creatures.get(want, [])
        return "infrastructure", depot + creature_points(rc), len(depot), []
    if t == (SOLDIER, 0):
        if obj not in soldier_events:
            return None
        return "holdout", creature_points(creature_soldier.get(m["Id"], [])), 0, []
    if t in ((SOLDIER, 4), (SOLDIER, 7), (SOLDIER, 5), (SOLDIER, 6), (EXPLORER, 17)):
        table = {4: assassinates, 7: swats, 5: soldier_activates, 6: soldier_activates, 17: explorer_activates}[mtype]
        e = table.get(obj)
        if not e:
            return None
        cids = ([e["Creature2Id"]] if e["Creature2Id"] else []) + tg_creatures(e["TargetGroupId"])
        return ("kill" if mtype in (4, 7) else "activate"), creature_points(cids), 0, []
    if t in ((EXPLORER, 3), (EXPLORER, 12), (EXPLORER, 15), (EXPLORER, 18), (SETTLER, 27)):
        ids = fields(m, "WorldLocation2Id", 4)
        if not ids and mtype in (3, 12, 15):
            ids = [nd["WorldLocation2Id"] for nd in explorer_nodes if nd["PathExplorerAreaId"] == obj and nd["WorldLocation2Id"]][:32]
        if not ids:
            return None
        return "location", wl2_points(ids), 0, []
    if t == (EXPLORER, 13):
        hunt = hunts.get(obj)
        if not hunt:
            return None
        cl = [c for c in fields(hunt, "PathExplorerScavengerClueId", 7) if c in clues]
        if not cl:
            return None
        pts = []
        for cid in cl:
            c = clues[cid]
            cp = creature_points(([c["Creature2Id"]] if c["Creature2Id"] else []) + tg_creatures(c["TargetGroupId"]))
            pts += cp if cp else wl2_points([c["WorldLocation2IdMiniMap"]] if c["WorldLocation2IdMiniMap"] else [])
        return "scavenger", pts, 0, []
    if t == (EXPLORER, 16):
        if m["PathEpisodeId"] not in episodes:
            return None
        return "cartography", [], 0, []
    if t == (SETTLER, 25):
        mayor = mayors.get(obj)
        if not mayor:
            return None
        ids = fields(mayor, "WorldLocation2Id", 8)
        if not ids:
            return None
        return "location", wl2_points(ids), 0, []
    if t == (SETTLER, 26):
        sh = sheriffs.get(obj)
        if not sh:
            return None
        qids = fields(sh, "Quest2IdSheriff", 8)
        if not qids:
            return None
        givers, objs = [], []
        for q in qids:
            givers += quest_givers.get(q, [])
            qe = quests.get(q)
            for o in (qe.get("Objectives", []) if qe else []):
                qo = quest_objectives.get(o)
                if qo:
                    objs += fields(qo, "WorldLocationsIdIndicator", 4)
        giver_pts = creature_points(givers)
        return "sheriff", giver_pts + wl2_points(objs), len(giver_pts), []
    return None


# ---------------------------------------------------------------------------------------------
# build
# ---------------------------------------------------------------------------------------------
out = {}
report = {"per_path": Counter(), "unsupported": Counter(), "no_location": [], "fallback": [], "off_world": []}
for m in sorted(missions, key=lambda e: e["Id"]):
    if m["PathTypeEnum"] not in PATH_NAMES:
        continue
    epi = episodes.get(m["PathEpisodeId"])
    res = mission_targets(m)
    if res is None:
        report["unsupported"][PATH_NAMES[m["PathTypeEnum"]]] += 1
        continue
    kind, pts, keep_first, extra = res
    name = clean_name(txt(m["LocalizedTextIdName"])) or ("mission %d" % m["Id"])
    world = epi["WorldId"] if epi else 0
    source = kind
    on_world = [p for p in pts if p[0] == world] if world else pts
    if not on_world and pts:
        # every target is in another world (an instance or a phase map): use the most common one
        world = Counter(p[0] for p in pts).most_common(1)[0][0]
        on_world = [p for p in pts if p[0] == world]
        report["off_world"].append((m["Id"], name, world))
    if not on_world:
        # no target spawned: fall back to the locations the mission itself carries
        fb = extra + wl2_points(fields(m, "WorldLocation2Id", 4)) + quest_direction_points(m.get("QuestDirectionId", 0))
        if world:
            fb = [p for p in fb if p[0] == world] or fb
        if fb:
            if not world:
                world = fb[0][0]
            on_world = [p for p in fb if p[0] == world]
            source = kind + " (mission location)"
            report["fallback"].append((m["Id"], PATH_NAMES[m["PathTypeEnum"]], kind, name))
    if not world and epi:
        world = epi["WorldId"]
    kf = min(keep_first, len(on_world)) if source == kind else 0
    # keep the priority points of the right world first
    prio = [p for p in pts[:keep_first] if p[0] == world] if kf else []
    rest = [p for p in on_world if p not in prio]
    points = thin([(p[1], p[2], p[3]) for p in prio + rest], keep_first=len(prio))
    report["per_path"][PATH_NAMES[m["PathTypeEnum"]]] += 1
    if not points:
        report["no_location"].append((m["Id"], PATH_NAMES[m["PathTypeEnum"]], kind, name))
    out[m["Id"]] = {
        "world": world, "path": m["PathTypeEnum"], "faction": m["PathMissionFactionEnum"],
        "episode": m["PathEpisodeId"], "kind": source, "name": name, "points": points,
    }


def lua_str(s):
    return '"' + s.replace("\\", "\\\\").replace('"', '\\"').replace("\n", " ").replace("\r", " ") + '"'


lines = [
    "-- PathArrowData.lua, generated by gen_data.py. Do not edit by hand.",
    "-- PathArrowData[missionId] = { world, path, faction, episode, kind, name, points = { {x, y, z}, ... } }",
    "-- path: 0 Soldier, 1 Settler, 2 Scientist, 3 Explorer. faction: 1 Exile, 2 Dominion, other both.",
    "PathArrowData = {}",
]
ids = sorted(out)
CHUNK = 150  # one function per chunk keeps every constant table far below the Lua 5.1 limit
for start in range(0, len(ids), CHUNK):
    lines.append("do local D = PathArrowData")
    for mid in ids[start:start + CHUNK]:
        e = out[mid]
        pts = ",".join("{%d,%d,%d}" % p for p in e["points"])
        lines.append("D[%d]={world=%d,path=%d,faction=%d,episode=%d,kind=%s,name=%s,points={%s}}" % (
            mid, e["world"], e["path"], e["faction"], e["episode"], lua_str(e["kind"]), lua_str(e["name"]), pts))
    lines.append("end")
with open(OUT, "w", encoding="utf-8", newline="\n") as f:
    f.write("\n".join(lines) + "\n")

print("written %s, %d missions" % (OUT, len(out)))
print("covered per path:", dict(report["per_path"]))
print("unsupported by the server (skipped) per path:", dict(report["unsupported"]))
kinds = Counter(out[i]["kind"] for i in out)
print("by kind:", dict(kinds))
print("no location (%d):" % len(report["no_location"]))
for r in report["no_location"]:
    print("  ", r)
print("fallback to the mission's own location (%d):" % len(report["fallback"]))
for r in report["fallback"]:
    print("  ", r)
print("targets only in another world (%d):" % len(report["off_world"]))
for r in report["off_world"]:
    print("  ", r)
