# Изменённые исходники

Все прежние Assets/Packages/ProjectSettings перенесены копированием.

| Файл | Действие |
|---|---|
| Assets/Editor/ProjectBootstrap/ReplicationBuilder.cs | создан |
| Assets/Scripts/Prediction/TankWeapon.cs | изменён в копии |
| Assets/Scripts/Prediction/NetworkBootstrap.cs | изменён в копии |
| Assets/Scripts/Prediction/Replication/ReplicationVerification.cs | создан |
| Assets/Scripts/Prediction/Replication/ReplicatedProp.cs | создан |
| Assets/Scripts/Prediction/Replication/ReplicationPolicy.cs | создан |
| Assets/Scripts/Prediction/Replication/MeteredTransport.cs | создан |
| Assets/Scripts/Prediction/Replication/ReplicationLab.cs | создан |
| Assets/Tests/PlayMode/ReplicationSmokeTests.cs | создан |
| Assets/Tests/EditMode/ReplicationPolicyTests.cs | создан |

Дополнительно созданы Resources/ReplicationProp.prefab, PlayMode asmdef, Tools/*.py, README.md, .gitignore, docs/* и копия LegacyUdp. Pipeline встроен из старого PackageCache в Packages/com.unity.pipeline. ProjectVersion и URP обновлены Unity при открытии. Полный SHA256-манифест сдаваемых файлов находится в submission-manifest.json. Исходный репозиторий не изменён, кроме штатных bin/obj при запуске dotnet test.
