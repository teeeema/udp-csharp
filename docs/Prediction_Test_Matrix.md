# ПР №4 — матрица проверок Client-Side Prediction

## Проверенная конфигурация

- Unity: **6000.3.8f1 (Unity 6.3.8f1)**.
- Netcode for GameObjects: **2.13.3**.
- Unity Transport: **2.6.0**.
- Input System: **1.18.0**.
- macOS standalone: `unity/PredictionDemo/Builds/PR4PredictionDemo.app`.
- EditMode tests: `docs/test-results/pr4-editmode-final5.xml` — **6/6 Passed, 0 Failed**.
- Regression ПР №1–№3: **78/78 Passed, 0 Failed**.
- Player build: `docs/test-results/pr4-build-final5.log` — **Build Finished, Result: Success**.
- Standalone smoke: host и client запущены из `.app` в T4; оба процесса завершились с кодом `0`.

## T1–T5

| Test | Network conditions | Action | Expected | Actual | Artifact |
|---|---|---|---|---|---|
| T1 — PASS | 0 ms / 0%, prediction ON | Client движется и выполняет dash | Обычная синхронизация, мгновенная локальная реакция, authoritative server | Client: `dash-input seq=42`, local reaction **0.0 ms**, затем authoritative ACK. Server: `seq=42 dash=ACCEPT validation=accepted`, authoritative pos `(6.175, 1.000, -4.000)`. Host/client завершились без runtime failure. | `docs/test-results/pr4-t1-local-host.log`, `docs/test-results/pr4-t1-local-client.log` |
| T2 — PASS | 100 ms one-way / 0%, prediction OFF | Client движется и выполняет dash без CSP | Dash виден только после authoritative ответа | Client: `dash-input seq=42 prediction=False`; measured authoritative reaction **210.4 ms**. Server: `seq=42 dash=ACCEPT validation=accepted`. | `docs/test-results/pr4-t2-baseline-host.log`, `docs/test-results/pr4-t2-baseline-client.log`, `docs/media/baseline_no_prediction.png` |
| T3 — PASS | 100 ms one-way / 0%, prediction ON | Client движется и выполняет dash с CSP | Немедленная локальная реакция, затем server ACK | Client local reaction **0.0 ms**; server принимает `seq=42`; client получает authoritative ACK. | `docs/test-results/pr4-t3-prediction-host.log`, `docs/test-results/pr4-t3-prediction-client.log`, `docs/media/prediction_enabled.png` |
| T4 — PASS | 175 ms one-way / 5% logical loss, prediction ON | Обычное движение + два разрешённых dash | CSP остаётся отзывчивым, server authority сохраняется, loss не ломает соединение | Dash `seq=42` и `seq=122`: local reaction **0.0 ms**, server `ACCEPT` для обоих. Реальные deterministic `DROP C->S` и `DROP S->C` зафиксированы; host/client завершились с кодом `0`. | `docs/test-results/pr4-t4-stress-host.log`, `docs/test-results/pr4-t4-stress-client.log` |
| T5 — PASS | 175 ms one-way / 5% logical loss, prediction ON, intentional divergence | Client локально предсказывает 2× dash, затем намеренно предсказывает второй dash во время server cooldown | Server отклоняет запрещённый intent, authoritative state исправляет клиента | Server: `seq=6 dash=ACCEPT`; затем `seq=16 dash=REJECT validation=rejected:cooldown`. Client: second predicted dash reaction **0.0 ms**; reconciliation `ack=16 error=0.535m ... authoritative=(4.280, 1.000, -4.000)`. Correction distance **0.535 m**. | `docs/test-results/pr4-t5-correction-host.log`, `docs/test-results/pr4-t5-correction-client.log`, `docs/media/correction_example.png` |

## Server authority и reject proof

Клиентский RPC передаёт только:

```text
sequence, move, dashRequested, profileId
```

Координаты, готовое состояние и локальный dash multiplier в RPC не передаются. Сервер сам нормализует `move`, проверяет sequence и возможность dash, использует authoritative dash multiplier `1.0f`, рассчитывает displacement и применяет collision resolution.

T5 даёт отдельное runtime-доказательство запрещённого intent:

```text
[PR4][Server] seq=6 dash=ACCEPT validation=accepted cooldown=1.250s pos=(3.480, 1.000, -4.000)
[PR4][Server] seq=16 dash=REJECT validation=rejected:cooldown cooldown=1.050s pos=(4.280, 1.000, -4.000)
[PR4][Reconcile] ack=16 error=0.535m pending=17 authoritative=(4.280, 1.000, -4.000)
```

Между `seq=6` и `seq=16` продолжается обычное движение, поэтому абсолютная позиция меняется; отклонён именно второй dash — дополнительный dash displacement сервер не применяет, а клиент исправляется к authoritative state.

## Проверка потерь T4/T5

Потери создаются детерминированно в `NetworkLabSettings.ShouldDrop`, поэтому сценарий воспроизводим. В свежем T4, например, зафиксированы:

- C→S drops: `25, 58, 60, 72, 96, 99, 116, 118, 140, 166, 167, 180, 192, 265, 281`;
- S→C drops: `52, 53, 71, 109, 117, 129, 131, 141, 159, 165, 173, 201, 222, 229, 250, 254, 257, 262, 264`.

## Воспроизводимый standalone-запуск

Исполняемый файл:

```bash
APP="$PWD/unity/PredictionDemo/Builds/PR4PredictionDemo.app/Contents/MacOS/PredictionDemo"
```

T2, два терминала:

```bash
"$APP" --pr4-host --pr4-profile 1 --pr4-exit-after 8
"$APP" --pr4-client --pr4-profile 1 --pr4-no-prediction --pr4-auto --pr4-exit-after 6
```

T3:

```bash
"$APP" --pr4-host --pr4-profile 1 --pr4-exit-after 8
"$APP" --pr4-client --pr4-profile 1 --pr4-auto --pr4-exit-after 6
```

T4:

```bash
"$APP" --pr4-host --pr4-profile 2 --pr4-exit-after 8
"$APP" --pr4-client --pr4-profile 2 --pr4-auto --pr4-exit-after 6
```

T5:

```bash
"$APP" --pr4-host --pr4-profile 2 --pr4-exit-after 8
"$APP" --pr4-client --pr4-profile 2 --pr4-auto-divergence --pr4-exit-after 6
```

Для скриншота к клиентской команде можно добавить `--pr4-screenshot <path> --pr4-screenshot-at <seconds>`.

## EditMode tests

Финальный XML `docs/test-results/pr4-editmode-final5.xml` содержит шесть успешных тестов:

1. `NetworkProfiles_MatchRequiredLabValues`;
2. `SanitizeMove_ClampsMagnitudeToOne`;
3. `Dash_RequiresDirectionAndFinishedCooldown`;
4. `Dash_RejectsSecondRequestDuringCooldown`;
5. `ClientOnlyDoubleDash_DivergesFromAuthoritativeDash`;
6. `PacketLoss_IsDeterministicAndOnlyEnabledForStressProfile`.

Итог: `total=6`, `passed=6`, `failed=0`.

## Медиа

Визуально проверены три PNG размером **960×540**:

- `docs/media/baseline_no_prediction.png` — профиль 100 ms / 0%, Prediction OFF, `Last dash reaction: 210.4 ms`;
- `docs/media/prediction_enabled.png` — профиль 100 ms / 0%, Prediction ON, `Last dash reaction: 0.0 ms`;
- `docs/media/correction_example.png` — профиль 175 ms / 5%, Prediction ON, automation divergence, `Max reconciliation error: 0.535 m`, correction count отображается в HUD.

`MEDIA_VISUAL_CHECK: PASS`.
