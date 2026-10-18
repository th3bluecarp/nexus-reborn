import re
# Build world-database spawns for creatures the starting-zone quests need but the world lacks,
# placed at Jabbithole map pins. Output: SQL + CSV report.
import json, csv, collections, math, sys
J='/w/json/'; ld=lambda n: json.load(open(J+n+'.json'))
text={int(k):v for k,v in json.load(open(J+'text.json')).items()}; T=lambda i:(text.get(i) or '')
cr={c['Id']:c for c in ld('Creature2')}
Q={q['Id']:q for q in ld('Quest2')}; objs={o['Id']:o for o in ld('QuestObjective')}; tg={t['Id']:t for t in ld('TargetGroup')}
dgrp=collections.defaultdict(list)
for e in ld('Creature2DisplayGroupEntry'): dgrp[e['Creature2DisplayGroupId']].append(e)
ogrp=collections.defaultdict(list)
for e in ld('Creature2OutfitGroupEntry'): ogrp[e['Creature2OutfitGroupId']].append(e)
zones={z['Id']:z for z in ld('WorldZone')}
def top(z):
    for _ in range(20):
        if z in zones and zones[z]['ParentZoneId']: z=zones[z]['ParentZoneId']
        else: break
    return z
ZONES={35:('northernwilds',426),1414:('everstargrove',990),622:('crimsonisle',870),1304:('levianbay',1387),
       13:('algoroc',51),248:('celestion',51),29:('galeras',51),122:('thayd',51),137:('whitevale',51),
       483:('deradune',22),37:('ellevar',22),36:('auroria',22),2191:('illium',22),
       207:('murkmire',51),377:('malgrave',1061),936:('farside',1421),1402:('wilderrun',22),2033:('grimvault',1061)}
spawnpos=collections.defaultdict(list); bycre=collections.defaultdict(list)
for r in csv.DictReader(open('/w/db/entity.tsv'),delimiter='\t'):
    w=int(r['world']); c=int(r['creature'])
    rec=(float(r['x']),float(r['y']),float(r['z']),int(r['area']),c,int(r['displayInfo']))
    spawnpos[w].append(rec); bycre[c].append((w,rec))
jh=json.load(open('/a/parsed/jh_npcs.json'))
jhby=collections.defaultdict(list)
for p in jh: jhby[(p['name'],p['map'])].append(p)
def mem(t,d=0):
    g=tg.get(t); s=set()
    if not g or d>3: return s
    for x in g['DataEntries']:
        if x: s|={x} if g['Type']==1 else mem(x,d+1)
    return s
need=collections.defaultdict(lambda: {'roles':set(),'quests':set()})   # (creature, zone) -> info
for q in Q.values():
    z=top(q['WorldZoneId'])
    if z not in ZONES: continue
    w=ZONES[z][1]
    for c in cr.values():
        if q['Id'] in c['QuestIdGiven']: need[(c['Id'],z)]['roles'].add('giver'); need[(c['Id'],z)]['quests'].add(q['Id'])
        if q['Id'] in c['QuestIdReceive']: need[(c['Id'],z)]['roles'].add('turn-in'); need[(c['Id'],z)]['quests'].add(q['Id'])
    for oid in q['Objectives']:
        o=objs.get(oid)
        if not o: continue
        ids=set()
        if o['Type'] in (2,3,5,9,12,15,21): ids={o['Data']}
        elif o['Type'] in (8,14,16,18,23): ids=mem(o['Data'])
        if o['Type'] in (2,5,12) and o['Data']==0:
            named=set(int(x) for x in re.findall(r'creature=(\d+)',T(o['LocalizedTextIdFull'])))
            ids=named if len(named)==1 else set()
            # rescue and use objectives that name no creature count the reward pane group (Unknown28 style)
            if not ids and o['TargetGroupIdRewardPane']: ids=mem(o['TargetGroupIdRewardPane'])
        elif o['Type']==32: ids=mem(o['TargetGroupIdRewardPane'])
        ids={i for i in ids if i in cr}
        topup=False
        if ids and any(any(w2==w for w2,_ in bycre[i]) for i in ids):
            # some target already in the zone: enough for kills, but objects to use or rescue need as many copies as the
            # objective counts (a reused object only counts again once its busy time is over)
            have=sum(1 for i in ids for w2,_ in bycre[i] if w2==w)
            if o['Type'] not in (5,9,12) or len(ids)!=1 or have>=min(12,o.get('Count',0) or 1): continue
            topup=True
        for i in ids:
            need[(i,z)]['roles'].add('target'); need[(i,z)]['quests'].add(q['Id'])
            if topup: need[(i,z)]['topup']=True
            # a kill or use count of 1 means a single (often named) creature
            need[(i,z)]['count']=max(need[(i,z)].get('count',0), o.get('Count',0) or 1)
            # the objective's map marker, a fallback position when no Jabbithole page has pins
            need[(i,z)].setdefault('locs',set()).update(l for l in (o['WorldLocationsIdIndicator0%d'%k] for k in range(4)) if l)
# path mission objects to activate (Explorer operations), placed like quest targets, once each
episodes={e['Id']:e for e in ld('PathEpisode')}
wloc={x['Id']:x for x in ld('WorldLocation2')}
for kind,table in ((17,'PathExplorerActivate'),):
    rows={r['Id']:r for r in ld(table)}
    for m in ld('PathMission'):
        if m['PathTypeEnum']!=3 or m['PathMissionTypeEnum']!=kind: continue
        e=episodes.get(m['PathEpisodeId']); r=rows.get(m['ObjectId'])
        if not e or not r: continue
        z=top(e['WorldZoneId'])
        if z not in ZONES: continue
        ids={r['Creature2Id']} if r['Creature2Id'] else set()
        ids|=mem(r['TargetGroupId']) if r['TargetGroupId'] else set()
        ids={i for i in ids if i in cr}
        if not ids or any(any(w2==ZONES[z][1] for w2,_ in bycre[i]) for i in ids): continue
        for i in ids:
            need[(i,z)]['roles'].add('target'); need[(i,z)]['count']=max(need[(i,z)].get('count',0), r.get('Count') or 1)
# drop givers/turn-ins already spawned in their zone world
todo={k:v for k,v in need.items() if v.get('topup') or not any(w==ZONES[k[1]][1] for w,_ in bycre[k[0]])}
def clusters(pins, r=8.0):
    cl=[]
    for x,z in pins:
        for c in cl:
            if math.hypot(c['x']-x,c['z']-z)<=r: c['pts'].append((x,z)); n=len(c['pts']); c['x']+= (x-c['x'])/n; c['z']+=(z-c['z'])/n; break
        else: cl.append({'x':x,'z':z,'pts':[(x,z)]})
    return sorted(cl,key=lambda c:-len(c['pts']))
# terrain heights from the server map files for pins far from any known spawn (see tools/heights)
ASSET={w['Id']:w['AssetPath'].split('\\')[-1] for w in ld('World')}
terrain={}; needed=[]
try:
    for l in open('/a/parsed/terrain_heights.tsv'):
        k,h,ar=l.rstrip('\n').split('\t'); terrain[k]=(float(h) if h else None, int(ar) if ar else 0)
except FileNotFoundError: pass
out=[]; report=[]; nid=900000
QTEXT=('LocalizedTextIdCompletionOverride','LocalizedTextIdText','LocalizedTextIdCompletedObjectiveShort')
# the path union reps stood on the old Exile arkship (Jabbithole 2014), which the final client no longer has: put them in Settler's Reach
# Everstar Grove (Aurin start) has the same reps: they stand at the Transport to Everstar Grove arrival spot (41641)
MANUAL={71088:[19397,41641], 71086:[19397,41641], 71085:[19397,41641]}
hubcount=collections.Counter()
for (cid,z),info in sorted(todo.items()):
    # Creature2 names carry a plural tag ("Shade of Water{p:Shades of Water}"), Jabbithole pages use the bare name
    c=cr[cid]; name=re.sub(r'\{p:[^}]*\}','',T(c['LocalizedTextIdName'])); mp,w=ZONES[z]
    cands=jhby.get((name,mp),[])
    # an NPC for this zone's quests may stand in another zone of the same world (a Whitevale quest giver in Thayd):
    # also accept quest-linked pages from those maps, their pins are in the same world coordinates
    other=[p for m2,w2 in ZONES.values() if w2==w and m2!=mp for p in jhby.get((name,m2),[])
           if (set(p['starts'])|set(p['completes'])|set(p['objective_of'])) & info['quests']]
    cands=cands+other
    def ev(p): return len(set(p['starts'])|set(p['completes'])|set(p['objective_of']) & info['quests']) if False else len((set(p['starts'])|set(p['completes'])|set(p['objective_of'])) & info['quests'])
    scored=sorted(cands,key=lambda p:(-ev(p),-len(p['pins'])))
    page=None; conf=''
    if scored and ev(scored[0])>0: page=scored[0]; conf='quest-linked'
    elif len(cands)==1 and cands[0]['pins']: page=cands[0]; conf='name-only'
    if (not page or not page['pins']) and 'target' in info['roles'] and info.get('locs') and not info.get('topup'):
        marks=[[wloc[l]['Position0'],wloc[l]['Position2']] for l in info['locs'] if l in wloc and wloc[l]['WorldId']==w]
        if marks: page={'pins':marks,'file':'(objective marker)'}; conf='objective marker'
    # in the capitals NPCs stand inside buildings the terrain does not cover: a giver or turn-in with its quest turn-in
    # location (WorldLocation2IdReceiver) stands there rather than at its pins
    if mp in ('illium','thayd') and info['roles']-{'target'} and any(q in c['QuestIdReceive'] and Q[q]['WorldLocation2IdReceiver'] in wloc and wloc[Q[q]['WorldLocation2IdReceiver']]['WorldId']==w for q in info['quests']):
        page=None
    if (not page or not page['pins']) and info['roles']-{'target'}:
        # a giver or turn-in with no usable page: the hub its quest text links to (Location:N), or a hand-picked hub in MANUAL
        # the quest's own turn-in location (WorldLocation2IdReceiver) for an NPC that receives it is the most exact
        recv=[Q[q]['WorldLocation2IdReceiver'] for q in sorted(info['quests']) if q in c['QuestIdReceive'] and Q[q]['WorldLocation2IdReceiver']]
        lids=list(MANUAL[cid]) if cid in MANUAL else recv+[int(x) for q in sorted(info['quests']) for k in QTEXT for x in re.findall(r'Location:(\d+)',T(Q[q][k]))]
        lids=[l for l in lids if l in wloc and wloc[l]['WorldId']==w]
        if lids:
            l=lids[0]; n=hubcount[l]; hubcount[l]+=1
            # stand NPCs sharing a hub 3 m apart
            page={'pins':[[wloc[l]['Position0']+3*n,wloc[l]['Position2']+3]],'file':f'(quest location {l})','y':wloc[l]['Position1']}; conf='location link'
    if not page or not page['pins']:
        report.append([cid,name,mp,'/'.join(sorted(info['roles'])),' '.join(map(str,sorted(info['quests']))),'SKIP', 'no Jabbithole page' if not cands else f'{len(cands)} pages, none quest-linked','',''])
        continue
    cl=clusters(page['pins'])
    # skip pin clusters where a same-named creature is already spawned (that location is served by another variant),
    # unless every cluster is such a spot (then it is a same-place phase and the visibility prerequisite separates them)
    samepos=[(rec[0],rec[2]) for o,lst in bycre.items() if (o!=cid or info.get('topup')) and T(cr[o]['LocalizedTextIdName'])==name for (w2,rec) in lst if w2==w]
    free=[c2 for c2 in cl if all(math.hypot(c2['x']-a,c2['z']-b)>15 for a,b in samepos)]
    if free or info.get('topup'): cl=free
    if info.get('topup'):
        # only the missing copies, away from the ones already placed
        info['count']=max(0,info.get('count',1)-sum(1 for w2,_ in bycre[cid] if w2==w))
    # givers and turn-ins once; targets as many times as their quests ask for, up to 12 pin clusters
    take=cl[:1] if info['roles']-{'target'} else cl[:min(12,info.get('count',12))] if info.get('topup') else cl[:max(1,min(12,info.get('count',12)))]
    # display / outfit: copy from a same-name spawn, else from the creature's groups
    same=[rec for (w2,rec) in sum((bycre[o] for o in cr if T(cr[o]['LocalizedTextIdName'])==name and bycre[o]),[]) ] if False else []
    disp=0; outfit=0
    groups=sorted(dgrp.get(c['Creature2DisplayGroupId'],[]), key=lambda e:-e['Weight'])
    if groups: disp=groups[0]['Creature2DisplayInfoId']
    og=sorted(ogrp.get(c['Creature2OutfitGroupId'],[]), key=lambda e:-e['Weight'])
    if og: outfit=og[0]['Creature2OutfitInfoId']
    placed=0
    for cc in take:
        near=sorted(spawnpos[w], key=lambda s: math.hypot(s[0]-cc['x'],s[2]-cc['z']))[:5]
        near=[s for s in near if math.hypot(s[0]-cc['x'],s[2]-cc['z'])<=40]
        if page.get('y') is not None and spawnpos[w]:
            # a quest location is a full 3D position (inside capital buildings too): its height, the area of the nearest spawns
            y=page['y']
            nearest=sorted(spawnpos[w], key=lambda s: math.hypot(s[0]-cc['x'],s[2]-cc['z']))[:5]
            area=collections.Counter(s[3] for s in nearest).most_common(1)[0][0]
        elif near:
            y=sorted(s[1] for s in near)[len(near)//2]; area=collections.Counter(s[3] for s in near).most_common(1)[0][0]
        else:
            # no known spawn nearby: terrain height and area from the server's map files (tools/heights, run between two passes);
            # not in the capitals, where NPCs stand on buildings and platforms the terrain does not include
            if mp in ('illium','thayd'): continue
            key=f"{w}|{cc['x']:.1f}|{cc['z']:.1f}"
            if key not in terrain:
                needed.append(f"{key}	{ASSET[w]}	{cc['x']:.1f}	{cc['z']:.1f}"); continue
            y,area=terrain[key]
            if y is None or not area: continue
        nid+=1; placed+=1
        lvl=min(max(c['MaxLevel'] or c['MinLevel'] or 1,1),50)
        out.append((nid, cid, w, area, round(cc['x'],3), round(y,3), round(cc['z'],3), disp, outfit, c['FactionId'], lvl, name, conf, '/'.join(sorted(info['roles']))))
    report.append([cid,name,mp,'/'.join(sorted(info['roles'])),' '.join(map(str,sorted(info['quests']))),'PLACED' if placed else 'SKIP', f'{conf}, {placed} spawn(s)' if placed else 'pins >40m from known spawns, no terrain height (capital or not looked up)', page['file'], len(page['pins'])])
with open('/a/parsed/starting_zone_spawns.sql','w') as f:
    f.write('-- Spawns for starting-zone quest NPCs and objectives missing from the world database.\n-- Positions from archived Jabbithole map pins, ground height and area from the nearest existing spawns, or from the server map files when none is within 40 m.\n-- Generated by wildstar-archive/tools/build_spawns.py. Remove with: DELETE FROM entity WHERE Id BETWEEN 900000 AND 999999;\n')
    f.write('DELETE FROM `entity_stats` WHERE `Id` BETWEEN 900000 AND 999999;\nDELETE FROM `entity` WHERE `Id` BETWEEN 900000 AND 999999;\n')
    for (i,cid,w,area,x,y,z,disp,outfit,fac,lvl,name,conf,roles) in out:
        f.write(f"-- {name.replace(chr(10),' ')} ({roles}, {conf})\nINSERT INTO `entity` (`Id`,`Type`,`Creature`,`World`,`Area`,`X`,`Y`,`Z`,`RX`,`RY`,`RZ`,`DisplayInfo`,`OutfitInfo`,`Faction1`,`Faction2`) VALUES ({i},0,{cid},{w},{area},{x},{y},{z},0,0,0,{disp},{outfit},{fac},{fac});\n")
        f.write(f"INSERT INTO `entity_stats` (`Id`,`Stat`,`Value`) VALUES ({i},10,{lvl}),({i},15,0);\n")
with open('/a/parsed/starting_zone_spawns_report.csv','w',newline='') as f:
    wr=csv.writer(f); wr.writerow(['creature','name','zone','role','quests','result','detail','jabbithole_page','pins']); wr.writerows(report)
cnt=collections.Counter(r[5] for r in report)
print('needed creatures',len(todo),'| placed',cnt['PLACED'],'skipped',cnt['SKIP'],'| spawn rows',len(out))
for z in ZONES: print(ZONES[z][0], 'spawns', sum(1 for o in out if o[2]==ZONES[z][1]))
print('skip reasons', collections.Counter(r[6].split(',')[0] for r in report if r[5]=='SKIP').most_common())
open('/a/parsed/terrain_needed.tsv','w').write(''.join(l+'\n' for l in dict.fromkeys(needed)))
print('terrain lookups needed', len(dict.fromkeys(needed)), 'known', len(terrain))
