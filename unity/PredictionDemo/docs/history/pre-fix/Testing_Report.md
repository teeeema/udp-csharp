# Реальное тестирование — 06.11.2026

TOTAL **108**, PASS **108**, FAIL **0** — сумма отдельных тестов и межпроцессных проверок ниже. Матрица требований не прибавляется повторно к счётчику.

| Набор | TOTAL | PASS | FAIL | Артефакт |
|---|---:|---:|---:|---|
| .NET protocol | 21 | 21 | 0 | test-results/udp/*.trx |
| .NET telemetry | 10 | 10 | 0 | test-results/udp/*.trx |
| .NET reliability | 27 | 27 | 0 | test-results/udp/*.trx |
| .NET integration | 20 | 20 | 0 | test-results/udp/*.trx |
| Unity EditMode | 18 | 18 | 0 | test-results/editmode.json |
| Unity PlayMode, составной smoke test | 1 | 1 | 0 | test-results/playmode-final.json |
| Host/Client отдельные процессы | 11 | 11 | 0 | verification/checks.json и логи |

58 исторических модульных UDP-тестов перепроверены, плюс найдены и выполнены 20 интеграционных тестов. SDK 10.0.403 установлен во временный /tmp/pr5-dotnet; исходники скопированы в LegacyUdp для повторения. Unity PlayMode использует порт 7790. Пробный синхронный запуск PlayMode не исполнился: Pipeline требует async_tests. При позднем опросе HTTP случился timeout, но фактический завершённый результат сохранён из Temp/pipeline_test_status.json в playmode-final.json: 1/1 PASS, длительность None с. Это не подмена timeout результатом PASS: файл содержит завершённый вердикт Unity Test Runner.

## Матрица 12 сценариев

| Сценарий | Статус / граница доказательства |
|---|---|
| ≥50 объектов | PASS: PlayMode проверяет 50 IsSpawned, baseline Client получает 50 |
| Подключение | PASS: Host+Client, 2 игрока в CSV, connected=True в verification |
| Репликация состояния | PASS: одинаковый NetworkObjectId и актуальное здоровье при возврате |
| Серверное изменение → клиент | PASS: Health=70 меняется сервером при скрытии; клиент видит 70 после NetworkShow |
| Фильтрация релевантности | PASS: targeted id отсутствует у клиента в дальней зоне |
| Повторное появление | PASS: тот же id возвращается с текущим состоянием |
| Сниженная частота | PASS для правил 30/5/2 Hz в EditMode; CSV подтверждает снижение потока. Отдельный packet-level cadence тест NOT_EXECUTED |
| Критичные события | PASS: сервер принял owner=1 FireRpc, Client получил эффект |
| Базовая регрессия | PASS: 78 UDP, прежние правила CSP/коллизий, smoke-подбор предмета. Полная ручная матрица ПР4 NOT_EXECUTED |
| Сбор метрик | PASS: реальные положительные TX Host / RX Client в парных CSV; не wire-level capture |
| Одинаковая игра в режимах | ЧАСТИЧНО: одинаковая нагрузка и серверная симуляция, правила частоты отдельно. Полный gameplay equivalence тест NOT_EXECUTED |
| Отключение | PASS для локального Shutdown в PlayMode и штатного закрытия процессов; аварийный WAN disconnect NOT_EXECUTED |

NOT_EXECUTED — перечисленные расширенные проверки; BLOCKED — нет оставшихся блокировок запуска основной лаборатории. Самостоятельный Unreal Network Profiler недоступен в Unity и заменён функционально.

Межпроцессные проверки:

- object_seen_before_hide: PASS
- object_hidden: PASS
- object_reappeared_current_state: PASS
- object_deleted_on_client: PASS
- client_connected: PASS
- server_hidden_health_changed: PASS
- client_received_current_health_after_reentry: PASS
- server_despawn: PASS
- critical_shoot_accepted: PASS
- shoot_effect_received: PASS
- no_runtime_exceptions: PASS

Изображения verification/host.png и client.png получены ScreenCapture из работающих процессов. Сборка выполнена BuildPipeline на macOS (Development), сцена PredictionDemo. Визуальная проверка выявила наложение HUD; панель ПР5 перенесена под заголовок, финальный снимок проверен отдельно. Допустимы предупреждения старых TMP shaders / URP при импорте. Логи verification проверяются на конкретные runtime exceptions; отсутствие всех возможных сетевых дефектов этим не доказывается.
