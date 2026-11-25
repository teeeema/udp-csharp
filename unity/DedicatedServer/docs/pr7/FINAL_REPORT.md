# Отчёт о ПР7 DUSTLINE

Цель — Linux Dedicated Server в Ubuntu 24.04/VirtualBox, воспроизводимый Docker, два внешних клиента и Steam Game Server c реальным discovery. ПР7 адаптирована к Unity 6000.6.7f1, NGO 2.13.3, UTP 6.6.0 и исходной сцене `PredictionDemo`.

## Реализация

- Сервер автоматически загружает сцену в Dedicated Server subtarget, вызывает StartServer и спавнит удалённых игроков. Существующие prediction, authority, стрельба, релевантность, UTP reliability и защищённые датаграммы PR6 используются и в новом сетевом пути.
- Клиентский сервис управляет поиском, соединением, disconnect и тайм-аутами. Меню F2 содержит Steam server browser и Direct IP Connect. Steam-профиль использует одну обёртку Steamworks.NET с реальным Game Server API и Steam Networking Sockets direct-IP.
- VirtualBox VM имеет 2 vCPU, 4 GiB памяти и 20 GiB динамического диска. Установлены Ubuntu 24.04.5 LTS, Docker CE и Compose из официального репозитория. SHA-256 установочного ISO записан в доказательства.
- `deploy/` содержит Dockerfile, Compose IP/Steam, entrypoint и шаблон конфигурации. `scripts/` содержит сборку, копирование, запуск, остановку, статус и автоматизированный аварийный тест.
- На Windows установлен Editor 6000.6.7f1 и его Linux Dedicated Server Build Support. Добавлена Steam-обёртка Steamworks.NET 2025.165.0. На VM подготовлены Valve SteamCMD и официальные бинарные файлы Playit.
- Unity разрешила Linux sysroot, SDK и Windows→Linux toolchain 1.1.1 при первой кросс-сборке. Редактор, NGO, UTP и URP остались на закреплённых версиях. Стоимость использованных сервисов и инструментов составила 0 ₽.

## Запуск и подтверждённые проверки

Linux-сборка находится в `Builds/PR7/LinuxServer`, Windows-клиент — в `Builds/PR7/WindowsClient`. В репозитории сохраняются исходники и рецепты сборки. Для локальной проверки откройте Windows-клиент и выберите **F2 → Direct IP Connect**, адрес `192.168.56.101` и UDP `7777`. Сервер IP-профиля развёрнут в виртуальной машине и подтверждён в состоянии running / healthy. SSH доступен через loopback-порт 2227 Windows.

На Docker-сервере подтвердились два одновременных клиента, взаимное отображение танков, движение, авторитетная стрельба и reconnect клиента в том же процессе. При SIGKILL Unity-плеера `RestartCount` вырос с 0 до 1, контейнер восстановил состояние healthy. Штатный stop вернул код 0. Windows-клиент показал сообщение об отключении при остановке сервера. [Проверки T01–T28](TEST_RESULTS.md) · [Скриншоты двух клиентов](evidence/2026-11-25-windows/vm-hostonly-stationary-views/)

| Нагрузка | CPU контейнера | Используемая RAM |
|---|---:|---:|
| Idle | 11,38 % | 148,4 MiB |
| Два клиента Windows | 14,51 % | 154,6 MiB |

Это короткие снимки `docker stats --no-stream`. При двух клиентах корневой раздел Ubuntu использовал 5,9 GiB из 20 GiB, доступно 13 GiB. Измеренный образ занимал 874 497 687 байт, серверный пакет в госте — 195 MiB. [Idle-замер](evidence/2026-11-25-windows/docker-ip-resources.txt) · [Замер с игроками](evidence/2026-11-25-windows/docker-with-two-clients.txt)

Steamworks.NET инициализировалась на Windows и Linux с согласованным App ID 480. Выделенный сервер успешно выполнил backend logon, включил объявление и ответил на стандартный запрос A2S в VM. Локальное подключение через Steam Networking Sockets direct-IP завершило PR6 handshake и получило подтверждение NGO. Публичный Internet-list вернул ноль совместимых записей при активном объявлении. В отдельных локальных попытках клиент получил `Remote_BadCert` (4003), один повтор после запуска Steam-контейнера завершился успешно. [Сетевой лог клиента](evidence/2026-11-25-windows/steam-final-direct-480.log) · [Ответ A2S](evidence/2026-11-25-windows/steam-local-a2s-retry-480.txt)

## Проверки и оставшиеся условия

Актуальные статусы T01–T28 и просмотренные артефакты находятся в [TEST_RESULTS.md](TEST_RESULTS.md). Аппаратные данные VM и правила сети описаны в [VM_SETUP.md](VM_SETUP.md) и [NETWORK_SETUP.md](NETWORK_SETUP.md).

Для полного закрытия нужен тест реальных двух внешних клиентов. Пользователь подтвердил, что таких устройств сейчас нет. Для Steam учебный App ID 480 согласован. Подтверждение видимости и присоединения через Steam Internet-list учитывается отдельно от backend-логина. SDR экспериментально не подтверждён.

Владелец может привязать подготовленный Playit-агент к аккаунту, создать бесплатный UDP-туннель и передать endpoint двум внешним тестировщикам. Для публичного Steam необходимо подтвердить видимость game/query endpoint и подключение через найденную запись. [Инструкция для Playit и Steam-портов](NETWORK_SETUP.md)

[Инструкции сборки](BUILD_GUIDE.md) · [Docker](DOCKER_GUIDE.md) · [Steam](STEAM_INTEGRATION.md) · [Соответствие методичке](REQUIREMENTS_TRACEABILITY.md)

## GitVerse

Публикация требует целевого репозитория и авторизации владельца. На старте remote отсутствовал. Для нового согласованного репозитория скопируйте его SSH URL из GitVerse, подтвердите отпечаток хоста по их руководству и выполните команды из корня проекта.

```sh
git status --short --branch
git remote add gitverse git@gitverse.ru:ACCOUNT/REPOSITORY.git
git ls-remote gitverse
git push -u gitverse feature/pr7-dedicated-steam
```

Если репозиторий уже содержит независимую историю, сначала согласуйте ветку и импорт. Локальные изменения Copper Basin сохранены отдельной точкой восстановления, описанной в исходном аудите. Для публикации этой редакции карты включите её проверенные исходники в отдельный коммит. Публикуйте исходники и доказательства после проверки отсутствия чувствительных данных. [Подключение к GitVerse по SSH](https://gitverse.ru/docs/collaborative/authentification/ssh-keys)
