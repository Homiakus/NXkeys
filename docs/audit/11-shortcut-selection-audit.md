# NX Shortcut and Selection Audit - 2026-07-28

## Scope

- Source profile: `config/nx2512-pro-hybrid.json`
- Selection policy: `config/nx2512-state-machines.json`
- Runtime bridge: `%LOCALAPPDATA%\NXKeys\bridge`
- Managed package: `%LOCALAPPDATA%\NXKeys\managed\NX2512.6000`
- Live NX process observed: `ugraf.exe` PID 1588

## Findings

1. The canonical profile had broad `requires_selection` markers on 69 commands, but only a small subset had declarative guard coverage. That made many commands fail before NX could open its own selection workflow.
2. Selection filters were represented as normal `UG_SEL_*` button execution. NX exposes direct global selection filter APIs, so filter commands must not depend on `InvokeMenuButtonAction`.
3. Mnemonic paths were correct but too long for frequent work. Primary commands kept `input_key`, but runtime path generation did not preserve it as a first-class alias.
4. Source package and live managed package can diverge while NX is running. The current NX session locks `custom\startup\NX2512_CommandBridge.dll`, so final live deployment requires closing NX.

## Changes Made

- Added explicit command metadata: `action` and `selection_type`.
- Preserved one-key aliases for every primary module command.
- Preserved submenu aliases such as `1W` for selection filter groups.
- Routed all `UG_SEL_*` commands through `set_selection_filter`.
- Added bridge-side NXOpen global selection filters using `SetEnabledGlobalFilterMembers`, `ResetEnabledGlobalFilterMembers`, and `ClearGlobalSelectionList`.
- Changed `requires_selection` semantics so normal NX selection workflows can launch without preselection.
- Kept hard preselection blocking for commands that truly operate on already selected objects.
- Added validator checks for aliases, selection filter actions, `selection_type`, protocol `selection_filter`, and bridge filter support.

## Audit Results

- Basic shortcuts: 12
- Enabled modules: 14
- Module commands in source profile: 277
- Derived runtime sequences after aliases: 526
- Selection filter commands: 112
- Selection-aware commands with explicit `selection_type`: 69 / 69
- Primary commands missing one-key alias: 0
- Runtime NX queue probe: 161 unique command IDs submitted; all 161 were rejected by runtime context because the observed NX session had an active modal dialog or changed context revision during the probe. This artifact proves bridge/context communication and captures the blocking condition; it is not a successful command-execution pass.
- Probe artifact: `docs/audit/runtime-command-probe-2026-07-28.json`

## Verification

- `node .\scripts\validate-command-tree.mjs` - PASS
- `dotnet run --project .\NXKeys.StateMachines.Tests\NXKeys.StateMachines.Tests.csproj --nologo` - PASS
- `dotnet build .\NX2512_HotkeyStudio\NX2512_HotkeyStudio.csproj -c Release --nologo -p:NXOpenDir="C:\Program Files\Siemens\DesigncenterNX2512\NXBIN\managed"` - PASS
- `dotnet build .\NX2512_CommandBridge\NX2512_CommandBridge.csproj -c Release --nologo -p:NXOpenDir="C:\Program Files\Siemens\DesigncenterNX2512\NXBIN\managed"` - PASS
- `.\NX2512_CommandBridge\build.ps1 -NxOpenDll "C:\Program Files\Siemens\DesigncenterNX2512\NXBIN\managed\NXOpen.dll"` - PASS
- `.\NX2512_HotkeyStudio\build.ps1` - PASS
- `.\NX2512_HotkeyStudio\dist\NX2512_HotkeyStudio.exe health --config .\config\nx2512-pro-hybrid.json` - PASS for current installed package

## Deployment Note

Attempted live install:

```powershell
.\NX2512_HotkeyStudio\dist\NX2512_HotkeyStudio.exe apply --config .\config\nx2512-pro-hybrid.json --yes --allow-running-nx
```

The install was blocked because NX holds:

```text
%LOCALAPPDATA%\NXKeys\managed\NX2512.6000\custom\startup\NX2512_CommandBridge.dll
```

Writable files touched before the lock were restored from backup `20260728_062615.813`, and health returned to `Managed package: OK`.

Final deployment command after closing NX:

```powershell
.\NX2512_HotkeyStudio\dist\NX2512_HotkeyStudio.exe apply --config .\config\nx2512-pro-hybrid.json --yes
```
