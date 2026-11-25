# UDP Game Protocol / DUSTLINE

Учебный репозиторий по сетевой разработке. ПР1–ПР3 последовательно расширяют консольный UDP-протокол на C#/.NET. ПР4–ПР7 развивают многопользовательскую игру DUSTLINE на Unity.

## Практические работы

| № | Тема | Состояние |
| --- | --- | --- |
| 1 | [Бинарный UDP-протокол и авторитетный сервер](practices/01-udp-protocol/) | Код и демонстрация |
| 2 | [Задержка, джиттер и потери](practices/02-network-metrics/) | Код, замеры и графики |
| 3 | [Надёжная отправка команд поверх UDP](practices/03-reliable-delivery/) | Код и экспериментальные результаты |
| 4 | [Клиентское предсказание и коррекция на Unity](practices/04-client-prediction/) | Код и демонстрация |
| 5 | [Оптимизация репликации и релевантность](practices/05-replication/) | Отчёт и измерения |
| 6 | [Шифрование игрового UDP](practices/06-encrypted-transport/) | Код, отчёт и захваты Wireshark |
| 7 | [Linux Dedicated Server, Docker и Steam](practices/07-dedicated-server-steam/) | 19 PASS, 9 BLOCKED |

## Структура репозитория

- [practices/](practices/) — страницы каждой работы со ссылками на код, запуск и результаты.
- [src/](src/), [tests/](tests/) и [UdpGame.sln](UdpGame.sln) — общее решение .NET для ПР1–ПР3.
- [docs/](docs/) — материалы консольного проекта, ПР4 и измерения ПР5.
- [unity/PredictionDemo/](unity/PredictionDemo/) — опубликованная версия ПР6 с ареной Copper Basin. Исходная версия ПР4 закреплена отдельной ссылкой в её разделе.
- [unity/DedicatedServer/](unity/DedicatedServer/) — отдельная версия Unity, на которой проверяли ПР7. Здесь находятся сборочные скрипты, Docker-конфигурация и доказательства прогонов.

## Быстрый запуск

Для консольного проекта установите .NET SDK 10.0. Из корня репозитория:

```sh
dotnet build UdpGame.sln
dotnet test UdpGame.sln --no-build
```

Запустите сервер в первом терминале.

```sh
dotnet run --project src/UdpGame.Server -- --port 27015 --log server.log
```

Во втором терминале выберите один режим клиента. Для повторных экспериментов записывайте CSV под новым именем.

```sh
# ПР1 · обычный обмен
dotnet run --project src/UdpGame.Client -- --host 127.0.0.1 --port 27015 --count 6
# ПР2 · измерение задержки и потерь
dotnet run --project src/UdpGame.Client -- --host 127.0.0.1 --port 27015 --experiment --csv docs/latency_samples_new.csv
# ПР3 · проверка надёжной отправки
dotnet run --project src/UdpGame.Client -- --host 127.0.0.1 --port 27015 --reliability-experiment --csv docs/reliability_samples_new.csv
```

Для Unity откройте через Unity Hub папку нужной версии, указанную в её разделе, и сцену `Assets/Scenes/PredictionDemo.unity`. Для ПР7 используйте [руководство по сборке Linux-сервера и Windows-клиента](unity/DedicatedServer/docs/pr7/BUILD_GUIDE.md).
