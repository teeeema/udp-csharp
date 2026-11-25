# Результаты ПР7

25 ноября 2026 г., Windows 11 и Ubuntu VM. Шаги, условия и ожидаемые результаты каждого ID приведены в [TEST_PLAN.md](TEST_PLAN.md). Доказательства помещены в [2026-11-25-windows](evidence/2026-11-25-windows/).

Итого — **19 PASS**, **9 BLOCKED**. Промежуточные неуспешные попытки описаны после таблицы.

| ID | Статус | Фактический результат | Доказательство |
|---|---|---|---|
| T01 | PASS | Unity 6000.6.7f1 собрала StandaloneLinux64 / Server. Linux file подтвердил ELF x86-64. | [артефакт](evidence/2026-11-25-windows/build-6000.6.7f1.txt) |
| T02 | PASS | Финальный контейнер загрузил PredictionDemo и вывел SERVER_READY с clients=0 и localPlayer=false. | [артефакт](evidence/2026-11-25-windows/docker-final-ip.txt) |
| T03 | PASS | Windows-клиент A по Host-Only завершил PR6 handshake и получил NGO client ID 1. | [артефакт](evidence/2026-11-25-windows/vm-hostonly-two-clients/client-A.log) |
| T04 | PASS | Клиент B подключился одновременно и получил NGO client ID 2. | [артефакт](evidence/2026-11-25-windows/vm-hostonly-two-clients/client-B.log) |
| T05 | PASS | Оба клиента видят два танка. Контрольные PNG сняты с двух отдельных процессов Windows. | [артефакт](evidence/2026-11-25-windows/vm-hostonly-stationary-views/) |
| T06 | PASS | Оба клиента записали по 50 выборок с двумя игроками. Позиции локальных и удалённых объектов менялись. | [артефакт](evidence/2026-11-25-windows/vm-docker-observed-movement.txt) |
| T07 | PASS | Linux-сервер записал принятые выстрелы и попадания. Оба клиента получили синхронизированные счётчики выстрелов. | [артефакт](evidence/2026-11-25-windows/vm-docker-authoritative-shots.txt) |
| T08 | PASS | После PLAYER_LEAVE клиента 1 сервер сохранил игрока 2 и продолжил обработку команд. | [артефакт](evidence/2026-11-25-windows/vm-docker-ip-lifecycle.txt) |
| T09 | PASS | Клиент A без перезапуска получил новый NGO client ID 3. Старый ID 1 был удалён. | [артефакт](evidence/2026-11-25-windows/vm-hostonly-two-clients/client-A.log) |
| T10 | PASS | Compose собрал dustline-pr7:local в Ubuntu 24.04.5 x86-64. | [артефакт](evidence/2026-11-25-windows/docker-game-deploy.txt) |
| T11 | PASS | Финальный контейнер healthy. UDP 7777 опубликован, процесс работает от UID 10001. | [артефакт](evidence/2026-11-25-windows/docker-final-ip.txt) |
| T12 | BLOCKED | Пользователь подтвердил отсутствие внешних клиентов. | — |
| T13 | BLOCKED | Пользователь подтвердил отсутствие внешних клиентов. | — |
| T14 | PASS | SIGKILL отправлен Unity-процессу. RestartCount вырос 0 → 1, контейнер вернулся в healthy. | [артефакт](evidence/2026-11-25-windows/docker-game-recovery.txt) |
| T15 | PASS | После Compose stop сервер записал SERVER_STOPPED, скрипт подтвердил exit 0. | [артефакт](evidence/2026-11-25-windows/docker-game-recovery.txt) |
| T16 | PASS | Steamworks.NET в Windows-клиенте инициализирован под App ID 480. | [артефакт](evidence/2026-11-25-windows/steam-client-480-first.log) |
| T17 | BLOCKED | Linux Game Server с App ID 480 вошёл в backend и запросил публикацию. A2S из Ubuntu подтвердил dedicated=true. Публичная видимость записи не подтверждена. | [лог публикации](evidence/2026-11-25-windows/docker-steam-final-retry.txt) · [A2S](evidence/2026-11-25-windows/steam-local-a2s-retry-480.txt) |
| T18 | BLOCKED | Реальный Internet-list вернул 0 совместимых записей, пока Linux Game Server объявлял сессию. | [артефакт](evidence/2026-11-25-windows/steam-live-internet-search-480.log) |
| T19 | PASS | Проверены фильтры проекта, версии и профиля, а также проверка заявленного количества игроков. | [артефакт](evidence/2026-11-25-windows/regression-after-replication-6000.6.7f1-EditMode.xml) |
| T20 | BLOCKED | В списке отсутствует endpoint для Join Server. Локальная проверка по известному IPv4 прошла через Steam Networking Sockets, PR6 handshake и NGO JOIN_OK. | [артефакт](evidence/2026-11-25-windows/steam-final-direct-480.log) |
| T21 | BLOCKED | Steam-соединение штатно закрывалось при завершении успешного диагностического клиента. В прогоне через UI Steam-соединение отклонилось с Remote_BadCert. Через общий HUD в IP-профиле выход проверен. | [Steam UI log](evidence/2026-11-25-windows/steam-final-disconnect-ui.log) · [IP UI](evidence/2026-11-25-windows/final-ip-ui-disconnect.png) |
| T22 | BLOCKED | Для повторного поиска и Join нужен видимый Steam Internet-list endpoint. | [артефакт](evidence/2026-11-25-windows/steam-live-internet-search-480.log) |
| T23 | PASS | Без App ID команда поиска перешла в Error и записала диагностическое сообщение. Проверка физически закрытого Steam отдельно не выполнялась. | [артефакт](evidence/2026-11-25-windows/steam-client-missing-app-id.log) |
| T24 | PASS | Во время игры выполнен Compose stop. Windows-клиент вернулся в меню и показал сообщение Disconnected due to server shutting down. Контейнер поднят снова. | [артефакт](evidence/2026-11-25-windows/ip-hostonly-client-loss.png) |
| T25 | BLOCKED | Штатный stop Linux-контейнера вызвал STEAM_DEREGISTER_REQUESTED. Исчезновение из Internet-list требует ранее видимой записи. | [лог остановки](evidence/2026-11-25-windows/docker-steam-final-stop.txt) |
| T26 | BLOCKED | После запуска VM наблюдались повторные Steam Game Server init и advertise. Видимость серверной записи после рестарта не подтверждена. | [артефакт](evidence/2026-11-25-windows/steam-hostonly-server-diagnostics.txt) |
| T27 | PASS | Unity 6000.6.7f1 после исправлений прошла 114 EditMode и 2 PlayMode. LegacyUdp на Windows прошёл 78 тестов. Игровой IP-прогон подтвердил шифрование и игровую репликацию. | [артефакт](evidence/2026-11-25-windows/regression-after-replication-6000.6.7f1-PlayMode.xml) |
| T28 | PASS | Idle составил 11,38% CPU и 148,4 MiB. С двумя игроками — 14,51% CPU и 154,6 MiB. Корневая ФС VM заняла 5,9 GiB из 20 GiB. | [артефакт](evidence/2026-11-25-windows/docker-with-two-clients.txt) |

В первом Windows-прогоне зашифрованное рукопожатие прошло, затем NGO отклонил connection request из-за настроек approval. Запуск тестов с активным Server Subtarget выявил нежелательный автозапуск сервера в Editor. Оба дефекта исправлены. Задержка загрузки Ubuntu устранена настройкой паравиртуализации VM. Двусторонний игровой обмен Windows → VM подтверждён по Host-Only после неудачного опыта с loopback NAT.

В одном диагностическом Steam-подключении через известный IPv4 зафиксирован `Remote_BadCert` (4003). После повторного запуска Steam-контейнера соединение прошло с `relayPop=0` и последующим PR6 handshake. Поиск совместимой записи через Internet-list остаётся неподтверждённым. Промежуточные журналы сохранены отдельно. Результаты ПР6 остаются в первоначальных каталогах.
