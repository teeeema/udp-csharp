#!/usr/bin/env python3
"""Launch actual macOS players; CSV values come exclusively from transport callbacks."""
import subprocess,time,pathlib,csv,json
root=pathlib.Path(__file__).resolve().parents[1]
exe=root/'Builds/ReplicationLab.app/Contents/MacOS/PredictionDemo'
if not exe.exists():
 candidates=list((root/'Builds/ReplicationLab.app/Contents/MacOS').glob('*'))
 exe=next(p for p in candidates if p.is_file())
out=root/'docs/new-idle-measurements';out.mkdir(parents=True,exist_ok=False)
for mode in ['baseline','optimized']:
 flags=['--optimized'] if mode=='optimized' else []
 procs=[]
 for role in ['host','client']:
  if role=='client':time.sleep(3)
  cmd=[str(exe),'--pr4-'+role,'--pr4-profile','0','--pr4-exit-after','48','--metrics',str(out/f'{mode}-{role}.csv'),'-logFile',str(out/f'{mode}-{role}.log'),'-screen-width','960','-screen-height','640']+flags
  procs.append(subprocess.Popen(cmd))
 for p in procs:
  code=p.wait(timeout=100)
  if code!=0:raise RuntimeError(f"Player failed: exit {code}")
summary={}
for mode in ['baseline','optimized']:
 summary[mode]={}
 for role in ['host','client']:
  rows=list(csv.DictReader((out/f'{mode}-{role}.csv').open()))
  samples=[r for r in rows if 10<=float(r['seconds'])<=40]
  if not samples or any(r['clients']!='2' for r in samples):raise RuntimeError(f'Incomplete connection window: {mode} {role}')
  summary[mode][role]={'samples':len(samples),'tx_KBps':sum(float(r['tx_KBps']) for r in samples)/len(samples),'rx_KBps':sum(float(r['rx_KBps']) for r in samples)/len(samples),'objects':sorted(set(r['objects'] for r in samples)),'clients':sorted(set(r['clients'] for r in samples))}
a=summary['baseline']['host']['tx_KBps'];b=summary['optimized']['host']['tx_KBps'];summary['reduction_percent']=(a-b)/a*100
(out/'summary.json').write_text(json.dumps(summary,indent=2));print(json.dumps(summary,indent=2))
