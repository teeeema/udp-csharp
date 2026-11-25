# ПР6. Шифрование игрового UDP

Unity-версия DUSTLINE с ареной Copper Basin шифрует датаграммы с помощью AES-GCM. Ключи выводятся после обмена X25519. Сервер и клиент проверяют целостность и защищают сеанс от повторной доставки ранее принятых датаграмм.

- [Unity-проект, запуск и управление](../../unity/PredictionDemo/)
- [Полный отчёт](../../unity/PredictionDemo/docs/PR6_Report.md) · [Устройство транспорта и модель угроз](../../unity/PredictionDemo/docs/Security.md)
- [Код транспорта](../../unity/PredictionDemo/Assets/Scripts/Prediction/Security/)
- [Захваты и результаты Wireshark](../../unity/PredictionDemo/docs/pr6-wireshark-2026-11-21/)
- [Сверка с методичкой](../../unity/PredictionDemo/docs/PR6_Methodichka_Audit.md)
