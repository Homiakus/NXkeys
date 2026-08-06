# NXKeys Mnemonic Command Language — v8  
## Ergonomic Edition — финальный контракт sequence policy

**Статус документа:** целевая нормативная спецификация  
**Редакция документа:** v8.1 — добавлены Layer Workspace, WAVE Workspace и Geometry Display Workspace  
**Целевая среда:** Siemens NX / Designcenter NX 2512.x, Windows  
**Основной профиль:** инженер-конструктор; дополнительно — сборки, чертежи, листовой металл, PMI, CAM, Simulation и Routing  
**Версия схемы профиля:** должна быть повышена относительно schema 6  
**Главный принцип:** короткая команда запускает намерение, а NXKeys автоматически управляет контекстом, допустимыми объектами, ролями выбора и Selection Intent.

---

# 1. Назначение v8

NXKeys v8 — контекстный язык управления Siemens NX, оптимизированный для работы левой рукой совместно с мышью.

Система должна сокращать не только путь до запуска команды, но и количество действий внутри операции:

```text
горячая клавиша
→ определение контекста NX
→ анализ предварительного выбора
→ запуск проверенной команды
→ назначение выбранных объектов подходящим ролям
→ включение подходящего Selection Intent
→ переход между коллекторами
→ безопасное завершение операции
```

v8 заменяет плоскую модель:

```text
sequence → BUTTON ID
```

на модель:

```text
input
→ semantic operation
→ current context
→ operation contract
→ selection role
→ selection intent
→ verified adapter
→ execution
```

---

# 2. Обязательное правило достоверности BUTTON ID

`BUTTON ID` считается исполняемым только после проверки на целевой установке NX 2512.x.

Статусы:

| Статус | Значение | Разрешено выполнять |
|---|---|---:|
| `verified_local` | Команда проверена на целевой сборке NX | Да |
| `verified_journal` | Поведение подтверждено Journal и повторным запуском | Да |
| `verified_menu` | Подтверждён точный MenuScript/BUTTON ID | Да |
| `declared_v8` | Идентификатор указан в принятой логике v8, но ещё не проверен локально | Только в тестовом профиле |
| `inherited_v7` | Идентификатор унаследован из v7 | После повторной проверки |
| `tbd_adapter` | Команда нужна, но корректный адаптер не найден | Нет |
| `suspect_mapping` | Идентификатор не соответствует смыслу команды | Нет |
| `disabled` | Команда запрещена политикой | Нет |

NXKeys не должен подставлять идентификатор другой команды по сходству английского или русского названия.

Примеры запрещённых сопоставлений:

- Trim Body через `UG_SKETCH_TRIM`;
- Through Curve Mesh через `UG_SIM_MESH`;
- Deselect через `UG_APP_GATEWAY`;
- Hole Callout через `UG_MODELING_HOLE_FEATURE`;
- Chamfer Dimension через `UG_MODELING_CHAMFER_FEATURE`;
- CAM drilling через Modeling Hole;
- CAM chamfer milling через Modeling Chamfer;
- Sketch Fillet через Modeling Edge Blend без реальной проверки.

В таблицах ниже такие команды сохраняются как намерения, но получают `tbd_adapter` либо `suspect_mapping`.

---

# 3. Трёхуровневая архитектура ввода

## 3.1. Уровень A — Direct Keys

Одноклавишные команды для самых частых операций. Leader не открывается.

Direct Keys работают только когда:

1. фокус находится в графическом окне NX;
2. не редактируется текст, имя, выражение, формула, таблица или числовое поле;
3. не открыт неизвестный модальный диалог;
4. команда разрешена текущим приложением;
5. не включён режим ввода пути Leader HUD;
6. клавиша не используется NX для подтверждённого drag/navigation-сценария.

Если хотя бы одно условие не выполнено, клавиша передаётся NX без перехвата.

## 3.2. Уровень B — Selection Intent Modifiers

Цифры `1–4` меняют правило распространения выбора внутри активного коллектора геометрической операции.

Они работают только если:

- NXKeys распознал активную команду;
- распознан активный selection collector;
- фокус не находится в числовом поле;
- текущий collector допускает Selection Intent;
- пользователь не вводит значение размера, количества, угла или допуска.

## 3.3. Уровень C — Leader HUD

`CapsLock` открывает мнемонический слой для остальных команд.

Длина основного пути:

- 1 токен — частые команды внутри узкого контекста, прежде всего Sketch;
- 2 токена — основные команды приложения;
- 3 токена — варианты, семейства и менее частые операции;
- 4 токена допускаются только для редких административных функций.

## 3.4. Постоянные рабочие пространства HUD

Для операций, где инженер последовательно выполняет несколько связанных действий, вводится режим **Workspace**. Он открывается обычным Leader-путём, но после выполнения команды не закрывается автоматически.

Обязательные пространства:

| Путь | Workspace | Назначение |
|---|---|---|
| `M → L` | Layer Workspace | рабочий слой, состояния слоёв, перенос объектов, фильтрация и пресеты |
| `M → W` | WAVE Workspace | создание, проверка, обновление и обслуживание междетальных ссылок |
| `V → G` | Geometry Display Workspace | hide/show/isolate, отображение по типу, слою и назначению, стили визуализации |

Поведение:

```text
CapsLock → путь Workspace
→ HUD остаётся открытым
→ следующая одиночная клавиша выполняет действие внутри пространства
→ результат и изменённое состояние сразу отображаются в HUD
→ Esc возвращает на уровень выше
→ повторный Esc или CapsLock закрывает Workspace
```

Workspace не считается активной командой NX, пока не запущен конкретный adapter. Поэтому он не должен блокировать вращение, панорамирование, предварительный выбор и обычную работу мышью.

`Enter` выполняет рекомендуемое действие Workspace:

- Layer Workspace — открыть стандартный Layer Settings;
- WAVE Workspace — создать ссылку с автоматическим определением типа выбранной геометрии;
- Geometry Display Workspace — применить выбранный display preset или подтвердить текущую операцию.

---

# 4. Приоритет обработки клавиатуры

Порядок обязателен:

```text
1. защищённый текстовый или числовой ввод
2. системные сочетания Ctrl/Alt/Win
3. Leader HUD
4. активный workflow NX
5. Selection Intent 1–4
6. Direct Keys
7. стандартное поведение NX
```

Это исключает случайный запуск Extrude при вводе буквы `X` в имени объекта и переключение Selection Intent при вводе числового размера.

---

# 5. Горячий слой Direct Keys

## 5.1. Базовый набор

| Клавиша | Контекст | Действие | Adapter / BUTTON ID | Статус |
|---|---|---|---|---|
| `S` | Modeling, нет активного Sketch | Создать эскиз | `UG_CREATE_SKETCH` | `declared_v8` |
| `S` | Активный Sketch | Закончить эскиз | `UG_SKETCH_FINISH` | `declared_v8` |
| `X` | Modeling | Extrude | `UG_MODELING_EXTRUDED_FEATURE` | `declared_v8` |
| `B` | Modeling | Edge Blend | `UG_MODELING_BLEND_FEATURE` | `declared_v8` |
| `C` | Modeling | Chamfer | `UG_MODELING_CHAMFER_FEATURE` | `declared_v8` |
| `Q` | Sketch | Rapid Dimension | `UG_SKETCH_RAPID_DIMENSION` | `declared_v8` |
| `Q` | Drafting | Rapid Dimension | `UG_DRAFTING_RAPID_DIMENSION` | `declared_v8` |
| `Space` | Есть выделение, графическое окно | Hide Selected | `UG_EDIT_BLANK_SELECTED` | `declared_v8` |
| `Shift+Space` | Графическое окно | Show All | `UG_EDIT_MD_SHOWHIDE_ALL` | `declared_v8` |
| `Esc` | Зависит от состояния | Закрыть HUD / отменить команду / снять выбор | state machine | см. ниже |

Direct Keys — физические алиасы. Их буква не обязана совпадать с корнем Leader. Поэтому `X` может означать eXtrude в прямом слое, хотя корень `X` внутри Leader означает Remove.

## 5.2. Необязательный расширенный набор

Отключён по умолчанию:

| Клавиша | Действие | Причина отключения по умолчанию |
|---|---|---|
| `R` | Revolve | Может мешать вводу имён и навигации |
| `H` | Hole | Может мешать стандартным пользовательским настройкам |
| `F` | Fit | Уже существует `Ctrl+F` |
| `M` | Measure | Частота зависит от роли пользователя |
| `D` | Sketch Rapid Dimension | Только в контексте Sketch; высокая частота |
| `T` | Sketch Trim | Только в контексте Sketch |
| `F` | Sketch Fillet | Только в контексте Sketch; конфликтует с Fit (отключён по умолчанию) |
| `E` | Sketch Extend | Только в контексте Sketch |
| `O` | Sketch Offset Curve | Только в контексте Sketch |

Расширенный набор включается персональным профилем после анализа telemetry.

## 5.3. Поведение `Space`

`Space` не должен скрывать объекты при пустом выборе.

Алгоритм:

```text
если открыт Leader:
    Space = поиск
иначе если редактируется текст:
    Space = обычный пробел
иначе если выполняется навигация/drag:
    Space передаётся NX
иначе если selection.count > 0:
    Hide Selected
иначе:
    no-op или стандартное действие NX
```

## 5.4. Поведение `Esc`

Приоритет:

```text
1. Leader открыт              → закрыть Leader
2. открыт список/меню NX      → передать Esc в NX
3. активен распознанный dialog→ Cancel/Back через проверенный workflow adapter
4. активна команда без adapter→ передать Esc в NX
5. команды нет, есть выбор    → UG_SEL_DESELECT_ALL
6. команды нет, выбора нет    → no-op
```

`Esc` не должен одновременно отменять feature и очищать выбор после отмены.

---

# 6. Стандартные системные сочетания

| Сочетание | Команда | BUTTON ID |
|---|---|---|
| `Ctrl+N` | New | `UG_FILE_NEW` |
| `Ctrl+O` | Open | `UG_FILE_OPEN` |
| `Ctrl+S` | Save | `UG_FILE_SAVE_PART` |
| `Ctrl+Shift+S` | Save As | `UG_FILE_SAVE_AS` |
| `Ctrl+Z` | Undo | `UG_EDIT_UNDO` |
| `Ctrl+Y` | Redo | `UG_EDIT_REDO` |
| `Ctrl+X` | Cut | `UG_EDIT_CUT` |
| `Ctrl+C` | Copy | `UG_EDIT_COPY` |
| `Ctrl+V` | Paste | `UG_EDIT_PASTE` |
| `Delete` | Delete Selected | `UG_EDIT_DELETE` |
| `Ctrl+F` | Fit | `UG_VIEW_FIT` |
| `F5` | Refresh | `UG_VIEW_REFRESH` |

Системные сочетания не дублируются в каждом модуле runtime-профиля.

---

# 7. Управление Leader HUD

| Клавиша | Действие |
|---|---|
| `CapsLock` | Открыть Leader текущего контекста |
| повторный `CapsLock` | Закрыть Leader |
| двойной `CapsLock` | Sticky Leader |
| `Space` | Глобальный поиск команд |
| буквы и цифры | Ввод пути |
| `Enter` | Выполнить однозначный путь |
| `Backspace` | Удалить токен; на корне закрыть HUD |
| `Esc` | Закрыть HUD |
| `Tab` | Следующая категория или следующий collector |
| `Shift+Tab` | Предыдущая категория или collector |
| `↑/↓` | Навигация по результатам поиска |
| `?` | Показать подсказку текущего узла |
| `F1` | Открыть диагностику команды |

CapsLock не должен изменять состояние регистра операционной системы.

---

# 8. Корни Leader

| Корень | Значение |
|---|---|
| `C` | Create |
| `E` | Edit |
| `T` | Transform |
| `X` | Remove / Suppress |
| `P` | Process / Generate / Update |
| `I` | Inspect / Analyze |
| `V` | View |
| `S` | Select |
| `A` | Annotate |
| `M` | Manage |
| `F` | File / Export |
| `G` | Go to application |
| `U` | Utilities |
| `H` | Help |

В Sketch допускаются однотокенные пути после CapsLock, потому что активный Sketch уже является однозначным внутренним префиксом.

---

# 9. Контекстный слой Selection Intent

## 9.1. Общая логика

Цифры не выбирают тип объекта. Они меняют **правило распространения** выбора внутри текущего collector.

Тип объекта задаётся контрактом операции или фильтром `CapsLock → S`.

## 9.2. Curve / Section Collector

Применяется к Extrude, Revolve, Sweep, Through Curves, Sketch Offset, Trim, Project Curve и подобным операциям.

| Клавиша | Selection Intent |
|---|---|
| `1` | Single Curve |
| `2` | Connected Curves |
| `3` | Tangent Curves |
| `4` | Region Boundary / Closed Region |

## 9.3. Edge Collector

Применяется к Edge Blend, Chamfer, Drafting edge selection и операциям рёбер.

| Клавиша | Selection Intent |
|---|---|
| `1` | Single Edge |
| `2` | Tangent Edges |
| `3` | Face Edges |
| `4` | Feature Edges |

## 9.4. Face Collector

Применяется к Move Face, Offset Face, Delete Face, Draft, Shell, Surface и CAE.

| Клавиша | Selection Intent |
|---|---|
| `1` | Single Face |
| `2` | Tangent Faces |
| `3` | Connected Faces / Face Region |
| `4` | Feature Faces |

## 9.5. Body Collector

| Клавиша | Selection Intent |
|---|---|
| `1` | Single Body |
| `2` | Touching / Connected Bodies |
| `3` | Bodies of Current Component |
| `4` | All Compatible Bodies in Scope |

## 9.6. Component Collector

| Клавиша | Selection Intent |
|---|---|
| `1` | Single Component |
| `2` | Same Part / Similar Component |
| `3` | All Occurrences |
| `4` | Assembly Subtree |

## 9.7. Drafting / PMI Collector

| Клавиша | Selection Intent |
|---|---|
| `1` | Single drafting object |
| `2` | Connected geometry |
| `3` | Objects in current view |
| `4` | All compatible annotations of type |

## 9.8. CAM / Simulation Collector

Смысл назначается конкретным operation contract:

| Клавиша | Типовое значение |
|---|---|
| `1` | Single object |
| `2` | Same geometry/group |
| `3` | Connected region |
| `4` | All compatible objects in active setup/solution |

HUD обязан показывать расшифровку текущих `1–4`. Пользователь не должен помнить динамическое значение вслепую.

## 9.9. Сброс intent

| Клавиша | Действие |
|---|---|
| `0` | Вернуть рекомендуемый intent команды |
| `Backspace` | Отменить последнее изменение intent, если Leader закрыт и collector активен |

---

# 10. Универсальный слой выбора через Leader

## 10.1. Тип объекта

| Путь | Фильтр |
|---|---|
| `S → B` | Bodies |
| `S → F` | Faces |
| `S → E` | Edges |
| `S → C` | Curves |
| `S → K` | Sketches |
| `S → T` | Features |
| `S → M` | Components |
| `S → D` | Datums |
| `S → P` | Points / Vertices |
| `S → A` | Annotations / PMI |
| `S → V` | Drafting Views |
| `S → O` | CAM Operations |
| `S → L` | Tools |
| `S → N` | FEM Nodes |
| `S → X` | FEM Elements |

Проверенные базовые ID:

| Тип | BUTTON ID |
|---|---|
| Body | `UG_SEL_BODY_PRIORITY` |
| Face | `UG_SEL_FACE_PRIORITY` |
| Edge | `UG_SEL_EDGE_PRIORITY` |
| Curve | `UG_SEL_CURVE_PRIORITY` |
| Feature | `UG_SEL_FEATURE_PRIORITY` |
| Component | `UG_SEL_COMPONENT_PRIORITY` |
| Datum | `UG_SEL_DATUM_PRIORITY` |

Остальные типы требуют локального inventory.

## 10.2. Smart Selection

| Путь | Действие | Adapter |
|---|---|---|
| `S → Q → F` | Select Similar Faces | `tbd_adapter` |
| `S → Q → E` | Select Similar Edges | `tbd_adapter` |
| `S → Q → C` | Select Similar Components | `tbd_adapter` |
| `S → Q → I` | Identical Only | command-local option |
| `S → Q → P` | Predicted Objects | command-local option |
| `S → Q → L` | Select from List | `tbd_adapter` |
| `S → Q → N` | Select by Name | `tbd_adapter` |
| `S → Q → A` | Select by Attribute | `tbd_adapter` |
| `S → Q → D` | Select by Display Attributes: color / line style / width | `tbd_adapter` |
| `S → Q → Y` | Select by Layer | `tbd_adapter` |

## 10.3. Scope

| Путь | Scope |
|---|---|
| `S → P → W` | Work Part |
| `S → P → D` | Display Part |
| `S → P → C` | Current Component |
| `S → P → R` | Work Part and Components |
| `S → P → A` | Entire Assembly |
| `S → P → V` | Visible Objects |
| `S → P → L` | Selected Layers |
| `S → P → G` | Active Drawing View |
| `S → P → S` | Active CAM Setup |
| `S → P → U` | Current Dialog Collector |

## 10.4. Изменение набора

| Путь | Режим |
|---|---|
| `S → M → R` | Replace |
| `S → M → A` | Add |
| `S → M → X` | Remove |
| `S → M → T` | Toggle |
| `S → M → I` | Invert |
| `S → M → N` | Deselect All |
| `S → M → P` | Previous Selection |
| `S → M → V` | Save Selection Set |

`S → M → N` вызывает `UG_SEL_DESELECT_ALL`.

## 10.5. Временные фильтры

Фильтр, установленный внутри активной команды:

- действует только на текущий collector;
- не изменяет пользовательский глобальный фильтр;
- автоматически снимается при переходе к следующему collector;
- полностью восстанавливается после OK или Cancel.

## 10.6. Выбор по слоям и видимости

Выбор по слою и управление отображением используют один общий resolver, но не смешивают состояния:

- **selection filter** определяет, что можно выбрать;
- **layer state** определяет, что видно и доступно для выбора;
- **object blanking** определяет индивидуальную видимость объекта;
- **scope** ограничивает Work Part, Display Part, компонент, сборку, Drawing View или CAM Setup.

`S → Q → Y` открывает компактный ввод слоя. Допустимы:

```text
1
10,20,30
20-29
@model
@datum
@sketch
@wave
@pmi
@cam
```

Имена с `@` являются логическими псевдонимами профиля. NXKeys не должен жёстко кодировать номера корпоративных слоёв.

Если выбранный слой скрыт, NXKeys не делает его видимым молча. HUD предлагает:

```text
Enter       — временно сделать слой Visible + Selectable
Ctrl+Enter  — изменить состояние слоя постоянно
V           — перейти в Geometry Display Workspace
Esc         — оставить состояние без изменения
```

---

# 11. Работа с предварительным выбором

## 11.1. Совместимый выбор

```text
выбрана плоская грань
→ X
→ грань назначается Extrude.Section
→ Extrude запускается с заполненным collector
```

## 11.2. Частично совместимый выбор

```text
выбраны два ребра и одна грань
→ B
→ два ребра передаются Edge Blend
→ грань остаётся вне collector
→ HUD: «Использовано 2 из 3 объектов»
```

## 11.3. Неоднозначный выбор

```text
выбрано линейное ребро
→ CapsLock → C → R
→ ребро может быть Section или Axis
→ HUD предлагает роль
```

## 11.4. Несовместимый выбор

NXKeys не очищает его автоматически.

Варианты HUD:

```text
Enter       — запустить команду с пустым collector
S           — изменить фильтр
Shift+Enter — сохранить текущий selection вне операции
Esc         — отменить запуск
```

---

# 12. Workflow Controls

| Клавиша | Действие |
|---|---|
| `Enter` | Default action: OK / Finish, только через проверенный adapter |
| `Ctrl+Enter` | Apply |
| `Esc` | Cancel / Close |
| `Tab` | Следующий collector |
| `Shift+Tab` | Предыдущий collector |
| `Alt+Left` | Предыдущий шаг мастера |
| `Alt+Right` | Следующий шаг мастера |

Если NXKeys не может надёжно определить кнопку или collector, он не эмулирует действие вслепую и передаёт клавишу NX.

---

# 13. Modeling — финальный каталог

## 13.1. Direct Keys

| Клавиша | Команда |
|---|---|
| `S` | Create Sketch |
| `X` | Extrude |
| `B` | Edge Blend |
| `C` | Chamfer |

## 13.2. Создание базовых features

| Путь | Команда | Выбор | Adapter | Статус |
|---|---|---|---|---|
| `C → R` | Revolve | section + axis | `UG_MODELING_REVOLVED_FEATURE` | `declared_v8` |
| `C → H` | Hole | placement face + point(s) | `UG_MODELING_HOLE_FEATURE` | `declared_v8` |
| `C → S` | Sweep | section + guide/spine | `UG_MODELING_SWEPT_FEATURE` | `declared_v8` |
| `C → L` | Through Curves | sections | `UG_MODELING_THROUGH_CURVES_FEATURE` | `inherited_v7` |
| `C → M` | Through Curve Mesh | primary + cross curves | `tbd_adapter` | `suspect_mapping` |
| `C → U` | Studio Surface | curves/edges | `UG_MODELING_STUDIO_SURFACE_FEATURE` | `inherited_v7` |
| `C → G` | Extract Geometry | source geometry | `UG_MODELING_EXTRACT_GEOMETRY` | `declared_v8` |

## 13.3. Boolean и тела

| Путь | Команда | Adapter | Статус |
|---|---|---|---|
| `C → B → U` | Unite | `tbd_adapter` | `tbd_adapter` |
| `C → B → S` | Subtract | `tbd_adapter` | `tbd_adapter` |
| `C → B → I` | Intersect | `tbd_adapter` | `tbd_adapter` |
| `E → H` | Shell | `tbd_adapter` | `tbd_adapter` |
| `E → D` | Draft | `tbd_adapter` | `tbd_adapter` |
| `E → T` | Trim Body | `tbd_adapter` | `suspect_mapping` |
| `T → O` | Move Object | `tbd_adapter` | `tbd_adapter` |
| `T → C` | Copy | `UG_EDIT_COPY` | `declared_v8` |
| `T → P` | Pattern Feature | `UG_MODELING_PATTERNFEATURE_FEATURE` | `declared_v8` |
| `T → M` | Mirror Feature | `UG_MODELING_MIRRORFEATURE_FEATURE` | `declared_v8` |

## 13.4. Синхронное и прямое редактирование

| Путь | Команда | Основной выбор | Adapter |
|---|---|---|---|
| `E → F → M` | Move Face | faces | `tbd_adapter` |
| `E → F → O` | Offset Face | faces | `tbd_adapter` |
| `E → F → D` | Delete Face | faces | `tbd_adapter` |
| `E → F → R` | Replace Face | target + replacement face | `tbd_adapter` |
| `E → F → S` | Resize Face / region, если доступно | faces | `tbd_adapter` |
| `E → P` | Edit Feature Parameters | selected feature | `tbd_adapter` |
| `X → S` | Suppress Feature | feature(s) | `tbd_adapter` |
| `X → U` | Unsuppress Feature | feature(s) | `tbd_adapter` |

## 13.5. Datum и служебная геометрия

| Путь | Команда | Adapter |
|---|---|---|
| `C → D → P` | Datum Plane | `tbd_adapter` |
| `C → D → A` | Datum Axis | `tbd_adapter` |
| `C → D → C` | Datum CSYS | `tbd_adapter` |
| `C → D → P → T` | Datum Point | `tbd_adapter` |
| `C → C → L` | 3D Line | `tbd_adapter` |
| `C → C → A` | 3D Arc | `tbd_adapter` |
| `C → C → S` | 3D Spline | `tbd_adapter` |

## 13.6. Анализ и управление

| Путь | Команда | Adapter |
|---|---|---|
| `I → M` | Measure Distance / Geometry | `UG_INFO_GEOMETRIC_MEASUREMENT` |
| `I → O` | Object Information | `UG_INFO_OBJECT` |
| `I → P` | Mass Properties | `tbd_adapter` |
| `I → G` | Examine Geometry | `tbd_adapter` |
| `I → C` | Check Geometry | `tbd_adapter` |
| `M → E` | Expressions | `UG_EXPRESSIONS` |
| `M → L` | Open Layer Workspace | internal workspace |
| `M → L → S` | Standard Layer Settings | `UG_LAYER_SETTINGS` |
| `M → L → M` | Move Selected to Layer | `UG_LAYER_MOVE` |
| `M → M` | Material Library | `UG_MATERIAL_LIBRARY_MANAGER` |
| `M → N` | Part Navigator | `tbd_adapter` |
| `V → G` | Open Geometry Display Workspace | internal workspace |
| `P → U` | Update Model | `tbd_adapter` |

## 13.7. Layer Workspace

Layer Workspace является универсальным и доступен в Modeling, Assembly, Drafting, PMI, CAM и Simulation, если соответствующий объект поддерживает слои.

### 13.7.1. Клавиши пространства

После `M → L`:

| Клавиша | Действие | Adapter / реализация |
|---|---|---|
| `Enter` или `S` | Открыть стандартный Layer Settings | `UG_LAYER_SETTINGS` |
| `W` | Сделать слой рабочим | `tbd_adapter` |
| `M` | Переместить выбранные объекты на слой | `UG_LAYER_MOVE` |
| `V` | Сделать слои Visible + Selectable | `tbd_adapter` |
| `O` | Сделать слои Visible Only | `tbd_adapter` |
| `H` | Скрыть слои | `tbd_adapter` |
| `I` | Изолировать слои выбранных объектов | internal snapshot + `tbd_adapter` |
| `A` | Показать все слои в текущем scope | `tbd_adapter` |
| `R` | Восстановить предыдущее состояние слоёв | internal layer-state stack |
| `F` | Временный selection filter по слоям | selection engine |
| `P` | Выбрать или сохранить layer preset | profile adapter |
| `N` | Найти слой по номеру, имени или псевдониму | internal resolver |
| `D` | Показать слои выбранных объектов и причины недоступности | diagnostics |
| `Tab` | Перейти Work Part → Display Part → Component → Assembly scope | scope state machine |
| `Esc` | Закрыть Layer Workspace без дополнительного изменения | internal workspace |

### 13.7.2. Логические группы слоёв

Профиль может определять назначение слоёв без привязки команд к конкретным номерам:

```yaml
layer_aliases:
  model: [1]
  sketch: [20-29]
  datum: [30-39]
  wave: [40-49]
  imported: [50-59]
  pmi: [60-69]
  cam: [70-79]
  simulation: [80-89]
  temporary: [240-249]
```

Это пример схемы, а не обязательная нумерация. Runtime использует реальные значения текущего профиля.

### 13.7.3. Layer presets

Минимальный набор настраиваемых пресетов:

| Preset | Назначение |
|---|---|
| `Design` | итоговые тела доступны; служебная геометрия скрыта или Visible Only |
| `Sketch` | активный Sketch, опорные кривые и датумы видимы; тела могут быть приглушены |
| `Reference` | WAVE и междетальные ссылки видимы; исходные компоненты могут быть See-Through |
| `Review` | модель, PMI и контрольная геометрия видимы без временных объектов |
| `CAM` | Part, Blank, Check, MCS и технологическая геометрия разделены по отображению |
| `Clean` | только итоговая геометрия текущего scope |

`P` показывает пресеты, `Enter` применяет, `Shift+Enter` сохраняет текущее состояние как пользовательский preset.

### 13.7.4. Инварианты безопасности

- Work Layer всегда остаётся Visible + Selectable.
- Перед скрытием Work Layer NXKeys требует сначала назначить другой рабочий слой.
- Объекты нельзя переносить между слоями разных owning parts одной командой.
- Компонент сборки не считается объектом слоя своей геометрии; изменение слоя выполняется только в owning part.
- Mixed selection сначала группируется по owning part, затем показывается preview.
- `I`, `P` и пакетные изменения создают снимок состояния до изменения.
- `R` восстанавливает последний снимок, но не отменяет созданную или удалённую геометрию.
- В Drafting учитываются глобальные состояния и состояния Visible in View; область применения всегда видна в HUD.
- В CAM layer/layout state операции не должен перезаписываться без явного подтверждения.

### 13.7.5. Быстрый сценарий

```text
выбрать служебную геометрию
→ M+L
→ N → @datum
→ H
→ выбрать тело
→ I  изолировать его слой
→ выполнить редактирование
→ R  восстановить отображение
```

## 13.8. Geometry Display Workspace

`V → G` открывает единое пространство управления тем, **что пользователь видит**, не смешивая Hide, слои, reference sets, suppression и display style.

### 13.8.1. Основные действия

После `V → G`:

| Клавиша | Действие | Adapter / реализация |
|---|---|---|
| `H` | Hide Selected | `UG_EDIT_BLANK_SELECTED` |
| `S` | Show Selected через список скрытых объектов | `tbd_adapter` |
| `O` | Show Only / Isolate Selected | `tbd_adapter` + visibility snapshot |
| `A` | Show All в текущем scope | `UG_EDIT_MD_SHOWHIDE_ALL` |
| `R` | Restore Previous Display | internal display-state stack |
| `T` | Show / Hide by Object Type | `tbd_adapter` |
| `L` | Перейти в Layer Workspace | internal workspace transition |
| `C` | Construction Geometry preset | profile display preset |
| `B` | Bodies preset | profile display preset |
| `W` | WAVE / Reference Geometry preset | profile display preset |
| `P` | PMI / Annotations preset | profile display preset |
| `M` | Components preset | profile display preset |
| `D` | Rendering Style subspace | command-local display adapter |
| `X` | See-Through / Translucency для выбранного | `tbd_adapter` |
| `F` | Fit Selected | `tbd_adapter` |
| `I` | Diagnose Why Hidden | visibility resolver |
| `Tab` | Сменить scope | scope state machine |

### 13.8.2. Rendering Style subspace

После `V → G → D`:

| Клавиша | Стиль |
|---|---|
| `S` | Shaded |
| `E` | Shaded with Edges |
| `W` | Static Wireframe |
| `H` | Hidden Edges / Wireframe with Dim Edges, если доступно |
| `P` | Partially Shaded |
| `X` | See-Through Selected |
| `R` | Restore previous rendering style and object translucency |

Все adapter ID стилей требуют локального inventory. До проверки они имеют статус `tbd_adapter`.

### 13.8.3. Cause-aware Show

Команда Show обязана определить, почему объект не виден:

| Причина | Допустимое действие |
|---|---|
| Object is blanked | Unblank object |
| Layer is Hidden | Предложить Visible + Selectable или Visible Only |
| Component is hidden | Показать компонент |
| Component is suppressed | Не показывать автоматически; предложить Unsuppress с подтверждением |
| Component is unloaded | Предложить Load с проверкой Load Options |
| Reference Set excludes geometry | Предложить смену Reference Set, не менять молча |
| Drawing View layer override | Изменить только Active Drawing View либо явно выбрать Global |
| CAM/Simulation display state | Изменить только активный setup/solution, если не выбран другой scope |

`V → G → I` выводит цепочку причин, например:

```text
Selected face is not visible
1. owning body: blanked
2. layer 42: Visible + Selectable
3. component: loaded and shown
4. reference set: MODEL

Enter — unblank body
```

### 13.8.4. Display-state stack

Перед `O`, применением preset, массовым Hide/Show, изменением прозрачности или отображения по типу NXKeys сохраняет снимок:

- object blank state;
- layer visibility/selectability;
- component show/hide state;
- active scope;
- rendering style;
- временная translucency;
- view-dependent layer overrides, если применимо.

Стек хранит не менее 10 состояний на вкладку NX. `R` восстанавливает одно состояние за нажатие. Снимок не должен автоматически unsuppress, load или менять Reference Set, если эти действия не входили в сохранённое состояние.

### 13.8.5. Контекстные категории

| Контекст | `T` и presets управляют |
|---|---|
| Modeling | bodies, sheets, curves, sketches, datums, points, PMI, WAVE geometry |
| Assembly | components, component geometry, constraints, WAVE sources/results |
| Drafting | views, dimensions, annotations, tables, centerlines, view-dependent layers |
| CAM | part, blank, check, fixtures, tools, toolpaths, IPW |
| Simulation | mesh, nodes/elements, loads, constraints, connections, result objects |

### 13.8.6. Быстрые сценарии

```text
выбрать тело
→ V+G
→ O  изолировать и автоматически Fit Selected
→ выполнить работу
→ R  вернуть прежнее отображение
```

```text
V+G
→ C  показать служебную геометрию по preset
→ L  перейти к слоям
→ N → @wave
→ V  сделать WAVE-слои видимыми и выбираемыми
```

---

# 14. Sketch — финальный каталог

Все однотокенные команды вызываются после `CapsLock`.

## 14.1. Геометрия

| Путь | Команда | Adapter | Статус |
|---|---|---|---|
| `L` | Line | `UG_SKETCH_LINE` | `declared_v8` |
| `R` | Rectangle | `UG_SKETCH_RECTANGLE` | `declared_v8` |
| `C` | Circle | `UG_SKETCH_CIRCLE` | `declared_v8` |
| `A` | Arc | `UG_SKETCH_ARC` | `declared_v8` |
| `S` | Spline | `tbd_adapter` | `tbd_adapter` |
| `P` | Point | `tbd_adapter` | `tbd_adapter` |
| `W` | Slot | `tbd_adapter` | `tbd_adapter` |
| `G` | Polygon | `tbd_adapter` | `tbd_adapter` |
| `I` | Ellipse | `tbd_adapter` | `tbd_adapter` |

## 14.2. Редактирование кривых

| Путь | Команда | Adapter |
|---|---|---|
| `T` | Trim | `UG_SKETCH_TRIM` |
| `E` | Extend | `UG_SKETCH_EXTEND` |
| `O` | Offset Curve | `UG_SKETCH_OFFSET_CURVE` |
| `F` | Sketch Fillet | `tbd_adapter` |
| `H` | Sketch Chamfer | `tbd_adapter` |
| `M` | Mirror Curve | `tbd_adapter` |
| `V` | Move Curve | `tbd_adapter` |
| `Y` | Pattern Curve | `UG_SKETCH_PATTERN_CURVES` |
| `K` | Make Corner | `UG_SKETCH_MAKE_CORNER` |
| `J → P` | Project Curve | `tbd_adapter` |
| `J → I` | Intersection Curve | `UG_SKETCH_ADD_QUILT_PLANE_AI_CURVES` |
| `J → D` | Derived Lines | `UG_SKETCH_CONSTRUCTION_LINES` |
| `J → K` | Make Corner | `UG_SKETCH_MAKE_CORNER` |

## 14.3. Размеры

`Q` остаётся прямой Rapid Dimension.

| Путь | Команда | Adapter |
|---|---|---|
| `D → Q` | Rapid Dimension | `UG_SKETCH_RAPID_DIMENSION` |
| `D → L` | Linear Dimension | `UG_SKETCH_LINEAR_DIMENSION` |
| `D → A` | Angular Dimension | `tbd_adapter` |
| `D → R` | Radius Dimension | `UG_SKETCH_RADIAL_DIMENSION` |
| `D → O` | Diameter Dimension | `UG_SKETCH_DIAMETER_DIM` |
| `D → P` | Perimeter Dimension | `UG_SKETCH_PERIMETER_DIM` |
| `D → M` | Animate Dimension | `UG_SKETCH_ANIMATE_DIMENSION` |

## 14.4. Ограничения

| Путь | Ограничение | Adapter |
|---|---|---|
| `K → C` | Coincident | `UG_SKETCH_COINCIDENT_CONSTRAINT` |
| `K → H` | Horizontal | `UG_SKETCH_HORIZONTAL_CONSTRAINT` |
| `K → V` | Vertical | `UG_SKETCH_VERTICAL_CONSTRAINT` |
| `K → T` | Tangent | `UG_SKETCH_TANGENT_CONSTRAINT` |
| `K → P` | Parallel | `UG_SKETCH_PARALLEL_CONSTRAINT` |
| `K → N` | Perpendicular | `UG_SKETCH_PERPENDICULAR_CONSTRAINT` |
| `K → O` | Concentric | `tbd_adapter` |
| `K → E` | Equal | `tbd_adapter` |
| `K → L` | Collinear | `tbd_adapter` |
| `K → M` | Midpoint | `tbd_adapter` |
| `K → S` | Symmetric | `tbd_adapter` |
| `K → F` | Fixed | `UG_SKETCH_FIXED_CONSTRAINT` |
| `K → A` | Auto Constrain | `UG_SKETCH_AUTO_CREATE_CONSTRAINTS` |
| `K → X` | Remove Constraint | `tbd_adapter` |

## 14.5. Диагностика Sketch

| Путь | Команда | Adapter |
|---|---|---|
| `N` | Sketch Navigator | `tbd_adapter` |
| `Z` | Sketch Checker | `UG_SKETCH_CHECKER` |
| `U → D` | Show Degrees of Freedom | `tbd_adapter` |
| `U → R` | Show Relations | `tbd_adapter` |
| `U → E` | External References | `tbd_adapter` |
| `U → I` | Issues | `UG_SKETCH_CHECKING` |
| `U → A` | Alternate Solution | `UG_SKETCH_ALTERNATE_SOLUTION` |
| `S` direct | Finish Sketch | `UG_SKETCH_FINISH` |

---

# 15. Assembly — финальный каталог

## 15.1. Основные операции

| Путь | Команда | Adapter |
|---|---|---|
| `C → A` | Add Component | `UG_ASSEMBLIES_ADD_COMPONENT` |
| `C → N` | New Component | `UG_ASSEMBLIES_NEW_COMPONENT` |
| `C → J` | Assembly Constraints | `UG_ASSEMBLIES_CONSTRAINTS` |
| `T → M` | Move Component | `UG_ASSEMBLIES_MOVE_COMPONENT` |
| `T → A` | Auto Align / Magnetic Snap | `tbd_adapter` |
| `T → P` | Pattern Component | `UG_ASSEMBLIES_PATTERN_COMPONENT` |
| `T → C` | Copy Component | `tbd_adapter` |
| `E → R` | Replace Component | `UG_ASSEMBLIES_REPLACE_COMPONENT` |
| `X → C` | Remove Component | `UG_ASSEMBLIES_REMOVE_COMPONENT` |
| `X → S` | Suppress Component | `tbd_adapter` |
| `E → U` | Unsuppress Component | `tbd_adapter` |

## 15.2. Структура и контекст

| Путь | Команда | Adapter |
|---|---|---|
| `M → N` | Assembly Navigator | `UG_ASSEMBLIES_NAVIGATOR` |
| `M → W` | Open WAVE Workspace | internal workspace |
| `M → W → G` | WAVE Geometry Linker | `UG_ASSY_WAVE_LINKER` |
| `M → W → P` | Make Work Part | `tbd_adapter` |
| `M → A` | Arrangements | `tbd_adapter` |
| `M → R` | Reference Sets | `tbd_adapter` |
| `M → O → L` | Load Options | `tbd_adapter` |
| `P → U` | Update Assembly | `tbd_adapter` |

## 15.3. Проверка и представление

| Путь | Команда | Adapter |
|---|---|---|
| `I → I` | Interference Analysis | `tbd_adapter` |
| `I → C` | Clearance Analysis | `tbd_adapter` |
| `I → M` | Measure | `UG_INFO_GEOMETRIC_MEASUREMENT` |
| `I → J` | Show Constraints | `tbd_adapter` |
| `V → E` | Exploded View | `tbd_adapter` |
| `V → A` | Auto Explode | `tbd_adapter` |
| `V → R` | Restore Assembly View | `tbd_adapter` |

## 15.4. WAVE Workspace

WAVE Workspace предназначен для interpart-моделирования и подготовки ассоциативной геометрии без постоянных переходов между командами и компонентами.

### 15.4.1. Клавиши пространства

После `M → W`:

| Клавиша | Действие | Adapter / статус |
|---|---|---|
| `Enter` или `G` | Geometry Linker с автоматическим определением типа | `UG_ASSY_WAVE_LINKER`, `declared_v8` |
| `B` | Body Link | command-local option, требует проверки |
| `F` | Face / Face Region Link | command-local option, требует проверки |
| `C` | Curve Link | command-local option, требует проверки |
| `S` | Sketch Link | command-local option, требует проверки |
| `D` | Datum Link | `tbd_adapter` |
| `V` | Point / Vertex Link | `tbd_adapter` |
| `A` | Associative mode | command-local option |
| `N` | Non-associative mode | command-local option |
| `P` | Make selected component Work Part | `tbd_adapter` |
| `U` | Update selected links / all links in scope | `tbd_adapter` |
| `E` | Edit Link Parameters | `tbd_adapter` |
| `R` | Replace Source Geometry | `tbd_adapter` |
| `L` | Locate and highlight Source | `tbd_adapter` |
| `I` | Dependency and timestamp diagnostics | internal graph + `tbd_adapter` |
| `H` | Toggle Hide Source after successful link | post-action option |
| `Y` | Assign result to logical layer `@wave` | Layer Workspace integration |
| `K` | Break Link | destructive `tbd_adapter`, preview required |
| `Tab` | Source collector → target part → options | collector state machine |
| `Esc` | Вернуться на уровень выше; активный NX dialog получает отдельный Cancel | workspace state machine |

### 15.4.2. Автоматический сценарий предварительного выбора

```text
выбрана геометрия компонента, который не является Work Part
→ M+W
→ NXKeys определяет source component и тип геометрии
→ target = текущий Work Part
→ mode = Associative по умолчанию
→ Enter
→ создаётся WAVE link
→ результат помещается на @wave, если псевдоним настроен
→ HUD показывает source, target, associativity и update status
```

Если выбрана геометрия текущего Work Part, источник неоднозначен. NXKeys не меняет Work Part автоматически и предлагает:

```text
P           — выбрать Target Work Part
S           — выбрать Source Component
Enter       — открыть Geometry Linker без заполнения source
Esc         — отменить
```

### 15.4.3. Контракт WAVE-ссылки

Обязательные роли:

| Роль | Требование |
|---|---|
| `source_geometry` | один или несколько совместимых объектов одного source part для конкретного типа link |
| `source_component` | occurrence или owning part выбранной геометрии |
| `target_part` | загруженный и доступный для записи Work Part |
| `link_type` | body, face/region, curve, sketch, point/vertex или datum |
| `associativity` | associative или non-associative, значение всегда видно в HUD |
| `source_visibility_after` | keep, hide или see-through |
| `result_layer` | логический alias либо конкретный слой target part |

### 15.4.4. Защита от ошибок

- NXKeys не создаёт WAVE-ссылку в source part на его собственную геометрию без явного подтверждения.
- Source и target всегда отображаются полными именами компонентов и частей до выполнения.
- При mixed selection из нескольких source parts объекты группируются; пакетная операция требует preview.
- При наличии эквивалентной ссылки HUD предлагает Reuse, Update или Edit вместо создания дубликата.
- Associative является рекомендуемым режимом; Non-associative всегда помечается предупреждением о потере обновляемости.
- Break Link, Replace Source и пакетное обновление считаются потенциально разрушительными.
- Автоматический переход Work Part разрешён только как отдельное подтверждённое действие.
- После Cancel восстанавливаются Work Part, selection filter, display state и временный layer preset.
- Если source component unloaded, NXKeys предлагает загрузку, но не меняет Load Options молча.
- Если source исключён Reference Set, NXKeys показывает причину и предлагает осознанную смену набора.

### 15.4.5. Визуальная диагностика зависимостей

`I` включает временное представление:

```text
SOURCE COMPONENT
→ source geometry
→ WAVE feature in target
→ dependent features
```

Цветовые и графические атрибуты задаются темой HUD; сама геометрия файла не перекрашивается. После выхода представление полностью удаляется.

### 15.4.6. Типовые сценарии

```text
выбрать тело исходного компонента
→ M+W
→ B
→ A
→ Y  поместить результат на @wave
→ H  скрывать source после создания
→ Enter
```

```text
выбрать существующий WAVE feature
→ M+W
→ I  проверить source и dependents
→ L  перейти к source
→ U  обновить
→ V+G+R  восстановить прежнее отображение при необходимости
```

---

# 16. Drafting — финальный каталог

## 16.1. Листы и виды

| Путь | Команда | Adapter |
|---|---|---|
| `C → S` | New Sheet | `tbd_adapter` |
| `V → B` | Base View | `UG_DRAFTING_BASE_VIEW` |
| `V → P` | Projected View | `UG_DRAFTING_PROJECTED_VIEW` |
| `V → C` | Section View | `UG_DRAFTING_SECTION_VIEW` |
| `V → D` | Detail View | `UG_DRAFTING_DETAIL_VIEW` |
| `V → O` | Breakout Section | `tbd_adapter` |
| `V → R` | Break View | `tbd_adapter` |
| `V → Y` | View Style | `UG_DRAFTING_VIEW_STYLE` |
| `V → U` | Update Views | `UG_DRAFTING_UPDATE_VIEWS` |
| `M → S` | Sheet Navigator / Sheet Settings | `tbd_adapter` |
| `M → V` | View Management | `tbd_adapter` |

Путь `V → S`, предложенный для Detail View, сохраняется как временный alias, но canonical path — `V → D`.

## 16.2. Размеры

`Q` — Direct Rapid Dimension.

| Путь | Команда | Adapter |
|---|---|---|
| `A → D → Q` | Rapid Dimension | `UG_DRAFTING_RAPID_DIMENSION` |
| `A → D → L` | Linear Dimension | `tbd_adapter` |
| `A → D → A` | Angular Dimension | `tbd_adapter` |
| `A → D → R` | Radial Dimension | `tbd_adapter` |
| `A → D → O` | Diameter Dimension | `tbd_adapter` |
| `A → D → H` | Hole Callout | `tbd_adapter` |
| `A → D → C` | Chamfer Dimension | `tbd_adapter` |
| `A → D → G` | Arc Length Dimension | `tbd_adapter` |

## 16.3. Аннотации и GD&T

| Путь | Команда | Adapter |
|---|---|---|
| `A → N` | Note | `UG_PMI_NOTE` |
| `A → G → D` | Datum Feature Symbol | `UG_PMI_DATUM_FEATURE_SYMBOL` |
| `A → G → F` | Feature Control Frame | `UG_PMI_FEATURE_CONTROL_FRAME` |
| `A → F` | Surface Finish | `UG_PMI_SURFACE_FINISH` |
| `A → W` | Weld Symbol | `tbd_adapter` |
| `A → C → M` | Center Mark | `tbd_adapter` |
| `A → C → L` | Centerline | `tbd_adapter` |
| `A → C → B` | Bolt Circle Centerline | `tbd_adapter` |

Старый путь `A → D` для Datum удаляется из canonical policy, потому что `A → D` зарезервирован под семейство Dimensions. Временный alias допускается только с предупреждением.

## 16.4. Спецификации и таблицы

| Путь | Команда | Adapter |
|---|---|---|
| `A → P` | Parts List | `UG_DRAFTING_PARTS_LIST` |
| `A → L` | Parts List, legacy alias | `UG_DRAFTING_PARTS_LIST` |
| `A → B` | Balloon | `tbd_adapter` |
| `A → T` | Table | `tbd_adapter` |
| `A → R` | Revision Symbol / Revision Table | `tbd_adapter` |
| `P → U` | Update All Drawing Objects | `tbd_adapter` |
| `F → P` | Export PDF | `tbd_adapter` |
| `F → D` | Export DXF/DWG | `tbd_adapter` |

---

# 17. Surface / Freeform — финальный каталог

Этот контекст является профилем внутри Modeling, а не обязательно отдельным NX application.

| Путь | Команда | Adapter |
|---|---|---|
| `C → T` | Through Curves | `UG_MODELING_THROUGH_CURVES_FEATURE` |
| `C → M` | Through Curve Mesh | `tbd_adapter` |
| `C → S` | Studio Surface | `UG_MODELING_STUDIO_SURFACE_FEATURE` |
| `C → W` | Swept Surface | `UG_MODELING_SWEPT_FEATURE` |
| `C → B` | Bridge Surface | `tbd_adapter` |
| `C → F` | Fill Surface | `tbd_adapter` |
| `C → N` | N-Sided Surface | `tbd_adapter` |
| `C → O` | Offset Surface | `tbd_adapter` |
| `E → T` | Trim Sheet | `UG_MODELING_TRIM_SHEET_FEATURE` |
| `E → X` | Extend Sheet | `UG_MODELING_FF_EXTEND_SHEET` |
| `E → S` | Sew | `UG_MODELING_SEW_FEATURE` |
| `E → U` | Untrim | `tbd_adapter` |
| `E → M` | Match Edge | `tbd_adapter` |
| `E → R` | Replace Face | `tbd_adapter` |
| `T → H` | Thicken | `tbd_adapter` |
| `I → C` | Continuity Analysis | `tbd_adapter` |
| `I → Z` | Zebra / Reflection Analysis | `tbd_adapter` |
| `I → G` | Examine Geometry | `tbd_adapter` |

---

# 18. Sheet Metal — финальный каталог

## 18.1. Базовые и формообразующие операции

| Путь | Команда | Adapter |
|---|---|---|
| `C → T` | Tab | `UG_SHEET_METAL_BASE_TAB` |
| `C → F` | Flange | `UG_SHEET_METAL_FLANGE` |
| `C → C` | Contour Flange | `UG_SHEET_METAL_CONTOUR_FLANGE` |
| `C → S` | Convert Solid to Sheet Metal | `UG_SBSM_SHEETMETAL_FROM_SOLID_FEATURE` |
| `C → N` | Normal Cutout | `tbd_adapter` |
| `C → J` | Jog | `tbd_adapter` |
| `C → H` | Hem | `tbd_adapter` |
| `C → B` | Bead | `tbd_adapter` |
| `C → D` | Dimple | `tbd_adapter` |
| `C → L` | Louver | `tbd_adapter` |

## 18.2. Гибы и производство

| Путь | Команда | Adapter |
|---|---|---|
| `E → B` | Bend | `UG_SHEET_METAL_BEND` |
| `E → C` | Corner Relief | `tbd_adapter` |
| `T → U` | Unbend | `UG_SHEET_METAL_UNBEND` |
| `T → R` | Rebend | `UG_SHEET_METAL_REBEND` |
| `P → P` | Flat Pattern | `UG_SHEET_METAL_FLAT_PATTERN` |
| `P → S` | Edit Bend Sequence | `tbd_adapter` |
| `P → V` | Validate Sheet Metal | `tbd_adapter` |
| `F → D` | Export Flat Pattern DXF | `tbd_adapter` |

---

# 19. PMI / Model-Based Definition — финальный каталог

| Путь | Команда | Adapter |
|---|---|---|
| `A → D → Q` | Rapid PMI Dimension | `tbd_adapter` |
| `A → D → L` | Linear PMI Dimension | `tbd_adapter` |
| `A → D → A` | Angular PMI Dimension | `tbd_adapter` |
| `A → D → R` | Radial PMI Dimension | `tbd_adapter` |
| `A → D → O` | Diameter PMI Dimension | `tbd_adapter` |
| `A → H` | Hole Callout | `tbd_adapter` |
| `A → N` | PMI Note | `UG_PMI_NOTE` |
| `A → G → D` | Datum Feature Symbol | `UG_PMI_DATUM_FEATURE_SYMBOL` |
| `A → G → F` | Feature Control Frame | `UG_PMI_FEATURE_CONTROL_FRAME` |
| `A → F` | Surface Finish | `UG_PMI_SURFACE_FINISH` |
| `A → W` | Weld Symbol | `tbd_adapter` |
| `M → V` | Model Views | `tbd_adapter` |
| `M → P` | Annotation Plane / Orientation | `tbd_adapter` |
| `P → V` | Validate PMI | `tbd_adapter` |
| `P → C` | Convert Drawing Objects to PMI | `tbd_adapter` |
| `P → T` | Publish Technical Data Package | `tbd_adapter` |
| `M → R` | MBD Rules / Reuse Rules | `tbd_adapter` |

---

# 20. CAM — финальный каталог

## 20.1. Setup и ресурсы

| Путь | Команда | Adapter |
|---|---|---|
| `C → S` | Create CAM Setup | `tbd_adapter` |
| `C → M` | Create / Edit MCS | `tbd_adapter` |
| `C → G` | Create Geometry Group | `tbd_adapter` |
| `C → G → P` | Define Part Geometry | `tbd_adapter` |
| `C → G → B` | Define Blank | `tbd_adapter` |
| `C → G → C` | Define Check / Fixture Geometry | `tbd_adapter` |
| `C → T` | Create Tool | `UG_CAM_CREATE_TOOL` |
| `C → O` | Create Operation | `UG_CAM_CREATE_OPERATION` |
| `M → N` | Operation Navigator | `UG_CAM_OPERATION_NAVIGATOR` |

## 20.2. Операции и траектории

| Путь | Команда | Adapter |
|---|---|---|
| `E → P` | Edit Operation Parameters | `tbd_adapter` |
| `E → D` | Duplicate Operation | `tbd_adapter` |
| `T → O` | Reorder Operation | `tbd_adapter` |
| `X → O` | Delete Operation | `UG_CAM_DELETE_OPERATION` |
| `X → S` | Suppress Operation | `tbd_adapter` |
| `E → U` | Unsuppress Operation | `tbd_adapter` |
| `P → G` | Generate Selected Toolpath | `UG_CAM_GENERATE_TOOL_PATH` |
| `P → A` | Generate All | `tbd_adapter` |
| `P → B` | Background Generate | `tbd_adapter` |
| `P → V` | Verify Toolpath | `UG_CAM_VERIFY_TOOL_PATH` |
| `P → S` | Machine Simulation | `tbd_adapter` |
| `P → C` | Collision / Gouge Check | `tbd_adapter` |
| `P → P` | Postprocess | `tbd_adapter` |
| `P → D` | Shop Documentation | `tbd_adapter` |

## 20.3. Selection contracts CAM

- Create Operation:
  - program group;
  - method;
  - tool;
  - geometry;
  - operation subtype.

- Part Geometry:
  - bodies/faces/features;
  - scope: active CAM setup.

- Blank:
  - body, bounding block/cylinder or stock definition.

- Check Geometry:
  - bodies/faces/components.

- Make Machining Suggestion:
  - exactly one compatible face;
  - Face filter;
  - scope: active part geometry.

---

# 21. Simulation / Simcenter 3D — финальный каталог

| Путь | Команда | Adapter |
|---|---|---|
| `C → A` | New Analysis / Solution | `tbd_adapter` |
| `C → F` | New FEM / Simulation File | `tbd_adapter` |
| `M → M` | Assign Material / Physical Property | `tbd_adapter` |
| `C → C` | Connection | `tbd_adapter` |
| `C → M` | Mesh | `tbd_adapter` |
| `C → M → C` | Mesh Control | `tbd_adapter` |
| `C → L` | Load | `tbd_adapter` |
| `C → B` | Boundary Condition / Constraint | `tbd_adapter` |
| `M → N` | Simulation Navigator | `tbd_adapter` |
| `P → S` | Solve | `tbd_adapter` |
| `P → R` | Open Results | `tbd_adapter` |
| `I → P` | Probe Result | `tbd_adapter` |
| `I → C` | Contour / Result Display | `tbd_adapter` |
| `P → D` | Publish Analysis Report | `tbd_adapter` |

Selection types must include:

- body;
- face;
- edge;
- point;
- node;
- element;
- mesh collector;
- load;
- constraint;
- connection;
- solution;
- result object.

---

# 22. Routing — финальный каталог

| Путь | Команда | Adapter |
|---|---|---|
| `C → R` | Create Route | `UG_ROUTE_CREATE_ROUTE` |
| `C → P` | Place Routing Part | `UG_ROUTE_PLACE_PART` |
| `C → F` | Place Fitting | `tbd_adapter` |
| `C → S` | Create Stock / Segment | `tbd_adapter` |
| `E → R` | Edit Route | `tbd_adapter` |
| `E → S` | Edit Segment / Stock | `tbd_adapter` |
| `T → P` | Move Routing Part | `tbd_adapter` |
| `M → N` | Routing Navigator | `tbd_adapter` |
| `P → V` | Validate Route | `tbd_adapter` |
| `A → B` | Routing BOM / Parts List | `tbd_adapter` |
| `F → E` | Export Routing Data | `tbd_adapter` |

---

# 23. Дополнительные переходы между приложениями

| Путь | Приложение | BUTTON ID | Статус |
|---|---|---|---|
| `G → M` | Modeling | `UG_APP_MODELING` | `declared_v8` |
| `G → A` | Assemblies | `UG_APP_ASSEMBLIES` | `declared_v8` |
| `G → D` | Drafting | `UG_APP_DRAFTING` | `declared_v8` |
| `G → H` | Sheet Metal | `UG_APP_SHEETMETAL` | `declared_v8` |
| `G → C` | Manufacturing / CAM | `UG_APP_MANUFACTURING` | `declared_v8` |
| `G → N` | Simulation | `UG_APP_SFEM` | `declared_v8` |
| `G → P` | PMI | `UG_APP_PMI` | `declared_v8` |
| `G → R` | Routing | `UG_APP_ROUTING` | `declared_v8` |
| `G → O` | Mold Wizard | `UG_APP_MOLDWIZARD` | `inherited_v7` |
| `G → L` | Reuse Library | `UG_NAVIGATOR_REUSE_LIBRARY` | `inherited_v7` |
| `G → V` | Gateway / View and Analysis | `UG_APP_GATEWAY` | `inherited_v7` |

`G → U` не считается реальным переключением приложения: Surface Modeling работает внутри Modeling. Путь может менять профиль HUD, но не должен выдавать `UG_APP_MODELING` за отдельное приложение.

---

# 24. Наиболее частые end-to-end сценарии

## 24.1. Деталь из эскиза

```text
S
→ выбрать плоскость или плоскую грань
→ CapsLock + L/R/C/A
→ K... ограничения
→ Q размеры
→ S
→ X Extrude
→ H Hole или CapsLock+C+H
→ B Edge Blend
→ C Chamfer
→ Ctrl+S
```

## 24.2. Быстрое редактирование импортированной детали

```text
выбрать грань
→ CapsLock + E + F + M/O/D
→ 1–4 выбрать распространение
→ CapsLock + S + Q + F/E для Similar
→ I + G Examine Geometry
→ Ctrl+S
```

## 24.3. Сборка

```text
CapsLock + G + A
→ C+A Add Component
→ T+M Move
→ C+J Constraint
→ T+A Auto Align
→ I+I Interference
→ Ctrl+S
```

## 24.4. Чертёж

```text
CapsLock + G + D
→ C+S New Sheet
→ V+B Base View
→ V+P Projected View
→ V+C Section
→ Q Rapid Dimension
→ A+C+M Center Mark
→ A+G+F Feature Control Frame
→ A+P Parts List
→ A+B Balloons
→ V+U Update
→ F+P Export PDF
```

## 24.5. Листовой металл

```text
G+H
→ C+T Tab или C+S Convert
→ C+F Flange
→ C+N Normal Cutout
→ T+U Unbend
→ feature operations
→ T+R Rebend
→ P+P Flat Pattern
→ F+D Export DXF
```

## 24.6. CAM

```text
G+C
→ C+S Setup
→ C+M MCS
→ C+G Part/Blank/Check
→ C+T Tool
→ C+O Operation
→ P+G Generate
→ P+V Verify
→ P+S Machine Simulation
→ P+P Postprocess
```

## 24.7. Simulation

```text
G+N
→ C+F FEM
→ M+M Material
→ C+C Connections
→ C+M Mesh
→ C+B Constraints
→ C+L Loads
→ P+S Solve
→ P+R Results
→ I+P Probe
→ P+D Report
```

## 24.8. Работа со слоями без открытия большого диалога

```text
выбрать объекты
→ M+L  открыть Layer Workspace
→ D    проверить owning part и текущие слои
→ M    Move Selected to Layer
→ ввести @wave или номер слоя
→ Enter
→ P    применить preset Design
→ Esc
```

## 24.9. Создание WAVE-ссылки в контексте сборки

```text
сделать целевой компонент Work Part
→ выбрать тело исходного компонента
→ M+W
→ B Body Link
→ A Associative
→ Y Result Layer = @wave
→ H Hide Source after creation
→ Enter
→ I проверить цепочку зависимости
```

## 24.10. Изоляция и восстановление геометрии

```text
выбрать тело, грань или компонент
→ V+G
→ O Show Only
→ F Fit Selected
→ выполнить моделирование или анализ
→ R Restore Previous Display
```

## 24.11. Поиск причины невидимой геометрии

```text
V+G
→ I Diagnose Why Hidden
→ выбрать объект из списка или Navigator
→ HUD показывает: blank / layer / component / reference set / view override
→ Enter применяет только минимально необходимое действие
```

---

# 25. HUD

## 25.1. Корневой экран

```text
NXKeys — MODELING
Work Part: bracket.prt
Selection: 2 edges

Direct:
S Sketch   X Extrude   B Blend   C Chamfer

Leader:
C Create   E Edit   T Transform
S Select   I Inspect V View
M Manage   P Process G Go
```

## 25.2. Selection Intent

```text
EDGE COLLECTOR — Edge Blend
1 Single Edge
2 Tangent Edges
3 Face Edges
4 Feature Edges
0 Recommended

Selected: 6 edges
Scope: Work Part
Mode: Add
```

## 25.3. Layer Workspace

```text
LAYER WORKSPACE — WORK PART
Input: @wave
Resolved layers: 40–49

W Work Layer   M Move Selected
V Visible+Selectable   O Visible Only   H Hidden
I Isolate      A Show All      R Restore
F Filter       P Presets       D Diagnose

Selection: 3 objects
Owners: bracket.prt
Current layers: 1, 23
Snapshot: available
```

## 25.4. WAVE Workspace

```text
WAVE WORKSPACE — BODY LINK
Source: frame.prt / FRAME(1)
Target: bracket.prt / BRACKET(1) [Work Part]
Mode: Associative
Result layer: @wave → 40
After creation: Hide Source

Enter Create   P Change Target   N Non-associative
I Dependencies   U Update   K Break Link
```

## 25.5. Geometry Display Workspace

```text
GEOMETRY DISPLAY — ASSEMBLY
Scope: Work Part and Components
Selection: 1 body

H Hide   S Show   O Show Only   A Show All
R Restore   T By Type   L Layers   D Style
C Construction   W WAVE   P PMI   I Diagnose

Display stack: 2 states
Current preset: Reference
```

## 25.6. Недоступная команда

```text
Machine Simulation unavailable

Reason:
- Manufacturing application is not active, or
- no CAM setup is active, or
- adapter is not verified for NX 2512.6000.

G → C   Open Manufacturing
F1      Diagnostics
```

---

# 26. Operation Contract

Каждая команда обязана иметь контракт:

```yaml
operation_id: modeling.extrude
paths:
  direct: X
  leader: [C, E]
adapter:
  kind: button_id
  value: UG_MODELING_EXTRUDED_FEATURE
  status: declared_v8
availability:
  applications: [modeling]
  requires_work_part: true
  blocked_in_text_input: true
inputs:
  - id: section
    role: section
    required: true
    cardinality: one_or_more
    accepts:
      - sketch
      - planar_face_region
      - closed_curve_chain
      - open_curve_chain
    intents:
      1: single_curve
      2: connected_curves
      3: tangent_curves
      4: region_boundary
  - id: direction
    role: direction
    required: false
    cardinality: zero_or_one
    accepts:
      - datum_axis
      - linear_edge
      - line
      - vector
  - id: end_limit
    role: limit
    required: false
    cardinality: zero_or_one
    accepts:
      - face
      - datum_plane
      - body
preselection:
  use_compatible: true
  preserve_incompatible: true
  ask_on_ambiguity: true
post:
  restore_selection_filter: true
  restore_focus: graphics_window
```

## 26.1. Layer operation contract

```yaml
operation_id: manage.layers.change_state
paths:
  workspace: [M, L]
input:
  layer_expression: "@wave"
  scope: work_part
  target_state: visible_selectable
preconditions:
  resolve_aliases: true
  prevent_hiding_work_layer: true
  group_selection_by_owning_part: true
snapshot:
  capture_layer_states: true
  stack: layer_state
post:
  preserve_object_selection: true
  show_changed_layers_in_hud: true
  restore_temporary_filters: true
```

## 26.2. WAVE operation contract

```yaml
operation_id: assembly.wave.create_link
paths:
  workspace: [M, W]
adapter:
  kind: button_id
  value: UG_ASSY_WAVE_LINKER
  status: declared_v8
availability:
  applications: [modeling, assemblies, manufacturing]
  requires_assembly_context: true
  requires_writable_target_part: true
inputs:
  - id: source_geometry
    required: true
    cardinality: one_or_more
    accepts: [body, face, face_region, curve, sketch, point, vertex, datum]
  - id: source_component
    required: true
    cardinality: one
  - id: target_part
    required: true
    cardinality: one
options:
  associativity: associative
  result_layer: "@wave"
  source_visibility_after: keep
preselection:
  infer_source_from_owning_part: true
  infer_target_from_work_part: true
  ask_on_same_part: true
  split_mixed_source_parts: true
safety:
  detect_duplicate_links: true
  preview_multi_source_batch: true
  require_confirmation_for_break_or_replace: true
post:
  restore_work_part_on_cancel: true
  restore_selection_filter: true
  restore_display_state: true
```

## 26.3. Geometry display contract

```yaml
operation_id: view.geometry.isolate
paths:
  workspace: [V, G]
input:
  objects: current_selection
  scope: work_part_and_components
snapshot:
  stack: display_state
  capture:
    - object_blank_state
    - layer_state
    - component_visibility
    - rendering_style
    - translucency
    - view_layer_overrides
behavior:
  fit_selected: true
  do_not_unsuppress: true
  do_not_load_components: true
  do_not_change_reference_set: true
post:
  preserve_selection: true
  expose_restore_action: true
```

---

# 27. Телеметрия и персональная оптимизация

Без записи геометрии и содержимого файлов разрешается собирать:

- operation ID;
- application;
- путь вызова;
- direct/leader/search;
- количество нажатий;
- отмена после запуска;
- время до OK/Cancel;
- использованный Selection Intent;
- ошибка adapter;
- конфликт клавиши;
- число повторных поисков;
- применение layer preset;
- частоту Move to Layer и смены Work Layer;
- число восстановлений layer/display state;
- тип созданной WAVE-ссылки и associative/non-associative mode;
- обнаружение дубликата WAVE link;
- причину невидимости геометрии без записи имён объектов;
- использование Hide / Show Only / Show All / Restore.

Через 2–4 недели профиль может пересчитать Direct Keys по фактической частоте пользователя.

Автоматическое изменение раскладки без явного подтверждения запрещено.

---

# 28. Компилятор профиля

Компилятор v8 блокирует профиль при:

1. конфликте Direct Key в одном состоянии;
2. перехвате букв в текстовом поле;
3. перехвате цифр в числовом поле;
4. дублировании canonical path;
5. использовании `suspect_mapping`;
6. отсутствии operation contract;
7. отсутствии cardinality;
8. несовместимом типе selection;
9. неизвестном приложении;
10. непроверенном destructive adapter;
11. использовании глобальной команды как локальной;
12. несоответствии класса `BUTTON ID` классу намерения;
13. попытке скрыть Work Layer без назначения нового;
14. жёстко заданном корпоративном номере слоя вместо logical alias в универсальной команде;
15. изменении слоёв объектов разных owning parts без batch preview;
16. WAVE-контракте без явных source, target и associativity;
17. destructive WAVE operation без preview и подтверждения;
18. Show/Isolate operation без display snapshot;
19. автоматическом Unsuppress, Load Component или изменении Reference Set из команды Show;
20. конфликте view-dependent layer state с Global scope без явного выбора области.

Пример:

```text
E3102 suspect adapter mapping

operation: modeling.trim_body
adapter: UG_SKETCH_TRIM
adapter class: SKETCH_EDIT
expected class: MODELING_BODY_EDIT

runtime action: disabled
```

---

# 29. Тестирование

Для каждой команды обязательны:

| Тест | Ожидание |
|---|---|
| Empty selection | Открывается первый collector |
| Valid single selection | Объект назначается правильной роли |
| Valid multi-selection | Соблюдается cardinality |
| Mixed selection | Используются только совместимые объекты |
| Invalid selection | Выбор не очищается молча |
| Ambiguous object | HUD предлагает роль |
| Wrong application | Команда не запускается |
| Text input focus | Direct Key не перехватывается |
| Numeric input focus | `1–4` вводят число, а не intent |
| Unknown modal dialog | Команда блокируется |
| Context changed | Dispatch повторно валидируется |
| Cancel | Фильтры восстанавливаются |
| OK / Apply | Контекст обновляется |
| Missing license | Показывается понятная причина |
| Destructive batch | Preview и подтверждение |
| Hide work layer | Операция блокируется и предлагается новый Work Layer |
| Mixed owning parts in Move to Layer | Объекты группируются, показывается batch preview |
| Layer alias missing | Понятная ошибка, номера не подставляются автоматически |
| Layer isolate and restore | Состояния Visible/Selectable/Hidden полностью восстанавливаются |
| WAVE valid preselection | Source и target определяются по owning part и Work Part |
| WAVE same-part ambiguity | Выполнение блокируется до выбора ролей |
| WAVE duplicate | Предлагается Reuse/Update/Edit, новый feature не создаётся молча |
| WAVE Cancel | Work Part, filters, layers и display state восстановлены |
| Break WAVE link | Обязательны dependents preview и подтверждение |
| Show hidden object | Определяется реальная причина невидимости |
| Suppressed or unloaded object | Show не выполняет Unsuppress/Load автоматически |
| Isolate selected | Создаётся display snapshot и выполняется Fit Selected |
| Restore display | Восстанавливаются blanking, layers, components, style и translucency |
| Drafting Visible in View | Изменяется выбранный view scope, а не Global state |

---

# 30. План внедрения

## Этап 1. Безопасность ввода

- focus guards;
- Direct Key state machine;
- `Esc`;
- `Space`;
- блокировка autorepeat;
- контекстная проверка перед dispatch.

## Этап 2. Базовый Modeling и Sketch

- S/X/B/C/Q;
- Revolve;
- Hole;
- Sweep;
- Sketch geometry;
- constraints;
- dimensions;
- Selection Intent `1–4`.

## Этап 3. Универсальный Selection Engine и отображение

- type;
- intent;
- scope;
- mode;
- temporary filters;
- Similar Faces/Edges/Components;
- Select from List;
- Layer Workspace;
- logical layer aliases;
- layer/display state stacks;
- Geometry Display Workspace;
- cause-aware Show;
- Hide / Show Only / Restore.

## Этап 4. Assembly и Drafting

- add/move/constraint;
- WAVE Workspace;
- source/target inference;
- associative/non-associative modes;
- duplicate-link detection;
- dependency diagnostics;
- interference;
- views;
- view-dependent layers;
- dimensions;
- GD&T;
- parts list;
- export.

## Этап 5. Surface и Sheet Metal

- surfaces;
- direct face editing;
- flat pattern;
- DXF.

## Этап 6. CAM, PMI, Simulation и Routing

Команды включаются только после adapter inventory и smoke tests.

---

# 31. Критерии готовности

v8 считается готовой, если:

- Direct Keys не мешают текстовому вводу;
- `1–4` не мешают числовому вводу;
- все enabled adapters проверены на целевой NX;
- preselection используется предсказуемо;
- selection intent виден в HUD;
- temporary filter восстанавливается;
- Layer Workspace поддерживает Work Layer, состояния Visible/Selectable/Hidden, presets и Restore;
- логические псевдонимы слоёв не зависят от фиксированной корпоративной нумерации;
- Work Layer невозможно скрыть случайно;
- Geometry Display Workspace различает object blanking, layer state, component state и Reference Set;
- Show Only всегда создаёт восстанавливаемый display snapshot;
- WAVE Workspace явно показывает source, target, link type и associativity;
- duplicate WAVE links не создаются молча;
- Break Link и Replace Source требуют preview зависимостей;
- несовместимые объекты не удаляются молча;
- `Esc` выполняет ровно одно действие;
- Surface не выдаётся за отдельное NX application;
- подозрительные ID отключены;
- базовые сценарии Modeling, Sketch, Assembly, Drafting, Sheet Metal и CAM проходят end-to-end тесты;
- команды без ID присутствуют в каталоге как намерения, но не вызываются.

---

# 32. Итоговая формула v8

```text
Direct Keys для частых действий
+
1–4 для Selection Intent
+
CapsLock Leader для полного каталога
+
Layer Workspace
+
WAVE Workspace
+
Geometry Display Workspace
+
восстанавливаемые Layer/Display State
+
Operation Contracts
+
Context Engine
+
Verified Adapters
=
быстрая, предсказуемая и безопасная система управления NX
```
