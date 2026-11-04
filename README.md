# Identity

Сервис хранит пользователей и роли ASP.NET Core Identity в PostgreSQL. gRPC-контракт находится в `identity/identity.proto`; модель данных и EF Core — в `identity/Infrastructure/Persistence/`.

## Создание миграции

Выполняйте команды из корня репозитория. Сначала установите EF CLI (один раз) и настройте строку подключения в User Secrets — EF запускает приложение для получения конфигурации:

```powershell
dotnet tool install --global dotnet-ef --version 10.0.14
dotnet user-secrets set "ConnectionStrings:Identity" "Host=localhost;Port=5432;Database=identity;Username=identity;Password=<локальный-пароль>" --project identity/identity.csproj
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

В production передавайте `ConnectionStrings:Identity` через secret store платформы (`ConnectionStrings__Identity`). Миграции запускайте отдельным deployment-шагом с правом изменения схемы; приложению выдавайте минимальные права. Используйте TLS с проверкой сертификата и предусмотрите резервное копирование и совместимость схемы при rolling deployment.

Роли `user` и `admin` добавляются миграцией. Создание роли само по себе не назначает её пользователю; назначение `admin` должно оставаться контролируемой операцией.
