# Шпаргалка NXKeys v8

Короткая инструкция для ежедневной работы с NXKeys в Siemens NX / Designcenter NX 2512.

Текущий runtime contract: [RUNTIME_V8.md](RUNTIME_V8.md). Если старый generated report или historical audit показывает другие пути, используйте current v8 profile/HUD.

## 1. Главное правило

NXKeys определяет активный модуль NX автоматически. Пользователь **не вводит module prefix**.

```text
CapsLock → путь команды внутри текущего контекста
```

Частые Sketch-команды могут состоять из одной клавиши.

## 2. CapsLock / Leader

| Ввод | Действие |
|---|---|
| `CapsLock` | открыть Leader для текущего контекста |
| буквы/цифры | продолжить mnemonic path |
| `Space` | поиск, если доступен в текущем UI |
| `Backspace` | удалить последний токен |
| `Esc` | закрыть/отменить Leader |

Физический CapsLock защёлкивается до отпускания. Удержание клавиши не должно порождать серию виртуальных Leader-нажатий из-за keyboard autorepeat.

## 3. Sketch v8

В активном Sketch:

| Путь после CapsLock | Команда |
|---|---|
| `L` | Line |
| `R` | Rectangle |
| `C` | Circle |
| `A` | Arc |
| `S` | Studio Spline |
| `P` | Point |
| `W` | Slot |
| `G` | Polygon |
| `I` | Ellipse |
| `T` | Trim |
| `E` | Extend |
| `O` | Offset Curve |
| `F` | Sketch Fillet |
| `H` | Sketch Chamfer |
| `M` | Mirror Curve |
| `V` | Move Curve |
| `Y` | Pattern Curve |
| `N` | Sketch Navigator |
| `Z` | Sketch Checker |

Примеры:

```text
CapsLock → L        линия
CapsLock → R        прямоугольник
CapsLock → T        trim
```

Полная таблица: [SKETCH_INTENT_LANGUAGE.md](SKETCH_INTENT_LANGUAGE.md).

## 4. Sketch constraints

Ограничения находятся под `K`:

| Путь | Ограничение |
|---|---|
| `K → C` | Coincident |
| `K → H` | Horizontal |
| `K → V` | Vertical |
| `K → T` | Tangent |
| `K → P` | Parallel |
| `K → N` | Perpendicular |
| `K → O` | Concentric |
| `K → E` | Equal Length |
| `K → L` | Collinear |
| `K → M` | Midpoint |
| `K → S` | Symmetric |
| `K → F` | Fixed |
| `K → A` | Auto Constrain |

Пример:

```text
CapsLock → K → C    Coincident
```

Если `K → …` не срабатывает, проверьте, что вы **внутри активного Sketch**, а после обновления Bridge/NXKeys NX был полностью перезапущен.

## 5. Sketch dimensions

| Путь | Команда |
|---|---|
| `D → Q` | Rapid Dimension |
| `D → L` | Linear Dimension |
| `D → A` | Angular Dimension |
| `D → R` | Radius Dimension |
| `D → O` | Diameter Dimension |
| `D → P` | Perimeter Dimension |
| `D → M` | Animate Dimension |

```text
CapsLock → D → Q    быстрый размер
```

## 6. Варианты построения Sketch

Варианты вынесены в prefix-free ветвь `C → V`:

```text
CapsLock → C → V → L → 2    Line by Two Points
CapsLock → C → V → L → M    Line from Midpoint
CapsLock → C → V → R → C    Rectangle from Center
CapsLock → C → V → C → 3    Circle by Three Points
CapsLock → C → V → A → C    Arc from Center
```

Базовая команда вроде `L` не является префиксом варианта.

## 7. Modeling: Manage

В Modeling `M` — смысловой корень Manage.

```text
CapsLock → M → L → S    Layer Settings
```

Внутренний runtime путь содержит скрытый module prefix Modeling, но пользователь вводит только `M → L → S`.

Workspace-local keys не считаются root commands без отдельного workspace state. Поэтому одиночный `M` не должен конфликтовать с Manage subtree.

## 8. Универсальные фильтры типа выбора

Leader selection filters отвечают на вопрос **что по типу выбирать**:

| Путь | Тип |
|---|---|
| `S → B` | Body |
| `S → F` | Face |
| `S → E` | Edge |
| `S → T` | Feature |
| `S → C` | Component |
| `S → U` | Curve |
| `S → D` | Datum |
| `S → R` | Reset filter |
| `S → A` | Select All |
| `S → N` | Deselect All |

## 9. Selection Intent `0…4` / `CapsLock → Q/W/E/R/~`

Selection Intent отвечает на вопрос: **как распространить выбор от seed-объекта**.

| Клавиша под `CapsLock` | Прямой ввод | Режим |
|---|---|---|
| `CapsLock → ~` | `0` | Reset |
| `CapsLock → Q` | `1` | Single |
| `CapsLock → W` | `2` | Connected / Chain |
| `CapsLock → E` | `3` | Tangent |
| `CapsLock → R` | `4` | Inferred Path / Region Boundary |

Используйте их при активном NX collector, например внутри Extrude/Section selection.

Пример:

```text
CapsLock → S → E    выбрать тип Edge
CapsLock → E        включить Tangent intent (или цифра 3)
```

Handler работает только когда NX foreground и обнаружен подходящий collector либо seed selection; обычный ввод текста/чисел защищен guards.

Подробности: [SELECTION_INTENT.md](SELECTION_INTENT.md).

## 10. Переходы между приложениями

Sequence policy резервирует `G → …` для module switches:

| Путь | Приложение |
|---|---|
| `G → M` | Modeling |
| `G → A` | Assembly |
| `G → D` | Drafting |
| `G → P` | PMI |
| `G → U` | Surface |
| `G → H` | Sheet Metal |
| `G → C` | Manufacturing/CAM |
| `G → N` | Simulation |
| `G → R` | Routing |
| `G → O` | Mold |
| `G → L` | Reuse |
| `G → V` | Inspect/View |
| `G → S` | Sketch |

Sheet Metal runtime использует canonical `UG_APP_SBSM`. Старое `UG_APP_SHEETMETAL` принимается только как compatibility mapping.

## 11. Sheet Metal

Новые/канонические feature IDs NX 2512 имеют вид:

```text
UG_SBSM_TAB_FEATURE
UG_SBSM_FLANGE_FEATURE
UG_SBSM_CONTOUR_FLANGE_FEATURE
UG_SBSM_BEND_FEATURE
UG_SBSM_UNBEND_FEATURE
UG_SBSM_REBEND_FEATURE
UG_SBSM_FLAT_PATTERN_FEATURE
```

Если после обновления в Sheet Metal видны только старые/нерабочие команды, проверьте фактически установленный v8 profile и полностью перезапустите NX.

## 12. Прямые привычные shortcuts

Базовый набор сохраняет системные сочетания:

```text
Ctrl+N          New
Ctrl+O          Open
Ctrl+S          Save
Ctrl+Shift+S    Save As
Ctrl+Z          Undo
Ctrl+Y          Redo
Ctrl+X/C/V      Cut/Copy/Paste
Delete          Delete
Ctrl+F          Fit
F5              Refresh
```

## 13. Где находится установленный профиль

Актуальный installer помещает v8 profile в managed root:

```text
%LOCALAPPDATA%\NXKeys\managed\NX2512.6000\nx2512-v8-profile.json
```

HotkeyStudio также понимает compatibility filename `nx2512-pro-hybrid.json`, но v8 имя имеет приоритет при автоматическом поиске.

## 14. Установка

Без параметров:

```powershell
.\install-nxkeys.ps1
```

открывается maintenance menu.

Чистая установка:

```powershell
.\install-nxkeys.ps1 `
  -Mode CleanInstall `
  -Yes `
  -NxRoot "C:\Program Files\Siemens\NX2512" `
  -Clean
```

Если `-ConfigPath` не задан, installer использует `config\nx2512-v8-profile.json`.

## 15. Проверка установки

```powershell
$root = "$env:LOCALAPPDATA\NXKeys\managed\NX2512.6000"
$studio = "$root\NX2512_HotkeyStudio.exe"
$config = "$root\nx2512-v8-profile.json"

& $studio validate --config $config
& $studio health --config $config
```

Затем запустите NX через:

```text
%LOCALAPPDATA%\NXKeys\managed\NX2512.6000\launch-nx2512-with-nxkeys.cmd
```

и выполните:

```powershell
& $studio bridge-status --config $config
```

Правильный порядок:

```text
validate → health → запуск NX через managed launcher → bridge-status
```

## 16. Почему нужен managed launcher

IPC schema 4 использует authenticated session. Launcher создаёт session capability для HotkeyStudio и NX/Bridge. Независимый запуск процессов не создаёт штатную общую security session.

## 17. После обновления DLL

`NX2512_CommandBridge.dll` загружена в процесс NX и может оставаться заблокированной. Перед переустановкой/обновлением Bridge:

1. сохраните работу;
2. полностью закройте NX;
3. убедитесь, что процессы NX завершены;
4. выполните install/update;
5. снова запустите NX через managed launcher.

`-AllowRunningNX` не означает hot reload DLL.

## 18. Быстрая диагностика

### CapsLock срабатывает несколько раз

Убедитесь, что используется актуальная HotkeyStudio build. В v8 physical key latch должен игнорировать autorepeat до `key-up`.

### `K → C` не запускает constraint

Проверьте:

- активен именно Sketch;
- установлен текущий v8 profile;
- Bridge свежий после полного restart NX;
- `UG_SKETCH_COINCIDENT_CONSTRAINT` доступен/чувствителен в вашей NX role.

### `0…4` ничего не делают

Нужен активный collector или seed selection. В обычном числовом поле цифры специально не перехватываются.

### Sheet Metal пустой/неправильный

Проверьте mapping на `UG_APP_SBSM` / `UG_SBSM_*`, свежесть profile и DLL.

### Bridge OFFLINE

При закрытом NX это нормально. При открытом NX проверьте launcher, custom dirs, DLL placement, `status.json` и `context.json`.

### Команда визуально открылась, но Bridge сообщил failure

Соберите Bridge log и request/result. Для некоторых интерактивных NX commands значение `DialogTester.InvokeMenuButtonAction(...)` требует проверки в живой NX; contract build не доказывает его семантику.

Полная диагностика: [TROUBLESHOOTING.md](TROUBLESHOOTING.md).

## 19. Проверки разработчика

```powershell
node .\scripts\validate-documentation.mjs
node .\scripts\validate-main-command-map.mjs
node .\scripts\validate-command-tree.mjs
node .\scripts\validate-full-command-map.mjs

dotnet run --project .\NXKeys.StateMachines.Tests\NXKeys.StateMachines.Tests.csproj -c Release
dotnet run --project .\NX2512_HotkeyStudio.Tests\NX2512_HotkeyStudio.Tests.csproj -c Release
```

Для Bridge дополнительно нужен contract build, а для доказательства интерактивного поведения — runtime test на целевой NX 2512.
