# Known Limitations

Честный, актуальный список того, что не работает, не проверено, или
работает не полностью — обновляется по мере разработки. Ссылки на этот
файл встречаются прямо в коде (`Server.Updater`, `UpdateService`).

## Сборка и тесты — теперь проверено (2026-08-23)

Ранее весь код был написан и вычитан вручную, без единого запуска
компилятора (в среде разработки не было .NET SDK/Docker/Windows/macOS).
На macOS с .NET 8 SDK и Docker это исправлено: `dotnet build` на всём
решении и `dotnet test` на всех 6 тестовых проектах (75 тестов) проходят
чисто. Также вручную прогнан живой `Server.Host` против реального
PostgreSQL 16 в Docker (не через Testcontainers) с полным сценарием:
master-миграции → регистрация модулей → создание initial admin →
`/health`/`/api/v1/version` → создание tenant-базы (все per-module
tenant-миграции: core/inventory/sales/expenses/documents/import) →
Purchase→Item (Inventory) → Listing→Sale→Fee→Financials (Sales, включая
кросс-модульное чтение cost basis) → все 5 отчётов `ReportsService`
(marketplace/category profitability, inventory aging, federal tax
summary, 1099-K — SQL, который ни разу не выполнялся, оказался
синтаксически верным) → Documents (upload/link/download,
content-addressable storage) → Import CSV (upload → staging → confirm →
Purchase создан) → Returns. Всё сработало end-to-end без ошибок, кроме
одного найденного и исправленного бага (см. ниже).

При первой попытке сборки обнаружены и исправлены:
- `ResellerSystem.sln` не содержал `GlobalSection(ProjectConfigurationPlatforms)`
  — без него `dotnet restore`/`build` не находили ни одного проекта для
  сборки ("Unable to find a project to restore"). Секция сгенерирована и
  добавлена для всех 26 проектов.
- `Desktop.ServerManager.csproj` (WinForms, `net8.0-windows`) не собирался
  на не-Windows без `<EnableWindowsTargeting>true</EnableWindowsTargeting>`
  (это только резолвинг reference assemblies для сборки — сам WinForms UI
  всё ещё имеет смысл только на Windows) и без пакета
  `System.ServiceProcess.ServiceController` (в .NoreFramework/.NET Core он
  не встроен в SDK, нужен отдельный NuGet-пакет).
- `Server.Host.csproj` дублировал `Content Include="appsettings*.json"`,
  которые `Microsoft.NET.Sdk.Web` уже включает по умолчанию (NETSDK1022).
- `Modules.Documents`/`Modules.Import` контроллерам не хватало
  `using Microsoft.AspNetCore.Http;` для `IFormFile` (обычный `Sdk`, а не
  `Sdk.Web`, не тянет ASP.NET Core namespaces неявно).
- `Server.Api.csproj` (`Sdk.Web`) по умолчанию получал `OutputType=Exe`, но
  у него нет `Program.cs` (это библиотека, которую хостит `Server.Host`) —
  добавлен явный `<OutputType>Library</OutputType>`.
- `SystemClock` в `Server.Api/DependencyInjection/ServiceCollectionExtensions.cs`
  был неоднозначен между `Microsoft.AspNetCore.Authentication.SystemClock` и
  собственным `ResellerSystem.Server.Infrastructure.Clock.SystemClock` —
  уточнена полная квалификация.
- `Server.Api.Tests/Middleware/ExceptionHandlingMiddlewareTests.cs` не хватало
  `using System.Net.Http.Json;` для `ReadFromJsonAsync`.
- Тест `DatabaseProvisioningServiceTests.CreateAsync_registers_tenant_with_status_Creating_before_provisioning`
  падал не из-за бага в production-коде, а из-за классической ловушки
  NSubstitute с мутируемыми объектами: `DatabaseProfile`, переданный в
  `AddAsync`, — та же ссылка, что потом мутируется `MarkReady()` дальше по
  тому же вызову `CreateAsync`, поэтому `Arg.Is<DatabaseProfile>(p =>
  p.Status == Creating)`, проверяемый постфактум, видел уже финальный
  статус `Ready`. Тест переписан на захват статуса в момент вызова через
  `.When(...).Do(...)`.

**Реальный production-баг, найденный только прогоном против настоящего
PostgreSQL** (юнит-тесты его не ловят, т.к. `Server.Api.Tests` не бьёт по
реальной БД для этого пути): `Migrations/Scripts/Master/0003_users.sql`
создавал таблицу `users` без колонок `created_by`/`updated_by`, хотя
`User : AuditableEntity` и EF-маппинг в `MasterDbContext` их ожидают —
любая попытка создать пользователя (включая initial admin на первом
старте) падала с `42703: column "created_by" of relation "users" does not
exist`. Исправлено добавлением обеих колонок в миграцию (с тем же
паттерном `DEFAULT 'system'`, что и в `0001_init.sql`).

```powershell
dotnet build
dotnet test tests/Server.Domain.Tests tests/Server.Application.Tests tests/Server.Api.Tests tests/Modules.Inventory.Tests tests/Modules.Sales.Tests
dotnet test tests/Server.Data.Tests   # требует Docker
```

## Update Engine

- **DB-уровень отката не автоматизирован.** `Server.Updater` откатывает
  файлы (symlink на предыдущую версию) автоматически при провале
  health-check, но если новая версия уже успела применить миграции БД
  (master и/или tenant) до отказа — эти миграции **не откатываются
  автоматически**. Нужно вручную нажать Restore в Server Manager, указав
  backup id, который `Server.Updater` печатает в лог. Полная автоматизация
  (например, вызов `/api/v1/backups/{id}/restore` из самого `Server.Updater`
  после отката файлов) — не реализована.
- **Side-by-side раскладка (`server-versions\{version}` + symlink `server`)
  — новая, не проверена end-to-end.** Инсталлятор обновлён под неё
  (`Initialize-ServerVersion.ps1`), но реальный цикл
  install → build v2 → Install Update → сравнить с ожиданием — ни разу не
  прогонялся (нет Windows-машины).
- **`Server.Updater` запускается с `Verb = "runas"`** — если Server.Host
  уже выполняется под LocalSystem (обычный случай для Windows Service),
  UAC не должен всплывать, но это не проверено на реальной службе.
- ~~Манифест обновлений публикуется вручную~~ Исправлено (2026-08-24) —
  добавлен job `publish-release` в `.github/workflows/build-release.yml`:
  после сборки Windows/macOS-артефактов он подставляет в
  `update-manifest.json` настоящие URL (`server.url`, `releaseNotesUrl`,
  вычисляются из предсказуемого паттерна `{repo}/releases/download/{tag}/*`
  — плейсхолдеры больше не нужны) и публикует всё как обычный GitHub
  Release (тег `v{version}-{run_number}`, уникален даже без ручного бампа
  версии). `Updates:ManifestUrl` в поставляемом `appsettings.json` теперь
  тоже прописывается автоматически на `releases/latest/download/update-manifest.json`
  — "latest" у GitHub всегда указывает на самый свежий непререлизный
  Release, так что этот URL не меняется между версиями.
  **Важно:** это закрывает только публикацию манифеста. Сам цикл
  скачивание → проверка checksum → бэкап → остановка службы → подмена
  symlink → health-check → (при неудаче) откат — реализован в
  `Server.Updater`/`UpdateService`, но **ни разу не прогонялся на реальной
  Windows-машине** (см. пункты выше про `runas` и side-by-side раскладку).
  Первое реальное нажатие кнопки "Install Update" в Server Manager будет
  первым живым тестом этого пути.

## macOS

- Полностью бесшовный (тихий, без предупреждений Gatekeeper) auto-update
  требует платного Apple Developer Program ($99/год) — сознательно не
  делаем (см. `product-development-plan-v1.md`, Часть 2.3, "Открытый
  вопрос"). Сейчас (и в обозримом будущем на бесплatном пути) обновление
  клиента на Mac — вручную, через скачанный `.dmg`.
- `.app`/`.dmg` собираются ad-hoc подписанными — при первом запуске на
  чужом Mac нужен правый клик → Open.

## Backup/Restore Engine

- Использует `pg_dump.exe`/`pg_restore.exe` из `Postgres:BinDirectory` —
  путь должен быть выставлен инсталлятором; в dev-режиме (`docker compose`)
  это поле пустое, и backup/restore в dev не заработает без ручной
  настройки `Postgres:BinDirectory` на локально установленные PostgreSQL
  client tools.
- Restore не проверяет, что схема бэкапа совместима с текущей версией
  сервера (нет version-compatibility gate) — полагается на то, что
  `pg_restore --clean --if-exists` не упадёт при небольших расхождениях.
  Для сильно отличающихся версий это может не сработать корректно.
- Нет автоматического расписания (daily/weekly) — только backup "по
  кнопке" или перед обновлением. Планировщик (Hangfire/аналог) не
  реализован.

## Security Foundation

- Один пользователь без ролей (сознательное решение по Architecture Plan
  v0.1 — "сложная система ролей не требуется на данном этапе").
- Сессионные токены не отзываются автоматически при смене пароля.
- Нет rate-limiting на `/api/v1/auth/login` — теоретически уязвимо к
  brute-force по локальной сети. Для локального single-business продукта
  риск невысокий, но не нулевой.
- Пароль первого admin генерируется случайно и пишется в
  `config\initial-admin-credentials.txt` в открытом виде (с ACL,
  ограничивающим доступ до Administrators/SYSTEM) — файл не удаляется
  автоматически после первого входа.

## Бизнес-модули

- **Inventory (Purchase + Item) — реализован как proof-of-concept первого
  модуля.** Есть: миграции, `InventoryDbContext`, API
  (`/api/v1/inventory/purchases`, `/api/v1/inventory/items`), Avalonia-экран,
  authorize-защита, soft delete, source-vs-calculated для cost basis.
  **Сознательно упрощено** относительно полного Architecture Plan v0.1:
  - `PurchaseSource`/`PaymentMethod`/`Category`/`Status` — свободный текст,
    а не полноценные редактируемые справочники ("constructor"-паттерн из
    раздела 4 плана). Полноценные reference-таблицы с CRUD-экранами не
    реализованы.
  - Нет `Listing`, `Sale`, `Return`, `Expense`, `Document` — только
    Purchase→Item, без остальной цепочки из раздела 6 плана.
  - Нет Reseller Permit деталей (номер, tax exempt amount) — только булев
    флаг `UsedResellerPermit`.
  - Нет фильтров/умных представлений/Excel-like таблицы из разделов 14-16 —
    список Item в клиенте самый простой (без сортировки/группировки в UI).
- **Sales (Listing, Sale, SaleFee, Return) — реализован как API + домен,
  БЕЗ Avalonia UI-экрана** (в отличие от Inventory, где экран есть). Только
  `/api/v1/sales/*` через Swagger/HTTP-клиент. Ключевые архитектурные
  фиксы из ревью предыдущего этапа соблюдены:
  - `GrossTransactionAmount` и `PayoutAmount` — отдельные, независимые поля
    (проверено тестом `CreateNew_keeps_gross_and_payout_as_independent_values`).
  - `sale_fees` содержит **только** marketplace-комиссии; `ReturnShippingCost`/
    `OtherExpense` **сознательно временно** остались на `Return` (а не
    вынесены в отдельный Expense, привязанный по ReturnId), поскольку
    Expenses-модуль ещё не существует — задокументировано в коде и здесь,
    не спрятано.
  - `Document`/`DocumentLink` (many-to-many, из ревью) — не реализованы,
    Documents-модуль не начат вообще.
  - Net Proceeds/Net Profit/ROI (`GET /api/v1/sales/{id}/financials`) —
    упрощённый расчёт (не учитывает Returns), кросс-модульное чтение
    `cost_basis` из таблицы Inventory напрямую через SQL (задокументировано
    в `ItemCostBasisReader` как осознанный паттерн, а не связывание модулей
    через C#-ссылки).
  - `item_id`/`listing_id` в таблицах Sales-модуля **не** имеют enforced FK
    на таблицы Inventory — сознательное решение о слабой связанности
    модулей на уровне схемы (см. заголовок миграции `sales/0001_init.sql`).
- **Purchasing (в смысле "Purchases" из вашего списка) уже покрыт
  Inventory-модулем** (Purchase — это уже там), отдельного модуля не
  создавалось.
- **Documents (Document + DocumentLink) — реализован.** Реальное
  файловое хранилище (content-addressable по SHA-256 под
  `{StorageRoot}/{физическое-имя-БД}/{hash[0:2]}/{hash[2:4]}/{hash}.{ext}`),
  оригиналы не сжимаются/не перекодируются (Architecture Plan v0.1 раздел
  9). `DocumentLink` — отдельная таблица many-to-many, как требовалось в
  ревью (`GET /api/v1/documents/for/{entityType}/{entityId}`). **Не
  реализовано**: UI для загрузки/просмотра документов в desktop-клиенте
  (только API), сканирование/OCR (заведомо вне Stage 1).
- **Reports (Marketplace/Category Profitability, Inventory Aging, Federal
  Tax Summary, упрощённый 1099-K) — реализованы как read-only SQL-запросы**
  поверх таблиц Inventory+Sales (тот же паттерн, что `ItemCostBasisReader`
  в Sales). **Важные упрощения**:
  - Federal Tax Summary и 1099-K **не учитывают Returns/Expenses** — только
    Sales+Inventory. Если товар был возвращён, отчёт всё равно посчитает
    исходную продажу как доход.
  - 1099-K Box 1a — приближение (`GrossTransactionAmount +
    MarketplaceCollectedTax`), это **внутренний инструмент сверки**, явно
    не официальный документ (как и требовалось), но точность не
    проверялась против реальной формы 1099-K ни разу.
  - Washington State Report **не реализован вообще** — требует
    juрисдикционных налоговых правил (marketplace facilitator, tax-paid-
    at-source), которые архитектура прямо запретила хардкодить без
    отдельного конфигурируемого tax profile; строить наспех не стал,
    чтобы не выдать недостоверную налоговую логику.
  - Все SQL-запросы в `ReportsService` написаны и вычитаны вручную, ни
    разу не выполнялись на реальном PostgreSQL — учитывая их сложность
    (агрегации, FILTER, LEFT JOIN), синтаксические ошибки весьма вероятны.
- **Import — реализован CSV-only** (Upload → Parse → Staging → Preview →
  Validation → Confirm, полностью соблюдён mandatory workflow из раздела
  40). **Не реализовано**: Excel/PDF импорт, mapping templates
  (source column → database field), настоящая проверка дублей по
  Order/Transaction ID (только примитивная проверка дублей внутри одного
  файла). `ImportModule` — единственный модуль с прямой C#-зависимостью
  на другой модуль (`Modules.Inventory`), это осознанное исключение из
  правила "модули не зависят друг от друга" (задокументировано в
  `Modules.Import.csproj` и `ImportModule.cs`), поскольку смысл Import —
  именно запись в чужие таблицы, а не read-only агрегация.
- **Каталог модулей применяет миграции только при СОЗДАНИИ новой tenant-
  базы.** Если модуль обновится (или, как сейчас, появится впервые) уже
  ПОСЛЕ того как какие-то tenant-базы существуют, для них миграции нового
  модуля не подтянутся автоматически — механизм "доприменить отстающие
  миграции при выборе базы", описанный в Product Development Plan v1.0
  (Часть 2.4), **спроектирован, но не реализован**. Практическое
  следствие: если создать базу ДО этого изменения, а затем открыть её —
  таблиц `purchases`/`items` в ней не будет, пока база не будет
  пересоздана или пока эта функция не будет достроена.

## Тесты

- Для `PgBackupService`/`UpdateService`/`Server.Updater` тестов нет —
  они интенсивно используют внешние процессы (`pg_dump`,`pg_restore`,
  `sc.exe`) и файловую систему, что делает их сложными для юнит-тестов;
  требуются интеграционные тесты на реальной Windows+PostgreSQL машине,
  которых пока нет.
