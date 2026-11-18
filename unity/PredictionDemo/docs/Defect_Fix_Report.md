# Исправление пяти подтверждённых дефектов — 06.11.2026

Разрешённый объём: минимальные исправления существующего DUSTLINE, без замены NGO, обновления движка или пакетов. Исходный аудит сохранён без изменений вне проекта и скопирован в history/pre-fix. Git commit/push/reset не выполнялись.

Резервный архив: ../Practice5Replication_PreFix_Backup.zip. SHA-256: `3aa6a71fcca6da8f2fa613ca1019c39cb6158ef4a9cc1a9889368f365f84b53a`. Проверены CRC и SHA-256 каждого из 1256 сохранённых файлов. Старый Submission.zip сохранён; его SHA-256 `428d52d67bbe114b11004c0c0b8588216f57eaae0530b5448aa9155a68bacbf6`. Backup включает исходные Assets, Packages, ProjectSettings, инструменты, документы, LegacyUdp, старый архив и два отчёта аудита. Кэши исключены.

| ID | Первопричина | Исправление | Подтверждение |
|---|---|---|---|
| HIGH-01 | Каждый RPC применял фиксированный шаг без временного бюджета | ServerMovementBudget, серверные монотонные часы, finite validation, coalesced reliable owner correction | Нормальные 50 ticks/s; 500 RPC: 495 отклонены, максимум 0,4 м; оба режима |
| HIGH-02 | spawn flag сохранялся после Shutdown | OnServerStarted/OnServerStopped, сброс epoch статистики, симметричная отписка | 4 запуска в PlayMode; 3 рестарта и 4 клиентские волны в каждом режиме |
| HIGH-03 | Damage выполнялся до окончательного выбора ближайшего hit | ServerShotTrace выбирает physics/tank bounds, только затем один Damage | открытая цель, танк, стена, две цели, range, повтор SHOOT; удалённое здоровье 90 |
| MEDIUM-01 | Initialize добавлял Count повторно | remove-before-add, отписка в Shutdown, reset counters | Контролируемое событие 10 B после 1/2/4 Initialize учитывается как 10 B |
| MEDIUM-02 | renderer.material создавал незакрытые native Material | Cached Renderer + MaterialPropertyBlock | 100 spawn/despawn в PlayMode; 100 реальных hide/show на клиенте в обоих режимах |


Бюджет движения: не более 100 мс накопленного server credit, обычный шаг 20 мс. Для произвольного интервала документированный допуск burst — до 0,4 м сверх 4 м/с × время. Разрешённый dash отдельно ограничен расстоянием 3 м и cooldown 1,25 с. В нормальном сценарном прогоне не отклонено ни одной команды. Дубликаты/старые sequence не применяются; нечисловой ввод отклоняется. Timestamp/deltaTime клиента отсутствуют в wire-контракте: тестируются некорректные/неубывающие серверные часы и численные значения, а не несуществующие клиентские поля. Клиент не передаёт target/направление выстрела: сервер берёт свой transform.forward. Нулевая/нечисловая/переполненная длина направления отклоняется helper.

Проверки: 117 framework tests, 117 PASS; 29 логических сетевых regression checks, 29 PASS. Отдельные suites и исходные 11 predicates перечислены в Testing_Report.md; они не складываются как независимые сценарии. Максимальная зафиксированная коррекция при UTP 175 ms/5% — 2 м. Состав и длительность соак-теста — final-soak/details.json.

Основные изменённые файлы: ServerMovementBudget.cs (новый); PredictedPlayer.cs; ServerShotTrace.cs (новый); TankWeapon.cs; ReplicationLab.cs; NetworkBootstrap.cs; MeteredTransport.cs; ReplicatedProp.cs; EditMode/DefectRegressionTests.cs; PlayMode/DefectPlayModeTests.cs; EditMode asmdef. Для воспроизводимости добавлен отдельный неактивный по умолчанию CLI verification module в Assets/Diagnostics/Verification и Tools/run_*validation.py. Он не включается без --fix-case. Production runtime DLL совпадает с DLL измеренного кандидата; поля сцены и prefabs функционально сохранены.

Дополнительно SnapshotRpc отбрасывает устаревший phase. Это проверка порядка в коде, не полноценный тест перестановки wire-пакетов. Режимы и оптимизации сохранены; scheduler/relevance не переписаны. Изменения метрик начинаются с нового epoch, отрицательные дельты прежней сессии не используются.

Промежуточные неудачи не скрыты: первый набор дал 20/25; короткий speed-процесс не подключился, ожидание завершения клиентов сдвинуло waves и часть не попала в окно. Последующий targeted дал одно неподтверждённое четвёртое подключение. Все 4 waves затем проверены по фактическим событиям в final-restart-handshake, 17/17 PASS. Критерии скорости, число рестартов и 100 hide/show не ослаблялись; исправлялась подготовка/расписание тестов. Полные попытки сохранены.

Оставшиеся границы: wire UDP capture BLOCKED; lower-level UTP totals, чистая машина без кэша, принудительная перестановка UDP, forged foreign-owner RPC и WAN/многочасовой soak не выполнены. Сетевые профили ПР4 в обычной игре остаются отладочной логической эмуляцией; настоящая Transport simulation в проверках включается отдельно. Это учебный сервер, не полный production anti-cheat. Формальное согласование Unity вместо Unreal остаётся внешним условием.


## Проверка финального архива

ZIP с исходниками и macOS .app распакован в отдельный каталог. Проверены CRC, SHA-256 каждого файла по Final_Manifest.json и права запуска. Из распакованного приложения запущены обычные Host и Client без --fix-case (порт 7900, profile 0): оба видят 2 игроков и 50 props, входящий payload положительный, исключений не обнаружено, оба завершились с кодом 0. Проверки 7/7 PASS; логи, CSV и кадр — final-testing/archive-smoke. Это проверка готовой .app, не чистая пересборка без package cache. Финальный репак добавляет только документацию и доказательства; runtime DLL совпадает с проверенной и измеренной.

Старый Submission.zip и PreFix_Backup.zip повторно сверены с исходными SHA-256; не изменены. Статус соседнего prac_1 совпадает с исходным. Проверка исходного текста на непустые секреты дала только два присваивания переменной privateKey в коде Unity Pipeline; реальных credentials не обнаружено, Library/signing.key не упакован. Финальная SHA-256 хранится рядом с архивом в Practice5Replication_Final.sha256.
