# DUSTLINE Dedicated Server

Unity-проект ПР7. Этот снимок подготовлен из локального коммита `55e5e1c`, на котором проводились проверки. Используйте Unity **6000.6.7f1** с Linux Dedicated Server Build Support и Windows Build Support (Mono). Основная сцена — `Assets/Scenes/PredictionDemo.unity`.

Из папки `unity/DedicatedServer` соберите Linux-сервер и Windows-клиент:

```powershell
pwsh -File scripts/build-server.ps1
pwsh -File scripts/build-server.ps1 -Target WindowsClient
```

Готовые каталоги появятся в `Builds/PR7/LinuxServer` и `Builds/PR7/WindowsClient`. На клиенте для подключения по адресу откройте **F2 → Direct IP Connect**. Введите адрес своего сервера и его UDP-порт (по умолчанию `7777`).

- [Руководство по сборке](docs/pr7/BUILD_GUIDE.md)
- [Развёртывание в Ubuntu VM и Docker](docs/pr7/DOCKER_GUIDE.md)
- [Профиль Steam и текущее состояние проверок](docs/pr7/STEAM_INTEGRATION.md)
- [Результаты и доказательства](docs/pr7/TEST_RESULTS.md)
- [Шифрование игрового транспорта в ПР6](../PredictionDemo/docs/Security.md)
