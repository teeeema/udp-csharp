#!/usr/bin/env python3
from validation_paths import result_directory
import pathlib,subprocess,time,json,re,math
r=pathlib.Path(__file__).resolve().parents[1];out=result_directory(r,"docs/final-soak");exe=r/'Builds/ReplicationLab.app/Contents/MacOS/PredictionDemo'
def launch(mode,role,label,duration):
 p=out/f'{mode}-{label}';cmd=[str(exe),'--pr4-'+role,'--pr4-profile','0','--pr4-port','7893','--pr4-exit-after',str(duration),'--fix-case','traffic' if role=='host' else 'idle','--fix-state',str(p)+'.state','-logFile',str(p)+'.log','-screen-width','800','-screen-height','600']
 if mode=='optimized':cmd+=['--optimized']
 return subprocess.Popen(cmd,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
def ready(mode,p):
 path=out/f'{mode}-host.state';deadline=time.monotonic()+45
 while time.monotonic()<deadline:
  if p.poll() is not None:raise RuntimeError('Host exited')
  if path.exists() and 'server=True' in path.read_text():return
  time.sleep(.1)
 raise RuntimeError('Host readiness timeout')
def finish(p):
 try:code=p.wait(timeout=260)
 except subprocess.TimeoutExpired:p.kill();p.wait();raise
 if code:raise RuntimeError(f'Unexpected exit {code}')
for mode in ['baseline','optimized']:
 print('starting',mode,flush=True);h=launch(mode,'host','host',185);ready(mode,h);start=time.monotonic();time.sleep(1);c1=launch(mode,'client','c1',185)
 time.sleep(max(0,50-(time.monotonic()-start)));c2=launch(mode,'client','c2',125)
 time.sleep(max(0,65-(time.monotonic()-start)));c1.kill();c1.wait();(out/f'{mode}-kill.json').write_text(json.dumps({'intentional_sigkill':True,'exit_code':c1.returncode}))
 time.sleep(max(0,70-(time.monotonic()-start)));c3=launch(mode,'client','reconnect',112)
 finish(h);finish(c2);finish(c3)
def rows(path):
 result=[]
 for l in path.read_text().splitlines():
  if not l.startswith('SAMPLE'):continue
  d={k:float(v) for k,v in re.findall(r'\b(t|props|players|materials|memory)=([\d.-]+)',l)}
  d['p']={int(i):tuple(map(float,p.split(','))) for i,p in re.findall(r'(\d+):\(([^)]+)\):shots=',l)};result.append(d)
 return result
checks={};details={}
for mode in ['baseline','optimized']:
 h=rows(out/f'{mode}-host.state');a=rows(out/f'{mode}-c1.state');b=rows(out/f'{mode}-c2.state');c=rows(out/f'{mode}-reconnect.state')
 checks[f'long-session-{mode}']=max(x['t'] for x in h)>=180 and all(x['props']==50 for x in h if x['t']>2)
 checks[f'two-remote-clients-{mode}']=any(x['players']>=3 for x in h) and any(x['players']>=3 for x in b)
 checks[f'abrupt-reconnect-{mode}']=any(x['players']>=3 for x in c) and any(set(x['p'])=={0,2,3} for x in h if 100<x['t']<160)
 owned=[x['p'][1] for x in a if 1 in x['p']];other=[x['p'][0] for x in a if 0 in x['p']]
 checks[f'host-only-movement-{mode}']=bool(owned and other) and max(math.dist(p,owned[0]) for p in owned)<.01 and max(math.dist(p,other[0]) for p in other)>5
 details[mode]={'seconds':max(x['t'] for x in h),'max_players':max(x['players'] for x in h),'material_range_host':[min(x['materials'] for x in h),max(x['materials'] for x in h)],'memory_start':h[5]['memory'],'memory_end':h[-1]['memory']}
logs=''.join(p.read_text() for p in out.glob('*.log'));checks['no-runtime-exceptions']=not re.search(r'^[\w.]+Exception:',logs,re.M)
(out/'checks.json').write_text(json.dumps(checks,indent=2));(out/'details.json').write_text(json.dumps(details,indent=2));print(checks)
if not all(checks.values()):raise SystemExit(1)
