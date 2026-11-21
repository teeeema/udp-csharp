# ПР №6 — проверка Wireshark 21.11.2026

Хост и клиент DUSTLINE запускались на macOS ARM64. Клиент подключался к `127.0.0.1:7777`, Docker передавал трафик на UDP 7777 контейнера, а реле пересылало его к хосту игры на `host.docker.internal:7778`. В обоих прогонах сервер принимал выстрелы клиента. В защищённом прогоне обе стороны зарегистрировали `HANDSHAKE_OK`.

## Захваты и снимки

| Режим | Файлы | Что видно в Wireshark |
| --- | --- | --- |
| ПР5, до шифрования | [PCAP](before.pcap), [снимок](before-wireshark.png) | В кадре 88 UDP-полезная нагрузка — 70 байт бинарного UTP/NGO. Внешнего заголовка `DSL6` нет. |
| ПР6, после шифрования | [PCAP](after.pcap), [снимок](after-wireshark.png) | В кадре 46 UDP-полезная нагрузка — 75 байт. Видны 20 байт заголовка `DSL6` (AAD), 12 байт nonce, 27 байт шифртекста и 16 байт тега GCM. |

В каждом захвате по 450 Ethernet-кадров с IPv4 и UDP. В захвате ПР6 два открытых пакета обмена ключами (`KEY_EXCHANGE`), два зашифрованных подтверждения ключей и 446 зашифрованных пакетов игрового транспорта (`ENCRYPTED`, `kind == 0`). В захвате ПР5 ни один пакет не содержит оболочку `DSL6`. Накладные расходы ПР6 составляют 48 байт на UTP-датаграмму. Для выбранного пакета ПР6 это 48 байт поверх 27 байт исходной нагрузки.

Исходные PCAP записаны `tcpdump` с `eth0` тестового контейнера. Снимки сняты из Wireshark 4.0.17, открывшего эти файлы, в изолированном Linux-сеансе. Адреса на снимках принадлежат локальной Docker-сети. [Журнал захвата ПР5](before-capture.txt), [журнал захвата ПР6](after-capture.txt)

## Как повторить просмотр

Выполните из каталога `unity/PredictionDemo`.

```sh
wireshark -X lua_script:Tools/pr6_dissector.lua -r docs/pr6-wireshark-2026-11-21/before.pcap
wireshark -X lua_script:Tools/pr6_dissector.lua -r docs/pr6-wireshark-2026-11-21/after.pcap
```

В обоих захватах работает фильтр `udp.port == 7777`. В защищённом захвате `dustline6.type == 10` выделяет обмен ключами, а `dustline6.type == 11 && dustline6.kind == 0` — пакеты игрового транспорта. Для нового захвата на интерфейсе реле использовалась команда `tcpdump -n -p -U -s 0 -c 450 -i eth0 -w trace.pcap 'udp port 7777'`.

Оба прогона использовали один хост, одного клиента, оптимизированную репликацию и автострельбу. SHA-256 `PredictionDemo.Runtime.dll` у архивной сборки ПР5, использованной для первого захвата — `26d6886d18fdd9d0c5a1a6af337d06b805912d151896cfde10ba1d8da657d59f`. У macOS-сборки DUSTLINE Copper Basin, использованной для второго захвата — `a6a127c16a13f0b236c17e668f762b65f60bc0dbcf23aa9648a6f1d9e3827f52`.

## Тесты Unity

Unity 6000.6.7f1 на macOS, исходники с доработанным `AUTH_FAIL`. В NUnit XML локальный путь к проекту заменён на `[Unity project]`.

- [EditMode XML](editmode.xml) — 99/99, включая проверку адреса отправителя и ограничения частоты `AUTH_FAIL`.
- [PlayMode XML](playmode.xml) — 2/2.

## Проверка собранной игры с двумя клиентами

После изменения `AUTH_FAIL` собрана macOS-версия из этой ветки. При локальном запуске хоста и двух клиентов пройдено 20/20 проверок, включая движение, стрельбу и reconciliation. Реле внесло 92 потери, 106 дублей и 55 повреждений тегов. Сервер зафиксировал `AUTH_FAIL remote=127.0.0.1:<порт>` и `REPLAY_DROP`. В интервале измерения 1 648 шифрованных датаграмм добавили 79 104 байта к 146 696 байтам UTP, то есть 53,92%. [Результат прогона](native-result.json), [события хоста](native-host-security-events.txt)

Выполните команды из каталога `unity/PredictionDemo` с установленным Unity CLI.

```sh
unity run . -- -executeMethod PredictionDemo.EditorTools.SecurityBuilder.Build
python3 Tools/run_pr6_game.py --app Builds/PR6/ReplicationLab.app --output /tmp/dustline-pr6-repeat --secure --optimized --clients 2 --duration 24 --impair --headless
```

Используйте новый каталог `--output` при каждом запуске. SHA-256 `PredictionDemo.Runtime.dll` сборки этого прогона — `fa580cb0f25622b4598c7bff40f05f44c356a47ea22681eebe93314674f9aae2`.

[Статус ПР №6 по методичке](../PR6_Methodichka_Audit.md) · [Правила Spectrum и приёмка](../Cloudflare_Spectrum_Setup.md)
