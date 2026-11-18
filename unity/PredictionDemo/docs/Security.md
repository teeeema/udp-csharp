# ПР №6 — защищённый UDP в DUSTLINE

Проверенный стек: Unity 6000.6.7f1 / Mono, NGO 2.13.3, UTP 6.6.0, C#, BouncyCastle.Cryptography 2.7.0 (netstandard2.0, MIT). Аудит и исходные тесты: [PR6_Audit.md](PR6_Audit.md). Код интегрирован в действующую игру; LegacyUdp остаётся историческим .NET-проектом и **не получает шифрование автоматически**.

## Модель угроз и ограничения

Предотвращаются чтение игровых payload пассивным наблюдателем, неаутентифицированная подмена пакетов, повторная обработка старых внешних datagram, использование сессии другого endpoint. Есть bounded resource/rate controls для handshake и UDP receive. Они не защищают канал доступа origin от объёмной DDoS-атаки. Размер, время пакетов, открытые ephemeral public keys и внешний заголовок остаются видимы.

**X25519 здесь не аутентифицирован. Активный MITM может подменить ключи.** Подтверждение ключей доказывает владение согласованным секретом, не личность сервера. Следующее совместимое усиление: сервер подписывает полный transcript заранее доверенным Ed25519 ключом, клиент проверяет закреплённый public key до Confirm; либо PSK аутентифицирует transcript и вводится в HKDF. Такой механизм не реализован и не заявляется. Игровая авторизация, ACL, защита от ботов и production anti-cheat не заменяются шифрованием.

## Интеграция

```
NGO RPC / variables → штатные UTP batch/header + reliability/fragmentation
    → SecureNetworkInterface → AES-GCM envelope → настоящий UDP socket
UDP socket → bounds/session/replay precheck → GCM verification
    → неизменённый UTP datagram → штатный UTP/NGO → игровая логика
```

MeteredTransport устанавливает защищённый driver при каждой инициализации. GetDefaultPipelineConfigurations сохраняет штатные pipeline NGO. VerificationTransport тоже использует защищённый интерфейс: simulator не создаёт plaintext канал. Публичный runtime не имеет флага plaintext fallback. До подтверждения ключей исходящие UTP connect datagram отбрасываются; штатный UTP retry повторяет их после handshake. Сервер передаёт вверх только аутентифицированный Data установленной сессии. Нет отдельного декоративного crypto-demo канала.

Существующие заголовки UTP **не переписываются и не заменяются**: полный datagram инкапсулирован в ciphertext. Внешний 20-байтовый заголовок — новая версия security envelope. Это адаптация учебного формата к фактическому UTP, а не ложное объявление собственного пятибайтового Header заголовком NGO. Значения 10/11 выделены в отдельном namespace внешней обёртки, поэтому не занимают UTP/NGO message IDs. Plaintext старой ПР5 несовместим с обязательным защищённым интерфейсом намеренно: downgrade не разрешён.

## Handshake X25519 + HKDF-SHA256

```mermaid
sequenceDiagram
  participant C as Unity client
  participant S as Unity server
  C->>S: KEY_EXCHANGE/Hello (sessionId, clientPublic32, challenge32)
  S->>C: KEY_EXCHANGE/Response (serverPublic32, echoedChallenge32)
  Note over C,S: X25519 shared secret; transcript=SHA256(Hello || Response)
  Note over C,S: HKDF-SHA256, separate c2s and s2c keys
  C->>S: ENCRYPTED/Confirm (transcript digest)
  Note right of S: verify GCM + digest → ESTABLISHED
  S->>C: ENCRYPTED/Ready (transcript digest)
  Note left of C: verify GCM + digest → ESTABLISHED
  C->>S: ENCRYPTED/Data (original UTP datagram)
  S->>C: ENCRYPTED/Data (state, ACK, heartbeat...)
```

Каждый endpoint имеет отдельный SecurePeer, эфемерный private key, направленные 32-byte AES keys, исходящий counter и ReplayWindow. CSPRNG Bouncy Castle создаёт private material, challenge и ненулевой случайный sessionId (64-bit, маршрутизация дополнительно привязана к endpoint). X25519 library выполняет RFC 7748 clamping. Нулевой/low-order общий секрет отвергается. Response проверяется по размеру, magic/version/type/kind/direction, ID, challenge и ожидаемому адресу отправителя; произвольный endpoint клиентом не принимается. DNS разрешается до UTP connect; numeric endpoint затем закреплён за сессией.

HKDF salt — SHA256 точных финальных Hello и Response. IKM — X25519 shared secret. Info — ASCII `DUSTLINE/pr6/v1/c2s` или `DUSTLINE/pr6/v1/s2c`, L=32. Transcript включает sessionId, обе public keys, challenge, protocol version и направления. Ключи/счётчики при дубле Hello/Response не пересоздаются. Старый plaintext Hello не может заменить установленную сессию.

Повторы каждые 500 мс, handshake timeout 8 с; истёкшие контексты удаляются. Hello/Response повторяются побайтно, Confirm/Ready всегда шифруются с **новым** counter. Потеря любого из четырёх этапов покрыта тестами. Сервер становится ESTABLISHED после Confirm; клиент после Ready. Если Ready потерян, повтор Confirm вызывает новый Ready. Idle timeout 30 с считается только по допустимым аутентифицированным сообщениям, не по мусорному UDP. Shutdown/Restart уничтожает engine; новая сессия генерирует новый X25519 keypair и ключи. Если UDP endpoint повторно использован при reconnect, допускается один bounded candidate с новым ID: старый установленный контекст остаётся до успешного нового Confirm, затем старые ключи стираются. Неаутентифицированный Hello сам по себе не заменяет активную сессию. Candidate тоже входит в лимиты и истекает через 8 с.

## Wire format, AAD и nonce

Все многобайтовые поля security envelope — **big endian**. Внутренние UTP/NGO сериализуются штатным кодом пакетов и сохраняют свои исходные правила.

| Offset | Размер | Поле |
|---|---:|---|
| 0 | 4 | ASCII `DSL6` |
| 4 | 1 | Version = 1 |
| 5 | 1 | Type: KEY_EXCHANGE=10, ENCRYPTED=11 |
| 6 | 1 | Kind: handshake Hello=1/Response=2; encrypted Data=0/Confirm=1/Ready=2 |
| 7 | 1 | Direction c2s=1, s2c=2 |
| 8 | 8 | SessionId |
| 16 | 2 | PayloadSize = весь остаток datagram |
| 18 | 2 | Reserved = 0 |
| 20 | 12 | Для ENCRYPTED: nonce |
| 32 | N | Ciphertext исходного UTP datagram или encrypted confirmation digest |
| 32+N | 16 | GCM tag |

Hello и Response имеют по 64 B payload и 84 B datagram. ENCRYPTED имеет `payloadSize=N+28`, полный размер `N+48`. **Header с финальным payloadSize формируется до EncryptAES и целиком передаётся AAD. После вычисления тега ни одно его поле не изменяется.** Это устраняет дефект учебного примера с изменением размера после GCM.

Nonce = `uint32(direction)` || `uint64(counter)`, 12 B. Counter начинается с 1, увеличивается для каждого нового шифрования, включая control; отдельные keys c2s/s2c. При ulong.MaxValue шифрование запрещено (нужно новое подключение). Для практического лимита GCM ниже см. эксплуатацию. Время/`rand()` не используется. Ключи не переживают restart/reconnect; старый counter никогда не применяется с новым plaintext на старом ключе. Секретные массивы обнуляются после derive и Dispose. GC/внутренние копии managed crypto не дают гарантии полного стирания из RAM; process dump требует отдельной защиты ОС.

DecryptAES возвращает plaintext только после проверки тега. Буфер частичного plaintext очищается на любой криптографической ошибке. Проверяются null/min/max длины, payloadSize, reserved/version/direction, ID и nonce prefix. Максимальная UDP payload 1200 B, максимальный внутренний datagram 1152 B. Для этого уменьшается maxMessageSize UTP, остальные параметры pipeline сохраняются; фрагментация больших NGO сообщений остаётся штатной.

## Replay и надёжность

Окно — 64 counter, highest + 64-bit bitmap. Допускаются неупорядоченные ранее не принятые пакеты в окне, старые/нулевые/повторные counter отклоняются. Precheck не меняет окно. **Commit окна выполняется только после успешной GCM-аутентификации**, поэтому поддельный большой counter его не сдвигает.

Повтор уже полученного encrypted datagram не передаётся UTP. UTP-повтор надёжного сообщения приходит **ниже reliability** как новая отправка и получает новый security counter. Он проходит replay, после чего UTP дедуплицирует собственный reliable sequence и отправляет свой зашифрованный ACK. Поэтому потерянный ACK не вызывает повторного FireRpc. Сохраняется и прикладная дедупликация sequence в TankWeapon.

MOVEMENT в исходной ПР5 был Reliable. Для требования ПР6 обычный SubmitInputRpc переведён на Unreliable; dash выделен в Reliable SubmitDashRpc. Если движение обогнало dash, применяется только dash increment с отдельной дедупликацией и серверным cooldown, без дополнительного базового движения и кредита времени. SHOOT остаётся Reliable. BroadcastState/ShotEffect остаются Unreliable. Prediction, server reconciliation, интерполяция, observer relevance и частоты ПР5 сохранены. Поздний dash отражается последующими авторитетными snapshots. RTT и ACK/RTO остаются внутри UTP; старые численные SRTT/jitter/loss тестируются в LegacyUdp, новый отдельный HUD для этих метрик не добавлен.

## Ресурсы и безопасные события

- Незавершённых handshake ≤64, всего crypto сессий ≤256.
- Global Hello budget 20/s burst 40; per-IP 2/s burst 4 (порт не меняет лимит), таблица IP ≤1024, записи очищаются через 60 с.
- Global receive crypto budget 5000/s burst 10000; не более 256 UDP receives за UTP update.
- Idle 30 с; handshake 8 с. Установленную сессию нельзя сбросить неаутентифицированным Hello.
- HANDSHAKE_OK, HANDSHAKE_FAIL, AUTH_FAIL, REPLAY_DROP, RATE_LIMIT, SESSION_EXPIRED. Повторяющиеся события ограничены одним сообщением на тип в секунду. TRAFFIC содержит только счётчики байтов/пакетов при Dispose. Ключи и сырые plaintext payload не выводятся. Существующая учебная игровая диагностика показывает координаты/события; она не является dump расшифрованных UDP-буферов.

Rate limiting ограничивает ресурсы приложения, не обеспечивает защиту сети от распределённой атаки и не содержит stateless cookie. За прокси несколько клиентов могут делить source IP: это учитывается в [Cloudflare_Spectrum_Setup.md](Cloudflare_Spectrum_Setup.md).

В автоматизированных native опытах явно передаётся `--pr6-wait-players N`: отсчёт screenshot/exit начинается после подключения требуемого числа игроков (на клиенте — появления всех PlayerObject), максимум ожидания 90 с. Только при этом opt-in флаге отключается vSync и задаётся 30 FPS, чтобы несколько графических проверок не конкурировали без лимита кадров. Обычный запуск игры этим флагом не пользуется. Сценарий проверки репликации при этом тоже начинает свой таймер после подключения. `--headless` у helper запускает тот же настоящий player с `-batchmode -nographics` и не создаёт/не обещает screenshots.

## Проверки, трафик и доказательства

Фактические XML, логи native players, исходные CSV и точные datagram наблюдения находятся в `docs/pr6-evidence`. Итоговые числа и статусы — [PR6_Report.md](PR6_Report.md). Изображения игры создаются самим ScreenCapture текущих игроков; скриншоты Wireshark/Cloudflare не подделываются. Наблюдения локального UDP-реле — JSONL с точным payload из recvfrom и экспорт `relay-payloads.pcapng` с LinkType USER0. В нём **нет придуманных IP/UDP заголовков**; timestamp — реальное elapsed-время реле относительно условной эпохи 0. Это экспорт реальных payload-наблюдений, **не захват сетевого интерфейса/BPF**. `tcpdump -r` подтвердил читаемость PCAPNG; Wireshark GUI остаётся NOT_VERIFIED. `packet-layout.html` размечает реальные bytes Header/nonce/ciphertext/tag, явно обозначен как визуализация, не screenshot.

Обязательная криптографическая надбавка: 12-byte nonce + 16-byte tag = 28 B. Адаптер дополнительно добавляет 20-byte outer Header, итого **48 B на каждый UTP datagram**. Процент для N внутренних байт — `4800/N`; при максимуме N=1152 это 4.167%, на маленьком ACK существенно больше. Handshake — отдельные пакеты, изменение MTU может увеличить число UTP фрагментов. Не путать старые NGO payload CSV с полным UDP wire. Сравнение before/after использует одинаковый сценарий, а точная стоимость crypto определяется по каждому наблюдаемому encrypted packet как `size - innerSize = 48`.

## Wireshark: NOT_VERIFIED

Wireshark/tshark не установлен; `/usr/sbin/tcpdump -i lo0 -c 1 -w /tmp/pr6-permission-check.pcap udp port 7777` вернул Permission denied на /dev/bpf0. Права ОС не менялись. Поэтому PCAPNG сетевого интерфейса и screenshots Wireshark в результате нет. Для просмотра USER0 экспорта запустить Wireshark с `-X lua_script:Tools/pr6_dissector.lua`, открыть `relay-payloads.pcapng`; фильтр для него `dustline6` / `dustline6.type == 11`, UDP port filter к USER0 неприменим. Lua GUI-разбор не запускался, статус NOT_VERIFIED. Ручное получение обычного IP/UDP capture на собственном loopback:

```sh
# Терминал с правами захвата; без запуска чужого трафика.
sudo tcpdump -i lo0 -s 0 -w /tmp/pr6-after.pcap 'udp port 7777'
./Builds/PR6/ReplicationLab.app/Contents/MacOS/PredictionDemo --pr4-host --pr4-profile 0
./Builds/PR6/ReplicationLab.app/Contents/MacOS/PredictionDemo --pr4-client --pr6-address localhost --pr4-profile 0 --pr4-auto --pr4-auto-fire
```

Повторить те же флаги для сохранённой `Builds/ReplicationLab.app`, записывая `/tmp/pr6-before.pcap`. В Wireshark открыть захваты, display filter `udp.port == 7777`, выбрать data packet. До: исходный бинарный UTP/NGO payload читается без crypto; это не обязательно ASCII-текст. После: `44 53 4c 36`/version/type и выделение bytes UDP payload: 0–19 Header, 20–31 nonce, 32..len-17 ciphertext, последние 16 tag. Hello/Response ожидаемо открыты. Использовать `Tools/pr6_dissector.lua` для выделения полей, затем Save As PCAPNG и сохранить настоящие screenshots пакетов и игры. Для автоматического relay-сценария фильтр `udp.port == 7906 || udp.port == 7907`.

## Эксплуатация

Развёртывать только одновременное обновление клиента и сервера, без downgrade. Открывать только игровой UDP-порт и согласованный management ingress. Не публиковать memory dumps, секреты, чужие payload или реальные origin адреса в учебных материалах. До production аутентифицировать сервер и определить требования к игрокам.

В этой учебной версии нет автоматического rekey долгоживущей сессии; для эксплуатации ограничивать сессию существенно раньше теоретических nonce limits и ориентироваться на лимиты AES-GCM выбранного профиля (например ≤2^32 отправок на ключ), закрывая и пересоздавая соединение. Большие нагрузки, WAN, IPv6-only DNS, Windows/Android/IL2CPP отдельно не проверены. Managed callbacks синхронно завершают dependency и выделяют массивы на пакет: это осознанный performance tradeoff, не production throughput benchmark.
