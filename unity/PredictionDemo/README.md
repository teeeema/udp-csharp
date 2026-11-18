# DUSTLINE — Практическая работа №6

Unity-версия сетевой игры с защищённым UDP-транспортом и low-poly ареной Copper Basin.

## Запуск

Откройте эту папку в Unity **6000.6.7f1** и сцену `Assets/Scenes/PredictionDemo.unity`. Для Windows выберите в Build Settings **Windows, x86_64**, убедитесь, что эта сцена включена в сборку, и соберите проект. Сборки и кэши Unity в Git не хранятся.

В игре нажмите **Host Game**. Для второго локального игрока запустите второй экземпляр, оставьте адрес `127.0.0.1` и нажмите **Join**. Управление: WASD — движение, Space — рывок, F или ЛКМ — выстрел. F3 открывает сетевую диагностику, F4 — статистику репликации.

## Практическая работа №6

Транспорт интегрирован под Unity Transport: X25519/HKDF-SHA256, AES-256-GCM для UDP-датаграмм, подтверждение обмена ключами, replay protection и ограничения ресурсов. Шифрование обязательно; незашифрованного fallback нет. Неаутентифицированный X25519 сам по себе не защищает от активной MITM.

- [Сверка требований методички](docs/PR6_Methodichka_Audit.md)
- [Отчёт по практике](docs/PR6_Report.md)
- [Модель безопасности и ограничения](docs/Security.md)
- [Исходное задание](docs/PR6_Original_Request_RU.md)
- [Свежие результаты на Windows](docs/pr6-evidence-windows-2026-11-18/)

В свежем прогоне сохранены результаты EditMode (99/99), PlayMode (2/2), многоклиентного сценария и проверки репликации с задержкой 100 мс. Файлы relay JSONL содержат UDP-датаграммы из локального реле и не являются захватом Wireshark с сетевого адаптера. Cloudflare Spectrum, правила firewall на origin и NIC-capture на внешней машине в этом прогоне не проверялись.

Для повторения сценариев используйте отдельные новые каталоги результатов:

```powershell
python Tools/run_pr6_game.py --app Builds/PR6-Windows-CopperBasin/DUSTLINE.exe --output docs/windows-multiclient-run --secure --optimized --clients 2 --duration 26 --impair
python Tools/run_pr6_game.py --app Builds/PR6-Windows-CopperBasin/DUSTLINE.exe --output docs/windows-replication-run --secure --optimized --clients 1 --duration 34 --transport-delay 100 --replication-verify
```

Обе команды предполагают, что вы отдельно собрали Windows player по инструкции выше и путь к .exe существует.
