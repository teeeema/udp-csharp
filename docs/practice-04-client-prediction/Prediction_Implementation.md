# ПР №4 — Client-Side Prediction и Server Reconciliation в Unity

## Проект и версия

Название демонстрационного проекта: **PredictionDemo** (`unity/PredictionDemo`). Практическая работа №4 выполнена в **Unity** по согласованию с преподавателем.

- Unity: **6000.3.8f1 (Unity 6.3.8f1)**;
- Netcode for GameObjects: **2.13.3**;
- Unity Transport: **2.6.0**;
- Input System: **1.18.0**.

В доступной документации проекта ФИО автора/состав команды не указаны; владелец репозитория указан как `teeeema`, поэтому дополнительные данные не выдумываются.

`DEMO_COMMIT: this commit (see git rev-parse HEAD)`.

Хэш текущего commit нельзя корректно записать внутрь этого же commit: изменение строки с hash создаёт новый hash. Поэтому документ ссылается на собственный commit через `git rev-parse HEAD`, а фактический hash фиксируется в итоговом отчёте.

## Архитектура потока

```text
Client Input
→ Client Prediction
→ Intent / RPC
→ Server Validation
→ Authoritative State
→ Replication
→ Reconciliation
```

Основные компоненты:

| Unity-компонент | Назначение |
|---|---|
| `NetworkBootstrap` | запускает Host/Client, создаёт игроков, выбирает профиль сети и режим prediction |
| `PredictedPlayer` | локальный input, prediction, RPC, server processing, replication и reconciliation |
| `PredictionInput` | sequence, movement intent, dash request и локальные данные для replay |
| `PredictionRules` | нормализация input, правила dash и расчёт displacement |
| `NetworkLabSettings` | профили задержки/потерь и детерминированная logical packet loss |

Клиент создаёт возрастающий `sequence`, применяет input локально при включённом CSP и кладёт его в `_pending`. После моделируемой one-way задержки surviving intent уходит через `SubmitInputRpc`. Сервер проверяет input и рассчитывает собственное состояние. Authoritative state возвращается клиенту; владелец удаляет подтверждённые input, устанавливает server state и повторно применяет оставшиеся pending input.

## Server Authority в этом проекте

Клиент не передаёт серверу готовые координаты, dash distance или client-only multiplier. RPC содержит только:

```text
sequence, move, dashRequested, profileId
```

`ProcessServerInput` реально выполняет следующие проверки и действия:

- отвергает устаревший/повторный `sequence` (`sequence <= _serverLastSequence`);
- повторно нормализует `move` через `PredictionRules.SanitizeMove`;
- требует ненулевое направление для dash через `PredictionRules.CanDash`;
- проверяет server-side cooldown;
- для разрешённого dash использует фиксированный authoritative multiplier `1.0f`;
- рассчитывает displacement на сервере;
- применяет `ResolveCollision`;
- отправляет authoritative position и cooldown обратно клиентам.

Таким образом client-only 2× dash не может заставить сервер принять увеличенную дистанцию.

## Client-Side Prediction в этом проекте

При `PredictionEnabled=true` локальный владелец применяет `PredictionInput` немедленно до server confirmation. Prediction реально двигает объект, а не только меняет HUD.

Для latency-sensitive action используется **dash**:

- WASD — движение;
- `Space` — обычный dash;
- `B` — намеренный client-only 2× dash для демонстрации divergence.

В T3 при 100 ms one-way delay локальная реакция dash измерена как **0.0 ms**, после чего приходит authoritative ACK.

## Server Reconciliation в этом проекте

При authoritative update владелец:

1. игнорирует stale ACK (`sequence <= _lastAckSequence`);
2. удаляет подтверждённые записи из `_pending`;
3. устанавливает authoritative position;
4. повторно проигрывает оставшиеся pending input;
5. измеряет correction distance.

Для remote players stale state также отбрасывается через `_remoteLastSequence`.

В T5 клиент намеренно создаёт расхождение и дополнительно предсказывает запрещённый второй dash во время server cooldown. Runtime evidence:

```text
[PR4][Server] seq=6 dash=ACCEPT validation=accepted cooldown=1.250s pos=(3.480, 1.000, -4.000)
[PR4][Evidence] T5 invalid second dash intentionally predicted during server cooldown
[PR4][Server] seq=16 dash=REJECT validation=rejected:cooldown cooldown=1.050s pos=(4.280, 1.000, -4.000)
[PR4][Reconcile] ack=16 error=0.535m pending=17 authoritative=(4.280, 1.000, -4.000)
```

Текущая измеренная correction distance: **0.535 m**. Сервер отклоняет второй dash, а клиент возвращается к authoritative state и продолжает работу.

## T2 против T3

Профиль: **100 ms one-way / 0% loss**.

- T2, prediction OFF: authoritative visible dash reaction **210.4 ms**;
- T3, prediction ON: local dash reaction **0.0 ms**, затем authoritative ACK.

Это показывает эффект CSP: локальное управление реагирует сразу, но окончательное состояние всё равно задаёт сервер.

## Network simulation T1–T5

| Test | Profile | Prediction | Scenario |
|---|---|---|---|
| T1 | 0 ms / 0% | ON | normal movement + dash |
| T2 | 100 ms / 0% | OFF | baseline authoritative dash |
| T3 | 100 ms / 0% | ON | immediate predicted dash |
| T4 | 175 ms / 5% | ON | movement + two legal dashes + deterministic bidirectional loss |
| T5 | 175 ms / 5% | ON | intentional 2× divergence + second invalid dash during cooldown + reconciliation |

Logical packet loss применяется детерминированно в `NetworkLabSettings.ShouldDrop`. Client→server intent RPC остаётся reliable, а лабораторная потеря применяется до RPC; server→client state также проходит через детерминированный logical drop. Это делает T4/T5 воспроизводимыми.

## Код

Ключевые файлы:

- `unity/PredictionDemo/Assets/Scripts/Prediction/NetworkLabSettings.cs`;
- `unity/PredictionDemo/Assets/Scripts/Prediction/PredictionRules.cs`;
- `unity/PredictionDemo/Assets/Scripts/Prediction/PredictionInput.cs`;
- `unity/PredictionDemo/Assets/Scripts/Prediction/NetworkBootstrap.cs`;
- `unity/PredictionDemo/Assets/Scripts/Prediction/PredictedPlayer.cs`;
- `unity/PredictionDemo/Assets/Tests/EditMode/PredictionRulesTests.cs`;
- `unity/PredictionDemo/Assets/Editor/ProjectBootstrap/PredictionDemoBuilder.cs`.

## Media и runtime evidence

- `docs/media/baseline_no_prediction.png` — T2, Prediction OFF, 210.4 ms;
- `docs/media/prediction_enabled.png` — T3, Prediction ON, 0.0 ms;
- `docs/media/correction_example.png` — T5, divergence/reconciliation, max error 0.535 m;
- `docs/test-results/pr4-t1-local-*.log`;
- `docs/test-results/pr4-t2-baseline-*.log`;
- `docs/test-results/pr4-t3-prediction-*.log`;
- `docs/test-results/pr4-t4-stress-*.log`;
- `docs/test-results/pr4-t5-correction-*.log`.

Три PNG визуально проверены; все имеют размер 960×540 и соответствуют своим сценариям. `MEDIA_VISUAL_CHECK: PASS`.

## Тесты и build

Финальный Unity EditMode прогон: **6/6 Passed, 0 Failed** (`docs/test-results/pr4-editmode-final5.xml`). В том числе есть отдельная проверка `Dash_RejectsSecondRequestDuringCooldown`.

Регрессия ПР №1–№3: **78/78 Passed, 0 Failed** (21 Protocol + 10 Telemetry + 27 Reliability + 20 Integration).

macOS standalone собран повторно: `docs/test-results/pr4-build-final5.log` содержит `Build Finished, Result: Success`. Готовая `.app`:

`unity/PredictionDemo/Builds/PR4PredictionDemo.app`

Свежий T4 был запущен непосредственно из этого `.app`; host и client завершились с кодом `0`, поэтому smoke launch подтверждён.

Сборка выполняется с `--burst-disable-compilation`: установленный Burst 1.8.27 в этой среде ранее падал внутри native `CompileLibrary`, а runtime-логика ПР №4 Burst не использует.

## Известные ограничения

- Эмуляция 5% потерь реализована на уровне логики приложения и детерминированно, а не через системный network conditioner.
- Это учебная демонстрация CSP/reconciliation; production lag compensation, rollback и snapshot interpolation не реализуются.
- Standalone собирается как development build для демонстрации и логирования.
