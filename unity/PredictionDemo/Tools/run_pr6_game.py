#!/usr/bin/env python3
"""Real native players through a loopback UDP relay; no synthetic network packets.

The JSONL records EXACT UDP payloads observed by recvfrom, not a BPF/Wireshark
capture. Per-client upstream sockets preserve independent transport endpoints.
"""
import argparse, base64, csv, hashlib, json, pathlib, re, selectors, socket, subprocess, time

root = pathlib.Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument('--output', required=True)
parser.add_argument('--app', default='Builds/ReplicationLab.app', help='macOS .app bundle or Windows .exe')
parser.add_argument('--secure', action='store_true')
parser.add_argument('--optimized', action='store_true')
parser.add_argument('--clients', type=int, default=1)
parser.add_argument('--duration', type=int, default=30)
parser.add_argument('--impair', action='store_true', help='deterministic drop/duplicate/corrupt after handshake')
parser.add_argument('--replication-verify', action='store_true')
parser.add_argument('--transport-delay', type=int, default=0)
parser.add_argument('--headless', action='store_true', help='run the actual player without graphics; no screenshot is claimed')
args = parser.parse_args()
app = (root / args.app).resolve()
if app.suffix.lower() == '.exe':
    exe = app
    assembly = app.parent / (app.stem + '_Data') / 'Managed/PredictionDemo.Runtime.dll'
else:
    exe = next(p for p in (app / 'Contents/MacOS').iterdir() if p.is_file())
    assembly = app / 'Contents/Resources/Data/Managed/PredictionDemo.Runtime.dll'
out = (root / args.output).resolve(); out.mkdir(parents=True, exist_ok=False)
(out/'build-fingerprint.json').write_text(json.dumps({'runtime_sha256': hashlib.sha256(assembly.read_bytes()).hexdigest(),
    'app':args.app,'scenario':vars(args)},indent=2))
sel = selectors.DefaultSelector(); downstream = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
downstream.bind(('127.0.0.1', 7906)); downstream.setblocking(False); sel.register(downstream, selectors.EVENT_READ, None)
upstreams = {}; processes = []; records = []; started = time.monotonic(); spawn_client = False
faults = dict(dropped=0, duplicated=0, corrupted=0)
capture = (out/'relay-datagrams.jsonl').open('w')

def launch(role, index=0):
    name = role if role == 'host' else f'client{index}'
    cmd = [str(exe), '--pr4-'+role, '--pr4-port', '7907' if role == 'host' else '7906',
           '--pr4-profile', '0', '--pr4-exit-after', str(args.duration), '--metrics', str(out/(name+'.csv')),
           '--fix-case', 'idle' if args.replication_verify else 'csp', '--fix-state', str(out/(name+'.state')), '--pr4-auto-fire',
           '-logFile', str(out/(name+'.log')), '-screen-width', '800', '-screen-height', '600']
    if args.headless: cmd += ['-batchmode','-nographics']
    else:
        cmd += ['--pr4-screenshot', str(out/(name+'.png')), '--pr4-screenshot-at', str(args.duration-3)]
        if exe.suffix.lower() == '.exe':
            cmd += ['-screen-fullscreen', '0']
    if role == 'client': cmd += ['--pr6-address', 'localhost']
    if args.optimized: cmd += ['--optimized']
    if args.secure: cmd += ['--pr6-wait-players', str(args.clients+1)]
    if args.replication_verify: cmd += ['--verify']
    if args.transport_delay: cmd += ['--fix-delay', str(args.transport_delay)]
    (out/(name+'-command.json')).write_text(json.dumps(cmd, ensure_ascii=False, indent=2))
    processes.append(subprocess.Popen(cmd, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL))

try:
    launch('host')
    while True:
        elapsed = time.monotonic()-started
        if elapsed > 3 and not spawn_client:
            for i in range(args.clients): launch('client', i+1)
            spawn_client = True
        for key, _ in sel.select(.01):
            try:
                packet, source = key.fileobj.recvfrom(65536)
            except ConnectionResetError as error:
                # Winsock may report the previous send to a closed UDP port on recvfrom.
                if getattr(error, 'winerror', None) == 10054:
                    continue
                raise
            from_server = key.fileobj is not downstream
            if not from_server:
                if source not in upstreams:
                    upstream = socket.socket(socket.AF_INET, socket.SOCK_DGRAM); upstream.bind(('127.0.0.1', 0)); upstream.setblocking(False)
                    upstreams[source] = upstream; sel.register(upstream, selectors.EVENT_READ, source)
                dest_socket, dest = upstreams[source], ('127.0.0.1', 7907)
            else: dest_socket, dest = downstream, key.data
            secure = len(packet) >= 20 and packet[:4] == b'DSL6' and packet[5] == 11
            record = dict(seconds=round(elapsed, 6), direction='s2c' if from_server else 'c2s',
                          size=len(packet), payload_base64=base64.b64encode(packet).decode(), encrypted=secure)
            records.append(record); capture.write(json.dumps(record)+'\n')
            n = len(records)
            # Only affect encrypted DATA after initial connection. Confirmations are untouched.
            if args.impair and secure and packet[6] == 0 and elapsed > 7:
                if n % 23 == 0: faults['dropped'] += 1; continue
                if n % 37 == 0:
                    bad = bytearray(packet); bad[-1] ^= 1; dest_socket.sendto(bad, dest); faults['corrupted'] += 1
                if n % 19 == 0: dest_socket.sendto(packet, dest); faults['duplicated'] += 1
            dest_socket.sendto(packet, dest)
        if spawn_client and all(p.poll() is not None for p in processes): break
        if elapsed > args.duration+150: raise TimeoutError('native players did not finish')
finally:
    capture.close()
    for p in processes:
        if p.poll() is None: p.terminate(); p.wait(timeout=15)
    for upstream in upstreams.values(): upstream.close()
    downstream.close(); sel.close()

logs = {p.stem:p.read_text(errors='replace') for p in out.glob('*.log')}
states = {p.stem:p.read_text(errors='replace') for p in out.glob('*.state')}
host = logs['host']; all_logs = '\n'.join(logs.values())
checks = {'exit_codes_zero': all(p.returncode == 0 for p in processes),
          'actual_udp_observed': bool(records),
          'no_runtime_exceptions': not re.search(r'^\S*Exception:', all_logs, re.M)}
for i in range(1, args.clients+1):
    name = f'client{i}'
    identity = re.search(r'\[PR6\] LOCAL_CLIENT id=(\d+)', logs[name])
    owner = identity.group(1) if identity else str(i)
    checks[name+'_connected'] = f'players={args.clients+1}' in states[name]
    checks[name+'_shoot_accepted'] = bool(re.search(rf'\[PR4\]\[Shoot\] ACCEPT owner={owner} ', host))
    checks[name+'_effect'] = '[PR4][Shoot] EFFECT' in logs[name]
    if not args.replication_verify:
        errors = re.findall(r'\[PR4\]\[Reconcile\].*?error=(\d+(?:[.,]\d+)?)', logs[name])
        checks[name+'_reconciliation'] = any(float(error.replace(',', '.')) > 1 for error in errors)
        checks[name+'_movement'] = len(set(re.findall(rf'playerstate=.*?{owner}:\(([^)]+)\)', states[name]))) > 3
accepted = re.findall(r'\[PR4\]\[Shoot\] ACCEPT owner=(\d+) shot=(\d+)', host)
checks['shoot_sequences_unique'] = bool(accepted) and len(accepted) == len(set(accepted))
if args.secure:
    checks['wire_only_handshake_or_encrypted'] = all(base64.b64decode(r['payload_base64'])[:4] == b'DSL6' and
        base64.b64decode(r['payload_base64'])[5] in (10, 11) for r in records)
    checks['handshake_ok'] = all('[PR6] HANDSHAKE_OK' in s for s in logs.values())
    checks['datagram_mtu'] = max(r['size'] for r in records) <= 1200
    if args.impair:
        checks['faults_injected'] = all(x > 0 for x in faults.values())
        checks['auth_fail_observed'] = '[PR6] AUTH_FAIL' in all_logs
        checks['replay_drop_observed'] = '[PR6] REPLAY_DROP' in all_logs
if args.transport_delay:
    checks['secure_simulator_active'] = all(f'UTP simulator RECEIVE delay={args.transport_delay}ms' in s for s in logs.values())
if args.replication_verify:
    match = re.search(r'near id=(\d+)', host)
    target = match.group(1) if match else 'MISSING'
    client = logs['client1']
    observations = [
        (float(t.replace(',', '.')), dict(re.findall(r'(\d+):(\d+);', ids)))
        for t, ids in re.findall(r'\[PR5 VERIFY\] t=(\d+(?:[.,]\d+)?) server=False .*?ids=([^\n]*)', client)
    ]
    checks.update({
        'prop_seen_before_hide': any(ids.get(target) == '100' for t, ids in observations if t < 14),
        'prop_hidden': any(target not in ids for t, ids in observations if 14 < t < 17),
        'prop_reappeared_current_state': any(ids.get(target) == '70' for t, ids in observations if t > 20),
        'prop_deleted_on_client': any(target not in ids for t, ids in observations if t > 29),
        'server_hidden_health_changed': 'hidden-health=70' in host,
        'server_despawn': '[PR5 VERIFY] despawn' in host,
    })
data_records = [r for r in records if not args.secure or (r['encrypted'] and base64.b64decode(r['payload_base64'])[6] == 0)]
window_start = (data_records[0]['seconds'] if data_records else 0) + 5
window_end = window_start + 17
steady = [r for r in records if window_start <= r['seconds'] <= window_end]
enc = [r for r in steady if r['encrypted']]
inner = sum(r['size']-48 for r in enc)
summary = dict(PASS=sum(bool(v) for v in checks.values()), FAIL=sum(not v for v in checks.values()), checks=checks,
               exit_codes=[p.returncode for p in processes], observed_datagrams=len(records), fault_injection=faults,
               capture_type='actual UDP payloads observed at local relay, NOT BPF/PCAPNG',
               steady_window=[window_start,window_end], steady_udp_bytes=sum(r['size'] for r in steady),
               encrypted_steady_packets=len(enc), encrypted_inner_bytes=inner, encryption_overhead_bytes=48*len(enc),
               encryption_overhead_percent=(4800*len(enc)/inner if inner else None))
(out/'result.json').write_text(json.dumps(summary, indent=2)); print(json.dumps(summary, indent=2))
raise SystemExit(0 if not summary['FAIL'] else 1)
