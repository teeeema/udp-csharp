#!/usr/bin/env python3
from validation_paths import result_directory
"""Retest the connection-dependent attempts; movement/damage criteria are unchanged."""
import pathlib,subprocess,time,json,math,re
ROOT=pathlib.Path(__file__).resolve().parents[1];OUT=result_directory(ROOT,"docs/final-targeted")
EXE=ROOT/'Builds/ReplicationLab.app/Contents/MacOS/PredictionDemo'
def launch(case,mode,role,label,duration):
 p=OUT/f'{case}-{mode}-{label}'
 cmd=[str(EXE),'--pr4-'+role,'--pr4-port','7891','--pr4-profile','0','--pr4-exit-after',str(duration),'--fix-case',case,'--fix-state',str(p)+'.state','--metrics',str(p)+'.csv','-logFile',str(p)+'.log','-screen-width','800','-screen-height','600']
 if mode=='optimized':cmd+=['--optimized']
 if case=='csp':cmd+=['--fix-delay','175','--fix-loss','5','--pr4-auto-fire']
 return subprocess.Popen(cmd,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
def ready(case,mode,p):
 path=OUT/f'{case}-{mode}-host.state';deadline=time.monotonic()+45
 while time.monotonic()<deadline:
  if p.poll() is not None:raise RuntimeError('Host exited before ready')
  if path.exists() and 'server=True' in path.read_text():return
  time.sleep(.1)
 raise RuntimeError('Host not ready')
def finish(p,timeout=120):
 try:code=p.wait(timeout=timeout)
 except subprocess.TimeoutExpired:p.kill();p.wait();raise
 if code:raise RuntimeError(f'player exit {code}')
for mode in ['baseline','optimized']:
 for case,duration in [('speed',30),('restart',43),('csp',30)]:
  print('starting',case,mode,flush=True);h=launch(case,mode,'host','host',duration);ready(case,mode,h)
  if case=='restart':
   start=time.monotonic();clients=[]
   for i,at in enumerate([2,12,22,32]):
    time.sleep(max(0,at-(time.monotonic()-start)));clients.append(launch(case,mode,'client',f'client{i}',7))
  else:time.sleep(1);clients=[launch(case,mode,'client','client',duration)]
  finish(h)
  for c in clients:finish(c)
def read(path):
 rows=[]
 for l in path.read_text().splitlines():
  if not l.startswith('SAMPLE'):continue
  x={k:float(v) for k,v in re.findall(r'\b(t|props|players|materials|memory)=([\d.-]+)',l)}
  x['p']={int(i):{'pos':tuple(map(float,p.split(','))),'shots':int(sh),'rejected':int(n)} for i,p,sh,n in re.findall(r'(\d+):\(([^)]+)\):shots=(\d+):rejected=(\d+)',l)};rows.append(x)
 return rows
checks={};details={}
for mode in ['baseline','optimized']:
 ss=[s for s in read(OUT/f'speed-{mode}-host.state') if s['t']>=6 and 1 in s['p']]
 jumps=[(math.dist(a['p'][1]['pos'],b['p'][1]['pos']),b['t']-a['t']) for a,b in zip(ss,ss[1:]) if b['t']>a['t']]
 checks[f'speed-{mode}']=bool(jumps) and all(d<=4*t+.401 for d,t in jumps) and max(s['p'][1]['rejected'] for s in ss)>400
 speed_client=read(OUT/f'speed-{mode}-client.state');owned=[r['p'][1]['pos'] for r in speed_client if 1 in r['p']];
 checks[f'speed-correction-{mode}']=bool(owned and ss) and math.dist(owned[-1],ss[-1]['p'][1]['pos'])<.101
 details[f'speed-{mode}']={'max_half_second_displacement':max([d for d,t in jumps] or [0]),'rejected':max([s['p'][1]['rejected'] for s in ss] or [0])}
 text=(OUT/f'restart-{mode}-host.state').read_text();checks[f'restart-{mode}']=text.count('restart-before props=50')==3 and text.count('restart-after props=50')==3
 for i in range(4):checks[f'reconnect-{mode}-{i}']=any(s['players']==2 and (s['props']==50 if mode=='baseline' else 0<s['props']<=50) for s in read(OUT/f'restart-{mode}-client{i}.state'))
 cl=(OUT/f'csp-{mode}-client.log').read_text();hl=(OUT/f'csp-{mode}-host.log').read_text()
 errors=[float(x) for x in re.findall(r'\[PR4\]\[Reconcile\].*?error=([\d.]+)m',cl)];checks[f'csp-loss-{mode}']=any(x>1 for x in errors)
 checks[f'delayed-shoot-{mode}']='[PR4][Shoot] ACCEPT owner=1' in hl and '[PR4][Shoot] EFFECT' in cl
 details[f'csp-{mode}']={'max_correction_m':max(errors or [0])}
logs=''.join(p.read_text() for p in OUT.glob('*.log'));checks['no-runtime-exceptions']=not re.search(r'^[\w.]+Exception:',logs,re.M)
(OUT/'checks.json').write_text(json.dumps(checks,indent=2));(OUT/'details.json').write_text(json.dumps(details,indent=2));print(checks)
if not all(checks.values()):raise SystemExit(1)
