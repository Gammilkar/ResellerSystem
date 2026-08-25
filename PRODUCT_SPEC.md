# RESELLERSYSTEM / «ЕДИНАЯ БАЗА» — ПОЛНОЕ ТЕХНИЧЕСКОЕ ЗАДАНИЕ

> Это главный Product Specification проекта. Перед началом любого крупного
> изменения сверяй решение с этим документом. Не урезай перечисленный
> функционал до «демо-версии» — можно реализовывать последовательно, но
> конечная цель, описанная здесь, обязательна.

Нужно создать не прототип, не демонстрационный проект и не набор исходников для программиста.
Нужно создать полностью устанавливаемую, рабочую, модульную локальную программу для учета закупок, товаров, продаж, расходов, документов, прибыли, налоговых данных и отчетности малого бизнеса в США.
По концепции программа должна напоминать сильно упрощенную и современную 1С, но адаптированную под американский формат бизнеса и перепродажу товаров.
Программа должна изначально проектироваться как долгоживущий продукт, который можно постоянно расширять посредством обновлений.
Пользователь не должен иметь навыков программирования.
После завершения разработки пользователь должен получить готовые установочные файлы, установить программу и пользоваться ей.

## 1. ГЛАВНЫЙ ПРИНЦИП

Пользователь НЕ должен:

* запускать `dotnet run`;
* использовать Docker;
* использовать PowerShell;
* вручную применять database migrations;
* устанавливать SDK;
* вручную редактировать JSON;
* вручную копировать программные файлы;
* вручную обновлять программу;
* самостоятельно собирать исходный код.

Все технические действия должны быть автоматизированы.

## 2. БЕСПЛАТНОСТЬ

Это обязательное правило всего проекта.
ВСЕ компоненты программы по умолчанию должны быть:

* бесплатными;
* open-source либо разрешенными для бесплатного использования;
* без обязательных подписок;
* без обязательной облачной инфраструктуры;
* без платного update server;
* без платного database service;
* без платного installer framework.

Платное решение допускается только если нормального бесплатного аналога объективно нет.
Перед использованием любого платного компонента необходимо получить отдельное разрешение пользователя.
Предпочитать:

* PostgreSQL;
* .NET;
* Avalonia;
* MAUI;
* Inno Setup / NSIS / другие бесплатные технологии;
* локальное хранение;
* бесплатные библиотеки.

## 3. ОСНОВНАЯ АРХИТЕКТУРА

Главная Windows-машина является сервером.
На ней находятся:

* ResellerSystem Server;
* PostgreSQL;
* File Storage;
* Backup Storage;
* Update Engine;
* Database Migration Engine;
* Server Manager.

Архитектура:

```
Windows Server
        ↓
Server API
        ↓
Business Logic
        ↓
PostgreSQL
        ↓
Local File Storage
```

К серверу подключаются клиенты:

```
Windows Client
Mac Client
Android Client
iPhone/iOS Client — в будущем
Web Client — возможно в будущем
```

Клиенты НИКОГДА не подключаются напрямую к PostgreSQL.
Только:

```
Client
   ↓
Server API
```

## 4. LOCAL-FIRST

Основная программа должна работать внутри локальной домашней/офисной сети.
Для основной работы интернет не требуется.
Интернет необходим только для будущих функций:

* обновлений;
* eBay API;
* Etsy API;
* других marketplace API;
* AI;
* OCR;
* внешних интеграций.

Не делать cloud обязательным.

## 5. УСТАНОВКА

После завершения разработки пользователь получает архив.
Например:

```
ResellerSystem.zip

Installers/

ResellerSystem-Server-Setup.exe
ResellerSystem-Windows-Client-Setup.exe
ResellerSystem-macOS.dmg
ResellerSystem-Android.apk
```

Windows Server устанавливается обычным installer.
Пользователь:

1. распаковал ZIP;
2. открыл `ResellerSystem-Server-Setup.exe`;
3. нажал Install;
4. дождался завершения;
5. программа работает.

## 6. WINDOWS SERVER INSTALLER

Установщик автоматически:

* устанавливает Server;
* устанавливает PostgreSQL;
* создает PostgreSQL service;
* генерирует безопасные credentials;
* создает master database;
* создает необходимые системные таблицы;
* применяет migrations;
* создает File Storage;
* создает Backup Storage;
* создает Temp;
* создает Update Storage;
* создает Logs;
* регистрирует Server как Windows Service;
* включает автоматический запуск;
* создает необходимые Windows Firewall правила только для LAN;
* запускает сервер;
* выполняет health check;
* сообщает пользователю результат.

После перезапуска Windows всё должно автоматически запуститься.
Docker Desktop не должен требоваться конечному пользователю.

## 7. SERVER MANAGER

Создать отдельное GUI-приложение:
ResellerSystem Server Manager
Показывать:

* Server Status;
* Database Status;
* PostgreSQL Status;
* Server Version;
* Database Schema Version;
* IP Address;
* Port;
* Storage location;
* Backup location;
* Update status;
* Available disk space;
* Last backup;
* Last update.

Кнопки:

* Start Server;
* Stop Server;
* Restart Server;
* Check Updates;
* Install Update;
* Backup;
* Restore;
* Settings;
* Open Logs;
* Open Client.

Никакой командной строки.

## 8. МОДУЛЬНАЯ АРХИТЕКТУРА

Программа должна строиться как:

```
Core
+
Business Modules
+
Integration Modules
```

CORE
Пример:

```
Core
├── Database Engine
├── API
├── Authentication foundation
├── User Context
├── Configuration
├── File Storage
├── Backup Engine
├── Restore Engine
├── Update Engine
├── Migration Engine
├── Logging
├── Audit
└── Module Manager
```

BUSINESS MODULES

```
Modules
├── Dashboard
├── Purchases
├── Inventory
├── Listings
├── Sales
├── Returns
├── Expenses
├── Documents
├── Import
├── Reports
├── Tax
└── Analytics
```

FUTURE INTEGRATIONS

```
Integrations
├── eBay
├── Etsy
├── Mercari
├── Facebook Marketplace
├── Bank
├── AI
└── OCR
```

Архитектура должна позволять добавлять новые модули без переписывания всей программы.

## 9. ОБНОВЛЕНИЯ — КРИТИЧЕСКОЕ ТРЕБОВАНИЕ

Update Engine — не функция «на потом».
Он является фундаментальной частью программы.
После первой установки все дальнейшие изменения должны доставляться посредством обновлений.

## 10. МОЙ БУДУЩИЙ WORKFLOW С CLAUDE

Процесс должен выглядеть так.
Я сообщаю Claude:
Добавь в карточку товара поле Barcode.
или:
Исправь ошибку расчета прибыли.
или:
Добавь новый отчет.
Claude:

1. меняет исходный код;
2. создает необходимые database migrations;
3. повышает номер версии;
4. запускает тесты;
5. собирает release;
6. создает update package;
7. помещает update package в предусмотренное системой место/update repository.

После этого я открываю:

```
Server Manager
→ Updates
→ Check Updates
```

вижу:

```
New Version: 1.4.2

[Install Update]
```

Нажимаю кнопку.
ВСЁ остальное происходит автоматически.

## 11. SERVER UPDATE FLOW

Обновление сервера:

```
Check Update
↓
Download
↓
Verify Signature/Checksum
↓
Backup
↓
Stop Services
↓
Install
↓
Run Database Migrations
↓
Start Server
↓
Health Check
```

Если всё нормально:

```
Update Successful
```

Если обновление сломалось:

```
Automatic Rollback
```

## 12. ОБЯЗАТЕЛЬНЫЙ BACKUP ПЕРЕД UPDATE

Перед каждым обновлением:

* backup базы;
* backup configuration;
* backup critical metadata.

Если обновление затрагивает документы — предусмотреть необходимую защиту File Storage.

## 13. ROLLBACK

При неудачном обновлении:

* вернуть предыдущие binaries;
* восстановить database backup при необходимости;
* вернуть configuration;
* запустить предыдущую версию;
* выполнить health check;
* записать ошибку в update log.

Пользователь не должен вручную восстанавливать систему.

## 14. ОБНОВЛЕНИЯ WINDOWS CLIENT

Windows Client должен проверять обновления.
При наличии новой версии:

```
Update Available

[Install]
```

Далее:

* download;
* verify;
* install;
* restart.

## 15. MAC CLIENT UPDATE

Архитектура обязательно должна предусматривать обновление macOS Client.
Если на первом релизе полноценное автоматическое обновление macOS невозможно реализовать бесплатно и надежно — допускается `.dmg`, но архитектуру нельзя делать так, чтобы потом пришлось переписывать приложение для добавления updater.

## 16. ANDROID

Android Client также должен иметь version compatibility.
На первом этапе допустим APK.
В будущем:

* встроенная проверка версии;
* update package либо Google Play.

## 17. VERSIONING

Использовать Semantic Versioning:

```
MAJOR.MINOR.PATCH
```

Например:

```
1.0.0
1.1.0
1.1.1
2.0.0
```

Server сообщает:

* Server Version;
* API Version;
* Database Schema Version;
* Minimum Supported Windows Client Version;
* Minimum Supported Mac Client Version;
* Minimum Supported Android Version.

## 18. DATABASE MIGRATIONS

Database schema должна изменяться автоматически при обновлении.
Например версия 1.0 имеет:

```
Item
Name
Cost
```

В версии 1.5 добавляется:

```
Barcode
StorageLocation
```

Update автоматически:

* обновляет программу;
* добавляет поля;
* сохраняет старые данные.

Нельзя требовать ручного изменения PostgreSQL.

## 19. НЕСКОЛЬКО НЕЗАВИСИМЫХ БАЗ

Одна установка Server поддерживает несколько независимых пользовательских баз.
Например:

```
Main Business
Daria
Business 2
Test
```

Название можно менять.
Каждая база полностью независима.
В будущем их может быть больше.
Сейчас сложная система ролей пользователей не нужна.
Но архитектура должна позволять потом добавить:

* Users;
* Roles;
* Permissions;
* Database Access.

## 20. ОТДЕЛЬНЫЕ POSTGRESQL DATABASE

Предпочтительно каждая пользовательская база является отдельной физической PostgreSQL database.
Например:

```
reseller_system
reseller_db_000001
reseller_db_000002
```

Пользователь видит только нормальное название.
Физическое имя базы не зависит от display name.

## 21. ГЛАВНОЕ ОКНО

После входа в выбранную базу открывается Dashboard.
Основные разделы:

* Товары;
* Закупки;
* Продажи;
* Расходы;
* Документы;
* Отчеты;
* Показатели;
* Импорт;
* Настройки.

## 22. DASHBOARD

Dashboard автоматически показывает:
Inventory

* общая закупочная стоимость товаров на балансе;
* количество товаров на балансе.

Profit

* чистая прибыль за всё время;
* чистая прибыль за месяц;
* чистая прибыль за неделю.

Sales

* количество проданных товаров за всё время;
* количество проданных за месяц;
* количество проданных за неделю.

Дополнительные показатели

* Gross Sales;
* Average ROI;
* Average Days to Sell;
* Inventory Aging;
* стоимость оставшегося inventory.

Все показатели пересчитываются автоматически.

## 23. ОСНОВНАЯ ФИНАНСОВАЯ ЦЕПОЧКА

Архитектура данных:

```
PURCHASE
   ↓
ITEM
   ↓
LISTING
   ↓
SALE
   ↓
RETURN / EXPENSE
```

Нельзя запихивать всё в одну огромную таблицу.

## 24. ЗАКУПКА

Каждая закупка является отдельным объектом.
Например:

```
Purchase #315

Date: 08/24/2026
Source: Estate Sale
Total: $500
```

Внутри закупки находятся отдельные товары.

## 25. ДАННЫЕ ЗАКУПКИ

При поступлении товара пользователь вводит:

* дата покупки;
* место покупки;
* тип закупки;
* стоимость;
* Sales Tax;
* Tax Rate;
* дополнительные fees;
* другие расходы;
* способ оплаты;
* Reseller Permit использовался или нет;
* комментарий;
* документы.

## 26. ТИПЫ ЗАКУПКИ

Обязательно различать:
Tax Paid
Товар приобретен с Sales Tax.
Reseller Permit / Tax Exempt
Sales Tax не уплачивался благодаря Reseller Permit.
No Tax / Private Purchase
Например:

* garage sale;
* частная покупка;
* другое место, где tax не взимался.

## 27. SALES TAX ПРИ ЗАКУПКЕ

Rate должен быть регулируемым.
Нельзя жестко зашивать одну ставку.
Хранить:

* Tax Rate;
* Tax Amount;
* Taxable Amount.

Программа может рассчитывать автоматически.
Но пользователь должен иметь возможность изменить сумму вручную.

## 28. ADDITIONAL FEES

Закупка может иметь дополнительные:

* buyer premium;
* auction fee;
* processing fee;
* delivery;
* другие расходы.

Все должны учитываться при расчете реальной себестоимости.

## 29. ТОВАРЫ ВНУТРИ ЗАКУПКИ

Каждый физический товар является отдельной записью.
Если куплено 10 одинаковых предметов:
создать:

```
Item #1001
Item #1002
Item #1003
...
Item #1010
```

А не одну запись quantity 10.

## 30. ITEM NUMBER

Номер товара создается автоматически.
Номера:

* уникальные;
* автоматически возрастают;
* не используются повторно после удаления товара.

Item Number должен отличаться от внутреннего database ID.

## 31. РАСПРЕДЕЛЕНИЕ СТОИМОСТИ ЗАКУПКИ

Например:
Purchase:

```
Total = $500
```

Товары:

```
Camera = $100
Books = $50
Receiver = $200
Other = $150
```

Программа автоматически показывает:

```
Allocated Total = $500
Remaining = $0
```

Если сумма не совпадает — предупреждение.
Пользователь может вручную корректировать распределение.

## 32. КАРТОЧКА ТОВАРА

Карточка должна содержать минимум:

* номер товара;
* наименование;
* Purchase ID;
* место покупки;
* дату покупки;
* стоимость закупки;
* allocated purchase expenses;
* Sales Tax;
* Cost Basis;
* дата публикации;
* дата продажи;
* marketplace;
* место продажи;
* sale price;
* стоимость продажи после вычетов;
* количество единиц;
* чистая прибыль;
* количество дней в продаже;
* штат продажи;
* категория;
* статус;
* заметки.

## 33. СТАТУСЫ

Минимальные:

* Purchased;
* In Stock;
* Not Listed;
* Listed;
* Sold;
* Returned;
* Relisted;
* Written Off;
* Lost;
* Personal Use.

Предусмотреть пользовательские статусы.

## 34. ТАБЛИЦА ТОВАРОВ

Основной Inventory экран должен быть похож по поведению на Excel.
По умолчанию сортировка:

```
Purchase Date DESC
```

Новые товары сверху.
Колонки:

* Item Number;
* Name;
* Purchase Source;
* Purchase Date;
* Purchase Cost;
* Cost Basis;
* Sale Proceeds;
* Published Date;
* Sale Date;
* Marketplace;
* Quantity;
* Net Profit;
* Days Listed;
* Status;
* Sale State.

## 35. EXCEL-LIKE UI

Требуется:

* регулировка ширины колонок мышью;
* изменение порядка колонок;
* скрытие колонок;
* сохранение layout;
* автоматическая высота строк;
* перенос длинного текста;
* текст не должен обрезаться;
* сортировка;
* поиск;
* multi-select;
* copy;
* фильтры.

## 36. ФИЛЬТРЫ

Фильтрация минимум по:

* дате покупки;
* диапазону дат покупки;
* дате продажи;
* диапазону дат продажи;
* месту покупки;
* месту продажи;
* marketplace;
* названию;
* Item Number;
* категории;
* статусу;
* штату;
* purchase price;
* sale price;
* profit;
* days listed.

Можно применять одновременно несколько фильтров.

## 37. SMART VIEWS

Быстрые представления:

* All;
* In Stock;
* Not Listed;
* Listed;
* Sold;
* Returned;
* Sold Last 7 Days;
* Sold This Month;
* Listed 30+ Days;
* Listed 60+ Days;
* Listed 90+ Days;
* Listed 180+ Days.

## 38. LISTINGS

Listing является отдельным объектом от Item.
Поля:

* Item;
* Marketplace;
* Marketplace Account;
* External Listing ID;
* Published Date;
* Listing Price;
* Shipping Setup;
* Promoted;
* Promoted Rate;
* Listing Status;
* URL;
* End Date.

Это нужно для будущей API-интеграции.

## 39. MARKETPLACES

Начальный справочник:

* eBay;
* Etsy;
* Mercari;
* Facebook Marketplace;
* Cash;
* Other.

Это должен быть редактируемый справочник.
Новые marketplaces добавляются без обновления программы.

## 40. MARKETPLACE ACCOUNTS

Marketplace и Marketplace Account — разные сущности.
Например:

```
Marketplace: eBay

Accounts:
eBay LLC
Old eBay
Other eBay
```

## 41. SALE

Sale — отдельный объект.
Хранить:

* Sale ID;
* Item ID;
* Listing ID;
* Marketplace;
* Marketplace Account;
* Order ID;
* Transaction ID;
* Sale Date;
* Item Sale Price;
* Buyer Paid Shipping;
* Buyer Paid Sales Tax;
* Handling;
* Seller Discount;
* Gross Transaction Amount;
* Marketplace Collected Tax;
* Payout Amount;
* Destination State;
* Destination ZIP;
* Payment Method.

## 42. GROSS И PAYOUT НЕЛЬЗЯ ПУТАТЬ

Обязательно отдельные поля:

```
Gross Transaction Amount
```

и:

```
Payout Amount
```

Payout нельзя использовать как Gross Sales.

## 43. EBAY EXPENSES / FEES

Для каждой продажи eBay должна быть возможность хранить отдельные фактические комиссии.
Минимум:

* Final Value Fee;
* Final Value Fee Rate;
* Per-order fixed fee;
* Insertion Fee;
* Listing Upgrade Fee;
* Promoted Listings General Fee;
* Promoted Listings Priority/CPC Fee;
* International Fee;
* Dispute Fee;
* Chargeback;
* Fee Credit;
* Tax on Seller Fees;
* Other eBay Fees.

Нельзя использовать одну общую колонку:

```
eBay Fee
```

## 44. SHIPPING НЕ ЯВЛЯЕТСЯ EBAY FEE

Shipping Label хранить как selling expense.
Например:

* USPS label;
* UPS label;
* FedEx label;
* Return Shipping.

Не смешивать с eBay Final Value Fee.

## 45. ДРУГИЕ SELLING EXPENSES

Возможность добавить:

* Shipping Label;
* Packaging;
* Supplies;
* Insurance;
* Return Shipping;
* Other Selling Expense.

## 46. NET PROCEEDS

Система автоматически считает реальные Net Proceeds.

## 47. NET PROFIT

Система автоматически рассчитывает:

```
Net Profit
=
Net Proceeds
− Cost Basis
− остальные расходы, которые еще не были учтены
```

Обязательно предотвращать двойное вычитание одного расхода.

## 48. ROI

Автоматически:

```
ROI %
=
Net Profit / Cost Basis × 100
```

## 49. RETURNS

Возврат является отдельным объектом.
Хранить:

* Return ID;
* Sale ID;
* Item ID;
* Return Date;
* Return Type;
* Refund Amount;
* Refunded Shipping;
* Marketplace Fee Credit;
* Return Shipping Cost;
* Other Expense;
* Physically Returned Yes/No;
* Condition;
* Comment.

## 50. FULL RETURN

При полном возврате история продажи не удаляется.
Workflow:

```
Sold
↓
Returned
↓
In Stock
```

или:

```
Returned
↓
Relisted
```

или:

```
Returned
↓
Written Off
```

## 51. PARTIAL REFUND

Обязательно поддерживать частичный refund.
Например:

```
Sale $100
Partial Refund $30
```

Продажа остается.
Финансовый результат пересчитывается.

## 52. EXPENSES

Создать полноценный модуль расходов.
Expense может относиться:

* к Purchase;
* Item;
* Sale;
* Marketplace;
* либо быть общим business expense.

## 53. DOCUMENTS

Документы являются отдельным модулем.
К системе можно прикреплять:

* фотографии;
* чеки;
* PDF;
* Excel;
* CSV;
* JPG;
* PNG;
* HEIC;
* другие документы.

## 54. ОРИГИНАЛЫ ФАЙЛОВ

Файлы должны храниться на Windows Server в исходном состоянии.
НЕ:

* уменьшать;
* сжимать;
* менять оригинал.

## 55. DOCUMENT METADATA

Хранить:

* Original Filename;
* Internal ID;
* MIME Type;
* Size;
* Upload Date;
* Storage Path;
* SHA-256;
* связи.

## 56. DOCUMENT LINKS

Один файл может относиться к нескольким объектам.
Использовать:

```
Document
DocumentLink
```

Например один чек относится:

```
Purchase #100
Item #1001
Item #1002
```

## 57. ИМПОРТ EXCEL

Программа должна иметь нормальный импорт:

* XLSX;
* CSV.

## 58. IMPORT MAPPING

Разные файлы могут иметь:

```
Cost
Purchase Price
Buy Price
```

Пользователь должен сопоставить:

```
Source Column
→
Database Field
```

Mapping можно сохранять как шаблон.

## 59. PDF IMPORT

Поддержать импорт PDF.
Workflow:

```
Upload PDF
↓
Extract Data
↓
Detect Tables
↓
Preview
↓
User Verification
↓
Import
```

В будущем можно добавить OCR/AI.

## 60. БЕЗОПАСНЫЙ IMPORT

НИКОГДА автоматически не записывать распознанные данные непосредственно в production tables.
Workflow:

```
Upload
↓
Parse
↓
Staging
↓
Preview
↓
Validation
↓
Correction
↓
Confirm
↓
Import
```

## 61. DUPLICATE DETECTION

Перед импортом проверять потенциальные дубликаты:

* Order ID;
* Transaction ID;
* External Listing ID;
* Marketplace;
* Amount;
* Date;
* Item ID.

Показывать:

```
Possible Duplicate
```

## 62. REPORTS

Полноценный Reports Module.
Периоды:

* неделя;
* месяц;
* квартал;
* год;
* custom range.

## 63. MARKETPLACE PROFITABILITY

По каждому marketplace:

* Gross Sales;
* Fees;
* Advertising;
* Shipping;
* Refunds;
* COGS;
* Net Profit;
* ROI.

## 64. PURCHASE SOURCE PROFITABILITY

Например:

```
Garage Sales
Invested
Sales
Remaining Inventory
Profit
ROI
```

То же:

* Estate Sales;
* Auctions;
* Storage Auctions;
* Online Auctions;
* Other.

## 65. INVENTORY AGING

Группы:

* 0–30;
* 31–60;
* 61–90;
* 91–180;
* 180+ дней.

## 66. CATEGORY PROFITABILITY

Для категорий:

* purchased;
* sold;
* average purchase cost;
* average sale;
* net profit;
* ROI;
* average days to sell.

## 67. 1099-K

Создать полноценный:
1099-K Reconciliation Report
Он предназначен для учета и сверки с официальной Form 1099-K.
Нельзя выдавать внутренний отчет за официальную форму, выпущенную payment processor.

## 68. 1099-K DATA

Хранить поля, необходимые для сравнения с реальной Form 1099-K.
В том числе:

* Tax Year;
* PSE/Marketplace;
* Marketplace Account;
* Box 1a Gross Payment Amount;
* Box 1b, если применимо;
* Number of Transactions;
* Federal Withholding;
* Monthly Gross Amounts;
* State;
* State data;
* State withholding.

## 69. 1099-K GROSS

1099-K gross должен считаться отдельно от:

* Net Profit;
* Payout;
* Net Proceeds.

Marketplace fees, refunds, shipping, COGS и другие расходы не должны случайно уменьшать 1099-K gross calculation.

## 70. 1099-K IMPORT

Пользователь должен иметь возможность загрузить:

* eBay 1099-K PDF;
* CSV;
* Excel.

После распознавания:

```
System Calculated
vs
Marketplace Reported
```

Например:

```
System: $52,431.22
eBay:   $52,431.22
Difference: $0
```

## 71. НЕСКОЛЬКО 1099-K

Поддерживать:

* несколько eBay аккаунтов;
* Etsy;
* PayPal;
* другие TPSO/PSE.

Показывать отдельно и consolidated.

## 72. FEDERAL REPORT

Создать внутренний Federal Business Tax Summary.
Поля:

* Gross Sales;
* Gross Payments;
* Refunds;
* Discounts;
* Marketplace Fees;
* Advertising;
* Shipping;
* COGS;
* Purchase Sales Tax;
* Other Expenses;
* Net Profit.

Не пытаться автоматически определять окончательный Federal Income Tax без соответствующего tax profile.

## 73. WASHINGTON STATE REPORT

Отдельный отчет для Washington State.
Показывать:

* Washington sales;
* Marketplace sales;
* Direct sales;
* Out-of-state sales;
* Gross Washington retail sales;
* Marketplace Facilitator sales;
* Marketplace-collected sales tax;
* Seller-collected sales tax;
* Reseller Permit purchases;
* Tax-paid purchases;
* B&O-relevant gross receipts.

## 74. MARKETPLACE FACILITATOR

Для marketplace хранить:

```
Marketplace Facilitator Yes/No
```

И:

```
Sales Tax Collected By Marketplace
```

## 75. ШТАТ ПРОДАЖИ

Каждая Sale должна позволять определить:

* Destination State;
* ZIP.

Это нужно для отчетности.

## 76. СПРАВОЧНИКИ — КОНСТРУКТОР

Это одно из главных требований.
Не зашивать все значения в программу.
Пользователь самостоятельно добавляет/редактирует:

* Marketplace;
* Marketplace Accounts;
* Purchase Sources;
* Purchase Types;
* Categories;
* Expense Types;
* Payment Methods;
* Sale Locations;
* Item Statuses;
* Return Types;
* Fee Types;
* States;
* другие справочники.

## 77. SOURCE DATA VS CALCULATED DATA

Обязательно разделять.
Пример:

```
Sale Price
```

— source/imported/manual.

```
Net Profit
```

— calculated.
Если пользователь изменяет calculated value вручную:
сохранить:

* original calculated value;
* override;
* дату;
* audit log.

## 78. AUDIT LOG

Для финансовых данных вести историю изменений.
Минимум:

* Entity;
* Field;
* Old Value;
* New Value;
* Date;
* User;
* Source.

Source:

* manual;
* import;
* API;
* migration;
* system.

## 79. SOFT DELETE

Финансовые данные нельзя сразу физически уничтожать.
Использовать Soft Delete для:

* Purchase;
* Item;
* Sale;
* Return;
* Expense.

## 80. BACKUP

Backup System обязателен.
Режимы:
Database Backup
БД.
Full Backup
БД + документы + configuration.

## 81. AUTOMATIC BACKUP

Настройки:

* daily;
* weekly;
* manual.

Retention регулируемый.
Например:

```
7 daily
4 weekly
```

## 82. RESTORE

Restore Wizard:

* выбрать backup;
* проверить checksum;
* проверить schema;
* проверить совместимость;
* автоматически сделать backup текущего состояния;
* восстановить;
* выполнить health check.

## 83. STORAGE MONITORING

Показывать:

* размер базы;
* размер документов;
* размер backup;
* свободное место.

Предупреждение при нехватке места.

## 84. GLOBAL SEARCH

Поиск по всей базе:

* Item Number;
* Name;
* Purchase ID;
* Sale ID;
* Order ID;
* Marketplace transaction;
* Purchase Source;
* Documents.

## 85. SETTINGS

Минимальные разделы:

```
General
Databases
Marketplaces
Marketplace Accounts
Purchase Sources
Categories
Tax
Files
Backups
Updates
Import Templates
Integrations
Logs
```

## 86. ДЕНЬГИ

Все денежные расчеты:

```
decimal / NUMERIC
```

Никогда:

```
float
double
```

Основная валюта:
USD.
Архитектура допускает другие валюты позже.

## 87. TIMEZONE

Server timestamps:
UTC.
Каждая база имеет собственный timezone.
Например:

```
America/Los_Angeles
```

UI отображает даты в timezone базы.

## 88. HISTORICAL VALUES

Новые настройки не должны изменять старые финансовые данные.
Например:
eBay fee изменился.
Старая продажа сохраняет фактическую историческую комиссию.

## 89. API-FIRST

Все клиенты используют API.
Например:

```
/api/v1/items
/api/v1/purchases
/api/v1/sales
/api/v1/reports
```

UI не должен быть жестко привязан непосредственно к структуре PostgreSQL.

## 90. EBAY API — БУДУЩЕЕ

Не обязательно реализовывать прямо сейчас, но архитектура должна позволять добавить:

* импорт listings;
* orders;
* sales;
* fees;
* payouts;
* shipping;
* returns;
* marketplace taxes.

Без полной переделки системы.

## 91. ДРУГИЕ API

Аналогично в будущем:

* Etsy;
* Mercari;
* Facebook;
* banking;
* другие сервисы.

## 92. AI / OCR

Архитектурно предусмотреть:

* OCR receipts;
* AI PDF parsing;
* AI classification;
* AI import mapping.

Но не делать платный AI обязательным для основной работы программы.

## 93. LOGGING

Логи:

* Application;
* Error;
* Database;
* Import;
* Backup;
* Update.

Logs должны ротироваться и не занимать бесконечно диск.

## 94. HEALTH CHECK

Server health должен проверять:

* API;
* PostgreSQL;
* master DB;
* tenant databases;
* File Storage;
* free space;
* schema version;
* server version.

## 95. DESKTOP CLIENT

Нормальное GUI-приложение.
Windows:

```
ResellerSystem-Windows-Client-Setup.exe
```

Mac:

```
ResellerSystem.dmg
```

Никакой командной строки.

## 96. MOBILE

Android должен иметь приложение-клиент.
Главная БД остается на Windows Server.
Android обращается через API.
Позже добавить iPhone на той же архитектуре.

## 97. ПОЛЬЗОВАТЕЛЬСКИЙ EXPERIENCE

Цель программы:
она должна быть понятна обычному человеку.
Не делать интерфейс для системного администратора.
Главные операции должны выполняться кнопками.

## 98. DEVELOPMENT / RELEASE

Разработчик может внутри использовать:

* source code;
* CLI;
* Docker;
* scripts;
* build pipeline.

Но всё это скрыто от конечного пользователя.

## 99. RELEASE BUILD

Нужна автоматическая release build pipeline.
Одна команда разработчика должна:

1. build;
2. tests;
3. migrations validation;
4. package server;
5. package clients;
6. create installers;
7. create update packages;
8. calculate checksums;
9. prepare release manifest.

## 100. UPDATE MANIFEST

Каждый release должен иметь machine-readable manifest.
Например:

```
Version
Release Date
Minimum Previous Version
Database Schema Version
Server Package
Windows Client Package
Mac Package
Android Package
Checksums
Migration List
Changelog
```

## 101. MODULAR UPDATES

Программа физически должна быть модульной.
Но на первом этапе использовать единый release cycle.
Например:

```
ResellerSystem 1.5.0
```

содержит совместимые версии:

* Core;
* Inventory;
* Purchases;
* Sales;
* Reports;
* Tax.

Не создавать сейчас чрезмерно сложную независимую версионность десятков модулей.

## 102. НИКАКИХ ВРЕМЕННЫХ КОСТЫЛЕЙ

Если фундаментальная функция нужна для долгосрочной архитектуры — реализовать нормальную основу сразу.
Особенно:

* modules;
* migrations;
* updater;
* backup;
* installer;
* API;
* audit;
* database isolation.

## 103. НО И БЕЗ ENTERPRISE OVERENGINEERING

Это программа сначала для небольшого бизнеса.
Не нужно:

* Kubernetes;
* enterprise message bus;
* десятки microservices;
* сложная distributed architecture;
* платный cloud.

Система должна нормально работать с:

* сотнями товаров;
* тысячами товаров;
* десятками тысяч товаров позже.

## 104. ГЛАВНЫЙ КРИТЕРИЙ ГОТОВНОСТИ

Финальная версия считается готовой только если обычный пользователь может:
Windows Server

1. скачать/получить ZIP;
2. распаковать;
3. запустить Server Setup;
4. нажать Install;
5. дождаться окончания;
6. запустить Server Manager;
7. увидеть Healthy;
8. создать базу;
9. работать.

Windows Client

1. запустить installer;
2. открыть программу;
3. найти Server;
4. выбрать базу;
5. работать.

Mac

1. открыть DMG;
2. установить приложение;
3. подключиться к Server;
4. работать.

Android

1. установить приложение;
2. подключиться к Server;
3. работать.

## 105. ГЛАВНЫЙ КРИТЕРИЙ ОБНОВЛЕНИЯ

После первой установки пользователь больше не должен переустанавливать программу при каждом изменении.
Пример:
я прошу Claude:
Исправь отчет.
Claude выпускает:

```
1.6.1
```

Я открываю:

```
Server Manager
→ Check Updates
→ Install
```

После этого новая функция появляется.
То же самое должно относиться к клиентам.

## 106. ГЛАВНАЯ ЦЕЛЬ

Мне нужна не программа, которую придется выбросить и переписать через полгода.
Мне нужна локальная модульная платформа учета, которую можно начать использовать в небольшом объеме сейчас и постепенно расширять:

```
учет
↓
аналитика
↓
налоговые отчеты
↓
marketplace APIs
↓
автоматический импорт
↓
AI/OCR
↓
другие интеграции
```

При этом старые данные, документы и история должны сохраняться на протяжении развития программы.

## 107. ЧТО НУЖНО ОТ ТЕБЯ КАК ОТ CLAUDE

Ты выступаешь как основной разработчик проекта.
Твоя задача — не просто генерировать куски кода, а поддерживать целостность продукта.
При каждом изменении:

1. учитывай существующую архитектуру;
2. не ломай существующие данные;
3. создавай migrations;
4. обновляй tests;
5. обновляй documentation;
6. увеличивай version при необходимости;
7. создавай update package;
8. проверяй совместимость server/client;
9. сохраняй возможность rollback.

## 108. РАЗРАБОТКА

Можешь внутри самостоятельно разбивать работу на технические этапы.
НО:
пользователь не должен заниматься этими этапами.
Не нужно после каждого маленького изменения заставлять пользователя:

* компилировать;
* проверять terminal;
* устанавливать SDK;
* запускать Docker;
* применять миграции.

Максимально выполняй эту работу самостоятельно.

## 109. КОНЕЧНЫЙ РЕЗУЛЬТАТ

Результатом разработки должен быть полноценный ResellerSystem:

* устанавливаемый;
* локальный;
* бесплатный по инфраструктуре;
* модульный;
* расширяемый;
* обновляемый;
* с Windows Server;
* с Windows/macOS/Android клиентами;
* с несколькими независимыми базами;
* с товарами;
* закупками;
* продажами;
* возвратами;
* расходами;
* документами;
* Excel/PDF import;
* Dashboard;
* аналитикой;
* 1099-K reconciliation;
* Federal summaries;
* Washington State reports;
* Backup/Restore;
* Audit Log;
* встроенными обновлениями;
* фундаментом для будущих marketplace API.

Не урезай перечисленный функционал до «демо-версии».
Можно реализовывать его последовательно внутри разработки, но конечная цель — полностью рабочая программа.
Перед началом крупного изменения всегда сверяй решение с этим документом как с главным Product Specification.
