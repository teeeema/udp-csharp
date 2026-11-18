#!/usr/bin/env python3
"""Export real relay recvfrom observations as USER0 PCAPNG, without inventing IP headers.

Timestamps are elapsed relay time (epoch deliberately zero). This is not a NIC/BPF
capture. Wireshark field dissection of USER0 requires the bundled Lua dissector.
"""
import base64, html, json, pathlib, struct

root = pathlib.Path(__file__).resolve().parents[1]
evidence = root/'docs/pr6-evidence'

def padded(b): return b + bytes((-len(b)) % 4)
def option(code, value): return struct.pack('<HH', code, len(value)) + padded(value)
def block(kind, body):
    n = len(body)+12
    return struct.pack('<II', kind, n)+body+struct.pack('<I', n)

for folder in sorted(evidence.iterdir()):
    source = folder/'relay-datagrams.jsonl'
    if not source.exists(): continue
    rows = [json.loads(s) for s in source.read_text().splitlines()]
    comment = b'Actual loopback relay recvfrom UDP payloads; USER0; no IP/UDP headers; timestamps=elapsed seconds since relay start, epoch zero; NOT NIC/BPF capture.'
    content = block(0x0A0D0D0A, struct.pack('<IHHq',0x1A2B3C4D,1,0,-1)+option(1,comment)+bytes(4))
    content += block(1,struct.pack('<HHI',147,0,65536)+option(2,b'loopback-relay-UDP-payload')+bytes(4))
    for row in rows:
        raw = base64.b64decode(row['payload_base64']); ticks = round(row['seconds']*1_000_000)
        content += block(6,struct.pack('<IIIII',0,ticks>>32,ticks&0xffffffff,len(raw),len(raw))+padded(raw)+
                         option(1,row['direction'].encode())+bytes(4))
    (folder/'relay-payloads.pcapng').write_bytes(content)

packet_case = next(name for name in ['optimized-bounded','after-ready-optimized','after'] if (evidence/name/'relay-datagrams.jsonl').exists())
packet_source = evidence/packet_case/'relay-datagrams.jsonl'
row = next(json.loads(s) for s in packet_source.read_text().splitlines()
           if json.loads(s)['encrypted'] and base64.b64decode(json.loads(s)['payload_base64'])[6] == 0)
b = base64.b64decode(row['payload_base64'])
sections = [('Open header / AAD',b[:20],'#b4d8f0'),('Nonce (12 B)',b[20:32],'#ffe0a0'),
            ('Ciphertext',b[32:-16],'#d3e8bf'),('GCM tag (16 B)',b[-16:],'#e7c6e8')]
body = ''.join(f'<section style="background:{color};padding:16px;margin:12px 0"><h2>{name}: {len(data)} B</h2><code style="overflow-wrap:anywhere">{data.hex(" ")}</code></section>' for name,data,color in sections)
(evidence/'packet-layout.html').write_text('<!doctype html><html lang="ru"><meta charset="utf-8"><title>Фактическая datagram ПР6</title>'
    '<body style="font:16px system-ui;max-width:1000px;margin:40px auto;line-height:1.5"><h1>Разметка фактической UDP datagram</h1>'
    f'<p>Наблюдение локального реле из {html.escape(str(packet_source.relative_to(evidence)))}. Это визуализация реальных bytes, не screenshot Wireshark и не NIC capture.</p>'
    f'<p>Direction {html.escape(row["direction"])}, elapsed {row["seconds"]} s, {len(b)} B. Keys/расшифрованный plaintext отсутствуют.</p>'+body+'</body></html>')
print('Exported exact relay UDP payloads as USER0 PCAPNG, plus packet-layout.html.')
