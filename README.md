# Identity

Сервис хранит пользователей и роли ASP.NET Core Identity в PostgreSQL. gRPC-контракт находится в `identity/identity.proto`; модель данных и EF Core — в `identity/Infrastructure/Persistence/`.

## Создание миграции

Выполняйте команды из корня репозитория. Сначала установите EF CLI (один раз) и настройте строку подключения в User Secrets — EF запускает приложение для получения конфигурации:

```powershell
dotnet tool install --global dotnet-ef --version 10.0.14
dotnet user-secrets set "ConnectionStrings:IdentityDb" "Host=localhost;Port=5432;Database=identity;Username=identity;Password=<локальный-пароль>" --project identity/identity.csproj
```

После изменения модели создайте миграцию, указав короткое имя в PascalCase:

```powershell
dotnet ef migrations add AddPhoneNumber --project identity/identity.csproj --startup-project identity/identity.csproj --output-dir Infrastructure/Persistence/Migrations
```

Проверьте сгенерированные `Up` и `Down`, затем примените миграции к локальной базе:

```powershell
dotnet ef database update --project identity/identity.csproj --startup-project identity/identity.csproj
```

Для проверочного или production-развёртывания можно сформировать идемпотентный SQL-скрипт:

```powershell
dotnet ef migrations script --idempotent --project identity/identity.csproj --startup-project identity/identity.csproj --output identity-migrations.sql
```

Добавляйте в репозиторий исходники миграции и snapshot, но не строку подключения или созданный SQL-файл с секретами.

## Локальный запуск

Запустите PostgreSQL, задайте строку подключения в User Secrets командами выше и примените миграции:

```powershell
docker run --name identity-postgres --publish 5432:5432 --env POSTGRES_DB=identity --env POSTGRES_USER=identity --env POSTGRES_PASSWORD=<локальный-пароль> --detach postgres:17
dotnet ef database update --project identity/identity.csproj --startup-project identity/identity.csproj
dotnet run --project identity/identity.csproj
```

## Вход и устройства

Запрос `POST /connect/token` с `grant_type=password` дополнительно требует OAuth-параметр `device_id` — GUID, который native-клиент генерирует один раз и сохраняет для последующих входов. При первом входе Identity регистрирует устройство за пользователем. При следующих входах устройство должно принадлежать этому же пользователю и не быть отозванным. Каждый успешный вход создает новую сессию на 10 минут; access token содержит `sid` и `device_id`. Сессия связана с пользователем через устройство (`Session.DeviceId -> Device.Id -> Device.UserId`). Сейчас ограничитель входа разрешает до пяти попыток в минуту для HMAC-раздела и работает в памяти одного процесса; при нескольких репликах нужен общий rate-limit store или ограничение на Gateway.

### Настройка production

**Ключ подписи JWT**

Identity подписывает им access-токены. Для подписи нужен PFX-файл (`.pfx`, формат PKCS#12), в котором есть сертификат и соответствующий ему закрытый ключ. Это не сертификат для HTTPS: его задача — доказать Gateway, что токен выпущен Identity.

Файл должен быть постоянным: после перезапуска Identity должен использовать тот же ключ, иначе Gateway не сможет проверить ранее выданные токены. Если Identity запущен в нескольких репликах, каждой нужен один и тот же ключ. Закрытый ключ и пароль от PFX нельзя добавлять в Git или образ Docker.

Compose читает PFX и файл с его паролем как secrets. Скопируйте пример настроек и создайте каталог для локальных secret-файлов:

```bash
cp infra/.env.example infra/.env
mkdir -p infra/secrets
```

Затем положите в `infra/secrets/` эти два файла, не добавляя их в Git:

```text
infra/secrets/identity-signing.pfx
infra/secrets/identity-signing-password.txt
```

В `.env` укажите пути к этим файлам в `IDENTITY_SIGNING_CERTIFICATE_FILE` и `IDENTITY_SIGNING_CERTIFICATE_PASSWORD_FILE`. Там должны быть только пути, не сам пароль. Compose подключит файлы контейнеру только для чтения: PFX будет доступен как `/run/secrets/identity-signing.pfx`, пароль — как `/run/secrets/identity-signing-password`. При старте Identity прочитает пароль из файла и загрузит PFX. Если файлы отсутствуют или PFX не открывается, Identity не запустится.

Чтобы создать файлы самостоятельно, из корня репозитория выполни команды в Linux/WSL с установленным OpenSSL:

```bash
openssl rand -base64 32 > infra/secrets/identity-signing-password.txt
openssl req -x509 -newkey rsa:3072 -sha256 -days 3650 \
	-keyout infra/secrets/identity-signing.key \
	-out infra/secrets/identity-signing.crt.pem \
	-subj "/CN=VA Identity JWT signing"
openssl pkcs12 -export \
	-out infra/secrets/identity-signing.pfx \
	-inkey infra/secrets/identity-signing.key \
	-in infra/secrets/identity-signing.crt.pem \
	-name "VA Identity JWT signing" \
	-passout file:infra/secrets/identity-signing-password.txt
openssl pkcs12 -info -in infra/secrets/identity-signing.pfx -noout \
	-passin file:infra/secrets/identity-signing-password.txt
```

OpenSSL попросит придумать пароль для временного файла `identity-signing.key`, а затем ввести его при упаковке в PFX. PFX-пароль генерируется отдельно и сохраняется в `identity-signing-password.txt`. Эти локальные файлы исключены из Git; не отправляй их в репозиторий. Сертификат внутри PFX может быть самоподписанным: он служит для подписи JWT, а не для HTTPS. После создания PFX сохрани его и файл пароля для последующих запусков Identity.

В Development используется временный development-сертификат; для production он не подходит.

Gateway получает открытый ключ через OpenID Connect discovery/JWKS. Передавать ему PFX или закрытый ключ нельзя.

**HTTPS и сеть**

Внешний клиент должен отправлять email и пароль в Gateway только по HTTPS. TLS-сертификат Gateway предназначен для HTTPS и отличается от PFX, которым Identity подписывает JWT. В текущей конфигурации Nginx настроен только на HTTP-порт 80, поэтому HTTPS для production пока не готов. Identity должен оставаться доступен только Gateway во внутренней сети.

Внутренний issuer по умолчанию — `http://identity/`. Он используется Gateway для discovery/JWKS внутри доверенной сети; это не публичный адрес Identity.

**База и развертывание**

Передавайте `ConnectionStrings:IdentityDb` через secret store платформы (`ConnectionStrings__IdentityDb`). Миграции запускайте отдельным deployment-шагом с правом изменения схемы; приложению выдавайте минимальные права. Предусмотрите резервное копирование и совместимость схемы при rolling deployment.

Роли `user` и `admin` добавляются миграцией. Создание роли само по себе не назначает её пользователю; назначение `admin` должно оставаться контролируемой операцией.
