# CRUD API финансовых моделей

**Дата проверки:** 11 ноября 2026 года, Europe/Moscow.
**Спринт:** №2 «Данные и базовая функциональность».

## Назначение и структура

Добавлены создание, чтение, список, обновление и удаление финансовых моделей
существующих концепций. Реализация использует Minimal API, FinancialDbContext,
EF Core 10.0.15 и Npgsql 10.0.3. Схема PostgreSQL, InitialCreate, JSON ТЭП,
параметры подключения и User Secrets не изменялись. Новых пакетов нет.

Финансовая модель содержит серверный Guid, conceptVariantId, восемь денежных
параметров и projectDuration в годах. Цены строительства/продажи жилья и коммерции
задаются за м², парковок — за машино-место; земля и дополнительные расходы — суммы.
Текущие DTO не содержат названия модели: такого поля нет в действующей сущности.
Несколько моделей могут относиться к одной концепции.

TotalCosts, TotalRevenue, Profit, Roi и SalesProfitability предусмотрены
как nullable-результаты, но расчёты на этом этапе не выполняются.

## Маршруты

| Метод и URL | Успех | Ошибки |
|---|---|---|
| POST /api/financial-models | 201, DTO и Location | 400 — запрос; 404 — нет концепции |
| GET /api/financial-models/{id} | 200, DTO модели | 404 — нет модели или неверный Guid |
| GET /api/concepts/{id}/financial-models | 200, массив DTO | 404 — нет концепции или неверный Guid |
| PUT /api/financial-models/{id} | 200, обновлённый DTO | 400 — запрос; 404 — нет модели или неверный Guid |
| DELETE /api/financial-models/{id} | 204 без тела | 404 — нет модели или неверный Guid |

Список проверяет существование концепции, фильтрует по conceptVariantId
и сортирует по Id. Существующая концепция без моделей возвращает 200 и [].
GET-запросы используют AsNoTracking, проекцию в DTO и асинхронные методы EF Core.
CancellationToken передаётся в чтение JSON и операции БД.

DELETE /api/financial-models без идентификатора не реализован и даёт 405;
массового удаления нет. Непредвиденные ошибки возвращаются как безопасный 500
ProblemDetails существующим обработчиком, включая окружение Development.
Пароли, строки подключения и стек исключения в HTTP-ответ не включаются.

## DTO

- FinancialModelParametersRequest — общий набор восьми денежных параметров
  и продолжительности. Nullable-входы позволяют отличить пропущенное поле от нуля.
- CreateFinancialModelRequest — параметры плюс conceptVariantId.
- UpdateFinancialModelRequest — только параметры, без id и conceptVariantId.
- FinancialModelResponse — id, conceptVariantId, параметры и пять nullable-результатов.

Для списка используется тот же FinancialModelResponse; отдельный summary DTO
пока не требуется. Навигационные свойства ConceptVariant и CashFlows, а также
EF-сущности напрямую клиенту не передаются. Формат JSON — camelCase.

## POST: пример

```http
POST /api/financial-models
Content-Type: application/json
```

```json
{
  "conceptVariantId": "a18d9b8a-3d42-4b92-b7e0-095b3e1a0101",
  "residentialConstructionCost": 100000,
  "commercialConstructionCost": 80000,
  "parkingCost": 500000,
  "residentialSalePrice": 200000,
  "commercialSalePrice": 150000,
  "parkingSalePrice": 1000000,
  "landCost": 50000000,
  "additionalCosts": 20000000,
  "projectDuration": 3
}
```

Пример 201 Created после реальной проверки локального backend:

```http
Location: /api/financial-models/af190dea-d77b-4c7f-b1ee-dbe7067cf08e
```

```json
{
  "id": "af190dea-d77b-4c7f-b1ee-dbe7067cf08e",
  "conceptVariantId": "a18d9b8a-3d42-4b92-b7e0-095b3e1a0101",
  "residentialConstructionCost": 100000,
  "commercialConstructionCost": 80000,
  "parkingCost": 500000,
  "residentialSalePrice": 200000,
  "commercialSalePrice": 150000,
  "parkingSalePrice": 1000000,
  "landCost": 50000000,
  "additionalCosts": 20000000,
  "projectDuration": 3,
  "totalCosts": null,
  "totalRevenue": null,
  "profit": null,
  "roi": null,
  "salesProfitability": null
}
```

Этот временный ID уже удалён после проверки. При ручной проверке нужно брать
новый id из ответа POST, а не использовать ID примера как существующую запись.
GET модели возвращает такую же структуру; список концепции — массив этих объектов.

## PUT: пример

PUT заменяет весь набор редактируемых параметров, а не выполняет частичный PATCH.
Каждое из девяти полей обязательно:

```json
{
  "residentialConstructionCost": 123456.78,
  "commercialConstructionCost": 91000.12,
  "parkingCost": 610000.34,
  "residentialSalePrice": 210000.56,
  "commercialSalePrice": 160000.78,
  "parkingSalePrice": 1100000.90,
  "landCost": 55000000.12,
  "additionalCosts": 21000000.34,
  "projectDuration": 4
}
```

ID и принадлежность к концепции не меняются. Результаты расчётов и дочерние потоки
через PUT не редактируются. Неизвестные результаты остаются null, не заменяются
нулём. Если результаты будут рассчитаны в будущем, задачу их инвалидации или
повторного расчёта необходимо решить в третьем спринте; текущий PUT их сохраняет.

Для отсутствующей модели PUT возвращает 404 до разбора тела запроса.
Для существующей модели невалидный запрос даёт 400 без частичного обновления.

## Серверная валидация

Восемь денежных полей обязательны, не допускают null и должны быть >= 0.
Допускается явно заданный ноль. Значения должны точно помещаться в numeric(20,2):
18 цифр целой части и до 2 значащих дробных разрядов, максимум
999999999999999999.99. Это ограничение хранения, не произвольный бизнес-лимит.

MoneyJsonConverter проверяет исходный числовой токен до decimal-преобразования.
Скрытое округление/превращение малого числа в ноль запрещены. Научная запись
1e3 и незначащие конечные нули 1.2300 разрешены как точные значения 1000 и 1.23.
NaN/Infinity, числа в строках, логические значения и другие неверные типы запрещены.

projectDuration обязателен, должен быть целым int > 0. Нулевой/отрицательный срок,
дробное число, строка и переполнение int отклоняются. Дополнительного бизнес-предела
продолжительности в текущем ТЗ нет, поэтому он не вводился.

conceptVariantId в POST обязателен и должен быть непустым Guid. Неверный Guid
в JSON даёт 400, корректный Guid отсутствующей концепции — 404.
В параметрах URL используется существующее соглашение {id:guid}: неверная строка
маршрута даёт 404, а не 500.

Чтение JSON строгое: требуется application/json (либо совместимый +json),
корневой объект, точные camelCase-имена, отсутствие повторяющихся и неизвестных
полей. Синтаксическая ошибка, пустое тело, null, массив вместо объекта дают 400.
Клиентские id и результаты расчётов запрещены как неизвестные входные поля;
conceptVariantId также запрещён в PUT.

Ошибки возвращаются как application/problem+json с HTTP 400,
стандартными title/status и словарём errors. Ошибки обязательности и диапазонов
привязаны к именам полей; ошибки синтаксиса/типов/точности относятся к body.
Сообщения понятны, без исходного исключения или конфигурации подключения.

## Сохранение и удаление

POST проверяет существование концепции, создаёт FinancialModel с серверным
Guid, присваивает только валидированные параметры и вызывает SaveChangesAsync.
Все результаты по умолчанию остаются null. Если концепция исчезла между проверкой
и INSERT, нарушение внешнего ключа обрабатывается как 404.

PUT сначала загружает указанную модель, полностью валидирует вход и затем
обновляет только финансовые параметры. На каждую запись приходится один
SaveChangesAsync; сохранение атомарно средствами EF Core/PostgreSQL.

DELETE удаляет только модель с указанным Guid. Существующий ON DELETE CASCADE
удаляет её CashFlow; родительские Project/ConceptVariant и соседние модели
не затрагиваются. Исчезновение модели перед UPDATE/DELETE обрабатывается как 404.

## Ручная проверка PowerShell

Запустить backend в Development с существующими User Secrets и локальной PostgreSQL.
Способ установки окружения только для процесса с последующим восстановлением
приведён в docs/13_projects_concepts_api.md. Для этого этапа использована отдельная
конфигурация FinancialReview и свободный порт 5189:

```powershell
dotnet build backend/UrbanSketch.Financial.Api --configuration FinancialReview
# В Development, в отдельном терминале:
dotnet run --project backend/UrbanSketch.Financial.Api --configuration FinancialReview --no-build --no-launch-profile -- --urls http://127.0.0.1:5189
```

Затем выполнить в другом терминале. Сценарий удаляет только собственную созданную
временную модель; демонстрационные проекты и концепции не изменяются:

```powershell
$api = 'http://127.0.0.1:5189'
$parameters = @{
    residentialConstructionCost = 100000
    commercialConstructionCost = 80000
    parkingCost = 500000
    residentialSalePrice = 200000
    commercialSalePrice = 150000
    parkingSalePrice = 1000000
    landCost = 50000000
    additionalCosts = 20000000
    projectDuration = 3
}
$create = $parameters.Clone()
$create.conceptVariantId = 'a18d9b8a-3d42-4b92-b7e0-095b3e1a0101'
$model = Invoke-RestMethod "$api/api/financial-models" -Method Post -ContentType 'application/json' -Body ($create | ConvertTo-Json)
$modelId = [Guid]::Parse($model.id)
try {
    Invoke-RestMethod "$api/api/financial-models/$modelId" | ConvertTo-Json
    Invoke-RestMethod "$api/api/concepts/$($create.conceptVariantId)/financial-models" | ConvertTo-Json -Depth 5
    $parameters.residentialConstructionCost = 123456.78
    $parameters.projectDuration = 4
    Invoke-RestMethod "$api/api/financial-models/$modelId" -Method Put -ContentType 'application/json' -Body ($parameters | ConvertTo-Json)
    Invoke-RestMethod "$api/api/financial-models/$modelId" | ConvertTo-Json
}
finally {
    Invoke-WebRequest "$api/api/financial-models/$modelId" -Method Delete -UseBasicParsing
}
# Следующий GET должен вернуть HTTP 404; PowerShell сообщит об HTTP-ошибке:
Invoke-RestMethod "$api/api/financial-models/$modelId"
```

Для REST Client использовать те же URL и JSON выше; id брать из ответа POST.
Пароли и строки подключения в HTTP-команды не передаются.

## Автоматические тесты и реальная PostgreSQL

Расширен существующий tests/UrbanSketch.Financial.ApiTests, дополнительный проект
или провайдер БД не создавались. Запуск из корня репозитория:

```powershell
dotnet run --project tests/UrbanSketch.Financial.ApiTests --configuration FinancialReview
dotnet run --project tests/UrbanSketch.Financial.SeedTests --configuration FinancialReview
```

**API: 69 проверок, 0 ошибок** — 18 ранее существовавших проверок и 51 новая.
Проверены POST/Location/GET, несколько моделей и фильтрация, обновление всех
параметров и повторное чтение, соседние модели, null результатов, 404,
все восемь отрицательных денежных полей, отсутствие/null обязательных полей,
неверные типы, NaN, Guid, переполнения int/decimal, лишняя точность/underflow,
неизвестные/повторяющиеся поля, попытки сменить ID/концепцию/результаты,
граница numeric(20,2), нулевые деньги и int.MaxValue, научная запись,
DELETE с каскадом тестового CashFlow, отказ массовому DELETE и безопасный 500.

CRUD-тесты обращаются настоящим HTTP-клиентом к Kestrel и выполняют реальные
EF/Npgsql операции в существующей локальной PostgreSQL. Для них существующий
тестовый host получает контекст с общей открытой NpgsqlConnection и внешней
транзакцией. Каждый HTTP-запрос имеет свой DbContext, присоединённый к этой
транзакции. После набора выполняется Rollback даже при ошибке теста.
Изменения не фиксируются в постоянной базе. Тестовая строка подключения
формируется в памяти из User Secrets и допускает только локальную базу проекта;
сами User Secrets не редактируются. Остальные GET-тесты по-прежнему read-only.

Финальный тест сравнивает полные снимки всех четырёх предметных таблиц
и истории с состоянием до тестов. Совпадение подтверждено.
**Импортёр: 30 автономных проверок, 0 ошибок.** Повторён также настоящий CLI:
добавлено 0 проектов и 0 концепций, пропущено 2 существующие концепции.

Отдельно проверен обычный запуск Program.cs на 127.0.0.1:5189, подключённый
к 127.0.0.1:5432 / urbansketch_financial (PostgreSQL 17.11):
POST 201 + Location → GET 200 → список 200 → PUT 200 → повторный GET с новыми
значениями → DELETE 204 → GET 404. Временная модель af190dea-d77b-4c7f-b1ee-dbe7067cf08e
удалена; оставшихся временных записей нет. Проверен /api/health.
Отдельный тестовый backend штатно остановлен; пользовательский backend не завершался.

Финальные SELECT PostgreSQL подтвердили:

| Таблица | Количество |
|---|---:|
| projects | 1 |
| concept_variants | 2 |
| financial_models | 0 |
| cash_flows | 0 |
| __EFMigrationsHistory | 1 |

Концепции А и Б сохранили ID, project_id, площади 1000/200 и 900/300 м²,
парковки 20 и 25. История содержит только InitialCreate, EF Core 10.0.15.
Модель EF не имеет изменений относительно миграции. Сборка backend и тестового
проекта — **0 ошибок, 0 предупреждений**. Независимый read-only обзор кода
критических и важных замечаний не выявил. Ветка не менялась, commit/push не выполнялись.

## Ограничения и следующий этап

Финансовых и CashFlow-расчётов нет: они относятся к следующим спринтам.
Никаких автоматических результатов или нулевых заглушек API не создаёт.
PUT является полной заменой параметров; PATCH и оптимистическая версия записи
не добавлены, при конкурентных PUT действует последний успешный запрос.
Авторизация, пагинация, CORS и интеграция React не добавлялись.

API готово для следующего этапа подключения React и формы финансовых параметров.
Для браузерного клиента на другом origin отдельно понадобится CORS или dev proxy.
Frontend и следующий этап в рамках этой задачи не реализовывались.
