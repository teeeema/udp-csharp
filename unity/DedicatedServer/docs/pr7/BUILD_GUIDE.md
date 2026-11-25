# Сборка сервера и клиента

Установите редактор **6000.6.7f1** через Unity Hub и модуль **Linux Dedicated Server Build Support** именно к нему. В Windows можно указать путь к редактору параметром `-UnityPath`. Для клиента нужен Windows Build Support (Mono). Unity Package Manager использует Git из PATH для закреплённой Steamworks.NET.

```powershell
pwsh -File scripts/build-server.ps1
pwsh -File scripts/build-server.ps1 -Target WindowsClient
```

Linux CLI использует `-buildTarget Linux64 -standaloneBuildSubtarget Server`. `DedicatedServerBuilder.BuildLinux` передаёт `BuildTarget.StandaloneLinux64` и `StandaloneBuildSubtarget.Server` в BuildPipeline. Пакет создаётся в `Builds/PR7/LinuxServer`. Нужны исполняемый файл, UnityPlayer и каталог `DustlineServer_Data`. Логи сборки находятся в игнорируемом `Builds/PR7/logs`. [Unity 6000.6.7f1](https://unity.com/releases/editor/whats-new/6000.6.7f1) · [Unity Dedicated Server](https://docs.unity3d.com/6000.6/Documentation/Manual/dedicated-server-build.html)

```sh
chmod +x DustlineServer.x86_64
./DustlineServer.x86_64 -batchmode -nographics -logFile - \
  --pr7-listen 0.0.0.0 --pr7-port 7777 --pr7-max-players 4 \
  --pr7-map PredictionDemo --pr7-name 'DUSTLINE Dedicated' --pr7-profile ip --pr7-log-level info
```

Допустимые карты — `PredictionDemo` и её отображаемое имя `Copper Basin`. Для Steam используйте отдельный query port `--pr7-query-port` и разрешённый `--pr7-app-id`. `--pr7-server` в обычном клиенте есть для локальной диагностики. Для T01 требуется отдельный Linux Server Build. На Windows в меню F2 выберите Direct IP Connect, введите хост и UDP-порт либо используйте CLI клиента `--pr4-client --pr6-address 127.0.0.1 --pr4-port 7777`.

После запуска сервера можно автоматически подключить два клиента. Скрипт сохраняет оба изображения, логи и состояние игроков, а клиент A переподключается в том же процессе.

```powershell
pwsh -File scripts/test-ip.ps1 -ServerAddress 192.168.56.101 -EvidenceDirectory Builds/PR7/ip-smoke
```
