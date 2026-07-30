# NXKeys Mnemonic Command Language

## Назначение

NXKeys использует язык намерений:

```text
CapsLock → действие → объект → команда → вариант
```

Пользователь запоминает смысл операции, а не положение кнопки на ribbon. Canonical path содержит 2–5 токенов. Внутренний module prefix добавляется runtime и пользователем не вводится.

## Контракты

- profile schema: 6;
- source sequence policy: 7;
- 14 context modules;
- source catalog: 1169 intents;
- main runtime scope: 885 K3–K5 intents;
- paths и aliases prefix-free внутри module.

Checked-in generated audit может отражать предыдущую policy до regeneration; source `scripts/sequence-policy.mjs` имеет приоритет.

## Корневой алфавит действий

| Token | Смысл |
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
| `M` | Manage — слои, материалы, навигаторы, библиотеки |
| `F` | File — файловые операции |
| `G` | Go — переход между приложениями NX |
| `U` | Utilities — выражения, журналы, настройки |
| `H` | Help — справка, поиск, диагностика |

Смысл корня должен оставаться стабильным между модулями. `S*` и `G*` зарезервированы source policy для universal support commands.

## Объектные токены

| Token | Объект/область |
|---|---|
| `A` | Annotation / Additive |
| `B` | Body / Base |
| `C` | Component |
| `D` | Dimension / Datum |
| `E` | Edge |
| `F` | Feature / Frame |
| `G` | Geometry / Curve |
| `H` | Sheet Metal |
| `K` | Constraint |
| `L` | Layout / Layer |
| `M` | Material / Mold |
| `N` | Simulation |
| `O` | CAM Operation |
| `P` | Part / Data |
| `R` | Routing |
| `S` | Sketch / Selection |
| `T` | Tool / Template |
| `U` | Surface |
| `V` | View |
| `W` | WAVE |
| `Y` | Assembly |
| `Z` | Deterministic fallback / other |

Object token выбирается по command ID/name, module и semantic group. Он не является отдельным публичным API.

## Контекст модуля

Пример:

```text
Пользователь: CapsLock → C → F → E
Внутри DFA:  M → C → F → E
Команда:      Modeling → Create → Feature → Extrude
```

Одинаковый user path может существовать в разных modules, поскольку internal prefixes различаются.

## Правила пути

1. Path читается как намерение.
2. Canonical path уникален внутри module.
3. Ни один path/alias не является префиксом другого terminal path.
4. Alias не затеняет canonical path или submenu.
5. Explicit source path рассматривается раньше known definition и generated fallback в текущем runtime generator.
6. Частотная цель задаёт максимальную длину, но не отменяет safety/conflict rules.
7. Support paths резервируются до обычных commands.
8. Command без exact ID не становится enabled.

Частотные цели policy v7:

| Frequency | Максимальная длина |
|---|---:|
| K5 | 2 |
| K4 | 3 |
| K3 | 4 |
| K2/K1 | 5 |
| support | 2 |

## Curated mappings, подтверждённые runtime generator

Ниже перечислены примеры из `MnemonicPathGenerator.BuildKnown()`. Generated main profile может выбрать другой fallback path для конфликтующей или другой command row, но известные IDs используют эти definitions, если explicit source path не имеет приоритета.

### Modeling

```text
C S K  Create Sketch               alias C S
C F E  Extrude                     alias C E
C F H  Hole                        alias C H
C F R  Revolve                     alias C R
E E B  Edge Blend                  alias E B
E E C  Edge Chamfer                alias E C
T F P  Pattern Feature             alias T P
T F M  Mirror Feature              alias T M
```

### Sketch geometry

```text
C G L    Line
C G R    Rectangle
C G C    Circle
C G A    Arc
E G T    Trim
E G E    Extend
T G O    Offset Curve
C G L 2  Line by Two Points
C G L M  Line from Midpoint
C G R 2  Rectangle by Two Points
C G R C  Rectangle from Center
C G R 3  Rectangle by Three Points
C G C C  Circle from Center
C G C 3  Circle by Three Points
C G A C  Arc from Center
C G A 3  Arc by Three Points
```

### Sketch dimensions and constraints

```text
A D R  Rapid Dimension
A D L  Linear Dimension
C K C  Coincident
C K T  Tangent
C K P  Parallel
C K N  Perpendicular
C K H  Horizontal
C K V  Vertical
I S C  Sketch Checker
```

**Важно:** более короткие Sketch mappings вида `CL`, `CR`, `DQ`, `KC` обсуждались как эргономическое направление, но текущий `MnemonicPathGenerator.cs` подтверждает mappings выше. Документация не описывает предложенную карту как внедрённую.

### Assembly

```text
C C A  Add Component               alias C A
C C N  New Component               alias C N
T C M  Move Component              alias T M
C K A  Assembly Constraints        alias C K
E C R  Replace Component           alias E R
X C R  Remove Component            alias X C
T C P  Pattern Component           alias T P
M N A  Assembly Navigator          alias M N
```

### Layers and materials

```text
M L S  Layer Settings
M L V  Layer View
M L A  Layer Category
M L C  Copy to Layer
M L M  Move to Layer
I L I  Information on Other Layers
M M A  Assign Material
M M L  Material Library Manager
```

Dedicated root `L*` для layers не подтверждён текущим generator; current curated paths используют `M L *`.

### Drafting and PMI

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
C O O  Create Operation            alias C O
C T T  Create Tool                 alias C T
P O G  Generate Tool Path
P O V  Verify Tool Path
P O P  Postprocess
```

## Universal selection paths

Source policy v7 закрепляет во всех 14 enabled modules:

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
S N  Deselect All
```

Они используют `action: set_selection_filter`, а Bridge применяет selection semantics через NXOpen.

## Module switches

```text
G M  Modeling
G A  Assembly
G D  Drafting
G P  PMI
G U  Surface
G H  Sheet Metal
G C  Manufacturing
G N  Simulation
G R  Routing
G O  Mold
G L  Reuse
G V  Inspect / View
```

Switch rows не добавляются в Sketch и Selection/Object module. Module не получает switch на самого себя.

Source policy экспортирует рекомендуемый cycle Modeling → Assembly → Drafting → Manufacturing. Не считайте его активным UI contract без подтверждения runtime implementation.

## Legacy aliases

Legacy grid `W/E/D/C/X/Z/A/Q` сохраняется для primary commands, если alias не конфликтует. Он является дополнительным motor layer и не ограничивает module восемью commands.

Legacy fields:

```text
slot, submenu_key, input_key
```

сохраняются для migration/редактора, но canonical routing использует `path` и `aliases`.

## Автогенерация

Compiler/runtime generator строит candidate из:

- action root;
- object token;
- command-name letters;
- deterministic fallback alphabet.

При конфликте текущая реализация перебирает alternative roots/object tokens/letters, соблюдая target length и prefix-free invariant. Формальная уникальность не гарантирует идеальную mnemonic ergonomics, поэтому high-frequency и critical commands должны иметь reviewed curated mapping.

## `path_locked` и `path_source`

Schema 6 содержит metadata:

- `path_locked`;
- `path_source`.

Они позволяют хранить происхождение и намерение закрепления пути. Отдельный fully implemented user override file/UI flow текущим кодом не подтверждён; изменения source mappings должны проходить code review и validators.

## Поиск

`CapsLock → Space` ищет по доступным runtime metadata, включая:

- command name и ID;
- русские/английские aliases;
- canonical path и labels;
- module;
- catalog section/group/frequency;
- локальную usage history, если она доступна runtime.

Search result не обходит enabled/resolution/context guards.

## Safety semantics

- `requires_selection` описывает ожидаемый workflow, но hard minimum задаётся policy/guard;
- destructive command требует confirmation;
- ambiguous/unresolved rows disabled;
- modal context может блокировать dispatch;
- module switch подтверждается новым context;
- selection action маршрутизируется отдельно от обычной command invocation.

## Изменение языка

При изменении curated path или policy:

1. обновите source policy/generator;
2. запустите full/main/tree validators;
3. пересоздайте sequence audit и generated profile;
4. проверьте все canonical paths и aliases;
5. обновите этот документ и command tree;
6. проверьте high-frequency workflows на Windows/NX.

```powershell
node .\scripts\validate-full-command-map.mjs
node .\scripts\validate-main-command-map.mjs
node .\scripts\validate-command-tree.mjs
node .\scripts\audit-command-sequences.mjs
```
