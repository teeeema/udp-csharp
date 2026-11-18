#!/usr/bin/env python3
from pathlib import Path
import json,hashlib,shutil,xml.etree.ElementTree as ET,re
R=Path(__file__).resolve().parents[1];E=R.parent/'Practice5Replication_FixEvidence';D=R/'docs'
def read(p):return json.loads(p.read_text())
traffic=read(D/'final-results/traffic-summary.json');core=read(D/'final-results/regression-checks.json');target=read(D/'final-targeted/checks.json');restart=read(D/'final-restart-handshake/checks.json');soak=read(D/'final-soak/checks.json');legacy=read(D/'final-verification/checks.json');latency=read(D/'final-latency100/checks.json');dynamic=read(D/'final-dynamic-atomic/checks.json')
merged=dict(core);merged.update(target)
for mode in ['baseline','optimized']:
 for i in range(4):merged[f'reconnect-{mode}-{i}']=restart[f'client-{mode}-{i}']
 merged[f'restart-{mode}']=all(restart[f'restart-{mode}-{i}'] for i in [1,2,3])
if not all(merged.values()) or not all(restart.values()) or not all(soak.values()) or not all(legacy.values()) or not all(latency.values()) or not all(dynamic.values()):raise SystemExit('Unclosed regression; reports must not claim all fixed')
edit=read(E/'editmode-complete.json')['data']['result']['Summary'];play=read(E/'playmode-complete.json')['summary'];udp={'total':0,'passed':0,'failed':0}
for p in (E/'udp').glob('*.trx'):
 c=ET.parse(p).getroot().find('.//{*}Counters')
 for k in udp:udp[k]+=int(c.attrib[k])
framework={'TOTAL':udp['total']+edit['Total']+play['total'],'PASS':udp['passed']+edit['Passed']+play['passed'],'FAIL':udp['failed']+edit['Failed']+play['failed'],'BLOCKED':0,'NOT_EXECUTED':0}
backup=read(E/'backup.json');before=read(E/'runtime-binary-before-harness-rebuild.json');runtime=R/'Builds/ReplicationLab.app/Contents/Resources/Data/Managed/PredictionDemo.Runtime.dll';same=hashlib.sha256(runtime.read_bytes()).hexdigest()==before['sha256']
proof=D/'final-testing';proof.mkdir(exist_ok=True)
for name in ['editmode-complete.json','playmode-complete.json','backup.json','build-final-stage.json','runtime-binary-comparison.json','runtime-source-freeze.json']:
 shutil.copy2(E/name,proof/name)
shutil.copytree(E/'udp',proof/'udp',dirs_exist_ok=True)
(proof/'merged-native-checks.json').write_text(json.dumps(merged,indent=2))
summary={'framework':framework,'native_logical_checks':{'TOTAL':len(merged),'PASS':sum(merged.values()),'FAIL':len(merged)-sum(merged.values())},'restart_event_checks':{'TOTAL':len(restart),'PASS':sum(restart.values())},'soak_checks':{'TOTAL':len(soak),'PASS':sum(soak.values())},'legacy_log_predicates':{'TOTAL':len(legacy),'PASS':sum(legacy.values())},'latency100_checks':{'TOTAL':len(latency),'PASS':sum(latency.values())},'dynamic_spawn_checks':{'TOTAL':len(dynamic),'PASS':sum(dynamic.values())},'old_audit_48':{'TOTAL':48,'PASS':42,'FAIL':0,'BLOCKED':1,'NOT_EXECUTED':5},'runtime_binary_unchanged_since_measurements':same}
(proof/'summary.json').write_text(json.dumps(summary,indent=2))
labels={'idle':'Неподвижные игроки','client_move':'Движение Client','both_move':'Движение обоих','shoot':'Стрельба','target_state':'Изменение мишени','zones':'Между зонами','combined':'Движение и стрельба обоих'}
rows=[]
for key,label in labels.items():
 v=traffic[key];a=v['baseline']['host']['tx_KBps'];b=v['optimized']['host']['tx_KBps'];rows.append(f'| {label} | {a:.3f} | {b:.3f} | {v["reduction_percent"]:.3f}% |')
table='\n'.join(rows)
conditions='''Unity 6000.6.7f1, NGO 2.13.3, Transport 6.6.0, URP 17.6.0; macOS Apple Silicon. Два отдельных процесса одной development-сборки, Host + один Client, 50 серверных props, profile 0. Сессия каждого режима — 120 с после старта сетевой роли. BASELINE и OPTIMIZED запускались последовательно; во время замеров другие сетевые диагностики не выполнялись. Сценарии задаются одинаковыми временными интервалами и кнопками. Реальное число выстрелов может немного отличаться из-за кадровой дискретизации и серверного cooldown. Измеряется NGO payload, KB=1000 байт, а не UDP wire-трафик; UTP ACK/повторы/UDP-IP заголовки исключены.

Окна по общим часам NGO ServerTime: покой 8–18 с; Client 21–31; оба 33–43; стрельба 45–55; состояния 57–67; зоны 69–95; комбинация 101–115. Начальная синхронизация исключена. Средняя скорость = разность накопленных байтов / фактическая длительность окна / 1000. Скрипт отклоняет окно, если не видит двух игроков. Сырые счётчики и входящий поток Client находятся в final-results/*.state и *.csv; агрегат — final-results/traffic-summary.json. Целевые частоты props 30 Hz / 5 Hz / 2 Hz, фактические ограничены Update/FPS.'''
(D/'Replication_Baseline.md').write_text('# BASELINE после исправлений — 06.11.2026\n\n'+conditions+'\n\nBASELINE посылает полный снимок всех 50 props каждому клиенту до 30 Hz, без distance culling и static sleep. Это намеренно наивная нагрузка: Position/Phase частично дублируются через NetworkVariable и SnapshotRpc. Это не штатный оптимальный NGO baseline и не profiler breakdown по каждому актору. В обоих режимах штатный dirty tracking NGO уже работает.\n\n| Сценарий | BASELINE TX Host KB/s | OPTIMIZED TX Host KB/s | Снижение |\n|---|---:|---:|---:|\n'+table+'\n\nПредыдущие отчёты сохранены в history/pre-fix; исторические docs/measurements не являются текущим прогоном.\n')
(D/'Replication_Optimization.md').write_text('# OPTIMIZED после исправлений — 06.11.2026\n\n'+conditions+'\n\n| Сценарий | BASELINE TX Host KB/s | OPTIMIZED TX Host KB/s | Снижение |\n|---|---:|---:|---:|\n'+table+f'''\n\nReduction = (Baseline - Optimized) / Baseline × 100%. Проценты вычислены из неокруглённых счётчиков, ориентир методички не использовался для подбора параметров. Для комбинированной нагрузки снижение {traffic['combined']['reduction_percent']:.3f}%; для покоя {traffic['idle']['reduction_percent']:.3f}%.

Две функциональные техники: (1) distance relevance — NetworkHide/Show отдельно для каждого клиента, радиус 38 м, проверка раз в 0,2 с; (2) scheduler — 5 Hz для мишеней, 2 Hz для индикаторов, отсутствие periodic snapshots статичных props. «Сон» является частью scheduler, а не третьей независимой движковой системой. Квантование до 0,1 м — порог изменений, float32 wire-размер Vector3 не сокращён. Dirty tracking NGO не считается новой оптимизацией. Игроки и критичные RPC не отсекаются по дистанции.

Большое снижение связано с избыточной базовой рассылкой и количеством нерелевантных/статичных объектов. Для активной игры выигрыш меньше, чем для покоя. Нельзя выдавать эти проценты за универсальную производительность NGO. Отдельная абляция и NGO-native baseline не выполнены. Начальный spawn ещё идёт всем наблюдателям до первого cull, поэтому стартовый пик не устранён полностью. Неизменность игрового runtime DLL между измерением и финальной пересборкой диагностического модуля: **{same}**; доказательство final-testing/runtime-binary-comparison.json.

Дополнительно проверены реальный пеший выход/возврат и актуальное состояние после возвращения, два удалённых клиента, повторные сессии и UTP simulation 100 ms/0%, 175 ms/5%. Полный UDP-захват остаётся BLOCKED из-за прав macOS BPF; права не менялись. Одного парного сценарного прогона недостаточно для статистической оценки всех сетей/карт. График: traffic-final.png.
''')
status_table='''| ID | Первопричина | Исправление | Подтверждение |
|---|---|---|---|
| HIGH-01 | Каждый RPC применял фиксированный шаг без временного бюджета | ServerMovementBudget, серверные монотонные часы, finite validation, coalesced reliable owner correction | Нормальные 50 ticks/s; 500 RPC: 495 отклонены, максимум 0,4 м; оба режима |
| HIGH-02 | spawn flag сохранялся после Shutdown | OnServerStarted/OnServerStopped, сброс epoch статистики, симметричная отписка | 4 запуска в PlayMode; 3 рестарта и 4 клиентские волны в каждом режиме |
| HIGH-03 | Damage выполнялся до окончательного выбора ближайшего hit | ServerShotTrace выбирает physics/tank bounds, только затем один Damage | открытая цель, танк, стена, две цели, range, повтор SHOOT; удалённое здоровье 90 |
| MEDIUM-01 | Initialize добавлял Count повторно | remove-before-add, отписка в Shutdown, reset counters | Контролируемое событие 10 B после 1/2/4 Initialize учитывается как 10 B |
| MEDIUM-02 | renderer.material создавал незакрытые native Material | Cached Renderer + MaterialPropertyBlock | 100 spawn/despawn в PlayMode; 100 реальных hide/show на клиенте в обоих режимах |
'''
(D/'Defect_Fix_Report.md').write_text(f'''# Исправление пяти подтверждённых дефектов — 06.11.2026

Разрешённый объём: минимальные исправления существующего DUSTLINE, без замены NGO, обновления движка или пакетов. Исходный аудит сохранён без изменений вне проекта и скопирован в history/pre-fix. Git commit/push/reset не выполнялись.

Резервный архив: ../Practice5Replication_PreFix_Backup.zip. SHA-256: `{backup['sha256']}`. Проверены CRC и SHA-256 каждого из 1256 сохранённых файлов. Старый Submission.zip сохранён; его SHA-256 `{backup['old_submission_sha256']}`. Backup включает исходные Assets, Packages, ProjectSettings, инструменты, документы, LegacyUdp, старый архив и два отчёта аудита. Кэши исключены.

{status_table}

Бюджет движения: не более 100 мс накопленного server credit, обычный шаг 20 мс. Для произвольного интервала документированный допуск burst — до 0,4 м сверх 4 м/с × время. Разрешённый dash отдельно ограничен расстоянием 3 м и cooldown 1,25 с. В нормальном сценарном прогоне не отклонено ни одной команды. Дубликаты/старые sequence не применяются; нечисловой ввод отклоняется. Timestamp/deltaTime клиента отсутствуют в wire-контракте: тестируются некорректные/неубывающие серверные часы и численные значения, а не несуществующие клиентские поля. Клиент не передаёт target/направление выстрела: сервер берёт свой transform.forward. Нулевая/нечисловая/переполненная длина направления отклоняется helper.

Проверки: {framework['TOTAL']} framework tests, {framework['PASS']} PASS; {len(merged)} логических сетевых regression checks, {sum(merged.values())} PASS. Отдельные suites и исходные 11 predicates перечислены в Testing_Report.md; они не складываются как независимые сценарии. Максимальная зафиксированная коррекция при UTP 175 ms/5% — 2 м. Состав и длительность соак-теста — final-soak/details.json.

Основные изменённые файлы: ServerMovementBudget.cs (новый); PredictedPlayer.cs; ServerShotTrace.cs (новый); TankWeapon.cs; ReplicationLab.cs; NetworkBootstrap.cs; MeteredTransport.cs; ReplicatedProp.cs; EditMode/DefectRegressionTests.cs; PlayMode/DefectPlayModeTests.cs; EditMode asmdef. Для воспроизводимости добавлен отдельный неактивный по умолчанию CLI verification module в Assets/Diagnostics/Verification и Tools/run_*validation.py. Он не включается без --fix-case. Production runtime DLL совпадает с DLL измеренного кандидата; поля сцены и prefabs функционально сохранены.

Дополнительно SnapshotRpc отбрасывает устаревший phase. Это проверка порядка в коде, не полноценный тест перестановки wire-пакетов. Режимы и оптимизации сохранены; scheduler/relevance не переписаны. Изменения метрик начинаются с нового epoch, отрицательные дельты прежней сессии не используются.

Промежуточные неудачи не скрыты: первый набор дал 20/25; короткий speed-процесс не подключился, ожидание завершения клиентов сдвинуло waves и часть не попала в окно. Последующий targeted дал одно неподтверждённое четвёртое подключение. Все 4 waves затем проверены по фактическим событиям в final-restart-handshake, 17/17 PASS. Критерии скорости, число рестартов и 100 hide/show не ослаблялись; исправлялась подготовка/расписание тестов. Полные попытки сохранены.

Оставшиеся границы: wire UDP capture BLOCKED; lower-level UTP totals, чистая машина без кэша, принудительная перестановка UDP, forged foreign-owner RPC и WAN/многочасовой soak не выполнены. Сетевые профили ПР4 в обычной игре остаются отладочной логической эмуляцией; настоящая Transport simulation в проверках включается отдельно. Это учебный сервер, не полный production anti-cheat. Формальное согласование Unity вместо Unreal остаётся внешним условием.
''')
(D/'Testing_Report.md').write_text(f'''# Проверки после исправлений — 06.11.2026

| Слой | TOTAL | PASS | FAIL | BLOCKED | NOT_EXECUTED |
|---|---:|---:|---:|---:|---:|
| Framework (.NET {udp['total']}, Unity EditMode {edit['Total']}, PlayMode {play['total']}) | {framework['TOTAL']} | {framework['PASS']} | {framework['FAIL']} | 0 | 0 |
| Логические network regression checks, с повтором неготовых попыток | {len(merged)} | {sum(merged.values())} | 0 | 0 | 0 |
| Прежняя аудиторская матрица 48, текущие доказательства | 48 | 42 | 0 | 1 | 5 |

Дополнительные suites не суммируются с этими строками: event-handshake {len(restart)}/{len(restart)}, soak {len(soak)}/{len(soak)}, 100 ms {len(latency)}/{len(latency)}, dynamic spawn {len(dynamic)}/{len(dynamic)}, старые log predicates 11/11. Они перекрывают те же механики. Исторические PASS не перенесены. Machine results: final-testing/summary.json, merged-native-checks.json, UDP trx, Unity JSON; native .state/.log в соответствующих final-* каталогах.

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
''')
# Keep the original adaptation mapping, then append the verified current status.
adapt=(D/'history/pre-fix/Unity_Adaptation.md').read_text();adapt+='''\n\n# После исправлений 06.11.2026\n\nПять подтверждённых дефектов исправлены и проверены, см. Defect_Fix_Report.md и Testing_Report.md. Новый профиль — семь сценариев в Replication_Optimization.md. Серверный бюджет ограничивает скорость реальным временем; репликация/observer API NGO и scheduler сохранены. Формальные UE API, grid, FOV и wire-профиль по-прежнему не заявляются как буквально выполненные. Архив Final включает исходники и локальную macOS .app; прежний Submission остаётся без изменений. Готовность к контролируемой Unity-демонстрации ПОДТВЕРЖДЕНА для проверенных сценариев, полная WAN/production/formal Unreal готовность — ЧАСТИЧНО ПОДТВЕРЖДЕНА/НЕДОСТАТОЧНО ДАННЫХ.\n''';(D/'Unity_Adaptation.md').write_text(adapt)
(R/'README.md').write_text('''# DUSTLINE / практическая работа №5 — исправленная версия

Unity 6000.6.7f1, NGO 2.13.3, Unity Transport 6.6.0, URP 17.6.0. Проверено на macOS Apple Silicon. Игра — исходный DUSTLINE с серверной авторитетностью, CSP/reconciliation, стрельбой и 50 сетевыми props.

## Запуск

Открыть папку в установленной Unity указанной версии, затем Assets/Scenes/PredictionDemo.unity. Play → Host/Client в игровом меню. Локальная собранная версия: Builds/ReplicationLab.app. Финальный ZIP содержит и исходники, и эту .app.

Два процесса из Terminal (из корня проекта):

```sh
./Builds/ReplicationLab.app/Contents/MacOS/PredictionDemo --pr4-host --pr4-profile 0
./Builds/ReplicationLab.app/Contents/MacOS/PredictionDemo --pr4-client --pr4-profile 0
```

Для OPTIMIZED добавить --optimized обоим процессам. Порт по умолчанию 7777; другой — --pr4-port 7900 на обоих. Адрес меняется в панели F3. Сервер без локального игрока — кнопка Dedicated server до подключения. Кнопка Switch mode на сервере меняет настоящую observer/scheduler логику. Shutdown/Restart теперь создаёт новую группу ровно из 50 props.

WASD — движение, Space — dash, F/ЛКМ — выстрел, B — намеренно неверное предсказание для диагностики, F3 — старая панель ПР4. Панель ПР5 показывает local props, видимость для первого удалённого клиента, игроков и реальные NGO payload TX/RX. Отображаемые 30/5/2 Hz — целевые частоты. В обычной игре старый логический профиль по умолчанию 100 мс; для опыта без эмуляции обязательно --pr4-profile 0. Настоящий Transport simulator включается только в CLI verification-тестах.

50 props: 20 статичных ящиков, 15 движущихся мишеней, 10 индикаторов, 5 pickups. Цвет задаётся через MaterialPropertyBlock. Танк блокирует выстрел по мишени; сервер сначала выбирает ближайший hit и затем применяет Damage. Клиент не передаёт готовую позицию, deltaTime или target. Скорость рассчитывает сервер с шагом 20 мс и максимум 100 мс burst credit (допуск до 0,4 м); dash имеет отдельный cooldown.

## Повторение проверок

```sh
python3 Tools/run_final_validation.py --output docs/new-validation-run
python3 Tools/run_targeted_regressions.py --output docs/new-targeted
python3 Tools/run_restart_handshake.py --output docs/new-restart
python3 Tools/run_soak_validation.py --output docs/new-soak
python3 Tools/run_latency100_validation.py --output docs/new-latency100
python3 Tools/run_final_log_verification.py --output docs/new-log-verification
```

Сценарные native suites занимают несколько минут. Скрипты принимают --output и отказываются перезаписывать существующие каталоги; для повторения выберите новое имя. Не запускайте performance-пары одновременно с другими игровыми опытами. Проверки dynamic spawn используют уже запущенный Editor Host на порту 7901 и выполняются отдельно — не запускайте этот инструмент без подготовки host. В нормальном запуске verification-модуль неактивен, нужен явный --fix-case.

Unity tests — Test Runner: PredictionDemo.EditModeTests и Replication.PlayModeTests. Сохранившийся UDP-проект: .NET SDK 10, `dotnet test LegacyUdp/UdpGame.sln`. Сборка: меню PR5 → Build macOS. Она использует ровно Assets/Scenes/PredictionDemo.unity.

Метрики — payload NGO (KB=1000 B), без UDP/IP заголовков и транспортных ACK/повторов. Свежие результаты — docs/final-results и final-*/checks.json. Старые docs/measurements и history/pre-fix — история, не текущий прогон. Подробности: docs/Defect_Fix_Report.md, Testing_Report.md, Replication_Optimization.md, Unity_Adaptation.md. Буквальное соответствие Unreal/GitVerse и принятие Unity преподавателем требуют отдельного согласования. Это development-сборка; WAN, часы работы и чистая машина не проверены.

Резервная копия и старый Submission.zip сохранены рядом с проектом. Commit/push не выполнялись. Основные пять дефектов закрыты; оставшиеся ограничения описаны честно в отчётах.
''')
try:
 import matplotlib;matplotlib.use('Agg');import matplotlib.pyplot as plt
 keys=list(labels);a=[traffic[k]['baseline']['host']['tx_KBps'] for k in keys];b=[traffic[k]['optimized']['host']['tx_KBps'] for k in keys];x=list(range(7));fig,ax=plt.subplots(figsize=(12,5));bars=ax.bar([v-.2 for v in x],a,.4,label='BASELINE');bars2=ax.bar([v+.2 for v in x],b,.4,label='OPTIMIZED');ax.bar_label(bars,fmt='%.2f',padding=3,fontsize=8);ax.bar_label(bars2,fmt='%.2f',padding=3,fontsize=8);ax.set_xticks(x,['Idle','Client','Both','Shoot','State','Zones','Combined']);ax.set_ylabel('NGO payload KB/s (1000 B)');ax.set_ylim(0,max(a)*1.15);ax.legend();fig.tight_layout();fig.savefig(D/'traffic-final.png',dpi=160);plt.close(fig)
except ImportError:pass
print(json.dumps(summary,indent=2))

archive_note = D/'Archive_Verification.md'
if archive_note.exists():
 for report in ['Testing_Report.md','Defect_Fix_Report.md']:
  target=D/report
  target.write_text(target.read_text()+'\n\n'+archive_note.read_text())
