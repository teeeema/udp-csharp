# Интеграция Steam

Используется **Steamworks.NET 2025.165.0 / SDK 1.65**, закреплённый на upstream commit `ea51b029a5dbcb47a99d3c1fac930b811672ed32`. У него есть обёртки client API, Game Server, Internet-list и Networking Sockets. Facepunch также предоставляет Game Server API. Unity Community Steam Networking Sockets заменяет NGO transport целиком и использует адресацию CSteamID. Здесь реализован собственный носитель для UTP, чтобы сохранить существующие UTP pipelines, метрики и PR6 AEAD. Требуемые Windows/Linux x86-64 API проверяются раздельной сборкой и подключением. [Steamworks.NET release](https://github.com/rlabrecque/Steamworks.NET/releases/tag/2025.165.0) · [Facepunch Game Server](https://wiki.facepunch.com/steamworks/SteamServer) · [Unity Community Transport](https://github.com/Unity-Technologies/multiplayer-community-contributions/tree/main/Transports/com.community.netcode.transport.steamnetworkingsockets)

## Регистрация и поиск

1. Подготовьте разрешённый App ID с включённым для проекта dedicated-путём. Пример из методички с 480 проверяется только после согласования. В Docker смонтируйте Linux x86-64 `steamclient.so` из официального Valve SteamCMD строго read-only. В `deploy/.env` заполните `DUSTLINE_APP_ID`, `DUSTLINE_STEAMCLIENT` и необходимые порты. Для 480 дополнительно `DUSTLINE_ALLOW_APP_ID_480=1`. [Valve SteamCMD](https://developer.valvesoftware.com/wiki/SteamCMD)
2. Запустите `sudo sh scripts/server-start.sh steam`. После `GameServer.InitEx` и запуска NGO-сервера вызывается `LogOnAnonymous`. Успешный backend callback начинается с `STEAM_ADVERTISE_REQUESTED`. Для подтверждения публичной регистрации дождитесь результата Internet-list с другой машины. Callback логина сам по себе подтверждает только вход в backend. [Steam Game Server](https://partner.steamgames.com/doc/api/ISteamGameServer)
3. Запустите Windows-клиент при открытом и авторизованном Steam с `--pr7-app-id <id>`. Для 480 добавьте `--pr7-allow-app-id-480`. Откройте F2 → Find Servers, выберите DUSTLINE и нажмите Join Server. Загрузка игрового соединения подтверждается сообщением `JOIN_OK`.
4. Выйдите, обновите список и присоединитесь из того же процесса клиента. При остановке сервер вызывает `SetAdvertiseServerActive(false)`, `LogOff` и `GameServer.Shutdown`. Проверьте исчезновение после обновления списка, затем повторную публикацию после рестарта. Учтите возможное кэширование Steam.

Одиночную проверку интерфейса поиска можно запустить из CLI Windows-клиента с `--pr7-find-servers --pr7-app-id <id> --pr4-exit-after 30 -logFile steam-search.log`. Для согласованного App ID 480 добавьте флаг opt-in из шага 3.

В development-клиенте `--pr7-test-steam-address <IPv4> --pr4-port <порт>` проверяет соединение Steam Networking Sockets, защищённый UTP и NGO по заранее известному игровому адресу. Для этого также требуется разрешённый App ID. Запись в Steam Internet-list проверяется командой поиска.

| Метаданные | Публикация | Использование в клиенте |
|---|---|---|
| PROJECT_TAG | KeyValue и tag `dustline-pr7-copper-basin` | Точное совпадение |
| BUILD_ID | KeyValue и tag `build-1.0.0` | Точное совпадение плюс payload NGO Connection Approval |
| SERVER_NAME | KeyValue и `SetServerName` | Имя в Internet-list |
| MAP_NAME | KeyValue и `SetMapName` | Карта в Internet-list |
| GAME_MODE | KeyValue `Tank Arena` | Режим этой сборки выводится из известного BUILD_ID |
| MAX_PLAYERS | KeyValue и `SetMaxPlayerCount` | Поле ёмкости Steam |
| CURRENT_PLAYERS | KeyValue и tag `players-N` | Игровые подключения NGO, отображение по tag |
| NETWORK_PROFILE | KeyValue и tag `steam` | Точное совпадение |

Steam принимает предварительный `gametagsand` фильтр. После callback клиент повторяет фильтрацию и проверяет App ID. Перед подключением к полной сессии UI показывает ошибку, а фактическую вместимость подтверждает сервер. При отмене освобождается handle запроса, поздние callbacks проверяют текущий handle. Поисковый тайм-аут 20 секунд, подключение 25 секунд. Backend logon может автоматически восстановиться после сбоя. Поддерживается IPv4 через `CreateListenSocketIP` / `ConnectByIPAddress`, в логах выводится `relayPop`. SDR является отдельным сценарием и здесь не подтверждён. У App ID 480 общий каталог, ограничения anonymous logon, сертификатов и discovery определяются Steam. [Matchmaking API](https://partner.steamgames.com/doc/api/ISteamMatchmakingServers) · [Networking Sockets](https://partner.steamgames.com/doc/api/ISteamNetworkingSockets)

## Результат на этой установке

Linux dedicated под App ID 480 объявлялся в Steam после входа в backend. Стандартный A2S запрос из Ubuntu вернул имя сервера, карту, App ID 480 и dedicated=true. Клиент выполнил Internet-list поиск во время объявления и получил ноль подходящих записей. Steam Networking Sockets подключились по IPv4 Host-Only с `relayPop=0`, затем зарегистрированы PR6 handshake и NGO JOIN_OK. В отдельной попытке локальный клиент получил код 4003 `Remote_BadCert`. Поэтому успешный локальный транспорт не подтверждает доступность публичного Steam-каталога или стабильность сертификата в этой конфигурации. [Результаты](TEST_RESULTS.md)
