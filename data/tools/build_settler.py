# Settler expansion spawns: a depot at the display point of every improvement group that has none, and resource nodes in the
# hub resource areas. Retail placed these from server scripts that are lost.
# Pass 1 writes the points that need a terrain height to parsed/terrain_needed.tsv (run tools/heights on the PC, append its output
# to parsed/terrain_heights.tsv), pass 2 writes parsed/settler_spawns.sql (entity ids 1090000 to 1099999).
# Usage: python -I build_settler.py   (mounts: /w/json client tables, /w/db/entity.tsv world database dump, /a the archive)
import json, csv, collections, math, random, re
J = '/w/json/'
ld = lambda n: json.load(open(J + n + '.json'))
text = {int(k): v for k, v in json.load(open(J + 'text.json')).items()}
T = lambda i: re.sub(r'\{p:[^}]*\}', '', text.get(i) or '')
WL = {w['Id']: w for w in ld('WorldLocation2')}
HUB = {h['Id']: h for h in ld('PathSettlerHub')}
GROUPS = ld('PathSettlerImprovementGroup')
CR = {c['Id']: c for c in ld('Creature2')}
# collectables (Creature2 CreationTypeEnum 8) must be CollectableUnit entities, the client won't let NonPlayer ones be used
etype = lambda cid: 8 if CR[cid].get('CreationTypeEnum') == 8 else 0
ITEMS = {i['Id']: i for i in ld('Item2')}
WORLD = {w['Id']: w for w in ld('World')}
EP = {e['Id']: e for e in ld('PathEpisode')}
dgrp = collections.defaultdict(list)
for e in ld('Creature2DisplayGroupEntry'): dgrp[e['Creature2DisplayGroupId']].append(e)
ASSET = {w: WORLD[w]['AssetPath'].split('\\')[-1] for w in WORLD if WORLD[w].get('AssetPath')}

spawns = collections.defaultdict(list)   # world -> (creature, x, y, z, area)
for r in csv.DictReader(open('/w/db/entity.tsv'), delimiter='\t'):
    spawns[int(r['world'])].append((int(r['creature']), float(r['x']), float(r['y']), float(r['z']), int(r['area'])))
terrain = {}
for l in open('/a/parsed/terrain_heights.tsv'):
    p = l.rstrip('\n').split('\t')
    if len(p) >= 3: terrain[p[0]] = (float(p[1]) if p[1] not in ('', 'None') else None, int(p[2]) if p[2].isdigit() else 0)

# hubs of the open world settler expansion missions
hubs = sorted({m['ObjectId'] for m in ld('PathMission') if m['PathTypeEnum'] == 1 and m['PathMissionTypeEnum'] == 19 and m['ObjectId'] in HUB})
# hub resource item -> resource node creature with the same name
resource_creature = {}
for c in CR.values():
    if 'Settler Resource' in (c.get('Description') or ''):
        resource_creature.setdefault(T(c['LocalizedTextIdName']).lower(), c['Id'])

def display(cid):
    g = sorted(dgrp.get(CR[cid]['Creature2DisplayGroupId'], []), key=lambda e: -e['Weight'])
    return g[0]['Creature2DisplayInfoId'] if g else 0

def nearest_area(world, x, z):
    near = sorted(spawns[world], key=lambda s: math.hypot(s[1] - x, s[3] - z))[:5]
    return collections.Counter(s[4] for s in near).most_common(1)[0][0] if near else 0

out, needed, report = [], [], collections.Counter()
# node spots are seeded by their resource area so hubs sharing an area get the same spots (removed as duplicates below)
for h in hubs:
    hub = HUB[h]
    for g in (g for g in GROUPS if g['PathSettlerHubId'] == h and g['Creature2IdDepot'] in CR):
        loc = WL.get(g['WorldLocation2IdDisplayPoint'])
        if not loc: report['depot without display point'] += 1; continue
        w, x, y, z = loc['WorldId'], loc['Position0'], loc['Position1'], loc['Position2']
        if any(s[0] == g['Creature2IdDepot'] and math.hypot(s[1] - x, s[3] - z) <= 10 for s in spawns[w]):
            report['depot already spawned'] += 1; continue
        out.append((g['Creature2IdDepot'], w, nearest_area(w, x, z), x, y, z, f'depot {T(g["LocalizedTextIdName"])} (hub {h})'))
        report['depot added'] += 1
    for n in range(3):
        item = hub[f'Item2IdResource0{n}']
        if not item or item not in ITEMS: continue
        cid = resource_creature.get(T(ITEMS[item]['LocalizedTextIdName']).lower())
        if not cid: report['resource without node creature'] += 1; continue
        for m in range(4):
            loc = WL.get(hub[f'WorldLocation2IdMapResource0{n}Loc0{m}'])
            if not loc: continue
            w = loc['WorldId']
            if w not in ASSET: continue
            radius = max(min(loc['Radius'] * 0.6, 40.0), 8.0)
            rnd = random.Random(loc["Id"])
            for k in range(6):
                a = rnd.random() * 2 * math.pi; d = radius * math.sqrt(rnd.random())
                x, z = loc['Position0'] + math.cos(a) * d, loc['Position2'] + math.sin(a) * d
                key = f'{w}|{x:.1f}|{z:.1f}'
                if key not in terrain:
                    needed.append(f'{key}\t{ASSET[w]}\t{x:.1f}\t{z:.1f}'); continue
                y, area = terrain[key]
                if y is None or not area: report['node without ground'] += 1; continue
                out.append((cid, w, area, x, y, z, f'{T(CR[cid]["LocalizedTextIdName"])} node (hub {h})'))
                report['node added'] += 1

# a node spot already used by another hub sharing the area is placed once
seen, rows = set(), []
for r in out:
    key = (r[0], r[1], round(r[3], 1), round(r[5], 1))
    if key not in seen: seen.add(key); rows.append(r)

with open('/a/parsed/terrain_needed.tsv', 'w') as f:
    f.write('\n'.join(dict.fromkeys(needed)) + ('\n' if needed else ''))
with open('/a/parsed/settler_spawns.sql', 'w') as f:
    f.write('-- Settler expansion depots (one per improvement group, at its display point) and hub resource nodes, missing from the world database.\n'
            '-- Generated by wildstar-archive/tools/build_settler.py. Remove with: DELETE FROM entity WHERE Id BETWEEN 1090000 AND 1099999;\n'
            'DELETE FROM `entity_stats` WHERE `Id` BETWEEN 1090000 AND 1099999;\nDELETE FROM `entity` WHERE `Id` BETWEEN 1090000 AND 1099999;\n')
    for i, (cid, w, area, x, y, z, note) in enumerate(rows, start=1090001):
        c = CR[cid]; lvl = min(max(c['MaxLevel'] or c['MinLevel'] or 1, 1), 50)
        f.write(f'-- {note}\nINSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) '
                f'VALUES ({i},{etype(cid)},{cid},{w},{area},{x:.3f},{y:.3f},{z:.3f},0,0,0,{display(cid)},0,{c["FactionId"]},{c["FactionId"]});\n')
        f.write(f'INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES ({i},10,{lvl}),({i},15,0);\n')
print('hubs', len(hubs), dict(report), 'rows', len(rows), 'terrain needed', len(set(needed)))
