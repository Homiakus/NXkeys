# Архитектура NX ESKD и правила Configurator

## 1. Назначение документа

Этот документ фиксирует обязательные границы архитектуры, правила безопасного выполнения NX Open и устройство Configurator. Он является нормативным для дальнейшей разработки проекта.

Ключевой принцип:

```text
JSON profile
    ↓ migration + schema + semantic validation
Typed configuration
    ↓ model analysis + value resolution
DrawingPlan
    ↓ operation DAG + executable preconditions
NX transaction
    ↓ postconditions
Publication transaction
```

Сырой JSON не должен становиться неявным вторым источником истины после построения `DrawingPlan`.

---

## 2. Слои решения

### NxEskd.Core

Не зависит от Siemens NX и WPF. Содержит:

- загрузку и миграцию профиля;
- JSON Schema и семантическую валидацию;
- модель `DrawingPlan`;
- `DrawingExecutionPolicy`;
- `DrawingPublicationPlan`;
- анализ модели в виде `ModelSnapshot`;
- планирование листов и видов;
- operation DAG;
- каталог известных preconditions;
- анализ полноты чертежа;
- анализ опасных политик;
- планирование комплектов документов.

### NxEskd.NxRuntime

Загружается только в процесс NX и отвечает за:

- capability preflight;
- создание Undo mark;
- выполнение operation DAG;
- явную активацию целевого листа;
- создание и обновление managed-объектов;
- postcondition-проверки;
- rollback;
- сохранение и экспорт.

Runtime не должен загружать WPF.

### NxEskd.Configurator

Отдельный WPF-процесс. Зависит от Core, но не от NXOpen DLL. Отвечает за:

- предметное редактирование документа;
- листы и виды;
- технические требования;
- PMI mapping;
- Parts List и позиции;
- публикацию PRT/PDF/DXF;
- опасные разрешения;
- расширенный JSON;
- валидацию фактического `DrawingPlan` до сохранения.

### NxEskd.Commands

Тонкие entry points NX. Они не содержат бизнес-логики.

---

## 3. Единственный источник истины

До planning источником является `ProfileDocument`.

После planning источником должен быть `DrawingPlan`:

```csharp
DrawingPlan
├── Sheets
├── Model
├── Operations
├── ExecutionPolicy
├── Publication
└── ResolvedVariables
```

NX-сервисам запрещено самостоятельно интерпретировать критические флаги, если они уже представлены в типизированном плане.

В первую очередь это относится к:

- удалению managed-объектов;
- защите ручных объектов;
- Save/SaveAs;
- перезаписи существующих файлов;
- перезаписи выпущенных документов;
- PDF/DXF;
- update-before-save.

---

## 4. NX Open boundary

### 4.1. Typed API является основным

Для подтверждённой версии NX 2512:6000 предпочтителен strongly typed NX Open API.

Reflection допускается только как compatibility layer и не должна:

- подменять один вид другим;
- выбирать метод только по похожему имени;
- считать void/null commit созданным объектом;
- скрывать исключение совместимого метода повтором другой перегрузки;
- подтверждать результат без postcondition.

### 4.2. Builder lifecycle

Каждый Builder обязан:

1. создаваться через подтверждённую factory;
2. получить обязательные параметры;
3. завершиться ровно одним Commit;
4. уничтожиться ровно один раз;
5. не использоваться после Destroy;
6. не оставаться живым при исключении.

Для объектных Builder используется `CommitObjectAndDestroy`, для command-style — `CommitCommandAndDestroy`.

### 4.3. Явные цели

Каждая операция должна иметь явную цель:

- WorkPart;
- DrawingSheet;
- View;
- Annotation;
- output path.

Операция не должна полагаться только на глобально активный лист. Перед штампом и группой видов выполняется подтверждённая активация target sheet.

---

## 5. Managed objects

Управляемый объект идентифицируется составным ключом:

```text
profileId + scopeId + objectKind + managedId
```

Обязательные правила:

- ручные объекты не присваиваются;
- чужой scope не изменяется;
- дубликаты ownership key блокируют выполнение;
- legacy-объекты без scope не мигрируются при неоднозначности;
- Generate запрещён поверх существующего scope;
- Update запрещён при отсутствии managed-объектов;
- stale deletion требует двух флагов и Preview.

---

## 6. Листы, шаблоны и штамп

Создание пустого листа вместо неудавшегося PRT-шаблона запрещено.

Успех листа требует:

- существующий template file;
- подтверждённую factory импорта;
- созданный объект DrawingSheet;
- требуемый формат и масштаб;
- подтверждённую активацию.

Успех основной надписи требует:

- точный `objectSelector.objectName`;
- фактически найденный TitleBlock;
- доступный editor Builder;
- запись обязательных ячеек;
- отсутствие fallback «первый похожий объект».

Запись атрибутов WorkPart сама по себе не доказывает наличие оформленного штампа.

---

## 7. Виды

Единственный каталог поддерживаемых типов — `DrawingViewKinds.RuntimeSupported`.

В текущей подтверждаемой реализации доступны:

- `base`;
- `projected`;
- `section`;
- `detail`;
- `flat_pattern`.

Любой другой тип блокируется до NX. Автоматическая подмена `BaseViewBuilder` запрещена.

Проекционный вид обязан иметь:

- parent view;
- направление `top`, `bottom`, `left` или `right`;
- размещение с сохранением осевой связи;
- postcondition выравнивания.

`half_section`, `stepped_section`, auxiliary, broken и exploded не должны появляться в обычном UI до отдельной реализации и station-теста.

---

## 8. Operation DAG

Каждая операция содержит:

- `operationId`;
- target;
- kind;
- dependencies;
- preconditions;
- payload.

Scheduler блокирует:

- повторяющиеся ID;
- отсутствующие зависимости;
- циклы;
- неизвестные preconditions.

Runtime обязан выполнить каждое precondition перед мутацией. Строка precondition без обработчика является ошибкой архитектуры, а не документацией.

NX mutation DAG и file publication являются разными транзакционными областями. Публикация выполняется только после успешного NX validation.

---

## 9. Публикация

### PRT

Для существующего target PRT требуются:

```text
allowOverwriteExisting = true
```

Для выпущенного/утверждённого документа дополнительно:

```text
allowOverwriteReleasedDocument = true
```

Перед SaveAs создаётся recovery backup. При ошибке выполняется восстановление.

Configurator дополнительно показывает итоговый confirmation dialog перед Generate/Update.

### PDF и DXF

Экспорт выполняется только после:

- NX update;
- postcondition validation;
- успешного сохранения PRT, если оно включено;
- отсутствия blocking diagnostics.

---

## 10. Configurator

### 10.1. Единый DocumentStore

Кнопка Save, Ctrl+S, Save As и запуск команды используют один `ProfileEditorDocument`:

```text
parse current JSON
→ schema validation
→ safety analysis
→ build DrawingPlan
→ normalize DAG
→ completeness analysis
→ atomic save
```

Семантически невалидный профиль не сохраняется и не формирует NX request.

### 10.2. Предметные workspaces

Основные экраны:

1. **Документ и выпуск** — реквизиты, штамп, подписи, PRT/PDF/DXF, опасные разрешения.
2. **Листы и виды** — создание/удаление листов и видов, parent-child, direction, scale и placement.
3. **Технические требования** — группы, формулировки и порядок.
4. **PMI и BOM** — model-view mapping, associativity, Parts List columns, numbering и balloons.
5. **Настройки** — расширенные скалярные поля.
6. **JSON** — advanced mode.
7. **Проверка** — ошибки, предупреждения и ManualReview.

### 10.3. Запрещённые UI-паттерны

Запрещены:

- `[ModuleInitializer]` для изменения окна;
- обход visual tree для подключения функций;
- reflection для поиска `EditableSetting`;
- два независимых механизма сохранения;
- молчаливое превращение ошибочного числа в JSON string;
- enum, отличающийся от Core/Schema;
- скрытые destructive flags без финального подтверждения.

---

## 11. Build isolation

Проект указывает зависимость от Siemens DLL только через:

```xml
<RequiresNxOpen>true</RequiresNxOpen>
```

Её имеют NxRuntime и NX Commands. Configurator и Core собираются без закрытых DLL Siemens.

NXOpen assemblies:

- не копируются в output;
- не коммитятся в Git;
- берутся только из подтверждённой установки NX;
- проверяются build script по версии и fingerprint.

---

## 12. Quality gates

Обязательные автоматические проверки:

- Schema view types равны `DrawingViewKinds.RuntimeSupported`;
- все preconditions зарегистрированы;
- Configurator не содержит ModuleInitializer/visual-tree patches;
- NX projects явно помечены `RequiresNxOpen`;
- destructive policies компилируются в `DrawingPlan`;
- projected direction проходит JSON → Planner → Plan;
- unsupported view не попадает в Runtime;
- example profile строит полный DAG;
- повторный Update не создаёт дубликаты managed objects.

---

## 13. Ограничения, требующие станции NX 2512:6000

До прохождения station protocol нельзя считать подтверждёнными:

- конкретные factory и overloads NX Open;
- импорт каждого PRT-шаблона;
- TitleBlock editor и labels;
- detail boundary;
- сложные линии разрезов;
- PMI inherit API;
- bounds видов;
- SaveAs behavior;
- PDF/DXF builders;
- Teamcenter-managed parts;
- длительный пакет из нескольких WorkPart;
- отсутствие утечек Builder и NX objects.

Результат station-теста должен включать inventory, execution report, NX syslog и проверенные fixture-файлы.
