> [!WARNING]
> **Статус 1.0.2-rc1 / development main:** исходный код существенно усилен, но промышленная совместимость с **Siemens NX 2512:6000** должна быть подтверждена сборкой и протоколом `docs/NX2512_6000_STATION_TESTS.md` на рабочей станции. До этого используйте только независимые копии `.prt`.

# NX ESKD Drawing Automation for Siemens NX 2512

Плагин формирует и обновляет управляемые чертежи Siemens NX по JSON-профилю: листы, виды, разрезы, PMI, Flat Pattern, спецификации, позиции, технические требования, таблицы, проверки, сохранение нативного `.prt` и экспорт.

Проект теперь также содержит Core-архитектуру **комплектов чертежей**: многофайловый манифест, зависимости документов, порядок выполнения, журнал возобновления, staging и запрет частичной публикации. Подключение станционного NX-адаптера, который последовательно открывает и обрабатывает несколько WorkPart, ещё требует проверки на целевой NX.

## Архитектура

- `NxEskd.Core` — schema, миграции, model-aware planning, исполняемый operation DAG, auto-scale, layout, проверки полноты и пакетный конвейер без зависимости от NX.
- `NxEskd.NxRuntime` — NX boundary, сервисы листов/видов/PMI/BOM, управляемое выполнение DAG и ограниченный compatibility/reflection gateway.
- `NxEskd.Configurator` — отдельный WPF-процесс; не загружает WPF в процесс NX.
- `NxEskd.CommandCenter` — открывает Configurator и исполняет версионированный запрос в исходной WorkPart.
- `NxEskd.PackagePlanner` — CLI-проверка манифеста комплекта и порядка документов.
- `Generate`, `Update`, `Validate`, `Preview`, `Inventory` — отдельные NX Open команды.

## Что реально автоматизируется

### Для одного чертежа

- анализ открытой модели до любых изменений;
- выбор применимого типа документа: деталь, сборка, листовой металл;
- создание и обновление нескольких листов;
- заполнение штампа через part attributes и, при доступности API, TitleBlock cells;
- основные, проекционные, секционные, detail- и flat-pattern виды;
- проекционно-ориентированная раскладка с контролем осевого выравнивания;
- наследование существующего PMI;
- BOM roll-up, стабильная нумерация позиций и balloons;
- технические требования и таблица гибов на явно выбранном листе;
- reconciliation устаревших exact-owned объектов;
- сохранение текущего файла или настроенный `SaveAs` нативного `.prt`;
- PDF и Flat Pattern DXF;
- postcondition validation и rollback при блокирующей ошибке.

### Инженерные ограничения

Плагин **не должен восприниматься как автономный конструктор**. Он пока не может доказать полноту размерной схемы и не заменяет проверку специалистом:

- автоматическое создание функционально обоснованных размеров по геометрии не реализовано;
- при отсутствии PMI формируется `PLAN_DIMENSION_SOURCE_MISSING`;
- состав видов проверяется эвристически, но окончательный выбор остаётся за конструктором;
- границы detail view и сложные линии ступенчатых/местных разрезов требуют NX-реализации и station verification;
- основные стили ЕСКД должны быть подготовлены в корпоративных PRT-шаблонах;
- перенос PMI, Parts List, balloons и Flat Pattern требует проверки на конкретной maintenance release.

## Исполняемый operation DAG

Preview и Runtime используют один нормализованный граф операций:

```text
setup:layers
  → setup:styles
  → attributes:canonical
  → feature/sheets
  → title blocks/views
  → PMI/BOM/notes/tables
  → reconciliation
  → NX update + validation
  → deferred output publication
```

Перед Undo проверяются:

- уникальность `operationId`;
- существование всех зависимостей;
- отсутствие циклов;
- поддержка необходимых NX capabilities.

Runtime больше не полагается на отдельную жёстко зашитую последовательность, расходящуюся с Preview.

## Ключевые гарантии исходного кода

- модель анализируется **до** построения плана;
- dry-run не вызывает mutation adapter;
- Generate и Update имеют разные preconditions;
- ownership: `profileId + jobId + objectKind + logicalId`;
- duplicate managed IDs блокируют выполнение до Undo;
- ручные и foreign-scope объекты не усыновляются;
- Builder уничтожается ровно одним владельцем;
- целевой лист для Parts List, техтребований и bend table выбирается явно;
- проекционный вид без родителя не размещается как свободный прямоугольник;
- postconditions выполняются внутри Undo scope;
- reconciliation строится по **эффективному отфильтрованному плану**;
- удаление stale managed objects и balloons требует Preview и явного подтверждения;
- Save/SaveAs failure блокирует PDF/DXF;
- PDF/DXF публикуются через temporary output и atomic move;
- DXF Flat Pattern не имеет whole-part fallback;
- неизвестные JSON-поля блокируются строгой schema;
- неподдержанное validation rule не считается пройденным.

Реальный статус каждой функции: `docs/CAPABILITY_MATRIX.md`.

## Комплекты чертежей

Пример: `config/drawing-package.example.json`.

Проверка манифеста:

```powershell
dotnet run --project .\tools\NxEskd.PackagePlanner\NxEskd.PackagePlanner.csproj -- `
  .\config\drawing-package.example.json
```

Проверяются:

- уникальные `documentId`, обозначения и выходные пути;
- наличие source `.prt` и профиля каждого документа;
- ссылки на зависимости;
- циклы между документами;
- порядок «детали → подсборки → сборочный чертёж»;
- нахождение выходов внутри package root при `atomicPublish=true`.

Core `DrawingPackageExecutor` предоставляет:

- последовательное выполнение документов по DAG;
- journal после каждого документа;
- возобновление с подтверждением непустого staging-файла;
- отсутствие публикации при неполном комплекте;
- backup и файловый rollback при ошибке финальной публикации;
- dry-run без вызова NX-исполнителя.

Подробности: `docs/DRAWING_PACKAGES.md`.

## Требования

- Windows x64;
- Siemens NX **2512:6000**;
- Visual Studio 2022 17.12 или .NET SDK 8;
- локальные NX assemblies из установленной NX;
- соответствующие лицензии Drafting, PMI, Parts List и Sheet Metal.

NX assemblies не входят в репозиторий и дистрибутив.

## Core CI

На каждом push в `main` выполняются:

- Core xUnit tests;
- Core smoke tests;
- сборка package planner CLI;
- сборка WPF Configurator;
- строгая JSON-проверка;
- проверка синтаксиса PowerShell;
- запрет коммита закрытых Siemens assemblies.

Локально:

```powershell
dotnet test .\tests\NxEskd.Core.Tests\NxEskd.Core.Tests.csproj -c Release
dotnet run --project .\tests\NxEskd.SmokeTests\NxEskd.SmokeTests.csproj -c Release -- `
  .\config\active-profile.example.json
```

## Сборка против точной NX 2512:6000

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\scripts\build.ps1 `
  -NxRoot "C:\Program Files\Siemens\NX2512" `
  -ExpectedNxRelease 2512 `
  -ExpectedNxMaintenance 6000
```

Скрипт:

1. находит `NXOpen.dll` и `NXOpen.UF.dll`;
2. проверяет FileVersion, ProductVersion, assembly version, издателя и public key token;
3. фиксирует SHA-256 обеих DLL в `build-info.json`;
4. запускает Core tests;
5. собирает x64 solution;
6. запрещает упаковку NX assemblies;
7. создаёт manifest и ZIP.

`-SkipNxVersionCheck` допустим только для диагностики и не создаёт подтверждённый релиз.

## Установка

```powershell
.\dist\NxEskd\scripts\install.ps1 -PackagePath .\dist\NxEskd
```

Установщик использует staging, проверку manifest, backup/swap/rollback, атомарное изменение `custom_dirs.dat` и сохраняет предыдущие пользовательские настройки для корректного uninstall.

После установки полностью перезапустите NX.

## Команды NX

```text
ЕСКД-генератор чертежей
├── Центр настройки и запуска...
├── Создать управляемый чертёж
├── Обновить управляемый чертёж
├── Предпросмотр плана
├── Проверить чертёж
└── Диагностика NX Open API
```

`Диагностика NX Open API` работает без открытой WorkPart и сохраняет inventory загруженных NXOpen/NXOpen.UF assemblies в `%LOCALAPPDATA%\NxEskdGenerator\reports`.

## Первый безопасный запуск

1. Создайте независимую копию `.prt`.
2. Запустите `Диагностика NX Open API`.
3. Проверьте пути PRT-шаблонов.
4. Выполните `Проверить JSON`.
5. Выполните `Предпросмотр`.
6. Проверьте исполняемый DAG, состав видов, масштабы, stale objects и ManualReview.
7. Только затем используйте Generate или Update.
8. После команды сравните before/after inventory и повторно откройте сохранённую копию.

## Managed ownership

Каждый созданный объект получает:

```text
AUTO_DWG_MANAGED=true
AUTO_DWG_PROFILE_ID=<profileId>
AUTO_DWG_SCOPE_ID=<jobId>
AUTO_DWG_OBJECT_KIND=<kind>
AUTO_DWG_ID=<logicalId>
AUTO_DWG_CONFIG_HASH=<sha256>
AUTO_DWG_GENERATOR_VERSION=<version>
```

Объект без полного ownership key не изменяется автоматически. Legacy-объект без scope не удаляется.

## Станционные испытания

```powershell
.\scripts\prepare-nx2512-integration-tests.ps1 `
  -NxRoot "C:\Program Files\Siemens\NX2512" `
  -FixtureDirectory "D:\NxEskdFixtures"
```

Полный протокол: `docs/NX2512_6000_STATION_TESTS.md`.

## Ограничение доказательности

Этот репозиторий не содержит Siemens NX и сам по себе не доказывает:

- точные сигнатуры NX Open в вашей NX 2512:6000;
- наличие лицензий;
- реальный rollback NX;
- корректность PMI, Parts List, balloons, Flat Pattern, SaveAs и export;
- работоспособность пакетного открытия/закрытия нескольких WorkPart;
- отсутствие аварии NX.

До прохождения station protocol пакет разрешено использовать только на тестовых копиях.
