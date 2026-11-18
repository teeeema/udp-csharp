#!/usr/bin/env python3
"""Actual macOS players, opt-in verification; never generates simulated measurements."""
import argparse,csv,json,math,pathlib,re,subprocess,time,concurrent.futures
ROOT=pathlib.Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser();parser.add_argument('--output',default='docs/final-results');args=parser.parse_args()
OUT=(ROOT/args.output).resolve();OUT.mkdir(parents=True,exist_ok=False)
EXE=ROOT/'Builds/ReplicationLab.app/Contents/MacOS/PredictionDemo'
def launch(case,mode,role,label,duration,port,delay=0,loss=0):
 prefix=OUT/f'{case}-{mode}-{label}'
 cmd=[str(EXE),'--pr4-'+role,'--pr4-profile','0','--pr4-port',str(port),'--pr4-exit-after',str(duration),'--fix-case',case,'--fix-state',str(prefix)+'.state','--metrics',str(prefix)+'.csv','-logFile',str(prefix)+'.log','-screen-width','960','-screen-height','640']
 if mode=='optimized':cmd+=['--optimized']
 if case=='csp':cmd+=['--pr4-auto-fire']
 if delay or loss:cmd+=['--fix-delay',str(delay),'--fix-loss',str(loss)]
 if case=='traffic' and role=='client':cmd+=['--pr4-screenshot',str(prefix)+'.png','--pr4-screenshot-at','60']
 return subprocess.Popen(cmd,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
def ready(case,mode,label,process):
 path=OUT/f'{case}-{mode}-{label}.state';deadline=time.monotonic()+45
 while time.monotonic()<deadline:
  if process.poll() is not None:raise RuntimeError('Host exited before ready')
  if path.exists() and 'server=True' in path.read_text():return
  time.sleep(.1)
 raise RuntimeError('Host readiness timeout')
def finish(p,timeout):
 try:code=p.wait(timeout=timeout)
 except subprocess.TimeoutExpired:p.kill();p.wait();raise
 if code!=0:raise RuntimeError(f'Player exit {code}')
def pair(case,mode,duration,port,delay=0,loss=0):
 h=launch(case,mode,'host','host',duration,port,delay,loss);ready(case,mode,'host',h);time.sleep(1);c=launch(case,mode,'client','client',duration,port,delay,loss)
 finish(h,duration+90);finish(c,duration+90)
def restart(mode,port):
 h=launch('restart',mode,'host','host',43,port);ready('restart',mode,'host',h);start=time.monotonic();clients=[]
 for i,at in enumerate([2,12,22,32]):
  time.sleep(max(0,at-(time.monotonic()-start)))
  clients.append(launch('restart',mode,'client',f'client{i}',7,port))
 finish(h,90)
 for client in clients:finish(client,90)
def samples(path):
 out=[]
 for line in path.read_text().splitlines():
  if not line.startswith('SAMPLE'):continue
  d={k:float(v) for k,v in re.findall(r'\b(t|props|players|tx|rx|materials|memory)=([\d.-]+)',line)}
  d['player']={int(i):{'pos':tuple(map(float,p.split(','))),'shots':int(sh),'rejected':int(rej)} for i,p,sh,rej in re.findall(r'(\d+):\(([^)]+)\):shots=(\d+):rejected=(\d+)',line)}
  d['prop']={int(i):{'kind':int(k),'hp':int(h)} for i,k,h in re.findall(r'(\d+):(\d+):(\d+):\(',line)};out.append(d)
 return out
def stats(rows,a,b):
 rs=[s for s in rows if a<=s['t']<=b]
 if len(rs)<2 or any(s['players']!=2 for s in rs):raise RuntimeError(f'Invalid comparison window {a}-{b}')
 dt=rs[-1]['t']-rs[0]['t']
 return {'samples':len(rs),'seconds':dt,'tx_KBps':(rs[-1]['tx']-rs[0]['tx'])/dt/1000,'rx_KBps':(rs[-1]['rx']-rs[0]['rx'])/dt/1000,'objects':sorted(set(int(x['props']) for x in rs))}
# Performance pairs run alone. Diagnostic suites below may share machine load, not performance data.
for mode in ['baseline','optimized']:pair('traffic',mode,120,7880)
windows={'idle':(8,18),'client_move':(21,31),'both_move':(33,43),'shoot':(45,55),'target_state':(57,67),'zones':(69,95),'combined':(101,115)}
traffic={}
for label,(a,b) in windows.items():
 traffic[label]={mode:{role:stats(samples(OUT/f'traffic-{mode}-{role}.state'),a,b) for role in ['host','client']} for mode in ['baseline','optimized']}
 x=traffic[label]['baseline']['host']['tx_KBps'];y=traffic[label]['optimized']['host']['tx_KBps'];traffic[label]['reduction_percent']=(x-y)/x*100
(OUT/'traffic-summary.json').write_text(json.dumps(traffic,indent=2))
def suite(mode,port):
 pair('speed',mode,30,port)
 restart(mode,port)
 pair('materials',mode,45,port)
 pair('walk',mode,42,port)
 pair('csp',mode,30,port,175,5)
 pair('occlusion',mode,20,port)
with concurrent.futures.ThreadPoolExecutor(max_workers=1) as pool:
 futures=[pool.submit(suite,'baseline',7881),pool.submit(suite,'optimized',7882)]
 for f in futures:f.result()
checks={}
for mode in ['baseline','optimized']:
 host=samples(OUT/f'speed-{mode}-host.state');sp=[s for s in host if s['t']>=6 and 1 in s['player']]
 jumps=[(math.dist(a['player'][1]['pos'],b['player'][1]['pos']),b['t']-a['t']) for a,b in zip(sp,sp[1:]) if b['t']>a['t']]
 checks[f'speed-{mode}']=bool(jumps) and all(distance<=4*dt+.401 for distance,dt in jumps) and max(s['player'][1]['rejected'] for s in sp)>400
 text=(OUT/f'restart-{mode}-host.state').read_text();checks[f'restart-{mode}']=text.count('restart-before props=50')==3 and text.count('restart-after props=50')==3
 for i in range(4):
  rs=samples(OUT/f'restart-{mode}-client{i}.state');checks[f'reconnect-{mode}-{i}']=any(s['players']==2 and (s['props']==50 if mode=='baseline' else 0<s['props']<=50) for s in rs)
 mat=samples(OUT/f'materials-{mode}-client.state');warm=[s for s in mat if s['t']>=5];end=[s for s in mat if s['t']>=29]
 checks[f'materials-{mode}']=bool(end and warm) and max(s['materials'] for s in end)<=min(s['materials'] for s in warm)+2 and 'hide-show-complete cycles=100' in (OUT/f'materials-{mode}-host.state').read_text()
 walk=samples(OUT/f'walk-{mode}-client.state');near=[s for s in walk if 2<s['t']<5];far=[s for s in walk if 14<s['t']<18];back=[s for s in walk if 29<s['t']<33]
 moved=any(1 in s['player'] and s['player'][1]['pos'][2]<-45 for s in far)
 checks[f'walk-{mode}']=bool(near and far and back and moved) and (min(s['props'] for s in far)<min(s['props'] for s in near) and max(s['props'] for s in back)>min(s['props'] for s in far) if mode=='optimized' else all(s['props']==50 for s in near+far+back))
 checks[f'walk-current-state-{mode}']=any(any(p['hp']==70 for p in s['prop'].values()) for s in back)
 text=(OUT/f'csp-{mode}-client.log').read_text();errors=[float(x) for x in re.findall(r'\[PR4\]\[Reconcile\].*?error=([\d.]+)m',text)]
 checks[f'csp-loss-{mode}']=any(x>1 for x in errors)
 oc=(OUT/f'occlusion-{mode}-host.state').read_text();checks[f'occlusion-{mode}']='open=False before=100 after=100' in oc and 'open=True before=100 after=90' in oc
 remote=samples(OUT/f'occlusion-{mode}-client.state');checks[f'health-replication-{mode}']=any(any(p['hp']==90 for p in s['prop'].values()) for s in remote)
logs=''.join(p.read_text() for p in OUT.glob('*.log'));checks['no-runtime-exceptions']=not re.search(r'^[\w.]+Exception:',logs,re.M)
(OUT/'regression-checks.json').write_text(json.dumps(checks,indent=2))
print(json.dumps({'checks':checks,'pass':sum(checks.values()),'total':len(checks)},indent=2))
if not all(checks.values()):raise SystemExit(1)
