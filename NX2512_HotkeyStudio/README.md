# NX2512_HotkeyStudio

HotkeyStudio — основной desktop/tray runtime NXKeys. Current runtime использует profile schema **8**, adaptive module resolution и authenticated IPC schema **4**.

## Ответственность

- single-instance desktop/tray lifecycle;
- physical CapsLock latch и global Leader hook;
- adaptive NX module resolution;
- v8 profile loading/normalization;
- `secondary_aliases` expansion;
- prefix-free Leader DFA + HFSM;
- HUD/search;
- signed IPC client;
- CLI, scan, deployment, backup/restore и health;
- managed launch integration.

HotkeyStudio не доказывает фактическую sensitivity/effect NX command — последняя граница находится в Command Bridge/NX.

## Profile loading

`Config.Load`:

1. если profile отсутствует — создаёт hardcoded v8 configuration;
2. если JSON есть — проверяет source schema **3…8**;
3. десериализует v8/legacy fields;
4. разворачивает environment paths;
5. разворачивает `secondary_aliases`;
6. исключает workspace-only keys из root DFA без workspace-state;
7. применяет defaults/compatibility normalization;
8. строит runtime modules/sequences;
9. валидирует config.

`Config.CurrentSchemaVersion = 8`.

Default profile resolution в `Program.cs` ищет сначала:

```text
nx2512-v8-profile.json
```

затем compatibility filename:

```text
nx2512-pro-hybrid.json
```

## CapsLock

Leader trigger использует physical key latch: первый real key-down захватывается, autorepeat игнорируется до real key-up. `WM_HOTKEY` не должен дублировать уже захваченный physical trigger.

## V8 aliases/workspace keys

`paths.secondary_aliases` — реальные routing aliases.

Modeling example:

```text
M → L → S    Layer Settings
```

`workspace_key` не становится root command автоматически. Это предотвращает terminal/prefix conflict вроде `M` против `M → …`.

## Sketch routing

В active Sketch:

```text
L             Line
R             Rectangle
K → C         Coincident
D → Q         Rapid Dimension
C → V → …     variants
```

Hardcoded/no-profile fallback проецирует constraint overlay в Sketch, поэтому пользователь вводит `K → …`, а не отдельный модуль constraints.

## Сборка

Без NX:

```powershell
dotnet build .\NX2512_HotkeyStudio\NX2512_HotkeyStudio.csproj `
  -c Release -p:Platform=x64 --nologo
```

Distribution build с готовым Bridge artifact:

```powershell
.\NX2512_HotkeyStudio\build.ps1 `
  -ProfilePath .\config\nx2512-v8-profile.json `
  -Clean
```

Если нужен Catalog Studio export, передайте `-CatalogDir`.

Current installer всегда передаёт выбранный profile в HotkeyStudio `build.ps1` через `-ProfilePath`.

## Запуск из исходников

```powershell
dotnet run --project .\NX2512_HotkeyStudio\NX2512_HotkeyStudio.csproj -- `
  --config .\config\nx2512-v8-profile.json
```

Без `--config` runtime попробует auto-resolution, затем hardcoded fallback.

## CLI

```text
validate, scan, catalog, plan, apply, launch, leader,
backups, restore, bridge-status, health, icons, export-icons
```

Документация: [`docs/CLI.md`](../docs/CLI.md).

## Authenticated launch

`launch`/managed launcher создаёт shared security session для HotkeyStudio и NX/Bridge. Request signing использует session fields из `NXKeys.Protocol`.

Независимый запуск HotkeyStudio и NX не эквивалентен authenticated managed launch.

## Tests

```powershell
node .\scripts\validate-documentation.mjs
node .\scripts\validate-command-tree.mjs
node .\scripts\validate-main-command-map.mjs

dotnet run --project .\NX2512_HotkeyStudio.Tests\NX2512_HotkeyStudio.Tests.csproj -c Release
dotnet run --project .\NXKeys.StateMachines.Tests\NXKeys.StateMachines.Tests.csproj -c Release

dotnet build .\NX2512_HotkeyStudio\NX2512_HotkeyStudio.csproj -c Release -p:Platform=x64 --nologo
```

Regression scope должен включать:

- hardcoded/no-profile v8 fallback;
- Modeling Manage aliases;
- отсутствие terminal root `M` из workspace-only key;
- Sketch `K → …` routing;
- Sheet Metal canonicalization/security;
- protocol schema 4 tests.

## Ограничения

- WinForms/global hook требуют Windows;
- build без NX не подтверждает live Bridge semantics;
- фактическая команда зависит от NX role/license;
- интерактивный NX dialog result требует live test;
- старый K3–K5 compiler остаётся отдельным catalog/compatibility pipeline и не определяет default v8 runtime.

Канонический runtime: [`docs/RUNTIME_V8.md`](../docs/RUNTIME_V8.md).
