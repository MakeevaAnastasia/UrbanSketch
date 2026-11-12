# REST API проектов, концепций и ТЭП

**Дата проверки:** 11 ноября 2026 года, Europe/Moscow.
**Спринт:** №2 «Данные и базовая функциональность».

## Назначение и реализация

API читает существующие проекты, концепции и ТЭП из PostgreSQL.
Используется текущий FinancialDbContext и Minimal API ASP.NET Core .NET 10.
Запросы EF Core асинхронные, с AsNoTracking и CancellationToken.
В SQL выбираются только поля ответов; EF-сущности и навигации не сериализуются.

Program.cs регистрирует отдельные группы маршрутов из Endpoints и безопасную
обработку непредвиденных ошибок. Существующие /api/health и CLI --seed-tep
сохранены. Обычный запуск не импортирует данные и не применяет миграции.
Пакеты backend, подключение, схема, InitialCreate и concepts.json не изменялись.

## Маршруты и статусы

| Метод и маршрут | Результат HTTP 200 | HTTP 404 |
|---|---|---|
| GET /api/projects | Массив ProjectResponse, включая пустой [] | Не используется |
| GET /api/projects/{id} | Один ProjectResponse | Нет проекта или неверный Guid |
| GET /api/projects/{id}/variants | Массив ConceptVariantSummaryResponse | Нет проекта или неверный Guid |
| GET /api/concepts/{id} | Один ConceptVariantDetailResponse | Нет концепции или неверный Guid |

Проекты и варианты сортируются по Name, затем по Id. Порядок строк с одинаковым
именем поэтому предсказуем. При существующем проекте без вариантов возвращается
200 и [], при отсутствующем проекте — 404. Существование проекта проверяется
отдельным запросом, а не выводится из наличия концепций.

Параметры маршрутов ограничены {id:guid}. Неверная строка, например not-a-guid,
не соответствует маршруту и даёт 404; Guid.Empty корректен по формату, но для
отсутствующей записи тоже даёт 404. Ответы 404 не содержат тела.

Непредвиденные ошибки, включая сбой получения контекста или запроса PostgreSQL,
дают 500 application/problem+json. Ошибка БД не заменяется успешным пустым массивом.
Ответ даже в Development не содержит исключения, стека или строки подключения:

```json
{
  "type": "about:blank",
  "title": "Внутренняя ошибка сервера.",
  "status": 500
}
```

Подробности остаются в серверном механизме журналирования и не включаются в HTTP.

## DTO и формат JSON

DTO находятся в backend/UrbanSketch.Financial.Api/Dtos:

| DTO | Поля |
|---|---|
| ProjectResponse | id, name, createdAt |
| ConceptVariantSummaryResponse | id, projectId, name, residentialArea, commercialArea, parkingSpaces |
| ConceptVariantDetailResponse | Все 14 полей текущей концепции, пример ниже |

Используется стандартный web JSON ASP.NET Core: camelCase, ISO 8601 для DateTime,
числа JSON для decimal/int, явный null для неизвестных nullable-показателей.
Коллекции вариантов и финансовых моделей, циклические ссылки и EF-метаданные
в DTO отсутствуют.

Имена DTO соответствуют текущим C#-свойствам. Исторические имена столбцов БД
не становятся именами JSON. Например, residential_sale_area_m2 отображается
в residentialArea, population — в residentsCount, max_storeys — в maxFloors,
kindergarten_places — в preschoolPlaces. Изменение схемы для этого не требуется.

## Примеры ответов реальной локальной базы

GET /api/projects:

```json
[
  {
    "id": "a18d9b8a-3d42-4b92-b7e0-095b3e1a0100",
    "name": "Демонстрационный жилой комплекс",
    "createdAt": "2026-11-11T08:21:40.797641Z"
  }
]
```

GET /api/projects/a18d9b8a-3d42-4b92-b7e0-095b3e1a0100 возвращает тот же объект
без внешнего массива. createdAt — фактическое UTC-время создания при импорте;
при другом первоначальном импорте timestamp будет отличаться.

GET /api/projects/a18d9b8a-3d42-4b92-b7e0-095b3e1a0100/variants:

```json
[
  {
    "id": "a18d9b8a-3d42-4b92-b7e0-095b3e1a0101",
    "projectId": "a18d9b8a-3d42-4b92-b7e0-095b3e1a0100",
    "name": "Концепция А",
    "residentialArea": 1000.00,
    "commercialArea": 200.00,
    "parkingSpaces": 20
  },
  {
    "id": "a18d9b8a-3d42-4b92-b7e0-095b3e1a0102",
    "projectId": "a18d9b8a-3d42-4b92-b7e0-095b3e1a0100",
    "name": "Концепция Б",
    "residentialArea": 900.00,
    "commercialArea": 300.00,
    "parkingSpaces": 25
  }
]
```

GET /api/concepts/a18d9b8a-3d42-4b92-b7e0-095b3e1a0101:

```json
{
  "id": "a18d9b8a-3d42-4b92-b7e0-095b3e1a0101",
  "projectId": "a18d9b8a-3d42-4b92-b7e0-095b3e1a0100",
  "name": "Концепция А",
  "residentialArea": 1000.00,
  "commercialArea": 200.00,
  "constructionResidentialArea": null,
  "constructionCommercialArea": null,
  "parkingSpaces": 20,
  "residentsCount": null,
  "buildingsCount": null,
  "maxFloors": null,
  "socialAreas": null,
  "preschoolPlaces": null,
  "schoolPlaces": null
}
```

Концепция Б доступна по id, заканчивающемуся на 0102; площади 900/300 м²,
парковочных мест 25. Остальные неизвестные ТЭП также null.
residentialArea / commercialArea — реализуемые площади; constructionResidentialArea /
constructionCommercialArea — отдельные строительные площади. API не подставляет
реализуемые вместо строительных и не заменяет неизвестное значение нулём.

## Локальный запуск и проверка

Предварительно должны работать PostgreSQL проекта и существующие User Secrets
Development. Реальное подключение при проверке: 127.0.0.1:5432,
база urbansketch_financial, PostgreSQL 17.11. Секреты в команды не передаются.

Из корня репозитория в PowerShell:

```powershell
dotnet build backend/UrbanSketch.Financial.Api --configuration ApiReview
if ($LASTEXITCODE -ne 0) { throw 'Сборка backend не выполнена' }

$previousAspnetEnvironment = $env:ASPNETCORE_ENVIRONMENT
$previousDotnetEnvironment = $env:DOTNET_ENVIRONMENT
try {
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:DOTNET_ENVIRONMENT = 'Development'
    dotnet run --project backend/UrbanSketch.Financial.Api --configuration ApiReview --no-build --no-launch-profile -- --urls http://127.0.0.1:5188
}
finally {
    $env:ASPNETCORE_ENVIRONMENT = $previousAspnetEnvironment
    $env:DOTNET_ENVIRONMENT = $previousDotnetEnvironment
}
```

ApiReview хранит сборку отдельно от Debug и не требует завершения работающего
Debug backend. Порт 5188 используется для отдельной проверки. Если он занят,
выбрать свободный порт, не завершая чужой процесс.

В другом терминале:

```powershell
$api = 'http://127.0.0.1:5188'
$projectId = 'a18d9b8a-3d42-4b92-b7e0-095b3e1a0100'
Invoke-RestMethod "$api/api/health"
Invoke-RestMethod "$api/api/projects" | ConvertTo-Json -Depth 5
Invoke-RestMethod "$api/api/projects/$projectId" | ConvertTo-Json
Invoke-RestMethod "$api/api/projects/$projectId/variants" | ConvertTo-Json -Depth 5
Invoke-RestMethod "$api/api/concepts/a18d9b8a-3d42-4b92-b7e0-095b3e1a0101" | ConvertTo-Json
Invoke-RestMethod "$api/api/concepts/a18d9b8a-3d42-4b92-b7e0-095b3e1a0102" | ConvertTo-Json
```

Эти же GET URL можно открыть в браузере или REST Client.
Для проверки 404 использовать /api/projects/not-a-guid либо корректный,
но отсутствующий Guid. PowerShell Invoke-RestMethod для 404 выбрасывает
исключение HTTP-клиента — это ожидаемая реакция на статус, не сбой сервера.

## Автоматические проверки

Добавлен tests/UrbanSketch.Financial.ApiTests по существующему стилю консольных
проверок проекта. Новый тестовый фреймворк, TestHost, SQLite и другие провайдеры
не устанавливались. Явная ссылка тестов на уже используемый EF Core Relational
10.0.15 согласует транзитивные версии.

Запуск из корня репозитория при работающей локальной PostgreSQL с демонстрационными
данными и настроенными Development User Secrets:

```powershell
dotnet run --project tests/UrbanSketch.Financial.ApiTests --configuration ApiReview
dotnet run --project tests/UrbanSketch.Financial.SeedTests --configuration ApiReview
```

**Результат API: 18 проверок, 0 ошибок.** Проверены:

1. Список проектов: DTO-поля camelCase, название, id и UTC-дата.
2. Конкретный проект.
3. Два отсортированных варианта, их принадлежность, площади и парковки.
4. Полный ответ концепции А.
5. Полный ответ концепции Б.
6. Восемь неизвестных nullable-показателей обеих концепций.
7. Отсутствующий проект — 404.
8. Варианты отсутствующего проекта — 404.
9. Отсутствующая концепция — 404.
10. Неверный Guid проекта — 404.
11. Неверный Guid проекта в списке вариантов — 404.
12. Неверный Guid концепции — 404.
13. Guid.Empty — отсутствующая запись, без 500.
14. Пустой список проектов — 200 и [].
15. Существующий проект без вариантов — 200 и [].
16. При пустом списке концепций отсутствующий проект всё равно даёт 404.
17. Искусственное исключение получения контекста в Development — безопасный
    ProblemDetails 500, без внутреннего диагностического маркера/стека и без [].
18. Полные снимки четырёх предметных таблиц и истории миграций до/после GET совпадают.

Тесты выполняют реальные HTTP-запросы к Kestrel на временных loopback-портах,
используют настоящие EF/Npgsql запросы к локальной БД и принудительно задают
default_transaction_read_only=on в отдельной копии строки подключения.
Исходная настройка User Secrets не меняется. Защитный SELECT подтверждает режим
read-only. Snapshot включает содержимое строк, а не только счётчики.

Для пустых списков используются отдельные тестовые подклассы FinancialDbContext
с query filters. Они меняют только видимость строк в управляемом тестовом контексте,
не схему и не данные PostgreSQL. Искусственные проекты/концепции не создаются,
демонстрационные записи не редактируются. Тесты требуют уже загруженный
демонстрационный набор и сами его не импортируют.

**Регрессия импортёра: 30 проверок, 0 ошибок.**
**Сборка backend и API-тестов: 0 ошибок, 0 предупреждений.**
**EF has-pending-model-changes:** изменений модели нет.
Независимый статический read-only review существенных замечаний не выявил.

Проверен также обычный запуск именно Program.cs на отдельном порту 5188:
health и все четыре GET-маршрута вернули ожидаемые ответы с реальными данными
PostgreSQL. Тестовый экземпляр затем штатно остановлен; ранее работавший
пользовательский backend не завершался.

После проверок в projects осталось 1, concept_variants — 2,
financial_models — 0, cash_flows — 0; в истории одна InitialCreate.
Старый Python-эталон финансовых формул на этом этапе не запускался:
ограничение отсутствующего интерпретатора описано в отчёте импорта.

## Ограничения и следующий этап

API пока только для чтения: нет CRUD финансовых моделей, вычислений, авторизации,
пагинации и подключения к рабочей платформе UrbanSketch.pro. Строительные площади
демонстрационных концепций неизвестны и не являются полным входом расчётов.
Проектный маршрут первого спринта /api/variants/{id}/tep не добавлялся:
текущим заданием закреплён /api/concepts/{id}.

Контракты готовы для подключения React. Frontend не изменялся и пока не вызывает
эти маршруты. Для браузерного клиента на другом origin потребуется отдельно
настроить CORS либо dev proxy Vite; сейчас такой настройки нет.

Можно переходить к отдельной задаче CRUD API финансовых моделей. Она не начата.
