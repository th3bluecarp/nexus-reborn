# Merge Jabbithole parses: oldest captures (parsed/v1) + newest captures (parsed/v2) -> parsed/jh_*.json.
# Newest wins, but a field the newer page lacks falls back to the older page; drops come from
# whichever capture has more recorded kills.
import json
P='/a/parsed/'
def load(v,k): return {d['file']:d for d in json.load(open(f'{P}{v}/jh_{k}.json'))}
def kills(d): return max([x['of'] or 0 for x in d.get('drops') or []] or [0])
out={}
for k in ('npcs','quests'):
    old,new=load('v1',k),load('v2',k); m={}
    for f in old.keys()|new.keys():
        o,n=old.get(f),new.get(f)
        if not (o and n): m[f]=n or o; continue
        d=dict(n)
        if k=='npcs':
            for fld in ('pins','sells','map','abilities'):
                if not d.get(fld): d[fld]=o.get(fld)
            if kills(o)>kills(n): d['drops']=o['drops']
            for fld in ('starts','completes','objective_of'): d[fld]=sorted(set(o[fld])|set(n[fld]))
        else:
            for fld in ('started_by','completed_by','involved'):
                if not d.get(fld): d[fld]=o.get(fld)
        m[f]=d
    json.dump(sorted(m.values(),key=lambda d:d['file']), open(f'{P}jh_{k}.json','w'), indent=0)
    out[k]=dict(old=len(old),new=len(new),merged=len(m),newer_used=len(new.keys()&old.keys()),only_new=len(new.keys()-old.keys()))
print(out)
