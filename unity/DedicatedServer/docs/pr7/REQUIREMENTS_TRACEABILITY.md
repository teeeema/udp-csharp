# Соответствие заданию ПР7

| Методичка Unreal | Реализация DUSTLINE Unity | Приёмка |
|---|---|---|
| Server Target, cooking, staging | Unity Linux Dedicated Server subtarget и BuildPipeline с выбранной сценой | T01–T02 |
| Dedicated GameMode, PlayerController и авторитетность | NGO StartServer, Connection Approval, PredictedPlayer, TankWeapon | T03–T09 |
| Docker, VM и публикация UDP | Ubuntu Server 24.04 x86-64 в VirtualBox, Docker Engine и Compose | T10–T15, T28 |
| OnlineSubsystemSteam / CreateSession | Steamworks.NET GameServer и anonymous logon с объявлением dedicated | T16–T17 |
| FindSessions и делегаты | Internet-list + Steam callbacks, обработка тайм-аутов и отмены | T18–T19, T23 |
| JoinSession, GetResolvedConnectString, ClientTravel | Endpoint из Steam и запуск NGO клиента в той же игровой сцене | T20 |
| Disconnect, DestroySession и повтор | NGO shutdown, release request, Steam logoff и новый logon | T21–T22, T24–T26 |
| Steam Sockets NetDriver | SteamNetworkingSockets direct-IP как носитель datagram, защищённый NGO / UTP | T20, T27 |
| Replication и серверная симуляция | Имеющиеся правила ПР4–ПР6 и оптимизированный режим dedicated | T06–T07, T27 |
| Журналы, скриншоты и GitVerse | Исходники, Compose, инструкции и проверяемые артефакты в Git | См. TEST_RESULTS и FINAL_REPORT |

Понятия UE `Target.cs`, `IOnlineSession`, `NetDriver`, `ClientTravel` адаптированы как операции сборки, сетевого старта и сессии Unity. Целевой проект и сцена остаются прежними.

Условия учебной работы дополнены требованиями пользователя об обязательном шифровании ПР6, нулевом бюджете и двух сетевых профилях. Блокирующие внешние требования вынесены в результаты соответствующих T-сценариев.
