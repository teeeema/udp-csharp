#!/usr/bin/env python3
"""Package the verified project and macOS app. Never overwrite the old submission/backup."""
import argparse,pathlib,hashlib,json,zipfile,stat,os
root=pathlib.Path(__file__).resolve().parents[1]
p=argparse.ArgumentParser();p.add_argument('--replace',action='store_true');p.add_argument('--output',default=str(root.parent/'Practice5Replication_Final.zip'));args=p.parse_args();out=pathlib.Path(args.output).resolve()
if out.name in ['Practice5Replication_Submission.zip','Practice5Replication_PreFix_Backup.zip']:raise SystemExit('Protected historical archive')
if out.exists() and not args.replace:raise SystemExit('Use a new filename or explicitly --replace for your generated final ZIP')
app=root/'Builds/ReplicationLab.app'
if not app.exists():raise SystemExit('macOS build missing')
def digest(p):
 h=hashlib.sha256()
 with p.open('rb') as f:
  for b in iter(lambda:f.read(1024*1024),b''):h.update(b)
 return h.hexdigest()
files=[]
for folder in ['Assets','Packages','ProjectSettings','LegacyUdp','Tools','docs','Builds/ReplicationLab.app']:
 for f in (root/folder).rglob('*'):
  if f.name in ['.DS_Store','editor.log'] or '__pycache__' in f.parts:continue
  rel=f.relative_to(root)
  if rel.parts[0]=='LegacyUdp' and any(x in rel.parts for x in ['bin','obj']):continue
  if f.is_symlink() or f.is_file():files.append(f)
files += [root/'README.md',root/'.gitignore']
manifest={}
for f in files:
 rel=str(f.relative_to(root))
 if rel=='docs/Final_Manifest.json':continue
 if f.is_symlink():
  target=os.readlink(f)
  if not (f.parent/target).resolve().is_relative_to(root):raise SystemExit('External symlink must be reviewed: '+rel)
  manifest[rel]={'type':'symlink','target':target}
 else:manifest[rel]={'type':'file','sha256':digest(f),'mode':stat.S_IMODE(f.stat().st_mode)}
mp=root/'docs/Final_Manifest.json';mp.write_text(json.dumps({'files':manifest,'note':'Manifest excludes its own digest; source project plus macOS .app, no Unity/.NET caches.'},indent=2));files=[f for f in files if f!=mp]+[mp]
with zipfile.ZipFile(out,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
 for f in files:
  name=root.name+'/'+str(f.relative_to(root))
  if f.is_symlink():
   info=zipfile.ZipInfo(name);info.create_system=3;info.external_attr=(stat.S_IFLNK|0o777)<<16;z.writestr(info,os.readlink(f))
  else:z.write(f,name)
with zipfile.ZipFile(out) as z:
 bad=z.testzip()
 if bad:raise SystemExit('CRC failure: '+bad)
sha=digest(out);out.with_suffix('.sha256').write_text(sha+'  '+out.name+'\n')
print(json.dumps({'archive':str(out),'bytes':out.stat().st_size,'sha256':sha,'entries':len(files),'crc_ok':True},indent=2))
