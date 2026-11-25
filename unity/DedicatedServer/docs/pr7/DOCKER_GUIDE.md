# Контейнер DUSTLINE

Соберите Linux Dedicated Server на Windows. Перед копированием проверьте SSH на VM и добавьте её проверенный ключ хоста в known_hosts. Затем из корня проекта:

```powershell
pwsh -File scripts/deploy-server.ps1 -IdentityFile <путь-к-ключу-VM> -Sudo
```

Если Docker уже доступен пользователю без sudo, параметр `-Sudo` можно опустить. Для ручного запуска поместите готовую сборку в `Builds/PR7/LinuxServer`, а конфигурацию из `deploy/.env.example` при необходимости скопируйте в `deploy/.env`. Выполните на VM:

```sh
sudo sh scripts/server-start.sh ip
sudo sh scripts/server-status.sh ip
sudo sh scripts/test-container.sh ip
sudo sh scripts/server-stop.sh ip
```

Образ использует Ubuntu 24.04, закреплённый digest, и пользователя UID 10001. Docker поднимает контейнер с `unless-stopped`, init-процессом и лимитом ротации логов 3 × 10 MB. Когда Unity завершает инициализацию, приложение создаёт маркер готовности. При SIGTERM entrypoint создаёт маркер остановки, Unity вызывает NGO Shutdown и очищает Steam-регистрацию при наличии Steam-профиля. Если процесс аварийно завершился, entrypoint возвращает код дочернего процесса.

T14 отправляет SIGKILL непосредственно Unity-процессу внутри лабораторного контейнера, ждёт увеличения RestartCount и проверяет состояние healthy. Штатный сценарий записывает код 0 и `SERVER_STOPPED`. Скрипт оставляет лабораторный контейнер запущенным для игровых проверок. Сетевые права и порты см. в [NETWORK_SETUP.md](NETWORK_SETUP.md).
