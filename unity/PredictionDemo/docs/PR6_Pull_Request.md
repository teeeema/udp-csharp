# feat: protect DUSTLINE UDP traffic with per-session AES-256-GCM

В ПР5 весь трафик Unity Transport передавался без шифрования. Теперь установленный под UTP managed network interface согласует эфемерные X25519 ключи, выводит разные c2s/s2c AES-256 keys через HKDF-SHA256 и подтверждает ключи перед пропуском игровых datagram. Шифруются также UTP ACK, heartbeat, надёжная стрельба и репликация; исходные UTP headers/pipelines сохраняются внутри authenticated ciphertext. Финальный outer header включён в AAD до шифрования.

Добавлены 64-packet replay window, bounded handshake/session/IP tables, rate limiting, retries/timeouts и безопасные события. Reconnect с тем же endpoint заменяет старые ключи только после нового Confirm. Обычное движение возвращено к Unreliable, dash вынесен в Reliable RPC с отдельной дедупликацией. NGO prediction/reconciliation, авторитетная стрельба и observer/scheduler ПР5 сохранены.

Validation: Unity 99/99 EditMode (62 новых crypto/protocol tests + прежние 37), 2/2 PlayMode, .NET historical regression 78/78. Реальные native player tests, включая двух удалённых клиентов, drop/duplicate/tag tampering, 100 ms UTP simulator и relevance/hide/show/despawn, перечислены с фактическими PASS/FAIL в docs/PR6_Report.md. Доступны logs, CSV, screenshots самой игры и точные relay payloads/USER0 PCAPNG. PCAPNG сетевого интерфейса и Wireshark GUI не подтверждены из-за BPF permissions / отсутствия Wireshark.

Ограничения: сервер ещё не аутентифицирован (X25519 не устраняет active MITM); production throughput/WAN/IL2CPP не проверены. Cloudflare Spectrum/реальная защита origin — BLOCKED_EXTERNAL, подготовлен пошаговый план, инфраструктура и платные услуги не изменены. 48 B overhead на UTP datagram плюс отдельный handshake; измерения см. отчёт.

## Checklist

- [x] Код встроен в реальную игру, plaintext fallback отсутствует.
- [x] Проверки crypto, protocol, regression и реального client/server сохранены.
- [x] Документация формата, threat model, build/run и честные внешние статусы.
- [ ] Аутентификация сервера для production.
- [ ] Реальный Wireshark capture/screenshots.
- [ ] Enterprise Spectrum, origin firewall и внешний тест после разрешения.
- [ ] Ревью второго участника (не утверждается; PDF методички не предоставлен).
- [ ] Согласовать репозиторий/историю для публикации PR.

## Куда публиковать

Текущий Unity-проект ранее не имел Git. Локальный baseline `8c2998a`, ветка `feature/encryption`, implementation commits `453f361` и `0ca5f8e`; remote **не настроен**. Соседний исторический репозиторий указывает на [GitHub teeeema/prac-1-udp-csharp](https://github.com/teeeema/prac-1-udp-csharp), но там другая корневая структура (`unity/PredictionDemo`) и незакоммиченные пользовательские изменения. Он оставлен нетронутым.

Эта ветка готова к **локальному ревью**. Нельзя просто force-push независимую историю поверх старого remote. Для внешнего PR нужно либо создать согласованный репозиторий текущей ПР5/6, либо перенести текущую ПР5 baseline и этот diff в правильную ветку/подкаталог прежней истории отдельным недеструктивным действием. GitHub/GitVerse PR не создан, push не выполнялся. Этот файл — готовое описание, не ссылка на якобы опубликованный PR.
