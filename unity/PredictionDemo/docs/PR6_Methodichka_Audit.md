# Сверка ПР №6 с методичкой

18.11.2026. Источники сверки — пятистраничная методичка «Практическая работа №6. Шифрование UDP-трафика и защита от DDoS», приложенные C++-примеры и действующая Unity-игра DUSTLINE в ветке `feature/encryption`.

**Итог — частичное соответствие.** Криптографический обмен и интеграция с игровым UDP реализованы. Для полной сдачи требуются снимки Wireshark, рабочее подключение через Spectrum с закрытым origin и ревью Pull Request в GitVerse.

| Пункт методички | Статус | Результат сверки |
| --- | --- | --- |
| Типы `KEY_EXCHANGE = 10` и `ENCRYPTED = 11` | Выполнено в Unity | Типы находятся в открытом внешнем заголовке `DSL6`. Структура заголовка отличается от пяти байт C++-примера. [SecureProtocol.cs](../Assets/Scripts/Prediction/Security/SecureProtocol.cs) |
| X25519, 32-байтовые открытые ключи, HKDF-SHA256, сессия на адрес и порт | Выполнено в Unity | Bouncy Castle в C# вместо OpenSSL. Hello/Response передают эфемерные ключи и challenge. HKDF связывает сессионные ключи с обменом и выводит отдельные ключи на каждое направление. Confirm/Ready подтверждают владение ключами перед игрой. [SecureCrypto.cs](../Assets/Scripts/Prediction/Security/SecureCrypto.cs) |
| AES-256-GCM, открытый заголовок как AAD, nonce 12 байт, tag 16 байт | Выполнено | Формат `[Header20][Nonce12][Ciphertext][Tag16]`. В nonce входят направление и возрастающий счётчик. Полный заголовок, включая окончательный размер, аутентифицируется GCM. Защита от повтора обновляет окно после успешной проверки тега. [Security.md](Security.md) |
| Шифрование игрового UDP, рабочие ACK и повторы | Выполнено в Unity | Защита работает под UTP. SequenceNumber и ACK обрабатывает штатный reliable pipeline UTP внутри шифрованной нагрузки. Повтор UTP получает новый внешний счётчик. Обычное движение отправляется Unreliable, рывок и выстрелы — Reliable. [SecureNetworkInterface.cs](../Assets/Scripts/Prediction/Security/SecureNetworkInterface.cs) |
| Отклонить изменённый пакет и зарегистрировать `AUTH_FAIL` с адресом | Частично | Повреждённые ciphertext и tag отклоняются. `AUTH_FAIL` ограничен по частоте и записывается без адреса отправителя, который указан в методичке. [SecureTransportTests.cs](../Assets/Tests/EditMode/SecureTransportTests.cs) |
| Подключение через loopback, скриншоты Wireshark до и после | Частично | Есть реальные исторические UDP payload до и после, наблюдавшиеся на локальном реле, и их PCAPNG-экспорт с LinkType USER0. Артефакты сняты на macOS 17.11.2026. На Windows служба Npcap/WinPcap отсутствует, снимков Wireshark с захватом интерфейса в сдаче пока нет. [Исторические измерения](PR6_Report.md#измерения-трафика) |
| VM/origin, DNS, Spectrum через Cloudflare, проверка подключения и source IP | Открыто | На Windows проверяется локальный UDP. Для произвольного игрового UDP требуется Enterprise с дополнением Spectrum. У проекта есть план настройки origin, DNS и закрытия UDP-порта для остальных источников. Рабочая конфигурация и подключение через edge отсутствуют. [Инструкция проекта](Cloudflare_Spectrum_Setup.md), [тарифы Cloudflare](https://developers.cloudflare.com/spectrum/protocols-per-plan/) |
| Снимки панели и DNS, настройки чувствительности DDoS, список портов VM | Открыто | Схема и целевой игровой порт UDP 7777 задокументированы. Фактические правила, чувствительность и список портов VM проверяются на развёрнутом origin. [Cloudflare Spectrum](Cloudflare_Spectrum_Setup.md) |
| Документация, ветка, Pull Request и ревью в GitVerse | Частично | `docs/Security.md` содержит модель угроз, схему, wire format и ограничение MITM. Ветка `feature/encryption` содержит коммиты реализации и локальное описание PR. Публикация и ревью второго участника отсутствуют. [Описание PR](PR6_Pull_Request.md) |

### Сопоставление с учебными исходниками

`CMakeLists.txt`, `crypto.h`, `crypto.cpp`, `crypto_test.cpp`, `protocol.h`, `client_main.cpp` и `server_main.cpp` описывают отдельный C++17/OpenSSL-пример на UDP 9000. Файл `Security.md` из приложений задаёт шаблон отчёта. В рабочем проекте эти роли выполняют Unity C#, Bouncy Castle, UTP и NUnit.

В учебных `client_main.cpp` и `server_main.cpp` размер полезной нагрузки меняется после вычисления GCM-тега по заголовку. Получатель подставляет уже изменённый заголовок, поэтому проверка тега не проходит. В Unity заголовок с итоговым размером создаётся до шифрования.

## Проверка на Windows 18.11.2026

Собрана и запущена `Builds/PR6-Windows-CopperBasin/DUSTLINE.exe` (Windows x64, Mono). Для проверки использован установленный Unity **6000.6.9f1** в отдельной копии проекта. В исходном проекте закреплён **6000.6.7f1**. PNG сделаны самой Windows-игрой.

| Проверка | Результат |
| --- | --- |
| Unity EditMode и PlayMode | **99/99** и **2/2**. [EditMode XML](pr6-evidence-windows-2026-11-18/editmode.xml), [PlayMode XML](pr6-evidence-windows-2026-11-18/playmode.xml) |
| Три процесса, хост и два клиента | **20/20**. Подключение, движение, reconciliation и авторитетная стрельба. Локальное реле наблюдало 2125 UDP-датаграмм, внесло 92 потери, 107 дублей и 55 повреждений тегов. В журналах подтверждены `AUTH_FAIL` и `REPLAY_DROP`. [Результат](pr6-evidence-windows-2026-11-18/multiclient/result.json), [журнал хоста](pr6-evidence-windows-2026-11-18/multiclient/host.log), [UDP payload реле](pr6-evidence-windows-2026-11-18/multiclient/relay-datagrams.jsonl) |
| Поведение репликации при задержке UTP 100 мс | **17/17**. Клиент увидел мишень со здоровьем 100, перестал её получать за радиусом релевантности, вновь получил со здоровьем 70 и увидел удаление после despawn. [Результат](pr6-evidence-windows-2026-11-18/replication-100ms/result.json), [журнал клиента](pr6-evidence-windows-2026-11-18/replication-100ms/client1.log) |

В сценарии с двумя клиентами внешний формат добавил **48 байт на датаграмму**. В измеренном интервале это 76 560 дополнительных байт для 1 595 пакетов, или 52,66% от внутренних 145 372 байт UTP. В расчёт не входят IP/UDP-заголовки и установление сессии. [Результат измерения](pr6-evidence-windows-2026-11-18/multiclient/result.json)

Первый отчёт Windows по репликации показал четыре ложных сбоя. Скрипт ожидал десятичную точку в отметках времени, а журналы на этом ПК используют запятую. Скрипт теперь учитывает обе формы и продолжает при уведомлении Winsock 10054 после закрытия UDP-порта. Доказательства первого прогона сохранены. [Первый результат](pr6-evidence-windows-2026-11-18/initial-replication-locale/result.json)

Copper Basin использует новую геометрию танков, окраску своего и чужих танков, ортографическую камеру, разметку арены и компактный HUD. Сверка сцены подтвердила идентичные настройки и трансформации всех 52 существующих коллайдеров. [Меню](pr6-evidence-windows-2026-11-18/copper-basin-menu.png), [кадр со стороны клиента](pr6-evidence-windows-2026-11-18/replication-100ms/client1.png), [SHA-256 игровой сборки](pr6-evidence-windows-2026-11-18/build-fingerprint.json)

Для повторного запуска из корня проекта используйте новые каталоги результатов.

```powershell
python Tools/run_pr6_game.py --app Builds/PR6-Windows-CopperBasin/DUSTLINE.exe --output docs/windows-multiclient-run --secure --optimized --clients 2 --duration 26 --impair
python Tools/run_pr6_game.py --app Builds/PR6-Windows-CopperBasin/DUSTLINE.exe --output docs/windows-replication-run --secure --optimized --clients 1 --duration 34 --transport-delay 100 --replication-verify
```

### Контрольные вопросы

- Уникальность nonce требует гарантии для каждого ключа. Случайный `rand()` такой гарантии не даёт. Монотонный счётчик при раздельных ключах и новой сессии исключает повтор в пределах срока службы ключа.
- При активном MITM злоумышленник может согласовать независимую X25519-сессию с каждой стороной. Для защиты клиенту нужно проверять подпись обмена доверенным ключом сервера или аутентифицировать обмен заранее согласованным PSK.
- Ограничение доступа к origin направляет входящий игровой UDP через Cloudflare, даже если адрес origin известен.
- Без Proxy Protocol origin видит адрес узла Spectrum. Бан такого адреса затрагивает других клиентов за тем же прокси. Текущая схема использует адрес для транспортной привязки, а игроков различает по криптосессии и NGO ClientId.

