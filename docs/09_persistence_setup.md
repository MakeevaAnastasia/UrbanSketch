# PostgreSQL и EF Core: подготовка спринта 2

`FinancialDbContext` использует Npgsql, получает options через DI и описывает
четыре таблицы: `projects`, `concept_variants`, `financial_models`, `cash_flows`.
Подключение не открывается при запуске приложения. `/api/health` проверяет
работу приложения, а не доступность PostgreSQL.

## Локальная конфигурация

Docker Compose читает `infrastructure/.env`; ASP.NET Core автоматически этот
файл не загружает. Необходимо отдельно настроить ключ приложения
`ConnectionStrings:DefaultConnection` через User Secrets (в Development)
или переменную окружения `ConnectionStrings__DefaultConnection`.

Шаблон строки подключения:

```text
Host=localhost;Port=5432;Database=<POSTGRES_DB>;Username=<POSTGRES_USER>;Password=<локальный пароль>
```

Значения должны соответствовать локальному `infrastructure/.env`. Для backend
на хосте используется `localhost:5432`; адрес сервиса `postgres` нужен только
при запуске backend в той же сети Docker. Реальные секреты не добавлять в Git.

Пример настройки User Secrets из корня репозитория (подставить локальные значения):

```powershell
dotnet user-secrets set 'ConnectionStrings:DefaultConnection' '<строка подключения>' --project backend/UrbanSketch.Financial.Api
```

`UserSecretsId` уже задан в проекте. Для переменной окружения использовать
тот же шаблон строки; стандартный конфигуратор ASP.NET Core преобразует `__`
в `:`. Если значение отсутствует или пусто, получение контекста из DI вызывает
понятное исключение с названием ключа, без вывода строки подключения.

## Отображение и отличия от проекта первого спринта

Текущая модель из четырёх сущностей определена заданием спринта 2.
`database/schema.sql` и `database/erd.mmd` остаются историческим проектом
семи таблиц, а не DDL текущего контекста. Финансовые входы и результаты теперь
хранятся в `financial_models`, распределения и потоки — в `cash_flows`,
строительные площади — в `concept_variants`.

Смысловые имена столбцов сохранены из первого спринта, в частности:

| C# | PostgreSQL |
|---|---|
| ResidentialArea / CommercialArea | residential_sale_area_m2 / commercial_sale_area_m2 |
| ConstructionResidentialArea / ConstructionCommercialArea | residential_construction_area_m2 / commercial_construction_area_m2 |
| ResidentsCount / MaxFloors | population / max_storeys |
| SocialAreas / PreschoolPlaces | social_area_m2 / kindergarten_places |
| FinancialModel.ConceptVariantId | variant_id |
| ProjectDuration | duration_years |
| Roi / SalesProfitability | roi_percent / sales_margin_percent |
| CostShare / RevenueShare | expense_percent / revenue_percent |
| PeriodCosts / PeriodRevenue | costs / revenue |
| NetCashFlow / CumulativeCashFlow | net_flow / cumulative_flow |

В соответствии с текущим заданием денежный поток имеет собственный `id`,
`financial_model_id`, `period_number` и уникальный индекс на два последних
столбца. Номер года положительный; доли заданы процентами от 0 до 100,
что закреплено CHECK-ограничениями. Проверка суммы долей по всем периодам
остаётся будущей задачей.

Связи обязательные, один ко многим, с `ON DELETE CASCADE` по всей цепочке:
проект → варианты → модели → потоки. Дочерние записи принадлежат родителю;
удаление родителя удалит всю его подчинённую структуру, как в исходном DDL.

Деньги: `numeric(20,2)`; площади: `numeric(18,2)`; доли: `numeric(7,4)`;
ROI и рентабельность: `numeric(12,4)`. Аннотации сущностей согласованы с Fluent API.
Guid отображается в `uuid`, int — в `integer`, CreatedAt — в
`timestamp with time zone` и инициализируется UTC-временем.
Nullable-показатели сохраняются nullable без SQL-значений по умолчанию.
Не добавляются отсутствующие в четырёх сущностях поля старой схемы:
название/валюта/временные метки финансовой модели и разделение парковочных мест.

## Перед первой миграцией (отдельная задача)

1. Проверить и согласовать модель из четырёх таблиц и её отличия от старого DDL.
2. Настроить локальную строку подключения и инструмент `dotnet-ef` версии 10.x,
   совместимый с установленным EF Core. Пакет EF Core Design уже присутствует.
3. В отдельной задаче создать первую миграцию и проверить SQL, nullable,
   точность, индексы и каскады до её применения.
4. Отдельно разрешить применение миграции к выбранной базе данных.

На этом этапе миграции не создаются и не применяются, реальная БД не меняется.

Следующий этап подготовки InitialCreate и результаты аудита описаны в
[10_initial_migration_review.md](10_initial_migration_review.md).
