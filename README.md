# UrbanSketch.pro — финансовая оценка концепции застройки

Учебный веб-модуль предварительной финансовой оценки проектов по ТЭП концепции
и пользовательским параметрам. Предназначен для архитекторов, девелоперов
и инвесторов; бухгалтерский и налоговый учёт в объём не входят.

**Состояние: техническая приёмка спринта 2 «Данные и базовая функциональность»
(2–13 ноября 2026 года) завершена с замечаниями.** Работают загрузка ТЭП,
выбор концепции и хранение финансовых параметров. Расчётов пока нет.
См. [приёмку](docs/17_sprint_2_acceptance.md) и [открытые замечания](docs/18_sprint_2_open_issues.md).

## Стек, структура и реализованные функции

Backend: .NET SDK 10, ASP.NET Core, EF Core 10, Npgsql, PostgreSQL 17.
Frontend: React 19, TypeScript 5.9, Vite 7, Tailwind CSS 4.
Инфраструктура: Docker Desktop/Compose. Проверки: node:test, headless Chrome/CDP,
консольные .NET-проекты HTTP и импорта. Локально проверены Node 24.22.1 и npm 11.19.1.

```text
backend/UrbanSketch.Financial.Api/
  Data/             FinancialDbContext, миграции, импорт JSON
  Dtos/             контракты запросов и ответов
  Endpoints/        API проектов, концепций и финансовых моделей
  Models/           Project, ConceptVariant, FinancialModel, CashFlow
  Validation/       обязательность, диапазоны, точные decimal
frontend/src/       экраны, API-клиенты, hooks, валидация формы
frontend/tests/     Node и браузерные сценарии
infrastructure/     Compose и .env.example
database/seed/      concepts.json с демонстрационными ТЭП
database/schema.sql исторический проект БД первого спринта
database/erd.mmd    историческая ER-схема
docs/               проектирование и отчёты этапов 01–18
mockups/index.html  автономный макет первого спринта
tests/UrbanSketch.Financial.ApiTests/   HTTP-проверки с PostgreSQL
tests/UrbanSketch.Financial.SeedTests/  проверки импортёра
tests/reference_case.py                эталон чисел, не код расчётов модуля
```

В спринте 1 подготовлены требования, сценарии, словарь данных, формулы,
архитектура, проект БД, четыре макета и каркас приложения. Документы 01–08
и автономный макет сохраняют исторические обозначения первого спринта.

В спринте 2 созданы четыре таблицы и InitialCreate, импорт JSON,
GET проектов/концепций, CRUD финансовых моделей. В UI работают выбор
проекта/концепции, 11 ТЭП, девять финансовых параметров, сохранение, открытие,
редактирование, несколько моделей на концепцию, удаление с подтверждением,
защита несохранённого ввода и обработка ошибок API.

## Первоначальная настройка

Команды — **PowerShell из корня репозитория**, кроме блоков с `Push-Location`.
Используется `npm.cmd`, чтобы не менять execution policy для npm.ps1.
Нужны .NET SDK 10, Node/npm и запущенный Docker Desktop с Compose.

### PostgreSQL

```powershell
if (-not (Test-Path infrastructure/.env)) {
    Copy-Item infrastructure/.env.example infrastructure/.env
}
```

В локальном env замените шаблон пароля `CHANGE_ME_FOR_LOCAL_ONLY`.
База — `urbansketch_financial`, пользователь по образцу — `urbansketch_local`.
Если контейнер уже инициализирован, используйте действующие учётные данные:
изменение env не меняет пароль в существующем томе. Том не пересоздавайте.

```powershell
docker compose -f infrastructure/docker-compose.yml up -d
docker compose -f infrastructure/docker-compose.yml ps
```

Дождитесь `healthy`. PostgreSQL доступна на **127.0.0.1:5432**; данные хранятся
в Docker volume `pgdata`. ASP.NET Core сам не читает `infrastructure/.env`.

### Зависимости и User Secrets

```powershell
dotnet restore backend/UrbanSketch.Financial.Api
Push-Location frontend
npm.cmd ci
Pop-Location
```

UserSecretsId уже задан в csproj. Введите действующие значения из локального
env: пароль вводится скрыто, JSON передаётся через stdin без временного файла.
Не выводите переменные с секретом и не публикуйте `dotnet user-secrets list`.

```powershell
$pgUser = Read-Host 'POSTGRES_USER из локальной конфигурации'
$pgPassword = Read-Host 'Действующий POSTGRES_PASSWORD' -AsSecureString
$localConnection = [System.Data.Common.DbConnectionStringBuilder]::new()
$previousOutputEncoding = $OutputEncoding
try {
    $OutputEncoding = [Text.UTF8Encoding]::new($false)
    $localConnection['Host'] = '127.0.0.1'
    $localConnection['Port'] = '5432'
    $localConnection['Database'] = 'urbansketch_financial'
    $localConnection['Username'] = $pgUser
    $localConnection['Password'] = [Net.NetworkCredential]::new('', $pgPassword).Password
    $secretJson = @{ ConnectionStrings = @{
        DefaultConnection = $localConnection.ConnectionString
    }} | ConvertTo-Json -Depth 3 -Compress
    $secretJson | dotnet user-secrets set --project backend/UrbanSketch.Financial.Api
    if ($LASTEXITCODE -ne 0) { throw 'Не удалось сохранить User Secrets' }
}
finally {
    $OutputEncoding = $previousOutputEncoding
    $localConnection.Clear()
    Remove-Variable secretJson, pgPassword -ErrorAction SilentlyContinue
}
```

Ключ — `ConnectionStrings:DefaultConnection`. User Secrets находятся вне Git
и автоматически читаются в **Development**. Альтернатива — переменная окружения
`ConnectionStrings__DefaultConnection`. Не помещайте её значение в исходники,
appsettings или frontend `VITE_*`. User Secrets используются для разработки.

### Существующая миграция

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:DOTNET_ENVIRONMENT = 'Development'
dotnet ef --version
```

Если инструмента нет: `dotnet tool install --global dotnet-ef --version 10.0.15`.
Установленную версию сверяйте с EF Core проекта. Переменные выше действуют
только в текущем процессе PowerShell, не глобально в Windows.

Перед записью подтвердите локальные host/port/database и состояние БД.
InitialCreate проверена для пустой локальной БД; при неожиданной схеме/данных
остановитесь. Сначала проверьте список миграций:

```powershell
dotnet ef migrations list --project backend/UrbanSketch.Financial.Api
dotnet ef migrations has-pending-model-changes --project backend/UrbanSketch.Financial.Api
```

В текущем проекте подготовлена только InitialCreate. Если нет более поздних
применённых миграций, примените существующую:

```powershell
dotnet ef database update 20261111075406_InitialCreate --project backend/UrbanSketch.Financial.Api
```

Уже применённая InitialCreate не создаёт таблицы повторно. Новые миграции для
запуска не нужны; при старте они не применяются автоматически. Текущая схема:
`projects`, `concept_variants`, `financial_models`, `cash_flows`, служебная
`__EFMigrationsHistory`. **Не исполняйте исторический `database/schema.sql`
поверх схемы EF Core.** См. документы [09](docs/09_persistence_setup.md),
[10](docs/10_initial_migration_review.md), [11](docs/11_database_migration_result.md).

### Демонстрационные ТЭП

После миграции, в Development:

```powershell
dotnet run --project backend/UrbanSketch.Financial.Api --no-launch-profile -- --seed-tep ((Resolve-Path database/seed/concepts.json).Path)
```

CLI ограничена локальной БД проекта. Повторный импорт пропускает существующие
ID без перезаписи; финансовые модели/потоки не создаёт. Автоматического импорта
при старте нет. См. [12](docs/12_tep_json_import.md).

| Концепция | Реализуемое жильё, м² | Коммерция, м² | Машино-места |
|---|---:|---:|---:|
| А | 1000 | 200 | 20 |
| Б | 900 | 300 | 25 |

Один проект: «Демонстрационный жилой комплекс». Строительные площади, жители,
здания, этажность, соцплощади и места ДОУ/СОУ неизвестны и отображаются как
«Нет данных», без замены нулём или реализуемой площадью. Реальные нули
показываются отдельно.

## Запуск приложения

Backend, отдельный терминал из корня:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:DOTNET_ENVIRONMENT = 'Development'
dotnet run --project backend/UrbanSketch.Financial.Api --no-launch-profile --urls http://localhost:5079
```

Frontend, второй терминал:

```powershell
Push-Location frontend
npm.cmd run dev -- --port 5173 --strictPort
Pop-Location
```

- Приложение: `http://localhost:5173`.
- API: `http://localhost:5079/api`; список проектов — `/api/projects`.
- Диагностика: `/api/health`, поля status/component/sprint. `sprint: 2` —
  временная метка этапа, не версия API. Health не проверяет БД.
- Модели: `/api/financial-models`, `/api/financial-models/{id}`;
  список концепции — `/api/concepts/{id}/financial-models`.

Vite проксирует `/api` на `http://localhost:5079`. Если порт занят, не завершайте
неизвестный процесс. При аудите старый backend на 5079 возвращал 404 для
`/api/projects`; его владельцу нужно вручную запустить актуальную сборку.
Проверенная отдельная пара портов:

```powershell
# backend: отдельный терминал, Development как выше
dotnet run --project backend/UrbanSketch.Financial.Api --no-launch-profile --urls http://127.0.0.1:5190

# frontend: другой терминал, из frontend
$env:API_PROXY_TARGET = 'http://127.0.0.1:5190'
npm.cmd run dev -- --port 5174 --strictPort
```

Адрес — `http://127.0.0.1:5174`. API_PROXY_TARGET относится к процессу Vite.
VITE_API_BASE_URL по умолчанию `/api`; менять его для proxy не нужно.
Обычный запуск и отдельная проверочная пара оцениваются раздельно.

## Сборка и тесты

```powershell
# из корня
dotnet build backend/UrbanSketch.Financial.Api --configuration Release
dotnet build tests/UrbanSketch.Financial.ApiTests --configuration Release
dotnet build tests/UrbanSketch.Financial.SeedTests --configuration Release
dotnet run --project tests/UrbanSketch.Financial.ApiTests --configuration Release --no-build --no-launch-profile
dotnet run --project tests/UrbanSketch.Financial.SeedTests --configuration Release --no-build --no-launch-profile

Push-Location frontend
npm.cmd test
npm.cmd run typecheck
npm.cmd run build
Pop-Location
```

API-проверки — консольный HTTP runner, не `dotnet test`. Нужна подготовленная
локальная БД с двумя концепциями; CRUD выполняется в откатываемых транзакциях,
снимки данных/истории сравниваются. 30 тестов импортёра по умолчанию не пишут
в БД. При блокировке исполняемого файла используйте отдельную конфигурацию
или выходной каталог, не завершайте работающие процессы автоматически.

Для браузерных тестов сначала запустите актуальную отдельную пару серверов.
Демонстрационные концепции не должны содержать чужих моделей: сценарий ожидает
пустой список. Затем из `frontend`:

```powershell
$env:FRONTEND_URL = 'http://127.0.0.1:5174'
npm.cmd run test:browser
```

Нужен Chrome по пути `C:/Program Files/Google/Chrome/Application/chrome.exe`
либо `BROWSER_BINARY` с другим путём. Реальный сценарий создаёт временную
модель и удаляет только собственные ID. Запускать только на подготовленной
локальной БД, не на dev/staging/production и не на неизвестных записях.

Аудит 12 ноября: **70 Node + 28 browser + 1 недоступный backend + 69 HTTP +
30 импорт = 198 успешных проверок**. Дополнительный сценарий ширин и
расширенный сквозной путь выполнялись в локальном игнорируемом harness:
обычный `test:browser` содержит 27 сценариев. Проверка реально отключённого
backend выполнялась отдельно. Исторические 198 не повторяются каждой сборкой.

## Ограничения и следующие спринты

- Затраты, выручка, прибыль, ROI и маржа не вычисляются; поля результатов
  nullable, экран результатов — явно обозначенный статический пример.
- Ввод строительных площадей/коэффициента отсутствует. Неизвестные площади
  нельзя автоматически заменять реализуемыми для будущих расчётов.
- Сравнение — макет; реальные потоки, график и суммы долей 100% не вычисляются.
- Экстремально большие ТЭП при отображении через Number могут потерять точность.
  Денежный CRUD numeric(20,2) использует точное десятичное представление.
- Авторизация, история версий/ETag и рабочая интеграция с платформой отсутствуют.
  Это локальный учебный прототип; доступ в интернет здесь не настраивается.
- При неоднозначном исходе POST сначала обновите список: автоматического
  повторного создания после сетевого сбоя нет.

По утверждённому плану: спринт 3 (16–27 ноября) — расчёты и пересчёт;
спринт 4 (30 ноября–11 декабря) — потоки, график, сравнение;
спринт 5 (14–25 декабря) — CSV, итоговые проверки и демонстрация.
Эталон формул: [docs/04_financial_rules.md](docs/04_financial_rules.md).

## Документы и Git

Точки входа — README, [приёмка](docs/17_sprint_2_acceptance.md) и
[открытые замечания](docs/18_sprint_2_open_issues.md). Docs/09–16 — исторические
отчёты последовательных этапов, ограничения в них относятся к соответствующим датам.

Оригинальное ТЗ и утверждённый график хранятся локально в `docs/references/`,
исключённом из Git: публикация этих файлов не разрешена. В публичной копии
ссылки приёмки на них недоступны; внутренний доступ запрашивается у владельца.
Исходники документов не удаляются. Не публикуйте `.env`, User Secrets, журналы
и генерируемые каталоги. `frontend/tsconfig.tsbuildinfo` исключён из индекса
при подготовке коммитов второго спринта и остаётся локальным игнорируемым кэшем.
Служебные планы `docs/superpowers/` также остаются локальными и не входят в поставку.
