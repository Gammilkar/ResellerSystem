# Reseller System — Stage 1 (Foundation)

Локальная клиент-серверная система учёта. Этот README покрывает **Этап 1**:
инфраструктура, множественные базы, версии, health-check, desktop-клиент,
и **готовые Windows/macOS инсталляторы** для конечного пользователя.
Purchase/Item/Sale и т.д. появятся на следующих этапах.

---

## Normal Installation (для конечного пользователя, без разработки)

### На Windows-сервере

1. Скачайте `ResellerSystem-Server-Setup.exe` из `/artifacts`.
2. Запустите файл (потребуется подтверждение UAC — установка регистрирует
   службы Windows).
3. Пройдите мастер: выберите папку установки → **Install**.
4. Дождитесь завершения (инсталлятор сам: разворачивает PostgreSQL,
   создаёт `reseller_system`, применяет миграции, регистрирует и
   запускает Windows-службы, открывает порт в брандмауэре только для
   локальной сети, проверяет `/health`).
5. По завершении откроется **Reseller System Server Manager** — там видно
   IP-адрес сервера (например `http://192.168.1.50:5000`), который нужно
   будет ввести на клиентских компьютерах.

Никаких `dotnet run`, `docker compose`, PowerShell-команд руками, правки
JSON-файлов — всё это инсталлятор делает сам. После перезагрузки Windows
обе службы (`ResellerSystemServer`, `ResellerSystemPostgreSQL`) стартуют
автоматически (`start= delayed-auto`).

### На клиентских компьютерах (Windows)

1. Скачайте `ResellerSystem-Client-Setup.exe`.
2. Запустите, **Install** (админ-права не нужны — установка в профиль пользователя).
3. Приложение откроется само. Введите адрес сервера (см. Server Manager
   на сервере) и нажмите **Connect**.

### На Mac

1. Скачайте `ResellerSystem-macOS.dmg`.
2. Откройте, перетащите **Reseller System.app** в Applications.
3. Первый запуск: правый клик → **Open** (т.к. сборка не нотаризована
   Apple — см. `THIRD-PARTY-NOTICES.md`), далее запускается двойным кликом как обычно.
4. Введите адрес Windows-сервера и подключитесь.

Список используемых бесплатных компонентов и их лицензии — в
[`THIRD-PARTY-NOTICES.md`](./THIRD-PARTY-NOTICES.md).

---

## Development (для разработчиков)

`dotnet run`/`docker compose` нужны **только разработчикам** — конечный
пользователь их никогда не видит (см. раздел выше).

### 0. Platform Refactor — статус (модульная архитектура)

Продукт строится как **Core + Modules** (см. `product-development-plan-v1.md`).
На этом этапе введён сам механизм модульности — бизнес-модулей (Inventory,
Sales, ...) ещё нет, но инфраструктура для них полностью готова:

- **`Server.Modules.Abstractions`** — новый нижний проект, не зависящий ни
  от чего в решении: контракт `IResellerModule` (ModuleKey, Version,
  MinimumCoreVersion, RegisterServices, MapEndpoints, миграции модуля),
  `IServerModuleCatalog`/`StaticServerModuleCatalog`, `SemanticVersion`.
- **`Server.Host/Program.cs`** собирает статический список модулей
  (`List<IResellerModule>`, сейчас пустой) и передаёт его в
  `AddServerApiServices(...)` — это единственное место в решении, которое
  будет знать о конкретных модулях, когда они появятся.
- **Миграции стали per-module.** Было: единая версия схемы на весь tenant.
  Стало: таблица `tenant_module_versions (module_key, script_version)` —
  каждый модуль (включая встроенный псевдо-модуль `"core"`, живущий внутри
  `Server.Data`) версионируется независимо. Master-миграции остались
  плоскими (`schema_migrations`) — master хранит только Core-таблицы,
  включая новую `installed_modules`.
- **`installed_modules`** (master БД) — реестр того, что реально
  установлено на этом сервере (Core + модули), заполняется
  `IModuleRegistry` на каждом старте (`StartupChecks`, шаг 5/5).
- **`Server.Api` стал "тонким"** — контроллеры Health/Version/Databases
  остались (это Core, не модуль), но добавлен цикл монтирования
  `module.MapEndpoints(app)` для любых будущих модулей из каталога —
  без изменений в `Server.Api`, когда появится первый модуль.

Следующие шаги Platform Refactor (пока не сделаны): Update Engine,
Backup/Restore Engine, полноценный Migration Engine поверх этого (авто-
подтягивание отстающих tenant-баз), Security foundation. Бизнес-модули
(Inventory и далее) начинаются только после них — см.
`product-development-plan-v1.md`, Часть 3.

### 1. Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (для локального PostgreSQL и для интеграционных тестов Server.Data.Tests)
- Windows, macOS или Linux — сервер разрабатывается кроссплатформенно на .NET, в production разворачивается на Windows

Проверка:

```bash
dotnet --version   # ожидается 8.0.x
docker --version
```

### 2. Поднять PostgreSQL

```bash
docker compose up -d postgres
```

Это поднимет PostgreSQL 16 на `localhost:5432` с пользователем `postgres` /
паролем `postgres_dev_password` (см. `docker-compose.yml`), с volume
`reseller_system_pgdata`, переживающим перезапуски контейнера.

Проверить, что база готова:

```bash
docker compose ps
docker exec reseller-system-postgres pg_isready -U postgres
```

### 3. Настроить конфигурацию сервера

Пароль для разработки уже прописан в `src/Server.Host/appsettings.Development.json`
и совпадает с паролем из `docker-compose.yml` — ничего менять не нужно для
локального запуска.

Для production скопируйте `src/Server.Host/appsettings.Production.example.json`
в `appsettings.Production.json` (файл в `.gitignore`, в git не попадёт) и
укажите реальный пароль, либо передайте его переменной окружения:

```bash
# Windows PowerShell
$env:Postgres__AdminPassword = "реальный-пароль"

# bash
export Postgres__AdminPassword="реальный-пароль"
```

Пароли никогда не хранятся в git — см. `.gitignore`.

### 4. Master-миграции применяются автоматически

Отдельной команды не требуется: при старте `Server.Host` сам создаёт базу
`reseller_system` (если её ещё нет) и применяет все master-миграции из
`src/Server.Data/Migrations/Scripts/Master/*.sql` (см. `StartupChecks`).
Tenant-миграции применяются точно так же — автоматически при создании
каждой новой базы через `DatabaseProvisioningService`.

### 5. Запустить сервер

```bash
cd src/Server.Host
dotnet run
```

При успешном старте в консоли будет последовательность:

```
Startup check 1/4: validating configuration...
Startup check 2/4: checking PostgreSQL connectivity...
Startup check 3/4: ensuring master database and migrations...
Startup check 4/4: verifying file storage...
All startup checks passed.
ResellerSystem.Server.Host starting on http://localhost:5000
```

Проверка вручную:

```bash
curl http://localhost:5000/health
curl http://localhost:5000/api/v1/version
```

В Development-режиме доступен Swagger UI: `http://localhost:5000/swagger`.

> Регистрация как Windows Service, PostgreSQL, брандмауэр — теперь делает
> `ResellerSystem-Server-Setup.exe` (см. "Normal Installation" выше и
> `installer/scripts/Install-ServerService.ps1`). Ручной `sc.exe create`
> в production больше не нужен.

### 6. Запустить Desktop-клиент

```bash
cd src/Desktop.App
dotnet run
```

Откроется окно **Server Connection**. Введите адрес сервера (например
`http://localhost:5000` при локальном запуске, или `http://192.168.1.100:5000`
для сервера в локальной сети) и нажмите **Connect**.

### 8. Создать первую базу (dev-режим)

После подключения откроется список баз (изначально пустой):

1. Нажмите **Create**.
2. Введите `Name` (например `Main Business`), проверьте `Time Zone`
   (определяется автоматически, можно изменить) и `Currency` (по умолчанию `USD`).
3. Нажмите **Save**.

Сервер сгенерирует внутреннее immutable-имя вида `reseller_db_000001`,
создаст физическую PostgreSQL-базу, применит tenant-миграции и пометит базу
статусом `Ready`. Клиент никогда не видит физическое имя — только `Name`.

### 9. Сборка релиза (installer-ы)

```powershell
# 1. Один раз: скачать PostgreSQL Windows x64 "binaries" zip и положить в redist/
#    (см. redist/README.md), скачать и установить Inno Setup 6 (бесплатно):
#    https://jrsoftware.org/isdl.php

# 2. Собрать всё одной командой:
.\build\build-release.ps1
```

Что делает скрипт (`build/build-release.ps1`):

1. Чистит `dist/` и `artifacts/`.
2. `dotnet publish` Server.Host → self-contained single-file win-x64 (пользователю не нужен .NET Runtime).
3. `dotnet publish` Desktop.ServerManager → self-contained win-x64.
4. `dotnet publish` Desktop.App (клиент) → self-contained win-x64.
5. Распаковывает PostgreSQL portable-бинарники из `redist/` в `dist/postgresql`.
6. Компилирует `installer/inno/ServerSetup.iss` и `ClientSetup.iss` через `ISCC.exe`.
7. Прогоняет unit-тесты (Domain/Application/Api — без Docker).
8. Кладёт готовые `.exe` в `/artifacts`.

**macOS собирается отдельным скриптом на Mac** (Apple-тулчейн — `codesign`,
`hdiutil` — недоступен на Windows):

```bash
chmod +x build/build-macos.sh
./build/build-macos.sh 0.1.0
```

Результат — `artifacts/ResellerSystem-macOS.dmg`.

### 10. Логи

```
src/Server.Host/logs/application/application-YYYYMMDD.log
src/Server.Host/logs/error/error-YYYYMMDD.log
src/Server.Host/logs/database/database-YYYYMMDD.log
src/Server.Host/logs/update/update-YYYYMMDD.log
```

Ротация — по дню и по 50 МБ, application-логи хранятся 30 дней, error-логи — 90 дней.

### 11. Файловое хранилище

По умолчанию (см. `appsettings.json`):

```
src/Server.Host/data/storage/   — StorageRoot (документы, пока не используется)
src/Server.Host/data/backups/   — BackupRoot
src/Server.Host/data/updates/   — UpdateRoot
src/Server.Host/data/temp/      — TempRoot
```

`/health` сообщает `availableDiskSpaceBytes`; предупреждение о низком месте
на диске (`Storage:LowDiskSpaceWarningBytes`, по умолчанию 20 ГБ) будет
использовано в UI на следующих этапах.

### 12. Как остановить систему

```bash
# Сервер: Ctrl+C в терминале с `dotnet run`, либо для Windows Service:
sc.exe stop ResellerSystemServer

# PostgreSQL:
docker compose down          # останавливает контейнер, данные сохраняются в volume
docker compose down -v       # ⚠ также удаляет volume со всеми базами
```

### 13. Тесты

```bash
# Модульные тесты (Domain, Application, Api) — не требуют Docker/Postgres:
dotnet test tests/Server.Domain.Tests
dotnet test tests/Server.Application.Tests
dotnet test tests/Server.Api.Tests

# Интеграционные тесты (Server.Data.Tests) — требуют запущенный Docker daemon,
# сами поднимают одноразовый PostgreSQL-контейнер через Testcontainers:
dotnet test tests/Server.Data.Tests

# Или всё сразу из корня:
dotnet test
```

### Список тестов Этапа 1

| Проект | Что проверяется |
|---|---|
| `Server.Domain.Tests` | `DatabaseProfile`: создание со статусом `Creating`, rename не трогает physical name, `MarkReady`/`MarkMigrationFailed`/`Disable` |
| `Server.Application.Tests` | `DatabaseProvisioningService`: генерация physical name из sequence (не из display name), регистрация до провижининга, `Ready` при успехе, `MigrationFailed` при ошибке (не `Ready`), провал валидации; `CreateDatabaseRequestValidator`/`UpdateDatabaseRequestValidator`: пустое имя, длина имени, невалидный timezone, невалидная валюта; `DatabaseContextResolver`: 404 для неизвестного id, отказ для не-`Ready`/неактивной базы |
| `Server.Api.Tests` | `/health` (healthy/unhealthy), `/api/v1/version`, `DatabasesController` (list/get/create/update, 404 для неизвестного id), `ExceptionHandlingMiddleware` (коды ошибок, скрытие деталей в Production) |
| `Server.Data.Tests` (integration, требует Docker) | Применение master/tenant миграций к реальному PostgreSQL, идемпотентность повторного прогона, создание физической базы, защита от небезопасных имён БД, **per-module tenant-миграции** (`tenant_module_versions`), **`ModuleRegistry`** (`installed_modules` upsert/roundtrip), `SemanticVersion` парсинг/сравнение |

## Известные ограничения Этапа 1

- Purchase/Item/Listing/Sale/Return/Expense/Documents — не реализованы (Этап 2+).
- Excel/CSV/PDF импорт, отчёты, 1099-K, Washington report — не реализованы.
- eBay/Etsy/bank/AI-интеграции — не реализованы; в коде заложены только
  архитектурные интерфейсы `IMarketplaceIntegration`, `IImportProvider`,
  `IExternalDataProvider` (Server.Domain/Abstractions) без публичных
  API-эндпоинтов — по явному указанию в задании эндпоинты `/integrations/*`
  сейчас не создаются.
- Server/Client updater (скачивание, установка, rollback) — не реализован;
  версии уже централизованы (`IVersionProvider`, `/api/v1/version`,
  `MinimumDesktopClientVersion`/`MinimumAndroidClientVersion`), это
  фундамент для будущего апдейтера.
- Backup/Restore — не реализован; `StorageOptions.BackupRoot` уже существует
  и проверяется на старте, но сам backup/restore workflow — Этап 2+.
- Android-клиент — не создавался (только Windows/macOS через Avalonia).
- Аутентификация/роли — не реализованы; `ICurrentUserContext` уже
  используется по всему API-коду вместо хардкода "один пользователь",
  так что роли добавляются без переписывания контроллеров/сервисов.
- Удаление баз ("специальная безопасная процедура") — сознательно не
  реализовано в этом API (нет `DELETE /api/v1/databases/{id}`); только
  `PATCH` для rename/deactivate.
- ~~Тесты `Server.Data.Tests` не запускались...~~ Больше не актуально —
  `dotnet build` и `dotnet test` (все 6 проектов, 75 тестов, включая
  `Server.Data.Tests` через Testcontainers) прогнаны и проходят на macOS с
  .NET 8 SDK и Docker; подробности и найденные/исправленные баги — см.
  `KNOWN_LIMITATIONS.md`, раздел "Сборка и тесты — теперь проверено".
- **Packaging/installer-код (этот раздел) написан, но НЕ собирался и НЕ
  тестировался end-to-end** — в текущей среде разработки нет ни .NET SDK,
  ни Inno Setup, ни Windows/macOS. Реально проверить `ResellerSystem-Server-Setup.exe`
  можно только на настоящей Windows-машине с установленным .NET 8 SDK и
  Inno Setup 6. См. раздел "Что реально собрано и проверено" ниже.
- Инсталлятор клиента (`ResellerSystem-Client-Setup.exe`) не проверяет,
  что порт сервера уже открыт/доступен — просто сохраняет введённый адрес;
  диагностика недоступности сервера — на едином экране входа (уже
  реализовано в `SignInViewModel`).
- macOS-сборка ad-hoc подписана, не нотаризована Apple (это платный
  необязательный шаг, $99/год) — при первом запуске на чужом Mac нужен
  правый клик → Open.
- Auto-detect сервера в локальной сети (mDNS/broadcast) на экране "Find
  Server" клиента — не реализован в Этапе 1; есть только ручной ввод
  адреса (сервер IP уже показывается в Server Manager).
- `build-release.ps1` требует вручную положить PostgreSQL zip в `redist/`
  один раз (осознанное решение — не тянуть бинарники по сети без ведома
  разработчика, см. комментарии в скрипте); сам процесс установки для
  конечного пользователя от этого не зависит — PostgreSQL уже упакован
  внутрь `ResellerSystem-Server-Setup.exe`.

## Структура `/artifacts` после сборки

```
artifacts/
  ResellerSystem-Server-Setup.exe   # Windows: сервер + PostgreSQL + Server Manager
  ResellerSystem-Client-Setup.exe   # Windows: desktop-клиент
  ResellerSystem-macOS.dmg          # macOS: desktop-клиент (собирается отдельно, build-macos.sh)
```

Промежуточные (не для распространения, только вход для Inno Setup):

```
dist/
  server/            # self-contained win-x64 publish Server.Host
  servermanager/      # self-contained win-x64 publish Desktop.ServerManager
  client-win/          # self-contained win-x64 publish Desktop.App
  client-macos/         # (только на Mac) .app + publish
  postgresql/            # распакованные portable-бинарники PostgreSQL
```

## Что реально собрано и проверено

Из-за отсутствия .NET SDK/Docker/Windows/Inno Setup в среде, где писался
этот код, **ничего из перечисленного ниже не запускалось фактически** —
весь packaging-код (installer/`*.iss`, `*.ps1`, `Desktop.ServerManager`,
`build-release.ps1`, `build-macos.sh`) написан и вычитан вручную, но
требует первого реального прогона на Windows-машине с .NET 8 SDK и Inno
Setup 6 перед тем, как считать Этап 1 полностью принятым. Известные места,
требующие проверки в первую очередь:

1. `dotnet publish ... -r win-x64 --self-contained true -p:PublishSingleFile=true`
   для `Server.Host` — совместимость `UseWindowsService()` с single-file
   publish обычно работает, но стоит проверить явно.
2. `Install-PostgreSql.ps1` / `Install-ServerService.ps1` — синтаксис
   PowerShell вычитан вручную, не выполнялся.
3. `.iss`-скрипты — не компилировались через `ISCC.exe`.
4. `Desktop.ServerManager` (WinForms, `ApplicationConfiguration.Initialize()`)
   — не собирался.

## Что готово для Этапа 2

- Стабильный API-контракт `/api/v1/databases`, `/health`, `/api/v1/version`
  для наращивания (`/api/v1/purchases`, `/api/v1/items`, ...).
- `IDatabaseContextResolver` готов к тому, чтобы новые tenant-scoped
  контроллеры резолвили `X-Database-Id` безопасно, без слепого доверия.
- `TenantDbContext` существует и готов принимать `DbSet<Purchase>`,
  `DbSet<Item>` и т.д. по мере появления сущностей — по той же схеме
  "SQL-скрипт задаёт схему, EF Core её мапит", что и `MasterDbContext`.
  Следующий tenant-миграционный скрипт — `Migrations/Scripts/Tenant/0002_*.sql`.
- `AuditableEntity` (CreatedAt/UpdatedAt/CreatedBy/UpdatedBy) — базовый класс,
  готовый для всех будущих финансовых сущностей.
- `Money` (Domain.Shared) — value object на `decimal`, готов к использованию
  в Purchase/Item/Sale.
- `IImportProvider`/`IMarketplaceIntegration`/`IExternalDataProvider` —
  архитектурные точки расширения зарезервированы, конкретные реализации и
  публичные эндпоинты добавляются только вместе с соответствующим модулем.
- Avalonia-клиент уже структурирован как MVVM с DI и навигацией
  (`INavigationService`) — новые экраны (Dashboard, Items, Purchases, ...)
  добавляются как новые ViewModel + View без изменения `App.axaml.cs`.
