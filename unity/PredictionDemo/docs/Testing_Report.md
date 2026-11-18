# Проверки после исправлений — 06.11.2026

| Слой | TOTAL | PASS | FAIL | BLOCKED | NOT_EXECUTED |
|---|---:|---:|---:|---:|---:|
| Framework (.NET 78, Unity EditMode 37, PlayMode 2) | 117 | 117 | 0 | 0 | 0 |
| Логические network regression checks, с повтором неготовых попыток | 29 | 29 | 0 | 0 | 0 |
| Прежняя аудиторская матрица 48, текущие доказательства | 48 | 42 | 0 | 1 | 5 |

Дополнительные suites не суммируются с этими строками: event-handshake 17/17, soak 9/9, 100 ms 7/7, dynamic spawn 6/6, старые log predicates 11/11. Они перекрывают те же механики. Исторические PASS не перенесены. Machine results: final-testing/summary.json, merged-native-checks.json, UDP trx, Unity JSON; native .state/.log в соответствующих final-* каталогах.

Оригинальные 7 FAIL закрыты: speed baseline/optimized; restart baseline/optimized; RX count; occluded damage; material lifecycle. Из прежних NOT_EXECUTED закрыты пешие выход/возврат обоих режимов (4), ненулевая CSP correction under loss (2), повторный Spawn того же неуничтоженного NetworkObject (1). Остались 5 NOT_EXECUTED и один BLOCKED, перечисленные ниже. 28 ранее положительных сценариев обеспечены свежими данными: семь traffic-этапов, настоящий walking path, динамический spawn/delete с отдельным Native Client, Soak с двумя удалёнными клиентами, SIGKILL и reconnect, server-only motion, профили 100/0 и 175/5. На 120-секундном обычном движении limiter rejection count = 0 в обоих режимах.

## Регрессионные тесты

- EditMode: нормальный бюджет 4 м/с; 500-командный и длительный flood; пачка 100 мс; одинаковое/отрицательное/nonfinite серверное время; нулевое/NaN/Inf/overflow направление; ближайшая из двух целей и range; единичный RX после повторных Initialize и reset epoch; прежние CSP/коллизии/policy tests.
- PlayMode: четыре запуска Host; точно 50 props каждый раз; один callback spawner; duplicate/stale/NaN movement; blocked/open shot через настоящий FireRpc; повтор SHOOT; стена и range; tint через property block; Despawn(false) → Spawn того же объекта; 100 spawn/despawn без роста материалов; прежний smoke.
- Native: 500 real owner RPC, максимум 0,4 м, 495 rejected; надежная коррекция искусственного локального расхождения; три Shutdown/StartHost с новыми клиентами; 100 NetworkHide/Show; walking down/up через реальную границу и актуальные HP70 после возврата; танк блокирует damage, открытая цель HP100→90, здоровье видно клиенту.
- Transport: SimulatorPipelineStage ReceivedPacketsOnly, 100 ms/0% и 175 ms/5%, seed 8123, 2048 queued packets. Клиент и Host получают одинаковые условия, логический profile 0 исключает двойную эмуляцию. Стрельба и ненулевая CSP correction подтверждены. App-конфигурация и machine logs сохранены.
- Soak: около 185 с на режим, Host + два удалённых Client, позднее подключение, SIGKILL первого клиента, новый клиент; старый actor уходит после штатного transport timeout, затем остаются владельцы 0/2/3. Это ограниченный localhost soak, не часы/WAN.

Число материалов на тестовых peers не растёт на 100 hide/show (до/после в .state); память фиксируется после прогрева, допустимы ограниченные выделения GUI/network caches. Цвет при 90 HP проверен количественно (red .925) и кадр клиента визуально проверен. Нельзя экстраполировать конечный тест на отсутствие всех утечек за любое время.

## Границы

BLOCKED: полный UDP capture (BPF permission denied в аудите), системные права не менялись. NOT_EXECUTED: UTP byte totals ниже NGO, чистая машина без package cache, принудительная перестановка wire UDP, forged foreign-owner RPC, WAN/hours soak. Конечный архив распаковывается и проверяется отдельно; это не чистая пересборка на другом компьютере. Timestamp/deltaTime и произвольный target отсутствуют в клиентском RPC — NON_APPLICABLE как подаваемые поля; соответствующие серверные инварианты проверены.

Компиляция и BuildPipeline финального runtime успешны. Промежуточный missing test assembly reference был исправлен до тестов. Попытка queued build во время domain reload потеряла job и повторена после isCompiling=false. Исходные логи попыток сохранены, текущие PASS подтверждаются завершёнными verdicts, а не ответом «started».


## Проверка финального архива

ZIP с исходниками и macOS .app распакован в отдельный каталог. Проверены CRC, SHA-256 каждого файла по Final_Manifest.json и права запуска. Из распакованного приложения запущены обычные Host и Client без --fix-case (порт 7900, profile 0): оба видят 2 игроков и 50 props, входящий payload положительный, исключений не обнаружено, оба завершились с кодом 0. Проверки 7/7 PASS; логи, CSV и кадр — final-testing/archive-smoke. Это проверка готовой .app, не чистая пересборка без package cache. Финальный репак добавляет только документацию и доказательства; runtime DLL совпадает с проверенной и измеренной.

Старый Submission.zip и PreFix_Backup.zip повторно сверены с исходными SHA-256; не изменены. Статус соседнего prac_1 совпадает с исходным. Проверка исходного текста на непустые секреты дала только два присваивания переменной privateKey в коде Unity Pipeline; реальных credentials не обнаружено, Library/signing.key не упакован. Финальная SHA-256 хранится рядом с архивом в Practice5Replication_Final.sha256.
