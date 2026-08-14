# Карта команд NXKeys v8 (Runtime-Driven)

> [!IMPORTANT]
> **Внимание:** Этот документ генерируется автоматически на основе рантайм-конфигурации NXKeys v8, вычисления `AdaptiveModuleResolver`, `V8SecondaryAliasExpander` и `CommandResolver`. Не редактируйте этот файл вручную.

## Обзор рантайм-контекста и каталога намерений

Настоящая спецификация отражает реальное поведение мнемонического языка ввода v8 в Siemens NX 2512. Все команды главного профиля **885** намерений уровней **K3–K5** и базового источника на **1169** намерений динамически связываются с модулями и резолвятся через адаптивные политики, утилиты установки `install-nxkeys.ps1` и фильтры конечного автомата.

- Исходный каталог намерений: **1169** (уровни **K1–K2** остаются в базе `06_ui_commands_buttons.csv`).
- Главный установленный профиль: **885** намерений (**K3–K5**).
- Активных адаптивных модулей v8: **14**.
- Всего сгенерировано исполняемых последовательностей: **143**.
- Статусы разрешения команд: `existing`, `resolved`, `ambiguous`, `unresolved`.

## Сводка по контекстным модулям v8

| Идентификатор модуля | Название (Префикс) | Приложения NX | Исполняемых мнемоник |
|----------------------|-------------------|---------------|----------------------|
| `v8_v` | **V** (`V`) | `UG_APP_GATEWAY` | 0 |
| `v8_c` | **C** (`C`) | `UG_APP_GATEWAY` | 0 |
| `v8_m` | **M** (`M`) | `UG_APP_MODELING` | 29 |
| `v8_s` | **S** (`S`) | `UG_APP_SKETCH` | 44 |
| `v8_a` | **A** (`A`) | `UG_APP_ASSEMBLIES` | 15 |
| `v8_d` | **D** (`D`) | `UG_APP_DRAFTING` | 14 |
| `v8_u` | **U** (`U`) | `UG_APP_STUDIO` | 6 |
| `v8_h` | **H** (`H`) | `UG_APP_SBSM, UG_APP_SHEETMETAL` | 9 |
| `v8_p` | **P** (`P`) | `UG_APP_PMI` | 4 |
| `v8_n` | **N** (`N`) | `UG_APP_MANUFACTURING` | 9 |
| `v8_i` | **I** (`I`) | `UG_APP_SFEM` | 0 |
| `v8_r` | **R** (`R`) | `UG_APP_ROUTING` | 2 |
| `v8_g` | **G** (`G`) | `UG_APP_GATEWAY` | 11 |
| `v8_w` | **W** (`W`) | `UG_APP_GATEWAY` | 0 |

## Полная таблица мнемоник по модулям

### Модуль `v8_v` (V)

| Мнемоника / Хоткей | Тип | NX Command ID | Название команды | Action / Filter | Статус |
|--------------------|-----|---------------|------------------|-----------------|--------|
| — | — | — | — | — | Нет доступных команд |

### Модуль `v8_c` (C)

| Мнемоника / Хоткей | Тип | NX Command ID | Название команды | Action / Filter | Статус |
|--------------------|-----|---------------|------------------|-----------------|--------|
| — | — | — | — | — | Нет доступных команд |

### Модуль `v8_m` (M)

| Мнемоника / Хоткей | Тип | NX Command ID | Название команды | Action / Filter | Статус |
|--------------------|-----|---------------|------------------|-----------------|--------|
| `M M S` | Canonical | `UG_CREATE_SKETCH` | Modeling, нет активного Sketch | `execute_command` | Resolved |
| `M E` | Alias | `UG_SKETCH_FINISH` | Активный Sketch | `execute_command` | Resolved |
| `M M Z V 8` | Canonical | `UG_SKETCH_FINISH` | Активный Sketch | `execute_command` | Resolved |
| `M D` | Alias | `UG_MODELING_EXTRUDED_FEATURE` | Modeling | `execute_command` | Resolved |
| `M M X` | Canonical | `UG_MODELING_EXTRUDED_FEATURE` | Modeling | `execute_command` | Resolved |
| `M M Q` | Canonical | `UG_SKETCH_RAPID_DIMENSION` | Sketch | `execute_command` | Resolved |
| `M M Z V D` | Canonical | `UG_DRAFTING_RAPID_DIMENSION` | Drafting | `execute_command` | Resolved |
| `M X` | Alias | `UG_DRAFTING_RAPID_DIMENSION` | Drafting | `execute_command` | Resolved |
| `M C R` | Canonical | `UG_MODELING_REVOLVED_FEATURE` | Revolve | `execute_command` | Resolved |
| `M C H` | Canonical | `UG_MODELING_HOLE_FEATURE` | Hole | `execute_command` | Resolved |
| `M C S` | Canonical | `UG_MODELING_SWEPT_FEATURE` | Sweep | `execute_command` | Resolved |
| `M C L` | Canonical | `UG_MODELING_THROUGH_CURVES_FEATURE` | Through Curves | `execute_command` | Resolved |
| `M C U` | Canonical | `UG_MODELING_STUDIO_SURFACE_FEATURE` | Studio Surface | `execute_command` | Resolved |
| `M C G` | Canonical | `UG_MODELING_EXTRACT_GEOMETRY` | Extract Geometry | `execute_command` | Resolved |
| `M T C` | Canonical | `UG_EDIT_COPY` | Copy | `execute_command` | Resolved |
| `M T P` | Canonical | `UG_MODELING_PATTERNFEATURE_FEATURE` | Pattern Feature | `execute_command` | Resolved |
| `M T M` | Canonical | `UG_MODELING_MIRRORFEATURE_FEATURE` | Mirror Feature | `execute_command` | Resolved |
| `M Q M` | Canonical | `UG_INFO_GEOMETRIC_MEASUREMENT` | Measure Distance / Geometry | `execute_command` | Resolved |
| `M Q O` | Canonical | `UG_INFO_OBJECT` | Object Information | `execute_command` | Resolved |
| `M W E` | Canonical | `UG_EXPRESSIONS` | Expressions | `execute_command` | Resolved |
| `M W L S` | Canonical | `UG_LAYER_SETTINGS` | Standard Layer Settings | `execute_command` | Resolved |
| `M W L M` | Canonical | `UG_LAYER_MOVE` | Move Selected to Layer | `execute_command` | Resolved |
| `M W M` | Canonical | `UG_MATERIAL_LIBRARY_MANAGER` | Material Library | `execute_command` | Resolved |
| `M I M` | Canonical | `UG_INFO_GEOMETRIC_MEASUREMENT` | Measure Distance / Geometry | `execute_command` | Resolved |
| `M I O` | Canonical | `UG_INFO_OBJECT` | Object Information | `execute_command` | Resolved |
| `M M E` | Canonical | `UG_EXPRESSIONS` | Expressions | `execute_command` | Resolved |
| `M M L S` | Canonical | `UG_LAYER_SETTINGS` | Standard Layer Settings | `execute_command` | Resolved |
| `M M L M` | Canonical | `UG_LAYER_MOVE` | Move Selected to Layer | `execute_command` | Resolved |
| `M M M` | Canonical | `UG_MATERIAL_LIBRARY_MANAGER` | Material Library | `execute_command` | Resolved |

### Модуль `v8_s` (S)

| Мнемоника / Хоткей | Тип | NX Command ID | Название команды | Action / Filter | Статус |
|--------------------|-----|---------------|------------------|-----------------|--------|
| `S L` | Canonical | `UG_SKETCH_LINE` | Line | `execute_command` | Resolved |
| `S R` | Canonical | `UG_SKETCH_RECTANGLE` | Rectangle | `execute_command` | Resolved |
| `S C` | Canonical | `UG_SKETCH_CIRCLE` | Circle | `execute_command` | Resolved |
| `S A` | Canonical | `UG_SKETCH_ARC` | Arc | `execute_command` | Resolved |
| `S S` | Canonical | `UG_SKETCH_STUDIO_SPLINE` | Spline | `execute_command` | Resolved |
| `S P` | Canonical | `UG_SKETCH_POINT` | Point | `execute_command` | Resolved |
| `S W` | Canonical | `UG_SKETCH_SLOT` | Slot | `execute_command` | Resolved |
| `S G` | Canonical | `UG_SKETCH_POLYGON` | Polygon | `execute_command` | Resolved |
| `S I` | Canonical | `UG_SKETCH_ELLIPSE` | Ellipse | `execute_command` | Resolved |
| `S T` | Canonical | `UG_SKETCH_TRIM` | Trim | `execute_command` | Resolved |
| `S E` | Canonical | `UG_SKETCH_EXTEND` | Extend | `execute_command` | Resolved |
| `S O` | Canonical | `UG_SKETCH_OFFSET_CURVE` | Offset Curve | `execute_command` | Resolved |
| `S F` | Canonical | `UG_SKETCH_FILLET` | Sketch Fillet | `execute_command` | Resolved |
| `S H` | Canonical | `UG_SKETCH_CHAMFER` | Sketch Chamfer | `execute_command` | Resolved |
| `S M` | Canonical | `UG_SKETCH_MIRROR_PATTERN` | Mirror Curve | `execute_command` | Resolved |
| `S V` | Canonical | `UG_SKETCH_MOVE_CURVES` | Move Curve | `execute_command` | Resolved |
| `S Y` | Canonical | `UG_SKETCH_PATTERN_CURVES` | Pattern Curve | `execute_command` | Resolved |
| `S J I` | Canonical | `UG_SKETCH_ADD_QUILT_PLANE_AI_CURVES` | Intersection Curve | `execute_command` | Resolved |
| `S D Q` | Canonical | `UG_SKETCH_RAPID_DIMENSION` | Rapid Dimension | `execute_command` | Resolved |
| `S D L` | Canonical | `UG_SKETCH_LINEAR_DIMENSION` | Linear Dimension | `execute_command` | Resolved |
| `S D A` | Canonical | `UG_SKETCH_ANGULAR_DIMENSION` | Angular Dimension | `execute_command` | Resolved |
| `S D R` | Canonical | `UG_SKETCH_RADIAL_DIMENSION` | Radius Dimension | `execute_command` | Resolved |
| `S D O` | Canonical | `UG_SKETCH_DIAMETER_DIM` | Diameter Dimension | `execute_command` | Resolved |
| `S D P` | Canonical | `UG_SKETCH_PERIMETER_DIM` | Perimeter Dimension | `execute_command` | Resolved |
| `S K C` | Canonical | `UG_SKETCH_COINCIDENT_CONSTRAINT` | Coincident | `execute_command` | Resolved |
| `S K H` | Canonical | `UG_SKETCH_HORIZONTAL_CONSTRAINT` | Horizontal | `execute_command` | Resolved |
| `S K V` | Canonical | `UG_SKETCH_VERTICAL_CONSTRAINT` | Vertical | `execute_command` | Resolved |
| `S K T` | Canonical | `UG_SKETCH_TANGENT_CONSTRAINT` | Tangent | `execute_command` | Resolved |
| `S K P` | Canonical | `UG_SKETCH_PARALLEL_CONSTRAINT` | Parallel | `execute_command` | Resolved |
| `S K N` | Canonical | `UG_SKETCH_PERPENDICULAR_CONSTRAINT` | Perpendicular | `execute_command` | Resolved |
| `S K O` | Canonical | `UG_SKETCH_CONCENTRIC_CONSTRAINT` | Concentric | `execute_command` | Resolved |
| `S K E` | Canonical | `UG_SKETCH_EQUAL_LENGTH_CONSTRAINT` | Equal | `execute_command` | Resolved |
| `S K L` | Canonical | `UG_SKETCH_COLLINEAR_CONSTRAINT` | Collinear | `execute_command` | Resolved |
| `S K M` | Canonical | `UG_SKETCH_MAKE_MIDPOINT_ALIGNED` | Midpoint | `execute_command` | Resolved |
| `S K S` | Canonical | `UG_SKETCH_SYMMETRIC_CONSTRAINT` | Symmetric | `execute_command` | Resolved |
| `S K F` | Canonical | `UG_SKETCH_FIXED_CONSTRAINT` | Fixed | `execute_command` | Resolved |
| `S N` | Canonical | `UG_SKETCH_CONSTRAINT_NAVIGATOR` | Sketch Navigator | `execute_command` | Resolved |
| `S Z` | Canonical | `UG_SKETCH_CHECKER` | Sketch Checker | `execute_command` | Resolved |
| `S U I` | Canonical | `UG_SKETCH_CHECKING` | Issues | `execute_command` | Resolved |
| `S J K` | Canonical | `UG_SKETCH_MAKE_CORNER` | Make Corner | `execute_command` | Resolved |
| `S J D` | Canonical | `UG_SKETCH_CONSTRUCTION_LINES` | Derived Lines | `execute_command` | Resolved |
| `S U A` | Canonical | `UG_SKETCH_ALTERNATE_SOLUTION` | Alternate Solution | `execute_command` | Resolved |
| `S K A` | Canonical | `UG_SKETCH_AUTO_CREATE_CONSTRAINTS` | Auto Constrain | `execute_command` | Resolved |
| `S D M` | Canonical | `UG_SKETCH_ANIMATE_DIMENSION` | Animate Dimension | `execute_command` | Resolved |

### Модуль `v8_a` (A)

| Мнемоника / Хоткей | Тип | NX Command ID | Название команды | Action / Filter | Статус |
|--------------------|-----|---------------|------------------|-----------------|--------|
| `A C A` | Canonical | `UG_ASSEMBLIES_ADD_COMPONENT` | Add Component | `execute_command` | Resolved |
| `A C N` | Canonical | `UG_ASSEMBLIES_NEW_COMPONENT` | New Component | `execute_command` | Resolved |
| `A C J` | Canonical | `UG_ASSEMBLIES_CONSTRAINTS` | Assembly Constraints | `execute_command` | Resolved |
| `A C K` | Alias | `UG_ASSEMBLIES_CONSTRAINTS` | Assembly Constraints | `execute_command` | Resolved |
| `A T M` | Canonical | `UG_ASSEMBLIES_MOVE_COMPONENT` | Move Component | `execute_command` | Resolved |
| `A T P` | Canonical | `UG_ASSEMBLIES_PATTERN_COMPONENT` | Pattern Component | `execute_command` | Resolved |
| `A E R` | Canonical | `UG_ASSEMBLIES_REPLACE_COMPONENT` | Replace Component | `execute_command` | Resolved |
| `A X C` | Canonical | `UG_ASSEMBLIES_REMOVE_COMPONENT` | Remove Component | `execute_command` | Resolved |
| `A W N` | Canonical | `UG_ASSEMBLIES_NAVIGATOR` | Assembly Navigator | `execute_command` | Resolved |
| `A C F G` | Alias | `UG_ASSY_WAVE_LINKER` | WAVE Geometry Linker | `execute_command` | Resolved |
| `A W W G` | Canonical | `UG_ASSY_WAVE_LINKER` | WAVE Geometry Linker | `execute_command` | Resolved |
| `A Q M` | Canonical | `UG_INFO_GEOMETRIC_MEASUREMENT` | Measure | `execute_command` | Resolved |
| `A M N` | Canonical | `UG_ASSEMBLIES_NAVIGATOR` | Assembly Navigator | `execute_command` | Resolved |
| `A M W G` | Canonical | `UG_ASSY_WAVE_LINKER` | WAVE Geometry Linker | `execute_command` | Resolved |
| `A I M` | Canonical | `UG_INFO_GEOMETRIC_MEASUREMENT` | Measure | `execute_command` | Resolved |

### Модуль `v8_d` (D)

| Мнемоника / Хоткей | Тип | NX Command ID | Название команды | Action / Filter | Статус |
|--------------------|-----|---------------|------------------|-----------------|--------|
| `D V B` | Canonical | `UG_DRAFTING_BASE_VIEW` | Base View | `execute_command` | Resolved |
| `D V P` | Canonical | `UG_DRAFTING_PROJECTED_VIEW` | Projected View | `execute_command` | Resolved |
| `D V C` | Canonical | `UG_DRAFTING_SECTION_VIEW` | Section View | `execute_command` | Resolved |
| `D V D` | Canonical | `UG_DRAFTING_DETAIL_VIEW` | Detail View | `execute_command` | Resolved |
| `D V Y` | Canonical | `UG_DRAFTING_VIEW_STYLE` | View Style | `execute_command` | Resolved |
| `D V U` | Canonical | `UG_DRAFTING_UPDATE_VIEWS` | Update Views | `execute_command` | Resolved |
| `D A D Q` | Canonical | `UG_DRAFTING_RAPID_DIMENSION` | Rapid Dimension | `execute_command` | Resolved |
| `D A N` | Canonical | `UG_PMI_NOTE` | Note | `execute_command` | Resolved |
| `D A G D` | Canonical | `UG_PMI_DATUM_FEATURE_SYMBOL` | Datum Feature Symbol | `execute_command` | Resolved |
| `D A G F` | Canonical | `UG_PMI_FEATURE_CONTROL_FRAME` | Feature Control Frame | `execute_command` | Resolved |
| `D A F` | Canonical | `UG_PMI_SURFACE_FINISH` | Surface Finish | `execute_command` | Resolved |
| `D A P` | Canonical | `UG_DRAFTING_PARTS_LIST` | Parts List | `execute_command` | Resolved |
| `D E` | Canonical | `nxeskd.open_workflow` | ЕСКД — подготовить или обновить чертёж | `execute_command` | Resolved |
| `D A L` | Canonical | `UG_DRAFTING_RENEW_LAYOUT` | Parts List, legacy alias | `execute_command` | Resolved |

### Модуль `v8_u` (U)

| Мнемоника / Хоткей | Тип | NX Command ID | Название команды | Action / Filter | Статус |
|--------------------|-----|---------------|------------------|-----------------|--------|
| `U C T` | Canonical | `UG_MODELING_THROUGH_CURVES_FEATURE` | Through Curves | `execute_command` | Resolved |
| `U C S` | Canonical | `UG_MODELING_STUDIO_SURFACE_FEATURE` | Studio Surface | `execute_command` | Resolved |
| `U S W` | Canonical | `UG_MODELING_SWEPT_FEATURE` | Swept Surface | `execute_command` | Resolved |
| `U E T` | Canonical | `UG_MODELING_TRIM_SHEET_FEATURE` | Trim Sheet | `execute_command` | Resolved |
| `U E X` | Canonical | `UG_MODELING_FF_EXTEND_SHEET` | Extend Sheet | `execute_command` | Resolved |
| `U E S` | Canonical | `UG_MODELING_SEW_FEATURE` | Sew | `execute_command` | Resolved |

### Модуль `v8_h` (H)

| Мнемоника / Хоткей | Тип | NX Command ID | Название команды | Action / Filter | Статус |
|--------------------|-----|---------------|------------------|-----------------|--------|
| `H C T` | Canonical | `UG_SBSM_TAB_FEATURE` | Tab | `execute_command` | Resolved |
| `H C F` | Canonical | `UG_SBSM_FLANGE_FEATURE` | Flange | `execute_command` | Resolved |
| `H C C` | Canonical | `UG_SBSM_CONTOUR_FLANGE_FEATURE` | Contour Flange | `execute_command` | Resolved |
| `H C S` | Canonical | `UG_SBSM_SHEETMETAL_FROM_SOLID_FEATURE` | Convert Solid to Sheet Metal | `execute_command` | Resolved |
| `H E B` | Canonical | `UG_SBSM_BEND_FEATURE` | Bend | `execute_command` | Resolved |
| `H T U` | Canonical | `UG_SBSM_UNBEND_FEATURE` | Unbend | `execute_command` | Resolved |
| `H T R` | Canonical | `UG_SBSM_REBEND_FEATURE` | Rebend | `execute_command` | Resolved |
| `H R P` | Canonical | `UG_SBSM_FLAT_PATTERN_FEATURE` | Flat Pattern | `execute_command` | Resolved |
| `H P P` | Canonical | `UG_SBSM_FLAT_PATTERN_FEATURE` | Flat Pattern | `execute_command` | Resolved |

### Модуль `v8_p` (P)

| Мнемоника / Хоткей | Тип | NX Command ID | Название команды | Action / Filter | Статус |
|--------------------|-----|---------------|------------------|-----------------|--------|
| `P A N` | Canonical | `LINEDESIGNER_ADD_PMI_OCCURRENCE_NOTE` | PMI Note | `execute_command` | Resolved |
| `P A G D` | Canonical | `UG_DRAFT_DATUM_SYMBOL` | Datum Feature Symbol | `execute_command` | Resolved |
| `P A G F` | Canonical | `UG_HUMAN_CONTROL_PANEL` | Feature Control Frame | `execute_command` | Resolved |
| `P A F` | Canonical | `UG_PMI_SURFACE_SYMBOL` | Surface Finish | `execute_command` | Resolved |

### Модуль `v8_n` (N)

| Мнемоника / Хоткей | Тип | NX Command ID | Название команды | Action / Filter | Статус |
|--------------------|-----|---------------|------------------|-----------------|--------|
| `N C T` | Canonical | `UG_CAM_CREATE_TOOL` | Create Tool | `execute_command` | Resolved |
| `N C O` | Canonical | `UG_CAM_CREATE_OPERATION` | Create Operation | `execute_command` | Resolved |
| `N W N` | Canonical | `UG_CAM_OPERATION_NAVIGATOR` | Operation Navigator | `execute_command` | Resolved |
| `N X O` | Canonical | `UG_CAM_DELETE_OPERATION` | Delete Operation | `execute_command` | Resolved |
| `N R G` | Canonical | `UG_CAM_GENERATE_TOOL_PATH` | Generate Selected Toolpath | `execute_command` | Resolved |
| `N R V` | Canonical | `UG_CAM_VERIFY_TOOL_PATH` | Verify Toolpath | `execute_command` | Resolved |
| `N M N` | Canonical | `UG_CAM_OPERATION_NAVIGATOR` | Operation Navigator | `execute_command` | Resolved |
| `N P G` | Canonical | `UG_CAM_GENERATE_TOOL_PATH` | Generate Selected Toolpath | `execute_command` | Resolved |
| `N P V` | Canonical | `UG_CAM_VERIFY_TOOL_PATH` | Verify Toolpath | `execute_command` | Resolved |

### Модуль `v8_i` (I)

| Мнемоника / Хоткей | Тип | NX Command ID | Название команды | Action / Filter | Статус |
|--------------------|-----|---------------|------------------|-----------------|--------|
| — | — | — | — | — | Нет доступных команд |

### Модуль `v8_r` (R)

| Мнемоника / Хоткей | Тип | NX Command ID | Название команды | Action / Filter | Статус |
|--------------------|-----|---------------|------------------|-----------------|--------|
| `R C R` | Canonical | `UG_ROUTE_CREATE_ROUTE` | Create Route | `execute_command` | Resolved |
| `R C P` | Canonical | `UG_ROUTE_PLACE_PART` | Place Routing Part | `execute_command` | Resolved |

### Модуль `v8_g` (G)

| Мнемоника / Хоткей | Тип | NX Command ID | Название команды | Action / Filter | Статус |
|--------------------|-----|---------------|------------------|-----------------|--------|
| `G M` | Canonical | `UG_APP_MODELING` | Modeling | `execute_command` | Resolved |
| `G A` | Canonical | `UG_APP_ASSEMBLIES` | Assemblies | `execute_command` | Resolved |
| `G D` | Canonical | `UG_APP_DRAFTING` | Drafting | `execute_command` | Resolved |
| `G H` | Canonical | `UG_APP_SHEETMETAL` | Sheet Metal | `execute_command` | Resolved |
| `G C` | Canonical | `UG_APP_MANUFACTURING` | Manufacturing / CAM | `execute_command` | Resolved |
| `G N` | Canonical | `UG_APP_SFEM` | Simulation | `execute_command` | Resolved |
| `G P` | Canonical | `UG_APP_PMI` | PMI | `execute_command` | Resolved |
| `G R` | Canonical | `UG_APP_ROUTING` | Routing | `execute_command` | Resolved |
| `G O` | Canonical | `UG_APP_MOLDWIZARD` | Mold Wizard | `execute_command` | Resolved |
| `G L` | Canonical | `UG_NAVIGATOR_REUSE_LIBRARY` | Reuse Library | `execute_command` | Resolved |
| `G V` | Canonical | `UG_APP_GATEWAY` | Gateway / View and Analysis | `execute_command` | Resolved |

### Модуль `v8_w` (W)

| Мнемоника / Хоткей | Тип | NX Command ID | Название команды | Action / Filter | Статус |
|--------------------|-----|---------------|------------------|-----------------|--------|
| — | — | — | — | — | Нет доступных команд |

## Разрешение неизвестных и неоднозначных команд

Если мнемонический путь ссылается на нераспознанный идентификатор, он квалифицируется как `unresolved` или `ambiguous` и блокируется до подтверждения через Siemens NX catalog probe.

