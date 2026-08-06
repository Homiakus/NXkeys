# Язык намерений Sketch (v8)

Sketch использует однотокенную модель мнемонического языка NXKeys. Активный Sketch уже является однозначным внутренним префиксом, поэтому команды эскиза вызываются **одной клавишей** после `CapsLock`. Многотокенные пути保留 для размеров, ограничений, проекции, диагностики и вариантов построения.

```text
CapsLock → одиночная клавиша → команда эскиза
```

## Три уровня ввода

### Уровень A — Direct Keys (без CapsLock)

Прямые клавиши, работающие в контексте Sketch без открытия Leader:

| Клавиша | Набор | Команда | BUTTON ID |
|---------|-------|---------|-----------|
| `Q` | Базовый | Rapid Dimension | `UG_SKETCH_RAPID_DIMENSION` |
| `S` | Базовый | Закончить эскиз | `UG_SKETCH_FINISH` |
| `D` | Расширенный | Rapid Dimension | `UG_SKETCH_RAPID_DIMENSION` |
| `T` | Расширенный | Trim | `UG_SKETCH_TRIM` |
| `F` | Расширенный | Sketch Fillet | `UG_SKETCH_FILLET` |
| `E` | Расширенный | Extend | `UG_SKETCH_EXTEND` |
| `O` | Расширенный | Offset Curve | `UG_SKETCH_OFFSET_CURVE` |

Расширенный набор включается персональным профилем. `F` конфликтует с Fit — отключён по умолчанию.

### Уровень B — Однотокенные команды (CapsLock + клавиша)

В активном Sketch после `CapsLock`:

#### Геометрия (создание)

| Клавиша | Команда | BUTTON ID | Статус |
|---------|---------|-----------|--------|
| `L` | Line | `UG_SKETCH_LINE` | `declared_v8` |
| `R` | Rectangle | `UG_SKETCH_RECTANGLE` | `declared_v8` |
| `C` | Circle | `UG_SKETCH_CIRCLE` | `declared_v8` |
| `A` | Arc | `UG_SKETCH_ARC` | `declared_v8` |
| `S` | Studio Spline | `UG_SKETCH_STUDIO_SPLINE` | `declared_v8` |
| `P` | Point | `UG_SKETCH_POINT` | `declared_v8` |
| `W` | Slot | `UG_SKETCH_SLOT` | `declared_v8` |
| `G` | Polygon | `UG_SKETCH_POLYGON` | `declared_v8` |
| `I` | Ellipse | `UG_SKETCH_ELLIPSE` | `declared_v8` |

#### Редактирование кривых

| Клавиша | Команда | BUTTON ID | Статус |
|---------|---------|-----------|--------|
| `T` | Trim | `UG_SKETCH_TRIM` | `declared_v8` |
| `E` | Extend | `UG_SKETCH_EXTEND` | `declared_v8` |
| `O` | Offset Curve | `UG_SKETCH_OFFSET_CURVE` | `declared_v8` |
| `F` | Sketch Fillet | `UG_SKETCH_FILLET` | `declared_v8` |
| `H` | Sketch Chamfer | `UG_SKETCH_CHAMFER` | `declared_v8` |
| `M` | Mirror Curve | `UG_SKETCH_MIRROR_PATTERN` | `declared_v8` |
| `V` | Move Curve | `UG_SKETCH_MOVE_CURVES` | `declared_v8` |
| `Y` | Pattern Curve | `UG_SKETCH_PATTERN_CURVES` | `declared_v8` |

#### Диагностика

| Клавиша | Команда | BUTTON ID | Статус |
|---------|---------|-----------|--------|
| `N` | Sketch Navigator | `UG_SKETCH_CONSTRAINT_NAVIGATOR` | `declared_v8` |
| `Z` | Sketch Checker | `UG_SKETCH_CHECKER` | `declared_v8` |

### Уровень C — Двухтокенные пути (CapsLock + префикс + клавиша)

#### Размеры (префикс `D`)

| Путь | Команда | BUTTON ID |
|------|---------|-----------|
| `D → Q` | Rapid Dimension | `UG_SKETCH_RAPID_DIMENSION` |
| `D → L` | Linear Dimension | `UG_SKETCH_LINEAR_DIMENSION` |
| `D → A` | Angular Dimension | `UG_SKETCH_ANGULAR_DIMENSION` |
| `D → R` | Radius Dimension | `UG_SKETCH_RADIAL_DIMENSION` |
| `D → O` | Diameter Dimension | `UG_SKETCH_DIAMETER_DIM` |
| `D → P` | Perimeter Dimension | `UG_SKETCH_PERIMETER_DIM` |
| `D → M` | Animate Dimension | `UG_SKETCH_ANIMATE_DIMENSION` |

#### Ограничения (префикс `K`)

| Путь | Ограничение | BUTTON ID |
|------|------------|-----------|
| `K → C` | Coincident | `UG_SKETCH_COINCIDENT_CONSTRAINT` |
| `K → H` | Horizontal | `UG_SKETCH_HORIZONTAL_CONSTRAINT` |
| `K → V` | Vertical | `UG_SKETCH_VERTICAL_CONSTRAINT` |
| `K → T` | Tangent | `UG_SKETCH_TANGENT_CONSTRAINT` |
| `K → P` | Parallel | `UG_SKETCH_PARALLEL_CONSTRAINT` |
| `K → N` | Perpendicular | `UG_SKETCH_PERPENDICULAR_CONSTRAINT` |
| `K → O` | Concentric | `UG_SKETCH_CONCENTRIC_CONSTRAINT` |
| `K → E` | Equal Length | `UG_SKETCH_EQUAL_LENGTH_CONSTRAINT` |
| `K → L` | Collinear | `UG_SKETCH_COLLINEAR_CONSTRAINT` |
| `K → M` | Midpoint | `UG_SKETCH_MAKE_MIDPOINT_ALIGNED` |
| `K → S` | Symmetric | `UG_SKETCH_SYMMETRIC_CONSTRAINT` |
| `K → F` | Fixed | `UG_SKETCH_FIXED_CONSTRAINT` |
| `K → A` | Auto Constrain | `UG_SKETCH_AUTO_CREATE_CONSTRAINTS` |

#### Проекция и производные (префикс `J`)

| Путь | Команда | BUTTON ID |
|------|---------|-----------|
| `J → P` | Project Curve | `tbd_adapter` |
| `J → I` | Intersection Curve | `UG_SKETCH_ADD_QUILT_PLANE_AI_CURVES` |
| `J → D` | Derived Lines | `UG_SKETCH_CONSTRUCTION_LINES` |
| `J → K` | Make Corner | `UG_SKETCH_MAKE_CORNER` |

#### Утилиты (префикс `U`)

| Путь | Команда | BUTTON ID |
|------|---------|-----------|
| `U → D` | Show Degrees of Freedom | `tbd_adapter` |
| `U → R` | Show Relations | `tbd_adapter` |
| `U → E` | External References | `tbd_adapter` |
| `U → I` | Issues | `UG_SKETCH_CHECKING` |
| `U → A` | Alternate Solution | `UG_SKETCH_ALTERNATE_SOLUTION` |

#### Удаление ограничений (префикс `K`)

| Путь | Команда | BUTTON ID |
|------|---------|-----------|
| `K → X` | Remove Constraint | `tbd_adapter` |

## Варианты построения (ветвь `C → V`)

Варианты не продолжают путь базовой команды — терминальная команда не может быть префиксом другой. Для вариантов выделена ветвь `C → V`:

| Путь | Вариант | BUTTON ID |
|------|---------|-----------|
| `C → V → L → 2` | Line by Two Points | `UG_SKETCH_LINE_BY_TWO_POINTS` |
| `C → V → L → M` | Line from Midpoint | `UG_SKETCH_LINE_FROM_MIDPOINT` |
| `C → V → R → 2` | Rectangle by Two Points | `UG_SKETCH_RECTANGLE_BY_TWO_POINTS` |
| `C → V → R → C` | Rectangle from Center | `UG_SKETCH_RECTANGLE_FROM_CENTER` |
| `C → V → R → 3` | Rectangle by Three Points | `UG_SKETCH_RECTANGLE_BY_THREE_POINTS` |
| `C → V → C → C` | Circle from Center | `UG_SKETCH_CIRCLE_FROM_CENTER` |
| `C → V → C → 3` | Circle by Three Points | `UG_SKETCH_CIRCLE_BY_THREE_POINTS` |
| `C → V → A → 3` | Arc by Three Points | `UG_SKETCH_ARC_BY_THREE_POINTS` |
| `C → V → A → C` | Arc from Center | `UG_SKETCH_ARC_FROM_CENTER` |

## Переход в эскиз из других модулей

- `G → S` — Switch to Sketch / Create Sketch из Modeling, Assembly, Surface и др.

## Границы контекста

Sketch-компилятор добавляет только намерения с `runtime_module: sketch`, подтверждённое ядро Sketch и универсальные фильтры выбора. Глобальные файловые команды, навигатор сборки, материалы, сшивка поверхностей и переходы между приложениями не дублируются в дерево Sketch.

Подтверждённое ядро Sketch сохраняется как runtime vocabulary независимо от частотной фильтрации K3–K5. Команда без точного `BUTTON ID` остаётся видимой, но отключённой.

## Алиасы и пользовательские пути

Старые позиционные алиасы `W/E/D/C/X/Z/A/Q` для Sketch автоматически удаляются. Пользовательский путь с `path_locked: true` или `path_source: user` сохраняется без изменений.

Новая команда `UG_SKETCH_*`, найденная в каталоге целевой установки NX, получает семейство по назначению и остаётся внутри него при разрешении коллизий. Неоднозначный или неразрешённый `BUTTON ID` остаётся видимым, но отключённым.

## Типовые сценарии

### Линия

```text
CapsLock → L
```

### Прямоугольник

```text
CapsLock → R
```

### Обрезка

```text
CapsLock → T
```

### Быстрый размер

```text
CapsLock → D → Q   (или прямой Q в Sketch)
```

### Закончить эскиз

```text
CapsLock → S   (или прямой S в активном Sketch)
```

### Вариант линии по двум точкам

```text
CapsLock → C → V → L → 2
```

### Совпадение (ограничение)

```text
CapsLock → K → C
```

### Make Corner

```text
CapsLock → J → K
```

## Обязательные регрессионные проверки

После изменения Sketch allocator, curated paths или compiler выполните:

```powershell
node .\scripts\validate-main-command-map.mjs
node .\scripts\validate-command-tree.mjs

dotnet run --project .\NX2512_HotkeyStudio.Tests\NX2512_HotkeyStudio.Tests.csproj -c Release

dotnet build .\NX2512_HotkeyStudio\NX2512_HotkeyStudio.csproj `
  -c Release -p:Platform=x64 --nologo
```

Проверьте:

- однотокенные пути (L, R, C, A, T, E, O, F, H, M, V, Y, N, Z);
- двухтокенные пути (D→*, K→*, J→*, U→*);
- ветвь `C → V → …`;
- prefix-free инвариант;
- сохранение user-locked paths;
- удаление legacy aliases;
- отсутствие чужих команд в Sketch;
- отсутствие enabled-команд без точного BUTTON ID;
- удержание новых Sketch-команд внутри смыслового семейства.
