# Предварительный аудит ПР №6 — 17.11.2026

Актуальная реализация: **Practice5Replication**, DUSTLINE. Это существующая Unity-игра ПР4/5, а не копия учебного crypto-client. Unity 6000.6.7f1, C#, Mono, NGO 2.13.3, UTP 6.6.0. Основная сцена Assets/Scenes/PredictionDemo.unity. LegacyUdp содержит сохранённую .NET 10 реализацию ПР1–3. C++-проекта здесь нет.

Корень открытой папки не является Git-репозиторием. Соседний `prac_1` имеет origin `https://github.com/teeeema/prac-1-udp-csharp.git`, HEAD `3e4258d`, ветку `codex/restore-original-dustline-20261023`, множество пользовательских изменений и удалений PDF. Он **не изменялся**. Текущая ПР5 ранее поставлялась ZIP без Git (это подтверждает README). В самой Practice5Replication создан локальный Git baseline `8c2998a` и рабочая ветка `feature/encryption`. Remote не придуман и не установлен. Новая локальная история не является готовой веткой старого GitHub remote.

Файл «Практическая работа №6.pdf» и перечисленные учебные C++-файлы среди доступных файлов и attachments отсутствуют. Нормативная сверка PDF имеет статус NOT_VERIFIED; источником реализации служит переданное подробное ТЗ. AGENTS.md не найден.

## Фактический протокол до изменений

`NetworkBootstrap.StartHost/StartClient` → `ReplicationLab` устанавливает `MeteredTransport` → `UnityTransport.Send/PollEvent` → NetworkDriver → UDPNetworkInterface, реальный UDP порт 7777. CLI `--pr4-port` меняет порт. Host — сервер и локальный игрок; каждый удалённый клиент имеет NGO ClientId, PlayerObject и UTP connection. ReplicationLab создаёт 50 props, управляет NetworkShow/NetworkHide, частотами и релевантностью.

Здесь нет пятибайтового Header, MOVEMENT/SHOOT enum или собственного ACK поверх NGO. UTP имеет заголовки соединения, pipeline и batched messages; NGO сериализует RPC/NetworkVariable через FastBufferWriter/Reader. ReliableSequencedPipelineStage отвечает за ACK, номера, повторы, дедупликацию и RTO. FireRpc надёжен, ShotEffectRpc и BroadcastStateRpc ненадёжны. Сессионные heartbeat/RTT находятся внутри UTP; прикладные Ping/Pong и SRTT/jitter/loss ПР2 остаются в LegacyUdp. Ранее криптографического handshake не было.

Обнаружено расхождение с формулировкой ТЗ: в **фактической ПР5** SubmitInputRpc использует Reliable, чтобы движение не обгоняло dash (см. комментарии и старые проверки). Поэтому нельзя утверждать, что эта версия изначально передавала MOVEMENT ненадёжно. Изменение этого поведения требует сохранения обработки dash и отдельной регрессионной проверки; фактический итог отражён в Security.md.

До изменений повторены Unity EditMode: **37 PASS / 0 FAIL**, PlayMode: **2 PASS / 0 FAIL**, LegacyUdp: **78 PASS / 0 FAIL** (21 protocol, 10 telemetry, 27 reliability, 20 integration). XML, runner JSON, stdout .NET сохранены в docs/pr6-evidence. В первом .NET запуске фиксированное имя baseline.trx перезаписывалось тестовыми проектами; полные результаты всех четырёх сборок сохранены в baseline.log. Итоговый повтор использует уникальные TRX имена.

## Карта изменений

| Существующий компонент | Доработка |
|---|---|
| MeteredTransport.Initialize | обязательный SecureDriverConstructor, сохранение payload-метрик |
| NetworkDriver/INetworkInterface | новый управляемый SecureNetworkInterface под штатными pipeline, все фактические UDP datagram |
| NetworkBootstrap | DNS, CLI адрес, проверка отправителя после резолвинга |
| VerificationTransport | тот же защищённый интерфейс под существующим simulator |
| PredictionDemo.Runtime.asmdef | Unity.Collections, unsafe только для UTP queue API |
| Assets/Tests/EditMode | тесты криптографии, handshake, replay, валидации, лимитов |
| Tools | реальные native player сценарии, UDP-реле с записью точных datagram |
| docs и README | формат, модель угроз, доказательства, Cloudflare, запуск и ограничения |

Примитивы — поддерживаемая BouncyCastle.Cryptography 2.7.0, netstandard2.0, MIT. Не используется платформенно несовместимый AesGcm Unity Mono. Поддержка managed INetworkInterface проверена непосредственно в установленном UTP 6.6: NetworkDriver.Create<N> допускает INetworkInterface без unmanaged constraint, NetworkInterfaceWrapper обеспечивает managed callbacks. Default pipeline configurations берутся из установленного NGO, код пакетов не редактируется.

CLI 1.0.0-beta.13 сообщает о доступном обновлении. Обновление не выполнялось: notification remediation требует user approval; существующие build/test команды работоспособны. Нет запущенного Editor, проверки выполняются headless установленной Unity.
