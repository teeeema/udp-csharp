# DUSTLINE — Практическая работа №6

Unity-версия сетевой игры с защищённым UDP-транспортом и low-poly ареной Copper Basin.

## Запуск

Откройте папку `unity/PredictionDemo` в Unity **6000.6.7f1**. В **File → Build Profiles** выберите целевую платформу, включите сцену `Assets/Scenes/PredictionDemo.unity` и соберите проект для Windows x86_64 или macOS. Для macOS также доступно меню **PR6 → Build secure macOS**. В результате появится `Builds/PR6/ReplicationLab.app`.

В игре нажмите **Host Game**. Для второго локального игрока запустите второй экземпляр, оставьте адрес `127.0.0.1` и нажмите **Join**. Управление: WASD — движение, Space — рывок, F или ЛКМ — выстрел. F3 открывает сетевую диагностику, F4 — статистику репликации.

Игровой порт по умолчанию — **UDP 7777**. Флаг `--pr4-port` задаёт порт, `--pr6-address` задаёт адрес или DNS-имя сервера на стороне клиента. Пример для macOS из папки Unity-проекта. Выполните команды в двух терминалах.

```sh
./Builds/PR6/ReplicationLab.app/Contents/MacOS/PredictionDemo --pr4-host --pr4-port 7777
./Builds/PR6/ReplicationLab.app/Contents/MacOS/PredictionDemo --pr4-client --pr6-address 127.0.0.1 --pr4-port 7777
```

## Практическая работа №6

Транспорт интегрирован под Unity Transport: X25519/HKDF-SHA256, AES-256-GCM для UDP-датаграмм, подтверждение обмена ключами, replay protection и ограничения ресурсов. Шифрование обязательно; незашифрованного fallback нет. Неаутентифицированный X25519 сам по себе не защищает от активной MITM.

- [Security.md — модель угроз, формат пакета и защита](docs/Security.md)
- [Cloudflare_Spectrum_Setup.md — настройка UDP, DNS, firewall и приёмка](docs/Cloudflare_Spectrum_Setup.md)
- [Сверка требований и статус сдачи](docs/PR6_Methodichka_Audit.md)
- [Wireshark — сравнение до и после шифрования](docs/pr6-wireshark-2026-11-21/)
- [Pull Request №10 в GitHub](https://github.com/teeeema/udp-csharp/pull/10)

На macOS 21.11.2026 Unity прошла 99/99 EditMode, 2/2 PlayMode и 20/20 сетевых проверок с двумя клиентами. Захваты Ethernet/IP/UDP получены с интерфейса `eth0` локального Docker-реле. Для проверки через Spectrum требуется активная зона с поддержкой UDP и доступ к игровому серверу. [Результаты тестов и файлы захвата](docs/pr6-wireshark-2026-11-21/)

Для сравнения откройте `before.pcap` и `after.pcap` в Wireshark с параметром `-X lua_script:Tools/pr6_dissector.lua`. Примените `udp.port == 7777` в обоих файлах и `dustline6.type == 11 && dustline6.kind == 0` для зашифрованных игровых пакетов. [Команды и номера кадров](docs/pr6-wireshark-2026-11-21/#как-повторить-просмотр)

## Тесты

Из этой папки запустите `unity test . --mode EditMode` и `unity test . --mode PlayMode` либо выберите соответствующие режимы в **Window → General → Test Runner**.

В [прогоне на Windows от 18.11.2026](docs/pr6-evidence-windows-2026-11-18/) Unity 6000.6.9f1 прошла 20/20 проверок с двумя клиентами и 17/17 проверок репликации с задержкой 100 мс. Для повторения соберите Windows player и используйте новые каталоги результатов.

```powershell
python Tools/run_pr6_game.py --app Builds/PR6-Windows-CopperBasin/DUSTLINE.exe --output docs/windows-multiclient-run --secure --optimized --clients 2 --duration 26 --impair
python Tools/run_pr6_game.py --app Builds/PR6-Windows-CopperBasin/DUSTLINE.exe --output docs/windows-replication-run --secure --optimized --clients 1 --duration 34 --transport-delay 100 --replication-verify
```
