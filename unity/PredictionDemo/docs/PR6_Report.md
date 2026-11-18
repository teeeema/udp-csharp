# ПР №6 — итоговый проверяемый отчёт

Отчёт ниже описывает исторические прогоны на macOS. [Сверка с полученной методичкой и новые проверки на Windows, 18.11.2026](PR6_Methodichka_Audit.md).

Дата выполнения: 17.11.2026 (Europe/Moscow). Реализована существующая Unity-игра **DUSTLINE / Practice5Replication**, Unity 6000.6.7f1, C#, Mono, NGO 2.13.3, Unity Transport 6.6.0. Ветка **feature/encryption**. Исходный локальный checkpoint ПР5 — `8c2998a`, основной implementation commit — `453f361`, финальная доработка verification — `0ca5f8e`; последующие локальные commits содержат усиление запуска проверок и документацию. Текущий HEAD проверяется `git log -3 --oneline`. Remote у текущего Unity-проекта отсутствует; Git соседнего `prac_1` и все его пользовательские изменения сохранены.

## Объективные вердикты

| Вердикт | Что именно оценивается |
|---|---|
| **Подтверждено** | AES-256-GCM встроен под настоящие UTP pipeline, обе стороны подтверждают X25519/HKDF keys, unit tests и реальные native client/server дают наблюдаемые результаты |
| **Опровергнуто** | Утверждение, что один неаутентифицированный X25519 предотвращает MITM; обычная orange-cloud DNS-запись защищает произвольный UDP; USER0 export равен NIC capture |
| **Частично подтверждено** | Полное выполнение всей ПР6: локальная программная часть и доказательства есть, внешние пункты ещё не выполнены |
| **Недостаточно данных** | Реальный Cloudflare Spectrum, закрытие origin firewall, Wireshark GUI, WAN/production throughput, формальная сверка отсутствующего PDF и ревью второго участника |

## Требования и статусы

| Требование | Статус | Проверяемый результат / граница |
|---|---|---|
| Найти текущий проект, не заменить demo | DONE | PR6_Audit.md, исходная игра с 50 props и прежними RPC |
| Исходные тесты до изменения | DONE | 37 EditMode, 2 PlayMode, 78 .NET PASS |
| X25519, HKDF-SHA256, CSPRNG | DONE | RFC vectors, invalid low-order key rejection, directional transcript-bound keys |
| Подтверждение ключей до игры | DONE | encrypted Confirm/Ready; потеря каждого этапа и timeout тестируются |
| AES-256-GCM 32/12/16 | DONE | NIST vector + ciphertext/AAD/tag/nonce/wrong-key negative tests |
| Header AAD и финальный payloadSize | DONE | Неизменяемый после Seal header, 20 header-byte mutation tests |
| Все игровые и служебные datagram | DONE | SecureNetworkInterface под reliability, никаких plaintext UDP кроме Hello/Response |
| Сохранить исходный UTP header | DONE | Полная инкапсуляция неизменённого datagram; внешний Header — отдельный namespace, не замена UTP |
| Counter, replay, reorder | DONE | 64-counter window, commit после GCM, проверка forged high counter |
| Bounded handshake/session/rate tables | DONE | 64 pending / 256 total, per-IP/global budgets, timeout/expiry, unit cap tests |
| Reconnect и отсутствие повторного nonce | DONE | Новая keypair/session, reused endpoint candidate заменяет старую сессию после Confirm |
| MOVEMENT Unreliable / SHOOT Reliable | DONE | Движение Unreliable, отдельный Reliable dash, FireRpc Reliable + sequence dedup |
| ACK/retry/RTO и дедупликация | DONE | Штатные UTP pipeline сохранены; новый outer counter при каждом UTP retry, duplicate SHOOT regression |
| Prediction/reconciliation/interpolation | DONE | Прежние tests + real movement и injected correction при 0/100 ms |
| Несколько независимых клиентов | DONE | Два remote clients + host, разные session IDs, движение/стрельба при relay faults |
| ПР5 relevance/частоты/репликация | DONE | Hide/show/current health/despawn, baseline/optimized measurements |
| RTT/SRTT/jitter/loss прежней реализации | DONE | 10 .NET telemetry tests; UTP RTT/heartbeat не менялись; отдельные новые численные Unity telemetry measurements не заявляются |
| Инструкция, события, модель угроз/MITM | DONE | Security.md, безопасные PR6 events, секреты не выводятся |
| Реальные payload before/after | DONE | recvfrom traces, CSV, game screenshots, USER0 PCAPNG export, packet-layout.html |
| Wireshark NIC capture и screenshots | NOT_VERIFIED | Нет Wireshark/tshark, BPF Permission denied; ручные команды подготовлены |
| Enterprise Spectrum/DNS/firewall/origin | BLOCKED | BLOCKED_EXTERNAL; нет доступов/разрешения на инфраструктуру, инструкция готова |
| PDF и буквальное соответствие методичке | NOT_VERIFIED | PDF и учебные C++ файлы не предоставлены, адаптация по подробному ТЗ |
| Локальные commits и описание PR | DONE | Независимый текущий Git baseline + feature branch, PR6_Pull_Request.md |
| Внешний push/создание PR/ревью | BLOCKED | Не согласована миграция текущего Unity baseline в remote; push/ревью не имитировались |

## Архитектура и изменённые файлы

`NGO → штатные UTP batch/reliability/fragmentation headers → SecureNetworkInterface → AES-GCM → Socket.SendTo`. Обратно: size/header/session/replay precheck → verify GCM → commit replay → исходный UTP/NGO handler. Никакие игровые данные не передаются через отдельный демонстрационный канал.

X25519 использует отдельную эфемерную пару на каждую сессию. SHA256 полного Hello/Response — HKDF salt, info содержит version/direction. Ключи c2s/s2c различаются. Nonce — uint32(direction) + uint64(counter), big endian. Wire — `[Header20 AAD][nonce12][ciphertext N][tag16]`, payloadSize устанавливается до GCM. Max UDP payload 1200 B. Replay window 64, неупорядоченные свежие пакеты принимаются. Старые encrypted datagram не исполняются; UTP-повторы шифруются заново с новым outer counter и дедуплицируются UTP, затем FireRpc sequence.

| Файл / группа | Изменение |
|---|---|
| Assets/Scripts/Prediction/Security/SecureCrypto.cs | Bouncy Castle X25519, HKDF-SHA256, AES-GCM, очищение частичного plaintext |
| Assets/Scripts/Prediction/Security/SecureProtocol.cs | Wire, peer/handshake, directed keys/counter, replay, token budgets, bounds, expiry/reconnect |
| Assets/Scripts/Prediction/Security/SecureNetworkInterface.cs | Настоящие UDP socket send/receive под UTP, sender pinning, bounded per-update work |
| Assets/Scripts/Prediction/Security/SecureDriverConstructor.cs | Сохранение default NGO pipeline, уменьшение inner MTU до 1152 B |
| Assets/Scripts/Prediction/Replication/MeteredTransport.cs | Обязательный secure driver при Initialize/Restart, payload meter сохранён |
| Assets/Scripts/Prediction/NetworkBootstrap.cs | DNS/CLI address, opt-in ожидание готовности игроков для native проверок |
| Assets/Scripts/Prediction/PredictedPlayer.cs | MOVEMENT Unreliable, Reliable dash и защита от повторного исполнения позднего dash |
| Assets/Scripts/Prediction/Replication/ReplicationVerification.cs | Только opt-in PR6: начало проверок после подключения |
| Assets/Diagnostics/Verification/VerificationTransport.cs | UTP simulator тоже использует secure interface |
| Assets/Scripts/Prediction/PredictionDemo.Runtime.asmdef | Unity.Collections и unsafe для публичного packet queue API |
| Assets/Tests/EditMode/SecureTransportTests.cs | 62 crypto/protocol/security tests |
| Assets/Tests/PlayMode/DefectPlayModeTests.cs | Late dash/duplicate increment regression + сохранение старых проверок |
| Assets/Editor/ProjectBootstrap/SecurityBuilder.cs | Отдельная macOS сборка в Builds/PR6 |
| Assets/Plugins/BouncyCastle/* | Зафиксированный netstandard2.0 DLL 2.7.0, MIT, provenance и SHA-256 |
| .gitignore, LegacyUdp/**/*.csproj, LegacyUdp/UdpGame.sln | Исправлен слишком широкий Unity ignore; существующие .NET build files включены в текущий Git без изменения содержимого |
| Tools/run_pr6_game.py | Реальные player процессы, relay, drop/duplicate/tag mutation, проверки logs/state и code fingerprint |
| Tools/export_pr6_evidence.py, Tools/pr6_dissector.lua | Честный USER0 PCAPNG export, визуальная разметка bytes и ручной Wireshark dissector |
| README.md, docs/PR6_Audit.md, Security.md, Cloudflare_Spectrum_Setup.md | Аудит, build/run, архитектура, ограничения и внешняя инструкция |
| docs/PR6_Commands.md, PR6_Pull_Request.md, PR6_Report.md, pr6-evidence/* | Реальные команды, описание PR, отчёт, XML/logs/CSV/screenshots/packet evidence |
| Новые .meta | Стабильные GUID новых Unity assets, ранее существующие пользовательские assets не заменялись |

Полные команды сборки и тестов: [PR6_Commands.md](PR6_Commands.md). Логи runner сохраняют exit status; ошибки не превращались в успешный код выхода. Native helper возвращает exit 1 при любом FAIL, timeout — исключение, Unity/SDK тесты возвращают свой фактический код.

## Автоматические и реальные игровые результаты

| Набор | PASS | FAIL | Доказательство |
|---|---:|---:|---|
| Unity EditMode (62 новых + прежние 37) | 99 | 0 | pr6-evidence/accepted-editmode.xml |
| Unity PlayMode (включая поздний dash/дубликат) | 2 | 0 | pr6-evidence/bounded-playmode.xml |
| .NET protocol/telemetry/reliability/integration | 78 | 0 | pr6-evidence/legacy-final/*.trx |

**Автоматические unit/regression: 179 PASS / 0 FAIL.** Это число без повторного сложения промежуточных запусков.

| Реальный графический опыт финальной сборки | PASS checks | FAIL | UDP datagram наблюдений | Каталог |
|---|---:|---:|---:|---|
| Обычная репликация + movement/shoot/CSP | 12 | 0 | 5477 | pr6-evidence/baseline-bounded |
| Оптимизированная репликация + movement/shoot/CSP | 12 | 0 | 1084 | pr6-evidence/optimized-bounded |
| Host + 2 remote clients, drop/duplicate/tag mutation | 20 | 0 | 2317 | pr6-evidence/multiclient-bounded |
| Hide/show, текущие 70 HP, despawn | 16 | 0 | 893 | pr6-evidence/replication-bounded |
| UTP simulator: 100 ms receive delay на каждой стороне | 13 | 0 | 1063 | pr6-evidence/latency-bounded |

**Native scenarios: 73 PASS checks / 0 FAIL**, пять отдельных запусков на одной финальной runtime DLL SHA-256 `f8f16889df931b1f3164906fd7a23472fcd372d8aa58e7358c2de56715433b0e`. Checks частично повторяются между сценариями и не объявляются независимыми unit tests. Все процессы завершились exit 0.

В multi-client опыте реально внесены 100 потерь, 116 дубликатов и 60 подмен тега. Зафиксированы AUTH_FAIL и REPLAY_DROP, отсутствуют повторные пары (owner, shot sequence) среди ACCEPT. LOCAL_CLIENT id связывает проверки с фактическим NGO ID, порядок подключения не предполагается.

Дополнительно сохранены успешные headless опыты на предыдущем readiness build: multi-client 20/20, replication 16/16. Они не используются вместо финальных графических доказательств; итоговые графические PNG действительно получены ScreenCapture (проверен client1.png с 3 CONNECTED и выстрелом).


Промежуточные неудачные результаты сохранены: первый replication helper увёл игрока за radius relevance; затем в batch были поздние старты клиента после старого host exit timer. Это не скрытые успешные проверки: исходные result.json остаются с FAIL, а причина и новые команды описаны в PR6_Commands.md. Для актуального результата используются завершённые readiness-aware прогоны на той же финальной сборке, указанные выше. Открытых FAIL в финальной согласованной серии нет. Uncapped graphics stress не объявлен production-профилем или гарантией устойчивости при любой нагрузке.

## Измерения трафика

Одинаковый сценарий: один remote client + host, профиль 0, scripted movement/CSP fault и стрельба, отдельно обычная/оптимизированная репликация. Окно каждого relay trace — 17 с после 5 с прогрева от первого Data. Значения агрегируют оба направления UDP; KB=1000 B. Полные значения/границы — pr6-evidence/traffic-comparison.json.

| Версия / репликация | UDP bytes за 17 с | UDP KB/s (оба направления) | NGO host TX KB/s | Crypto overhead bytes | Crypto overhead / inner |
|---|---:|---:|---:|---:|---:|
| ПР5 / обычная | 1397606 | 82.212 | 76.522 (17 samples) | — | — |
| ПР5 / optimized | 87467 | 5.145 | 1.540 (17 samples) | — | — |
| ПР6 / обычная | 1917424 | 112.790 | 74.896 (17 samples) | 160176 | 9.12% |
| ПР6 / optimized | 95470 | 5.616 | 1.348 (17 samples) | 37200 | 63.84% |

В ПР6 optimized снизил наблюдаемый UDP объём относительно обычной репликации на **95.02%**, NGO host TX — на **98.20%**. Это результат этого локального сценария, не универсальная гарантия.

Важно для сравнимости: final verification ограничен 30 FPS явным opt-in флагом; сохранённый бинарник ПР5 такого флага ещё не имеет. Вместе с изменением MOVEMENT delivery и MTU это мешает приписывать всю разницу before/after одному шифрованию. Одинаков сценарий ввода/стрельбы; точная чистая надбавка AES/envelope измеряется независимо от FPS как 48 B на datagram. Профиль больших WAN-нагрузок и статистическая оценка FPS/latency не заявляются.


Security overhead неизменен: **48 B на UTP datagram = 20 outer Header + 12 nonce + 16 tag**, не считая IP/UDP. Процент считается по фактическому внутреннему размеру; на малых ACK он выше, чем на больших payload. Отдельный handshake добавляет 84+84+80+80=328 B в идеальном четырёхпакетном случае, плюс повторы при потерях. Изменение MTU/числа фрагментов, направление MOVEMENT Unreliable и частоты кадров тоже влияют на before/after wire totals; разница totals не выдаётся за чистую стоимость AES. Предыдущие ПР5 отчёты измеряли NGO payload, а не весь wire: поэтому эти величины представлены отдельно.

## Документация, доказательства и оставшиеся ограничения

- [Security.md](Security.md): threat model, handshake diagram, wire/AAD/nonce, replay/reliability, logging, MITM, ручной Wireshark сценарий.
- [Cloudflare_Spectrum_Setup.md](Cloudflare_Spectrum_Setup.md): актуальные официальные ссылки, Enterprise UDP entitlement, DNS/firewall, ranges, Proxy Protocol off, origin обход, ограничения fragments, тест-план и BLOCKED_EXTERNAL.
- `docs/pr6-evidence/*`: XML и TRX, build logs без access token, native logs/state/CSV, настоящие game PNG, exact payload JSONL и `relay-payloads.pcapng` USER0. USER0 не содержит IP/UDP headers, timestamp — elapsed от условной эпохи 0. Container прочитан tcpdump; GUI Wireshark не запускался.
- `docs/pr6-evidence/packet-layout.html`: цветовая разметка настоящего зашифрованного datagram; явно обозначена визуализацией, не screenshot Wireshark.
- `Builds/PR6/ReplicationLab.app`: новая рабочая macOS сборка. `Builds/ReplicationLab.app` и все старые ZIP остаются прежней ПР5. Builds не хранится в Git.

MITM остаётся возможным: нет PSK/подписи/закрепления identity сервера. Не реализована автоматическая ротация ключей многодневного соединения. Managed interface завершает job dependencies синхронно и выделяет массивы, throughput больших серверов не оценивался. Локальный resource limiting не заменяет upstream DDoS mitigation. Spectrum/Simple Proxy Protocol не развёрнуты; Proxy Protocol должен быть off, иначе current parser отвергнет дополнительные заголовки. IPv6-only DNS, IL2CPP, Windows/Android, WAN и большие нагрузки не проверены. Ревью второго участника не подтверждено. Отсутствующий PDF не позволяет заявить буквальное полное соответствие методичке.

## Git diff и готовность PR

<!-- GIT_STATS_START -->
Сравнение текущего проекта с локальным baseline `8c2998a`:

```text
446 files changed, 91043 insertions(+), 22 deletions(-)
```

Объём включает все настоящие журналы, packet JSONL/PCAPNG, screenshots и промежуточные FAIL, а также ранее игнорировавшиеся существующие LegacyUdp project files. Новая игровая логика ограничена transport/security, MOVEMENT/dash и opt-in verification; сцена и прежние игровые assets не заменялись. `git diff --check` завершился без замечаний.
<!-- GIT_STATS_END -->

Есть готовое [описание PR и checklist](PR6_Pull_Request.md). Локальная ветка пригодна для ревью текущего проекта. Внешний PR ещё не создан: у этой копии независимая история без remote; старый remote имеет другую структуру и пользовательские изменения. Согласование целевого репозитория/перенос baseline требуется до публикации. Force push, reset --hard, clean -fd и перезапись пользовательских изменений не использовались.
