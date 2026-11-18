#!/usr/bin/env python3
from validation_paths import result_directory
import pathlib,subprocess,time,json,re
r=pathlib.Path(__file__).resolve().parents[1];o=result_directory(r,"docs/final-latency100");exe=r/'Builds/ReplicationLab.app/Contents/MacOS/PredictionDemo'
def run(mode,role):
 p=o/f'{mode}-{role}';a=[str(exe),'--pr4-'+role,'--pr4-profile','0','--pr4-port','7894','--pr4-exit-after','30','--pr4-auto-fire','--fix-case','csp','--fix-state',str(p)+'.state','--fix-delay','100','--fix-loss','0','-logFile',str(p)+'.log','-screen-width','800','-screen-height','600']
 if mode=='optimized':a+=['--optimized']
 return subprocess.Popen(a,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
checks={}
for mode in ['baseline','optimized']:
 h=run(mode,'host');p=o/f'{mode}-host.state';deadline=time.monotonic()+45
 while time.monotonic()<deadline:
  if p.exists() and 'server=True' in p.read_text():break
  time.sleep(.1)
 else:raise RuntimeError('host not ready')
 c=run(mode,'client');assert h.wait(timeout=120)==0;assert c.wait(timeout=120)==0
 cl=(o/f'{mode}-client.log').read_text();hl=(o/f'{mode}-host.log').read_text();st=(o/f'{mode}-client.state').read_text()
 checks[f'connected-{mode}']='players=2' in st
 checks[f'correction-{mode}']=any(float(x)>1 for x in re.findall(r'\[PR4\]\[Reconcile\].*?error=([\d.]+)',cl))
 checks[f'shoot-{mode}']='[PR4][Shoot] ACCEPT owner=1' in hl and '[PR4][Shoot] EFFECT' in cl
checks['no-runtime-exceptions']=not any(re.search(r'^[\w.]+Exception:',p.read_text(),re.M) for p in o.glob('*.log'))
(o/'checks.json').write_text(json.dumps(checks,indent=2));print(checks)
if not all(checks.values()):raise SystemExit(1)
