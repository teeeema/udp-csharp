# Сетевые профили и внешний доступ

## IP-профиль

На VM Docker публикует игровой UDP 7777. На подготовленной машине локальные клиенты подключаются через **F2 → Direct IP Connect** к `192.168.56.101:7777` по Host-Only-сети. NAT остаётся маршрутом Ubuntu в интернет и слушает SSH на Windows loopback.

Публичный доступ подготовлен для бесплатного **UDP**-туннеля Playit.gg. На проверенной странице тарифов тип UDP доступен без Premium, тогда как общий тип TCP+UDP требует Premium. Перед развёртыванием сверяйте доступный вариант в кабинете. [Тарифы Playit.gg](https://playit.gg/pricing)

1. На VM подготовлены проверенные бинарные файлы официального Playit v2.0.2 в `~/pr7-playit`. Запустите daemon и выполните привязку из собственного браузера. Храните claim URL и config агента локально.
2. Создайте туннель типа **UDP**, адрес назначения `127.0.0.1:7777` на VM, Proxy Protocol — None. Запишите выданный публичный host и UDP port для тестировщиков. Игровой клиент принимает их раздельно.
3. Проверьте с двух реальных внешних устройств. На сервере зафиксируйте две строки `PLAYER_JOIN` и две успешные сессии шифрования. На клиентах соберите `JOIN_OK`, общее появление игроков, движение, выстрел и переподключение. [Официальный агент](https://github.com/playit-cloud/playit-agent) · [Руководство Playit по UDP](https://discuss.playit.gg/t/nethernet-support-guide-for-free-users/5866)

В первом терминале Ubuntu запустите daemon:

```sh
cd ~/pr7-playit
umask 077
./playit --secret-path "$PWD/private-agent.toml" --socket-path "$PWD/agent.sock"
```

Во втором терминале `cd ~/pr7-playit` и выполните `./playit-cli --socket-path "$PWD/agent.sock" setup`. Откройте выданную ссылку и выберите бесплатный UDP в кабинете. Привязка личного аккаунта и внешний тест остаются шагами владельца. [Официальные Linux-пакеты Playit](https://packages.playit.gg/)

Альтернатива с публичным IPv4 требует согласованного ingress только на игровой UDP-порт и прохождения всех NAT. При CGNAT используйте туннель. Tailscale подходит для диагностики в частной сети. SSH оставляйте на loopback Windows или ограничивайте административными адресами. Docker обрабатывает публикуемые порты раньше некоторых правил UFW. [Docker packet filtering](https://docs.docker.com/engine/network/packet-filtering-firewalls/)

## Steam-профиль

Steam Internet-list использует query endpoint и сообщает игровой endpoint. Клиенты должны иметь достижимость обоих опубликованных UDP-портов по адресам и портам, видимым backend. Одиночный туннель для IP-профиля эту проверку не покрывает. Steam A2S query и игровой сокет проверяются отдельно. Для типового проброса держите публичные и внутренние порты Steam-профиля одинаковыми. [Steam Matchmaking Servers](https://partner.steamgames.com/doc/api/ISteamMatchmakingServers) · [Steam Networking Sockets](https://partner.steamgames.com/doc/api/ISteamNetworkingSockets)
