# Parse archived Jabbithole NPC and quest pages into JSON.
import re, os, json, html, sys
P=sys.argv[1]; OUT=sys.argv[2]
def text(s): return re.sub(r'\s+',' ',html.unescape(re.sub(r'<[^>]+>',' ',s))).strip()
def section(h, tab):
    i=h.find(f'id="{tab}"')
    if i<0: return ''
    j=h.find('</table>', i)
    return h[i:j if j>0 else i+200000]
def rows(sec):
    return re.findall(r'<tr>(.*?)</tr>', sec, flags=re.S)
def info(h, label):
    m=re.search(label+r'\s*:?\s*</[^>]+>\s*(.*?)<(?:br|/div|/li|/p|div)', h, flags=re.S)
    return text(m.group(1)) if m else None
npcs=[]; quests=[]
for fn in sorted(os.listdir(P)):
    h=open(os.path.join(P,fn),errors='ignore').read()
    m=re.match(r'(npcs|quests)_(.+)-(\d+)\.html$', fn)
    if not m: continue
    kind, slug, num = m.group(1), m.group(2), int(m.group(3))
    title=re.search(r'<title>(.*?)</title>', h, flags=re.S)
    name=text(title.group(1)).split(' : ')[0] if title else slug
    body=h[h.find('‹‹'):] if '‹‹' in h else h
    if kind=='npcs':
        nb=h[h.find('side-info'):] if 'side-info' in h else body
        t=text(nb[:20000])
        g=lambda pat: (re.search(pat, t).group(1).strip() if re.search(pat, t) else None)
        d=dict(file=fn, slug=slug, num=num, name=name,
               found_in=g(r'Found in:\s*(.*?)\s+(?:Level|Reaction|Health|Rank)'),
               level=g(r'Level:\s*([\d\s\-]+?)\s+[A-Z]'), rank=g(r'Rank:\s*(\w+)'), eliteness=g(r'Eliteness:\s*(\w+)'),
               reaction=g(r'Reaction:\s*(\w)'), health=g(r'Health:\s*([\d,\s\-]+?)\s+[A-Z]'), last_seen=g(r'Last seen in:\s*(.+?)\s+(?:-->|[A-Z][a-z]+ [A-Z])'))
        large=h[h.find('id="zonemap-large"'):] if 'id="zonemap-large"' in h else ''
        mp=re.search(r'/assets/maps/([a-z0-9_]+)\.jpg', large); d['map']=mp.group(1) if mp else None
        d['pins']=[[int(a),int(b)] for a,b in re.findall(r'class="location_large"[^>]*?title="(-?\d+),(-?\d+)"', h, flags=re.S)]
        if not d['pins']:
            d['pins']=[[int(a),int(b)] for a,b in re.findall(r'class="location_small"[^>]*?title="(-?\d+),(-?\d+)"', h, flags=re.S)]
        drops=[]
        for tab in ('tab-odrop','tab-drop','tab-vdrop'):
            for r in rows(section(h,tab)):
                it=re.search(r'href="/items/[^"]*?-(\d+)">([^<]+)</a>', r)
                ch=re.search(r'([\d.]+)%\s*<br>\s*<span class="small">\((\d+)/(\d+)\)', r)
                if it: drops.append(dict(tab=tab, item_id=int(it.group(1)), item=html.unescape(it.group(2)), chance=float(ch.group(1)) if ch else None, n=int(ch.group(2)) if ch else None, of=int(ch.group(3)) if ch else None))
        d['drops']=drops
        sells=[]
        for r in rows(section(h,'tab-sell')):
            it=re.search(r'href="/items/[^"]*?-(\d+)">([^<]+)</a>', r)
            pr=re.search(r'class="nobr">\s*(.*?)\s*</td>', r, flags=re.S)
            if it: sells.append(dict(item_id=int(it.group(1)), item=html.unescape(it.group(2)), price=text(pr.group(1)) if pr else None))
        d['sells']=sells
        # spells Jabbithole saw this NPC use (Abilities tab): Spell4 id, name, tier, school
        d['abilities']=[]
        for r in rows(section(h,'tab-abi')):
            sp=re.search(r'href="/spells/[^"]*?-(\d+)"[^>]*>([^<]+)</a>', r)
            cells=re.findall(r'<td class="center">(.*?)</td>', r, flags=re.S)
            if sp: d['abilities'].append(dict(spell4_id=int(sp.group(1)), name=html.unescape(sp.group(2)), tier=int(text(cells[0])) if cells and text(cells[0]).isdigit() else None, school=text(cells[1]) if len(cells)>1 else None))
        for tab,key in (('tab-sq','starts'),('tab-cq','completes'),('tab-oq','objective_of')):
            d[key]=sorted({int(q) for q in re.findall(r'href="/quests/[^"]*?-(\d+)"', section(h,tab))})
        npcs.append(d)
    else:
        t=text(body[:30000])
        d=dict(file=fn, id=num, slug=slug, title=name)
        for lab,key in (('Started by:','started_by'),('Completed by:','completed_by')):
            i=body.find(lab)
            seg=body[i:i+3000] if i>=0 else ''
            seg=seg[:seg.find('Last seen')] if 'Last seen' in seg else seg
            d[key]=[dict(slug=s, num=int(n), name=html.unescape(nm)) for s,n,nm in re.findall(r'href="/npcs/([^"]+?)-(\d+)"[^>]*>([^<]+)</a>', seg)]
            d[key+'_call']='Call in' in text(seg)
        inv=section(h,'tab-cr')
        d['involved']=[dict(slug=s, num=int(n), name=html.unescape(nm)) for s,n,nm in re.findall(r'href="/npcs/([^"]+?)-(\d+)"[^>]*>([^<]+)</a>', inv)]
        for lab in ('Level','Minimum level','XP reward'):
            mm=re.search(lab+r':\s*([\d,]+)', t); d[lab.lower().replace(' ','_')]=int(mm.group(1).replace(',','')) if mm else None
        quests.append(d)
json.dump(npcs, open(os.path.join(OUT,'jh_npcs.json'),'w'), indent=0)
json.dump(quests, open(os.path.join(OUT,'jh_quests.json'),'w'), indent=0)
print('npcs',len(npcs),'with pins',sum(1 for n in npcs if n['pins']),'pins',sum(len(n['pins']) for n in npcs),'with drops',sum(1 for n in npcs if n['drops']),'drop rows',sum(len(n['drops']) for n in npcs),'vendors',sum(1 for n in npcs if n['sells']),'sell rows',sum(len(n['sells']) for n in npcs),'with abilities',sum(1 for n in npcs if n['abilities']),'ability rows',sum(len(n['abilities']) for n in npcs))
print('quests',len(quests),'with starter',sum(1 for q in quests if q['started_by']),'with ender',sum(1 for q in quests if q['completed_by']),'xp',sum(1 for q in quests if q['xp_reward']))
