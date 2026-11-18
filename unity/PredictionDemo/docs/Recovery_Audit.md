# Аудит восстановления — 06.11.2026

Найден полноценный исходный проект `../prac_1/unity/PredictionDemo`, а не только описание. Он содержит сцену PredictionDemo, prefab игрока, исходники CSP/reconciliation, TankWeapon, EditMode tests и кэш пакетов. Скопированы Assets, Packages, ProjectSettings в отдельный каталог Practice5Replication. Старые незакоммиченные изменения сохранены; старый проект не изменялся.

Исходная версия Unity 6000.3.8f1; установлен и используется 6000.6.7f1. NGO 2.13.3, Transport автоматически обновлён редактором с исторической 2.6.0 до 6.6.0 (подтверждено package.json и packages-lock.json). URP автоматически обновлён редактором до 17.6.0. Pipeline 0.9.0-exp.1 встроен из сохранившегося локального кэша, поскольку реестр отказал в скачивании (authentication credentials).

Локальный Git: ветка codex/restore-original-dustline-20261023, HEAD 3e4258d. Найдены исходники protocol/telemetry/reliability и четыре проекта тестов .NET. Remote `https://github.com/teeeema/prac-1-udp-csharp.git` доступен для чтения; `ls-remote` подтвердил main, prac_4, feature/reliable-delivery, feature/udp-echo. Fetch, push, коммиты и изменения веток не выполнялись.

История Codex проверена через list_threads(history, codex): прежних чатов этой разработки в доступном списке нет. Локальные отчёты сохранены в prac_1/docs; их старые результаты не считаются новым тестированием. Новая реализация ПР5 находится в Assets/Scripts/Prediction/Replication.

Методички №1–5 прочитаны. №1–3 описывают собственный UDP, №4–5 — Unreal Engine. Unity-реализация сохраняет функциональные требования; API Unreal не объявляются реализованными буквально. Восстанавливать UDP-протокол в NGO не требуется: старый консольный протокол сохранён отдельно.
