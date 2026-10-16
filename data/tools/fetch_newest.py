# For each NPC slug / quest slug: ask the Wayback CDX for every 200 capture of jabbithole pages under that slug,
# keep the NEWEST capture per URL, and download it into pages2/. Polite: one request at a time with backoff.
import os, re, time, json, urllib.request, urllib.parse
A='/a/jabbithole'; OUT=A+'/pages2'; os.makedirs(OUT, exist_ok=True)
UA={'User-Agent':'Mozilla/5.0 (research; personal WildStar emulator project)'}
def get(url, tries=7):
    delay=5
    for _ in range(tries):
        try:
            with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=90) as r:
                return r.read()
        except Exception as e:
            if getattr(e,'code',None)==404: return None
            time.sleep(delay); delay=min(delay*2,300)
    return None
def cdx(prefix):
    q=urllib.parse.urlencode({'url':f'jabbithole.com/{prefix}','matchType':'prefix','output':'txt','fl':'original,timestamp','filter':'statuscode:200','limit':'5000'})
    b=get('https://web.archive.org/cdx/search/cdx?'+q)
    if not b or b.lstrip().startswith(b'<'): return None
    return [l.split() for l in b.decode(errors='ignore').splitlines() if l.strip()]
state_path=os.environ.get('STATE', A+'/newest_state.json')
state=json.load(open(state_path)) if os.path.exists(state_path) else {'cdx_done':[], 'targets':{}}
slug_file=os.environ.get('SLUGS')  # optional: NPC slug list for another pass, quests are skipped then
jobs=[('npcs/'+s+'-', r'/npcs/'+re.escape(s)+r'-\d+(-veteran)?$') for s in open(slug_file or A+'/slugs.txt').read().split()]
if not slug_file: jobs+=[('quests/'+s, r'/quests/'+re.escape(s)+r'$') for s in open(A+'/quest_slugs.txt').read().split()]
for prefix,pat in jobs:
    if prefix in state['cdx_done']: continue
    rows=cdx(prefix)
    if rows is None: print('cdx failed', prefix, flush=True); time.sleep(10); continue
    for row in rows:
        if len(row)<2: continue
        u,ts=row[0],row[1]
        path=re.sub(r'^https?://(www\.)?jabbithole\.com(:80)?','',u)
        if not re.search(pat, path): continue
        if path not in state['targets'] or ts>state['targets'][path][1]: state['targets'][path]=[u,ts]
    state['cdx_done'].append(prefix); json.dump(state, open(state_path,'w'))
    time.sleep(1.5)
print('cdx done; pages', len(state['targets']), flush=True)
done=fail=0
for i,(path,(u,ts)) in enumerate(sorted(state['targets'].items())):
    name=path.strip('/').replace('/','_')+'.html'; p=os.path.join(OUT,name)
    if os.path.exists(p) and os.path.getsize(p)>2000: continue
    b=get(f'https://web.archive.org/web/{ts}id_/{u}')
    if b: open(p+'.tmp','wb').write(b); os.replace(p+'.tmp',p); done+=1
    else: fail+=1
    if i%50==0: print(time.strftime('%H:%M:%S'), i, len(state['targets']), 'ok', done, 'fail', fail, flush=True)
    time.sleep(1.5)
print('DONE ok', done, 'fail', fail, flush=True)
