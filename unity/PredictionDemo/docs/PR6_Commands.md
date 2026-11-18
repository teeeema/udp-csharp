# Фактически выполненные сборки и проверки

Рабочий каталог всех команд: `Practice5Replication`. Дата: 17.11.2026. Каждый результат сохранялся отдельно, предыдущие доказательства не заменялись. Список не включает обычные команды чтения исходников. JSON каждого native player command хранится внутри каталога опыта; final suite дополнительно сохраняет `native-suite-commands.json` с exit code.

## Unity NUnit — реальные запуски

```sh
unity test . --mode EditMode --output docs/pr6-evidence/baseline-editmode.xml --timeout 300 --format json
unity test . --mode PlayMode --output docs/pr6-evidence/baseline-playmode.xml --timeout 300 --format json
unity test . --mode EditMode --output docs/pr6-evidence/secure-editmode.xml --timeout 300 --format json
unity test . --mode PlayMode --output docs/pr6-evidence/secure-playmode.xml --timeout 300 --format json
unity test . --mode PlayMode --output docs/pr6-evidence/final-playmode.xml --timeout 300 --format json
unity test . --mode EditMode --output docs/pr6-evidence/final-editmode.xml --timeout 300 --format json
unity test . --mode EditMode --output docs/pr6-evidence/rekey-editmode.xml --timeout 300 --format json
unity test . --mode PlayMode --output docs/pr6-evidence/rekey-playmode.xml --timeout 300 --format json
unity test . --mode PlayMode --output docs/pr6-evidence/readiness-playmode.xml --timeout 300 --format json
unity test . --mode PlayMode --output docs/pr6-evidence/bounded-playmode.xml --timeout 300 --format json
unity test . --mode EditMode --output docs/pr6-evidence/accepted-editmode.xml --timeout 300 --format json
```

Stdout/stderr каждого запуска перенаправлены в соответствующий `*-run.json`. Последние по смыслу — accepted-editmode (все 99 тестов) и bounded-playmode (все 2 сценарных теста). Имена final-* отражают промежуточный этап до усиления reconnect; исторические результаты не выдаются за новый запуск.

## Сборка Unity — успешные запуски

```sh
unity run . --timeout 600 --log-file docs/pr6-evidence/build-editor.log --no-tail --format json -- -executeMethod PredictionDemo.EditorTools.SecurityBuilder.Build
unity run . --timeout 600 --log-file docs/pr6-evidence/build-final-editor.log --no-tail --format json -- -executeMethod PredictionDemo.EditorTools.SecurityBuilder.Build
unity run . --timeout 600 --log-file docs/pr6-evidence/build-ready-editor.log --no-tail --format json -- -executeMethod PredictionDemo.EditorTools.SecurityBuilder.Build
unity run . --timeout 600 --log-file docs/pr6-evidence/build-bounded-editor.log --no-tail --format json -- -executeMethod PredictionDemo.EditorTools.SecurityBuilder.Build
```

Результат: Builds/PR6/ReplicationLab.app. Runner JSON — build-run.json / build-final-run.json / build-ready-run.json / build-bounded-run.json. Эфемерный Unity CLI access token в command line Editor log заменён на `[REDACTED_EPHEMERAL_UNITY_TOKEN]` до помещения в Git; у текстовых .log нормализованы завершающие пробелы/переводы строк, packet JSONL/PCAPNG не изменялись. Это инфраструктурный токен Unity, не игровой ключ. Runtime DLL SHA-256 сохранён в каждом build-fingerprint.json; early/native финальные результаты различаются сборкой корректно.

## .NET regression

SDK найден вне PATH, поэтому фактически использован полный путь:

```sh
'/Users/tema/Library/Application Support/Code/User/globalStorage/ms-dotnettools.vscode-dotnet-runtime/.dotnet/dotnet' test LegacyUdp/UdpGame.sln --logger 'trx;LogFileName=baseline.trx' --results-directory docs/pr6-evidence/legacy-baseline
'/Users/tema/Library/Application Support/Code/User/globalStorage/ms-dotnettools.vscode-dotnet-runtime/.dotnet/dotnet' test LegacyUdp/UdpGame.sln --logger trx --results-directory docs/pr6-evidence/legacy-final
```

Stdout — legacy-baseline.log / legacy-final.log. В baseline четыре проекта перезаписывали один TRX, поэтому полный исходный stdout необходим; final сохраняет четыре уникальных TRX. В обоих прогонах 21+10+27+20=78 PASS.

## Native game — исходные и промежуточные опыты

```sh
python3 Tools/run_pr6_game.py --output docs/pr6-evidence/before --duration 30 --optimized
python3 Tools/run_pr6_game.py --app Builds/PR6/ReplicationLab.app --output docs/pr6-evidence/after --duration 30 --optimized --secure
python3 Tools/run_pr6_game.py --app Builds/PR6/ReplicationLab.app --output docs/pr6-evidence/multiclient-faults --duration 30 --optimized --secure --clients 2 --impair
python3 Tools/run_pr6_game.py --app Builds/PR6/ReplicationLab.app --output docs/pr6-evidence/replication --duration 37 --optimized --secure --replication-verify
python3 Tools/run_pr6_game.py --app Builds/PR6/ReplicationLab.app --output docs/pr6-evidence/replication-final --duration 37 --optimized --secure --replication-verify
```

Первый replication запуск был сделан с ранней версией helper, одновременно включавшей CSP-движение; это увело игрока за радиус релевантности и дало 17 PASS / 1 FAIL. Исправлен сам сценарий: для replication-verify выбирается idle, а движение/reconciliation проверяются отдельно. Исправленный запуск — 16 PASS / 0 FAIL. Оригинальные player command JSON и FAIL сохранены. Позже helper получил отдельную проверку уникальности SHOOT sequence и собственных координат каждого игрока; ранние result.json не пересчитывались задним числом.

## Native game — финальная сборка

Последовательно, без одновременных performance-пар:

```sh
python3 Tools/run_pr6_game.py --output docs/pr6-evidence/before-baseline
python3 Tools/run_pr6_game.py --output docs/pr6-evidence/after-final --app Builds/PR6/ReplicationLab.app --optimized --secure
python3 Tools/run_pr6_game.py --output docs/pr6-evidence/after-baseline --app Builds/PR6/ReplicationLab.app --secure
python3 Tools/run_pr6_game.py --output docs/pr6-evidence/multiclient-final --app Builds/PR6/ReplicationLab.app --optimized --secure --clients 2 --impair
python3 Tools/run_pr6_game.py --output docs/pr6-evidence/replication-final-build --app Builds/PR6/ReplicationLab.app --optimized --secure --replication-verify --duration 37
python3 Tools/run_pr6_game.py --output docs/pr6-evidence/latency100 --app Builds/PR6/ReplicationLab.app --optimized --secure --transport-delay 100
```

В этой серии after-baseline и multiclient-final завершились до начала клиентского handshake: по фактическому relay trace первые Hello появились на ~35-й секунде после истечения 30-секундного таймера host. replication-final-build тоже не выполнил временные ожидания. Эти FAIL сохранены. after-final, before-baseline и latency100 прошли. Добавлен явный opt-in `--pr6-wait-players` в bootstrap и новый прогон на пересобранной игре:

```sh
python3 Tools/run_pr6_game.py --output docs/pr6-evidence/after-ready-optimized --app Builds/PR6/ReplicationLab.app --optimized --secure
python3 Tools/run_pr6_game.py --output docs/pr6-evidence/after-ready-baseline --app Builds/PR6/ReplicationLab.app --secure
python3 Tools/run_pr6_game.py --output docs/pr6-evidence/multiclient-ready --app Builds/PR6/ReplicationLab.app --optimized --secure --clients 2 --impair
python3 Tools/run_pr6_game.py --output docs/pr6-evidence/replication-ready --app Builds/PR6/ReplicationLab.app --optimized --secure --replication-verify --duration 37
python3 Tools/run_pr6_game.py --output docs/pr6-evidence/latency-ready --app Builds/PR6/ReplicationLab.app --optimized --secure --transport-delay 100
```

Продолжительность по умолчанию 30 с для каждого player **после готовности**, максимум ожидания подключения 90 с. Клиент стартует на 3 с позже host; обновлённое steady window — 17 с, начиная через 5 с после первого реального Data datagram, чтобы исключить время запуска приложения. replication — 37 с. Все сценарии запускают реальный .app executable, без заглушки игровой логики. `--impair` применяется к настоящим encrypted Data packets после 7 с: drop every 23rd, duplicate every 19th, corrupt tag every 37th relay observation. Подменённые дополнительные пакеты не заменяют валидную отправку. `--transport-delay 100` использует сохранённый UTP simulator по 100 мс на входе каждой стороны.

Ready graphics серия выявила нестабильность при нескольких uncapped графических процессах (handshake timeout у одного клиента и неуспевший прийти health при возврате). Оба сценария повторены без рендеринга теми же .app процессами: **20/20** и **16/16**, с тем же build fingerprint. Эти прогоны подтверждают nominal transport/replication, но не объявляются успешными screenshots или доказательством устойчивости при любой графической нагрузке.

```sh
python3 Tools/run_pr6_game.py --output docs/pr6-evidence/replication-headless --app Builds/PR6/ReplicationLab.app --secure --optimized --replication-verify --duration 37 --headless
python3 Tools/run_pr6_game.py --output docs/pr6-evidence/multiclient-headless --app Builds/PR6/ReplicationLab.app --secure --optimized --clients 2 --impair --headless
```

Последующее улучшение verification: только при явном `--pr6-wait-players` задаётся 30 FPS; клиент ждёт все PlayerObject. Helper сопоставляет процессы с реальным LOCAL_CLIENT id, а не предполагает порядок подключения. После пересборки выполнены также графические проверки:

```sh
python3 Tools/run_pr6_game.py --output docs/pr6-evidence/multiclient-bounded --app Builds/PR6/ReplicationLab.app --secure --optimized --clients 2 --impair
python3 Tools/run_pr6_game.py --output docs/pr6-evidence/replication-bounded --app Builds/PR6/ReplicationLab.app --secure --optimized --replication-verify --duration 37
python3 Tools/run_pr6_game.py --output docs/pr6-evidence/baseline-bounded --app Builds/PR6/ReplicationLab.app --secure
python3 Tools/run_pr6_game.py --output docs/pr6-evidence/optimized-bounded --app Builds/PR6/ReplicationLab.app --secure --optimized
python3 Tools/run_pr6_game.py --output docs/pr6-evidence/latency-bounded --app Builds/PR6/ReplicationLab.app --secure --optimized --transport-delay 100
```

## Доказательства и проверки файлов

```sh
tcpdump -i lo0 -c 1 -w /tmp/pr6-permission-check.pcap udp port 7777
# Выше: exit 1, BPF Permission denied. Права не менялись.
python3 Tools/export_pr6_evidence.py
tcpdump -r docs/pr6-evidence/after/relay-payloads.pcapng -c 1 -xx
python3 -m py_compile Tools/run_pr6_game.py Tools/export_pr6_evidence.py
git diff --check
```

PCAPNG USER0 прочитан tcpdump; UNSUPPORTED означает отсутствие decoder для USER0, а не ошибку контейнера. GUI-разбор Lua/Wireshark NOT_VERIFIED. Первый git whitespace check нашёл стандартные trailing spaces Unity .meta и CRLF сторонней лицензии; в новых файлах нормализованы, финальный результат проверен отдельно. Изменять чужие .meta или код вне ПР6 для этого не требовалось.
