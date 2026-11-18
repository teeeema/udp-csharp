import subprocess,time,pathlib,re,json
root=pathlib.Path(__file__).resolve().parents[1];out=root/'docs/verification';out.mkdir(parents=True,exist_ok=True)
exe=next(p for p in (root/'Builds/ReplicationLab.app/Contents/MacOS').iterdir() if p.is_file())
a=[]
for role in ['host','client']:
 if role=='client':time.sleep(3)
 args=[str(exe),'--pr4-'+role,'--pr4-profile','0','--pr4-port','7785','--optimized','--verify','--pr4-exit-after','37','-logFile',str(out/(role+'.log')),'--pr4-screenshot',str(out/(role+'.png')),'--pr4-screenshot-at','24','-screen-width','1100','-screen-height','720']
 if role=='client':args+=['--pr4-auto-fire']
 a.append(subprocess.Popen(args))
for p in a:p.wait(timeout=90)
s=(out/'host.log').read_text();c=(out/'client.log').read_text()
target=re.search(r'near id=(\d+)',s).group(1)
observations=[(float(t),ids) for t,ids in re.findall(r'\[PR5 VERIFY\] t=([0-9.]+) server=False .*?ids=([^\n]*)',c)]
checks={
 'object_seen_before_hide':any(target+':100;' in ids for t,ids in observations),
 'object_hidden':any(target+':' not in ids for t,ids in observations if 14<t<17),
 'object_reappeared_current_state':any(target+':70;' in ids for t,ids in observations if t>18),
 'object_deleted_on_client':any(target+':' not in ids for t,ids in observations if t>29),
 'client_connected':'connected=True' in c,
 'server_hidden_health_changed':'hidden-health=70' in s,
 'client_received_current_health_after_reentry':'injured=1' in c,
 'server_despawn':'[PR5 VERIFY] despawn' in s,
 'critical_shoot_accepted':'[PR4][Shoot] ACCEPT owner=1' in s,
 'shoot_effect_received':'[PR4][Shoot] EFFECT' in c,
 'no_runtime_exceptions':not any(x in s+c for x in ['NullReferenceException:','InvalidOperationException:','VisibilityChangeException:'])
}
(out/'checks.json').write_text(json.dumps(checks,indent=2));print(checks)
