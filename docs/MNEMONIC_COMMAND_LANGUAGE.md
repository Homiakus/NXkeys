# NXKeys Mnemonic Command Language

## 1. Цель

NXKeys использует язык намерений:

```text
CapsLock → действие → объект → команда → вариант
```

Пользователь запоминает смысл операции, а не позицию кнопки на ленте NX.

Язык обслуживает:

- базовый curated-профиль;
- 14 контекстных модулей;
- полную карту из 1169 намерений в 32 разделах;
- безопасные короткие aliases для частых команд.

## 2. Корневой алфавит действий

| Клавиша | Смысл |
|---|---|
| `C` | Create — создать или добавить |
| `E` | Edit — изменить существующее |
| `T` | Transform — переместить, отразить, размножить |
| `X` | Remove — удалить, убрать, подавить |
| `P` | Process — рассчитать, сгенерировать, решить |
| `I` | Inspect — измерить, проверить, проанализировать |
| `V` | View — показать, скрыть, ориентировать |
| `S` | Select — выбор и фильтры |
| `A` | Annotate — размеры, PMI, символы, примечания |
| `M` | Manage — навигаторы, слои, материалы, библиотеки |
| `F` | File — файловые операции |
| `G` | Go — переход между приложениями NX |
| `U` | Utilities — выражения, журналы, настройки |
| `H` | Help — справка, поиск, диагностика |

Смысл первой буквы не меняется между модулями.

## 3. Основные объекты

| Клавиша | Объект |
|---|---|
| `A` | Annotation / Additive |
| `B` | Body / Base |
| `C` | Component |
| `D` | Dimension / Datum |
| `E` | Edge |
| `F` | Feature / Frame |
| `G` | Geometry / Curve |
| `H` | Sheet Metal |
| `I` | Inspection |
| `J` | Fixture |
| `K` | Constraint |
| `L` | Layout / Layer |
| `M` | Material / Mold |
| `N` | Simulation |
| `O` | CAM Operation |
| `P` | Part / Data |
| `Q` | Quality |
| `R` | Routing |
| `S` | Sketch / Selection |
| `T` | Tool / Template |
| `U` | Surface |
| `V` | View |
| `W` | WAVE |
| `Y` | Assembly / Ship |
| `Z` | Other |

## 4. Контекст модуля

Пользователь не вводит module prefix. Его добавляет движок:

```text
Пользователь: CapsLock → C → F → E
Внутри DFA:  M → C → F → E
```

`M` здесь означает Modeling. Такой же пользовательский путь может существовать в другом модуле и вести к другой контекстной команде.

## 5. Длина и структура пути

Канонический путь содержит 2–5 буквенно-цифровых токенов:

```text
действие → объект → команда → вариант → подвариант
```

Правила:

1. Путь должен читаться как намерение.
2. Внутри модуля путь уникален.
3. Ни один путь не является префиксом другого.
4. Alias не может затенять команду или submenu.
5. Известные ручные пути имеют приоритет над сгенерированными.
6. Частота `K5`/`K4` учитывается при проектировании коротких путей, но не отменяет правила безопасности.

## 6. Curated-примеры

### Modeling

```text
C S K  Create Sketch
C F E  Create Feature Extrude       alias C E
C F H  Create Feature Hole          alias C H
C F R  Create Feature Revolve       alias C R
E E B  Edit Edge Blend              alias E B
E E C  Edit Edge Chamfer            alias E C
T F P  Transform Feature Pattern    alias T P
T F M  Transform Feature Mirror     alias T M
```

### Sketch

```text
C G L    Line
C G R    Rectangle
C G C    Circle
C G A    Arc
E G T    Trim
E G E    Extend
T G O    Offset Curve
C G R 3  Rectangle by Three Points
```

### Assembly

```text
C C A  Add Component
C C N  Create New Component
T C M  Move Component
C K A  Assembly Constraints
E C R  Replace Component
X C R  Remove Component
T C P  Pattern Component
M N A  Assembly Navigator
```

### Drafting / PMI

```text
C V B  Base View
C V P  Projected View
C V S  Section View
C V D  Detail View
P V U  Update Views
A D R  Rapid Dimension
A G F  Feature Control Frame
A S F  Surface Finish
```

### Manufacturing

```text
C O O  Create Operation
C T T  Create Tool
P O G  Generate Tool Path
P O V  Verify Tool Path
P O P  Postprocess
X O D  Delete Operation
M N O  Operation Navigator
```

### Selection

```text
S B  Body
S F  Face
S E  Edge
S T  Feature
S C  Component
S U  Curve
S D  Datum
S R  Reset Filter
S A  Select All
S N  Select None
```

## 7. Полная карта 1169 команд

Исходные намерения находятся в `config/full-command-map/`. Для каждого сохранены:

- стабильный `intent_id`;
- исходный раздел и группа;
- `K1–K5`;
- английское и русское имя;
- runtime module;
- path hint.

`scripts/compile-full-command-map.mjs`:

1. загружает базовый профиль;
2. загружает 1169 намерений;
3. читает каталог `BUTTON ID` конкретной NX;
4. сохраняет известные ручные пути;
5. разрешает новые команды;
6. резервирует prefix-free путь внутри модуля;
7. удаляет конфликтующие aliases;
8. отключает ambiguous/unresolved-команды;
9. создаёт отчёт разрешения.

Полная карта не ограничивается восемью командами на модуль. Legacy-grid `QWE/A·D/ZXC` используется как слой быстрых primary aliases.

## 8. Частота использования

| Коэффициент | Интерпретация | Рекомендуемый доступ |
|---|---|---|
| `K5` | многократно в течение часа | короткий curated path или alias |
| `K4` | обычно ежедневно | короткий путь 2–3 токена |
| `K3` | несколько раз в неделю / на этапе | обычный путь 3–4 токена |
| `K2` | специализированная функция | путь 3–5 токенов или поиск |
| `K1` | редкая административная функция | поиск, полный путь, отдельный scope |

`Kч` — экспертная оценка, а не официальная телеметрия Siemens.

## 9. Автоматическая классификация

Типовые признаки действия:

```text
CREATE / NEW / ADD          → C
EDIT / REPLACE / TRIM       → E
MOVE / MIRROR / PATTERN     → T
DELETE / REMOVE             → X
GENERATE / SOLVE / POST     → P
MEASURE / INFO / VALIDATE   → I
VIEW / SHOW / HIDE          → V
SELECT                      → S
NAVIGATOR / LAYER / LIBRARY → M
```

Объект определяется по имени, `BUTTON ID`, модулю и группе. При коллизии меняется командный токен, затем объектный токен, затем используется детерминированный резервный суффикс.

## 10. Aliases

Alias — дополнительный путь к той же команде.

Он принимается только если:

- не равен каноническому пути;
- не совпадает с другой командой;
- не является префиксом другой команды;
- другой путь не является его префиксом.

Primary-команды сохраняют one-key legacy alias, когда это не создаёт конфликт.

## 11. Безопасность и выбор

`requires_selection` описывает expected workflow, но не всегда блокирует команду до preselection. Жёсткое требование задаётся policy.

Selection-фильтры используют:

```text
action: set_selection_filter
selection_type: edge | face | body | component | curve |
                datum | feature | operation | all | reset | none
```

Bridge применяет global NXOpen filter members, а не запускает `UG_SEL_*` как обычные menu buttons.

Разрушительные команды требуют `Enter`.

## 12. Поиск

`CapsLock → Space` ищет по:

- имени команды;
- `BUTTON ID`;
- русскому и английскому названию;
- aliases;
- каноническому пути и labels;
- модулю;
- разделу и группе полного каталога;
- частоте;
- локальной истории использования.

## 13. Совместимость

Legacy-поля `slot`, `submenu_key` и `input_key` остаются читаемыми. Runtime schema 5 назначает `path`, `path_labels`, `aliases`, `search_aliases`, `action` и `selection_type`.

Исходный JSON сохраняется как schema 4 для совместимости установщика. IPC использует отдельную schema 3.

## 14. Проверка

```powershell
node .\scripts\validate-command-tree.mjs
node .\scripts\validate-full-command-map.mjs
```

Проверяются известные пути, 1169 намерений, 32 раздела, `K1–K5`, уникальность, prefix-free свойства и запрет включённых команд без точного ID.
