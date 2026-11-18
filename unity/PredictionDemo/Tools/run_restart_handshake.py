#!/usr/bin/env python3
from validation_paths import result_directory
import pathlib,subprocess,time,json,re
r=pathlib.Path(__file__).resolve().parents[1];out=result_directory(r,"docs/final-restart-handshake");exe=r/'Builds/ReplicationLab.app/Contents/MacOS/PredictionDemo'
def launch(mode,role,label,duration):
 p=out/f'{mode}-{label}';cmd=[str(exe),'--pr4-'+role,'--pr4-port','7892','--pr4-profile','0','--pr4-exit-after',str(duration),'--fix-case','restart','--fix-state',str(p)+'.state','-logFile',str(p)+'.log','-screen-width','800','-screen-height','600']
 if mode=='optimized':cmd+=['--optimized']
 return subprocess.Popen(cmd,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
def wait_for(path,predicate,timeout):
 deadline=time.monotonic()+timeout
 while time.monotonic()<deadline:
  if path.exists() and predicate(path.read_text()):return True
  time.sleep(.1)
 return False
checks={};events=[]
for mode in ['baseline','optimized']:
 host=launch(mode,'host','host',70);hp=out/f'{mode}-host.state';checks[f'host-ready-{mode}']=wait_for(hp,lambda s:'server=True' in s,30);clients=[]
 for i in range(4):
  if i:checks[f'restart-{mode}-{i}']=wait_for(hp,lambda s:s.count('restart-after props=50')>=i,20)
  c=launch(mode,'client',f'client{i}',20 if i==3 else 12);clients.append(c);p=out/f'{mode}-client{i}.state'
  def connected(s):
   for line in s.splitlines():
    if line.startswith('SAMPLE') and 'players=2 ' in line:
     count=int(re.search(r'props=(\d+)',line)[1])
     if count==50 if mode=='baseline' else count>0:return True
   return False
  checks[f'client-{mode}-{i}']=wait_for(p,connected,18)
  events.append({'mode':mode,'wave':i,'connected':checks[f'client-{mode}-{i}']})
  (out/'progress.json').write_text(json.dumps({'checks':checks,'events':events},indent=2))
 for c in clients:
  try:c.wait(timeout=60)
  except subprocess.TimeoutExpired:c.kill();c.wait()
 try:host.wait(timeout=90)
 except subprocess.TimeoutExpired:host.kill();host.wait()
checks['no-runtime-exceptions']=not any(re.search(r'^[\w.]+Exception:',p.read_text(),re.M) for p in out.glob('*.log'))
(out/'checks.json').write_text(json.dumps(checks,indent=2));print(checks)
if not all(checks.values()):raise SystemExit(1)
