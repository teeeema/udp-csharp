#!/usr/bin/env python3
from validation_paths import result_directory
"""Live fixed Editor Host + separate fixed macOS clients. Never edits scene assets."""
import pathlib,subprocess,json,time,re
r=pathlib.Path(__file__).resolve().parents[1];o=result_directory(r,"docs/final-dynamic-atomic")
def evaluate(code,name):
 a=['unity','command','eval',code,'--project-path',str(r),'--caller','plugin','--skill','unity-cli','--format','json']
 p=subprocess.run(a,capture_output=True,text=True,timeout=45);(o/(name+'.json')).write_text(p.stdout)
 j=json.loads(p.stdout)
 if not j.get('success') or not j['data']['result'].get('success'):raise RuntimeError(p.stdout)
 return j['data']['result']['result']
def wait(path,test,timeout):
 end=time.monotonic()+timeout
 while time.monotonic()<end:
  if path.exists() and test(path.read_text()):return True
  time.sleep(.1)
 return False
checks={}
for mode in ['baseline','optimized']:
 evaluate('foreach(var p in UnityEngine.Object.FindObjectsByType<PredictionDemo.ReplicatedProp>(UnityEngine.FindObjectsSortMode.None))if(p.Health.Value==55)p.NetworkObject.Despawn();PredictionDemo.ReplicationLab.Instance.Optimized='+('true' if mode=='optimized' else 'false')+';return "configured";',mode+'-setup')
 state=o/f'{mode}-client.state';cmd=[str(r/'Builds/ReplicationLab.app/Contents/MacOS/PredictionDemo'),'--pr4-client','--pr4-port','7901','--pr4-profile','0','--pr4-exit-after','35','--fix-case','idle','--fix-state',str(state),'-logFile',str(o/f'{mode}-client.log'),'-screen-width','800','-screen-height','600']
 if mode=='optimized':cmd+=['--optimized']
 p=subprocess.Popen(cmd,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
 checks[f'connected-{mode}']=wait(state,lambda s:bool(re.search(r'props=\d+ players=2',s)),25)
 spawn=evaluate('var go=UnityEngine.Object.Instantiate(UnityEngine.Resources.Load<UnityEngine.GameObject>("ReplicationProp"));var p=go.GetComponent<PredictionDemo.ReplicatedProp>();p.Kind.Value=2;p.Health.Value=55;p.Position.Value=new UnityEngine.Vector3(0,1,16);go.GetComponent<Unity.Netcode.NetworkObject>().Spawn();return new {id=p.NetworkObjectId,objects=UnityEngine.Object.FindObjectsByType<PredictionDemo.ReplicatedProp>(UnityEngine.FindObjectsSortMode.None).Length};',mode+'-spawn')
 target=spawn['id'];checks[f'created-{mode}']=spawn['objects']==51 and wait(state,lambda s:f'{target}:2:55:' in s,8)
 samples_before=sum(l.startswith('SAMPLE') for l in state.read_text().splitlines())
 evaluate(f'foreach(var p in UnityEngine.Object.FindObjectsByType<PredictionDemo.ReplicatedProp>(UnityEngine.FindObjectsSortMode.None))if(p.NetworkObjectId=={target})p.NetworkObject.Despawn();return UnityEngine.Object.FindObjectsByType<PredictionDemo.ReplicatedProp>(UnityEngine.FindObjectsSortMode.None).Length;',mode+'-despawn')
 checks[f'deleted-{mode}']=wait(state,lambda s:len([l for l in s.splitlines() if l.startswith('SAMPLE')])>samples_before and f'{target}:2:55:' not in [l for l in s.splitlines() if l.startswith('SAMPLE')][-1],8)
 try:code=p.wait(timeout=100)
 except subprocess.TimeoutExpired:p.kill();p.wait();raise
 if code:raise RuntimeError(f'Client exit {code}')
(o/'checks.json').write_text(json.dumps(checks,indent=2));print(checks)
if not all(checks.values()):raise SystemExit(1)
