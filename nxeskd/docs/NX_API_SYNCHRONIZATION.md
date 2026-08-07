# Автоматическая синхронизация NX Open API

## Назначение

NX ESKD больше не полагается только на статический `config/nx2512-api-map.json`. При создании runtime-контекста программа анализирует **фактически загруженные текущим процессом NX** сборки `NXOpen.dll` и `NXOpen.UF.dll`, а также доступные объекты рабочей детали.

Это устраняет типичную причину ошибок `NX_CAPABILITY_METHOD_MISSING`: имя или владелец builder-метода отличаются от предположений статической карты, хотя API присутствует в установленной NX.

## Как работает синхронизация

1. Runtime получает версии, MVID, product version и пути загруженных NXOpen-сборок.
2. Из этих данных рассчитывается SHA-256 fingerprint установки.
3. На живых объектах NX и экспортированных типах ищутся:
   - коллекции листов и чертёжных видов;
   - builders базовых, проекционных, секционных и detail views;
   - `Features.SheetmetalManager` и `CreateFlatPatternBuilder`;
   - `WorkPart.PlotManager` и `CreatePrintPdfbuilder`;
   - `ViewStyle.InheritPmi` и методы настройки наследования PMI.
4. Результат сохраняется в машинно-локальный кэш:

   `%LOCALAPPDATA%\NxEskdGenerator\api-cache\nx-api-map-<version>-<fingerprint>.json`

5. При последующих запусках кэш используется только при совпадении fingerprint. После обновления NX или замены NXOpen-сборок формируется новый файл.
6. Итоговый порядок алиасов:
   1. обнаруженные в текущей NX;
   2. базовая карта `config/nx2512-api-map.json`;
   3. встроенные безопасные fallback-имена.

Статическая карта больше не может вытеснить встроенный fallback и сделать рабочий API недоступным.

## Исправленные точки NX 2512

### Классификация модели

Наличие `SheetmetalManager` больше не считается доказательством, что открыта листовая деталь. Runtime получает менеджер через `WorkPart.Features.SheetmetalManager` и вызывает read-only `IsSheetmetalBody(body)` для каждого тела. Только положительный результат включает sheet-metal сценарий.

Снимок модели также содержит имена datum planes. Они используются при подготовке плана разрезов до начала изменения детали.

### Flat Pattern

Builder разрешается через `WorkPart.Features.SheetmetalManager`, а не через предполагаемый `WorkPart.SheetMetalManager`. Стационарная грань и направление задаются через селекторы builder:

- `UpwardFace`;
- `XAxisEdge`.

Для обычной детали без признаков sheet metal универсальный профиль:

- переводится из `sheet_metal_drawing` в `part_drawing`;
- исключает Flat Pattern и Bend Table;
- удаляет лист, содержащий только развертку;
- не запрашивает sheet-metal лицензию.

Рабочие профили со статусом `active` остаются строгими: несоответствие `job.documentKind` фактической модели является ошибкой.

### Разрезы по datum plane

Если example-профиль содержит разрез, но указанной datum plane нет в модели, такой разрез исключается из эффективного плана с предупреждением. Для рабочего профиля отсутствие datum plane является блокирующей ошибкой до Undo.

### PMI

Наследование PMI выполняется через стиль чертёжного вида:

- `DrawingView.Style.InheritPmi`, либо
- `DraftingViews.CreateDrawingViewBuilder(view).ViewStyle.InheritPmi`.

Runtime устанавливает режим, содержащий `FromModelView`, и включает перенос PMI в drawing. Несуществующие универсальные методы `ImportPmi`/`CreateInheritedPmi` больше не являются основным механизмом.

Если анализ модели показывает `pmiCount = 0`, PMI-операция исключается из плана до запуска NX и не вызывает проверку PMI API или лицензии.

### PDF

PDF builder разрешается через `WorkPart.PlotManager.CreatePrintPdfbuilder()` без передачи фиктивного `null`-аргумента. Имя с фактическим регистром также включено в карту.

## Plan-aware preflight

Capability preflight получает уже отфильтрованный `DrawingPlan`, а не перечитывает универсальный профиль. Проверяются только реально запланированные листы, виды и операции. Это предотвращает ложные блокировки из-за Flat Pattern, PMI, Bend Table или разрезов, удалённых на этапе анализа применимости.

Проверки выполняются до видимого Undo mark и до первого изменения модели.

## Диагностика

Команда **Диагностика NX Open API** формирует:

- полный JSON inventory;
- Markdown inventory;
- путь и fingerprint активной runtime-карты в `messages` и `metrics` отчёта.

Поля отчёта:

- `inventory.apiMapPath`;
- `inventory.apiMapFingerprint`;
- `inventory.apiMapSynchronized`;
- `inventory.recordCount`;
- `model.isSheetMetal`;
- `model.datumPlanes`.

## Проверка на станции NX 2512

После обновления сборки необходимо выполнить:

```powershell
.\scripts\verify-all.ps1 -NxRoot "C:\Program Files\Siemens\DesigncenterNX2512"
```

Затем в NX:

1. открыть целевую деталь;
2. запустить **Диагностика NX Open API**;
3. проверить `inventory.apiMapSynchronized = true`;
4. выполнить `Preview` и убедиться, что в плане нет неприменимых операций;
5. выполнить `Generate`;
6. повторить командой `Update` для проверки идемпотентности.

## Безопасность

Синхронизация только читает метаданные сборок и отражение типов/живых объектов. Она не вызывает builders и не изменяет модель. Побочные операции по-прежнему начинаются только после capability preflight и установки Undo mark.
