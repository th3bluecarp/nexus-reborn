# Creature combat abilities for the starting zones from archived Jabbithole "Abilities" tabs.
# Instant spells become the creature's auto attacks, spells with a cast time are specials used on a timer.
# Output: parsed/creature_spells.json {creatureId: {"name", "auto": [spell4], "special": [spell4]}} for the world server.
import json, csv, collections
J='/w/json/'; ld=lambda n: json.load(open(J+n+'.json'))
text={int(k):v for k,v in json.load(open(J+'text.json')).items()}; T=lambda i:(text.get(i) or '')
cr={c['Id']:c for c in ld('Creature2')}; sp={s['Id']:s for s in ld('Spell4')}
MAPW={'northernwilds':426,'everstargrove':990,'crimsonisle':870,'levianbay':1387,'algoroc':51,'celestion':51,'galeras':51,'thayd':51,'whitevale':51,'deradune':22,'ellevar':22,'auroria':22,'illium':22}
spawned=collections.defaultdict(set)
for r in csv.DictReader(open('/w/db/entity.tsv'),delimiter='\t'):
    w=int(r['world']); c=cr.get(int(r['creature']))
    if c and w in MAPW.values(): spawned[(w,T(c['LocalizedTextIdName']))].add(c['Id'])
out={}; stats=collections.Counter()
for p in json.load(open('/a/parsed/jh_npcs.json')):
    abil=[a for a in p.get('abilities') or [] if a['spell4_id'] in sp]
    if not abil or p['map'] not in MAPW: continue
    cids=spawned.get((MAPW[p['map']],p['name']))
    if not cids: stats['no spawned creature']+=1; continue
    auto=sorted({a['spell4_id'] for a in abil if sp[a['spell4_id']]['CastTime']==0})
    special=sorted({a['spell4_id'] for a in abil if sp[a['spell4_id']]['CastTime']>0})
    for c in cids:
        e=out.setdefault(str(c),dict(name=p['name'],auto=[],special=[]))
        e['auto']=sorted(set(e['auto'])|set(auto)); e['special']=sorted(set(e['special'])|set(special))
    stats['pages']+=1; stats['creatures']+=len(cids)
json.dump(out,open('/a/parsed/creature_spells.json','w'),indent=1,sort_keys=True)
print(dict(stats),'with auto',sum(1 for e in out.values() if e['auto']),'with special',sum(1 for e in out.values() if e['special']))
