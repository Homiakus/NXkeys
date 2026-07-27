# NXKeys Mnemonic Command Language

## 1. Цель

NXKeys использует клавиатурный язык:

```text
CapsLock → действие → объект → команда → вариант
```

Пользователь запоминает намерение, а не положение команды на ленте NX.

## 2. Корневой алфавит

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

Значение первой буквы не меняется между модулями.

## 3. Основные объекты

| Клавиша | Объект |
|---|---|
| `A` | Annotation |
| `B` | Body / Base |
| `C` | Component |
| `D` | Dimension / Datum |
| `E` | Edge |
| `F` | Feature |
| `G` | Geometry / Curve |
| `H` | Sheet Metal |
| `K` | Constraint |
| `L` | Layer |
| `M` | Material / Mold |
| `N` | Simulation |
| `O` | CAM Operation |
| `P` | Part |
| `R` | Routing |
| `S` | Sketch |
| `T` | Tool / Template |
| `U` | Surface |
| `V` | View |
| `W` | WAVE |
| `Y` | Assembly |
| `Z` | Other |

## 4. Modeling

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

## 5. Sketch

```text
C G L  Create Geometry Line
C G R  Create Geometry Rectangle
C G C  Create Geometry Circle
C G A  Create Geometry Arc
E G T  Edit Geometry Trim
E G E  Edit Geometry Extend
T G O  Transform Geometry Offset
```

Подтипы:

```text
C G L 2  Line by Two Points
C G L M  Line from Midpoint
C G R 2  Rectangle by Two Points
C G R C  Rectangle from Center
C G R 3  Rectangle by Three Points
C G C C  Circle from Center
C G C 3  Circle by Three Points
C G A 3  Arc by Three Points
C G A C  Arc from Center
```

Ограничения:

```text
C K C  Coincident
C K T  Tangent
C K P  Parallel
C K N  Perpendicular
C K H  Horizontal
C K V  Vertical
A D R  Rapid Dimension
A D L  Linear Dimension
I S C  Sketch Checker
```

## 6. Assembly

```text
C C A  Add Component
C C N  Create New Component
T C M  Move Component
C K A  Assembly Constraint
E C R  Replace Component
X C R  Remove Component
T C P  Pattern Component
M N A  Assembly Navigator
```

## 7. Drafting и PMI

```text
C V B  Base View
C V P  Projected View
C V S  Section View
C V D  Detail View
P V U  Update Views
E V S  View Style
A P L  Parts List
A D R  Rapid Dimension
```

PMI:

```text
A D R  Rapid Dimension
A G D  Datum Feature Symbol
A G F  Feature Control Frame
A S F  Surface Finish
A N P  PMI Note
E A P  Edit PMI
V M P  PMI Model View
I A V  Validate PMI
```

## 8. Surface и Sheet Metal

```text
C U T  Through Curves
C U S  Swept
C U D  Studio Surface
E U T  Trim Sheet
E U S  Sew
E U U  Untrim
C G E  Extract Geometry
I U C  Face Curvature
```

Sheet Metal:

```text
C H B  Base Tab
C H F  Flange
C H C  Contour Flange
E H B  Bend
T H U  Unbend
T H R  Rebend
P H F  Flat Pattern
I H V  Validate Sheet Metal
T H C  Sheet Metal from Solid
C H S  Sheet Feature
E H E  Extend Sheet
I H B  Sheet Boundary Analysis
```

## 9. Manufacturing

```text
C O O  Create Operation
C T T  Create Tool
P O G  Generate Tool Path
P O V  Verify Tool Path
P O P  Postprocess
X O D  Delete Operation
M N O  Operation Navigator
I O T  Tool Path Information
```

## 10. Simulation

```text
C N S  Create Solution
C N L  Create Load
C N C  Create Constraint
P N M  Mesh
P N S  Solve
X N D  Delete Simulation Object
M N S  Simulation Navigator
I N R  Results
```

## 11. Routing

```text
C R R  Create Route
C R P  Place Part
C R S  Add Stock
E R R  Edit Route
X R D  Delete Route Object
X R P  Remove Part
M N R  Routing Navigator
I R V  Validate Route
```

## 12. Mold и Reuse

```text
C M I  Initialize Mold Project
C M P  Parting
C M B  Mold Base
C M G  Gate
C M C  Cooling
C M E  Ejector
M L M  Mold Library
I M V  Validate Mold
```

Reuse:

```text
U E X  Expressions
M L R  Reuse Library
C T F  Create Feature Template
E T F  Replace Feature Template
M N P  Part Navigator
M P T  Parameter Table
H C F  Command Finder
```

## 13. View, Inspect и Selection

```text
V F T  Fit                       alias V F
V T R  Trimetric
I M G  Geometric Measurement     alias I M
I O B  Object Information
V H S  Hide Selected
V S H  Show and Hide
```

Selection:

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

## 14. WAVE, Layers и Materials

```text
M W L  WAVE Geometry Linker
M W I  WAVE Interface Linker
M W A  WAVE Associativity Manager
M W G  WAVE Graph Browser
M W D  Load WAVE Data
```

```text
M L S  Layer Settings
M L V  Visible Layers
M L A  Layer Category
M L C  Copy to Layer
M L M  Move to Layer
I L I  Layer Information
```

```text
M M A  Assign Material
M M L  Material Library
M M S  System Materials
M M P  Part Materials
V M V  Visual Material Display
X M V  Remove Visual Override
```

## 15. Полное покрытие каталога

Список команд NX зависит от лицензий, роли и пользовательских расширений. Поэтому неизвестные заранее команды получают путь автоматически.

Алгоритм:

1. извлечь точный `BUTTON ID` и название;
2. определить действие;
3. определить объект;
4. выбрать значимую букву команды;
5. проверить уникальность внутри модуля;
6. изменить последнюю или объектную букву при коллизии;
7. удалить alias, создающий конфликт префиксов;
8. добавить название и `BUTTON ID` в поиск.

Примеры классификации:

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

## 16. Безопасность

Команды с `requires_selection` блокируются без подходящего выбора. Разрушительные команды требуют `Enter`.

Политики для критичных операций перенесены на новые последовательности:

```text
M E E B  Modeling Edge Blend
M E E C  Modeling Edge Chamfer
A E C R  Assembly Replace Component
A X C R  Assembly Remove Component
C P O P  CAM Postprocess
C X O D  CAM Delete Operation
X P N S  Simulation Solve
X X N D  Simulation Delete
G X R D  Routing Delete
G X R P  Routing Remove Part
R E T F  Reuse Replace Template
```

## 17. Поиск

`CapsLock → Space` выполняет поиск по:

- имени команды;
- `BUTTON ID`;
- русским и английским aliases;
- каноническому пути;
- подписям уровней;
- модулю;
- истории использования.

## 18. Совместимость

Старые поля `slot`, `submenu_key` и `input_key` остаются читаемыми. При загрузке им автоматически назначаются новые `path`, `path_labels`, `aliases` и `search_aliases`.
