# Локальная разработка NXKeys v8

## Среда

| Инструмент | Требование |
|---|---|
| .NET SDK | 8.x |
| Node.js | 20+ |
| PowerShell | 5.1 или 7 |
| Windows x64 | для WinForms/runtime/deployment |
| Siemens NX / Designcenter NX | 2512 для production Bridge/Catalog tests |

Node scripts используют стандартную библиотеку; обязательного `npm install` нет.

Перед изменением runtime прочитайте:

- [docs/RUNTIME_V8.md](docs/RUNTIME_V8.md);
- [docs/CHEATSHEET.md](docs/CHEATSHEET.md);
- [docs/SELECTION_INTENT.md](docs/SELECTION_INTENT.md);
- [docs/SKETCH_INTENT_LANGUAGE.md](docs/SKETCH_INTENT_LANGUAGE.md);
- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Current contracts

```text
profile schema 8
minimum readable profile 3
IPC schema 4
sequence policy v8
default runtime profile config/nx2512-v8-profile.json
```

## Что проверить без Siemens NX

### Documentation/profile validators

```powershell
node .\scripts\validate-documentation.mjs
node .\scripts\validate-main-command-map.mjs
node .\scripts\validate-command-tree.mjs
node .\scripts\validate-full-command-map.mjs
node .\scripts\audit-command-sequences.mjs
```

`audit-command-sequences.mjs` изменяет generated audit. Проверяйте semantic diff; timestamp-only churn не должен маскировать stale generation.

### State-machine/protocol tests

```powershell
dotnet run --project .\NXKeys.StateMachines.Tests\NXKeys.StateMachines.Tests.csproj -c Release
```

### HotkeyStudio regression tests

```powershell
dotnet run --project .\NX2512_HotkeyStudio.Tests\NX2512_HotkeyStudio.Tests.csproj -c Release
```

Current regression scope должен включать:

- schema v8 load;
- no-profile hardcoded v8 fallback;
- `secondary_aliases` expansion;
- Modeling Manage `M → L → S`;
- отсутствие terminal root `M` от workspace-only operation;
- Sketch `K → …` constraints;
- Sheet Metal `UG_SBSM_*` canonicalization/security permissions;
- IPC schema 4/security invariants.

### Desktop builds

```powershell
dotnet build .\NX2512_HotkeyStudio\NX2512_HotkeyStudio.csproj -c Release -p:Platform=x64 --nologo
dotnet build .\NX2512_ControlCenter\NX2512_ControlCenter.csproj -c Release -p:Platform=x64 --nologo
```

### Command Bridge contract build

```powershell
New-Item -ItemType Directory -Force .\artifacts\nxopen-contract | Out-Null

dotnet build .\NX2512_CommandBridge.Tests\NXOpenUI\NXOpenUI.csproj `
  -c Release -o .\artifacts\nxopen-contract --nologo

$nxOpenDir = (Resolve-Path .\artifacts\nxopen-contract).Path

dotnet build .\NX2512_CommandBridge\NX2512_CommandBridge.csproj `
  -c Release -p:Platform=x64 -p:NXOpenDir="$nxOpenDir" --nologo
```

Contract stubs подтверждают API shape, но не live NX behavior.

## Production Bridge build

```powershell
.\NX2512_CommandBridge\build.ps1 `
  -NxRoot "C:\Program Files\Siemens\NX2512" `
  -Clean
```

Либо используйте точный `-NxOpenDll`.

После изменения Bridge обязательно полностью перезапускайте NX перед integration test.

## Current v8 profile

Запуск из source:

```powershell
dotnet run --project .\NX2512_HotkeyStudio\NX2512_HotkeyStudio.csproj -- `
  --config .\config\nx2512-v8-profile.json
```

Если profile не передан и не найден, runtime строит hardcoded v8 fallback. Для production tests всегда предпочитайте explicit/versioned profile.

## Legacy/catalog generation pipeline

Репозиторий сохраняет полный K1–K5 intent catalog и generated K3–K5 profile pipeline:

```powershell
node .\scripts\compile-main-command-map.mjs `
  --profile .\config\nx2512-pro-hybrid.json `
  --intents .\config\full-command-map `
  --probe .\docs\audit\runtime-command-probe-2026-07-28.json `
  --out .\config\nx2512-pro-main.generated.json `
  --report .\docs\generated\main-profile-resolution.md
```

Этот pipeline нужен для coverage/resolution/compatibility исследований. Он не является default `install-nxkeys.ps1` profile source.

## `install-nxkeys.ps1`

Default `Resolve-Config` выбирает:

```text
config\nx2512-v8-profile.json
```

При schema < 8 installer сохраняет compatibility requirement generated K3–K5/885. Для schema 8 проверяется v8 operations profile.

`-CompileOnly` на default v8 profile только разрешает/проверяет profile и завершает работу; он не генерирует новый v8 JSON.

## Selection Intent development

Implementation:

```text
NX2512_CommandBridge/SelectionIntentHotkeys.cs
```

Modes:

```text
0 reset
1 single
2 connected/chain
3 tangent
4 inferred path/region boundary
```

При изменении:

1. не превращайте `0…4` в безусловные global hotkeys;
2. сохраняйте foreground/modifier/text-input/collector guards;
3. сохраняйте physical key latch;
4. обновляйте NXOpen contract stubs при новых API calls;
5. проверяйте native NX control IDs;
6. выполняйте live NX test во всех пяти modes.

## Sketch development

Current user grammar:

```text
L/R/C/A/T...       frequent one-token commands
K → …              constraints
D → …              dimensions
C → V → …          variants
J → …              projection/derived
U → …              utilities
```

Не восстанавливайте старые examples `CGL`, `C→L` как current user paths.

## Modeling Manage/workspace

`M` — Manage subtree. Regression example:

```text
M → L → S
```

Operation, имеющая только `workspace_key`, не должна появляться как root terminal без explicit workspace state.

## Sheet Metal

Используйте canonical NX2512 IDs `UG_APP_SBSM` / `UG_SBSM_*`. Compatibility mapping старых IDs должно быть одинаковым в runtime и security layer.

## IPC/security changes

При изменении `NxProtocol.cs`/`NxBridgeSecurity.cs` обновляйте согласованно:

- schema/JSON models;
- HMAC canonicalization;
- source-process/session/anti-replay logic;
- profile permission digest;
- Bridge/HotkeyStudio tests;
- `docs/api.md`, architecture и safety docs.

## Full pre-commit

```powershell
node .\scripts\validate-documentation.mjs
node .\scripts\validate-main-command-map.mjs
node .\scripts\validate-command-tree.mjs
node .\scripts\validate-full-command-map.mjs
node .\scripts\audit-command-sequences.mjs

dotnet run --project .\NXKeys.StateMachines.Tests\NXKeys.StateMachines.Tests.csproj -c Release
dotnet run --project .\NX2512_HotkeyStudio.Tests\NX2512_HotkeyStudio.Tests.csproj -c Release

dotnet build .\NX2512_HotkeyStudio\NX2512_HotkeyStudio.csproj -c Release -p:Platform=x64 --nologo
dotnet build .\NX2512_ControlCenter\NX2512_ControlCenter.csproj -c Release -p:Platform=x64 --nologo
```

Bridge changes additionally require contract build. NXOpen/interactive changes additionally require target NX 2512 integration test.

## Live NX acceptance

CI не может подтвердить:

- command sensitivity/license;
- actual application mapping corporate role;
- Selection Intent interaction with every collector;
- interactive dialog semantics;
- `DialogTester.InvokeMenuButtonAction` result semantics;
- destructive side effects.

Для таких изменений сохраняйте runtime evidence на тестовой детали.
