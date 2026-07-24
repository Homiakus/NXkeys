# Project Inventory (Stage 1) — Repository Structure & Component Map

**Date:** July 24, 2026  
**Repository Path:** `d:\Programms\NXkeys`  

---

## 1. Primary Components & Projects

```text
d:\Programms\NXkeys
├── NX2512_HotkeyStudio/       # [C# .NET 8 WinForms/CLI] Win32 Keyboard Hook, Leader Engine, HUD, Deployment Engine
├── NX2512_CommandBridge/      # [C# .NET 8 DLL] In-Process Siemens NX Plugin, IPC Execution Bridge
├── NX2512_CommandBridge.Tests/ # [C# .NET 8] Siemens NXOpen/NXOpenUI Contract Assembly Stubs
├── NX2512_ControlCenter/      # [C# .NET 8 WinForms] Monitoring Dashboard, Command Coverage Explorer, Bridge Diagnostics
├── NX2512_Catalog_Studio/     # [C# .NET 8 WinForms/Tool] Reflection & Export Utility for NXOpen/UFUN UI-to-API mapping
├── NXKeys.Protocol/           # [C# .NET 8 Library] IPC Message Models, Schema Serialization (context.json, status.json)
├── NXKeys.StateMachines/      # [C# .NET 8 Library] Deterministic DFA & HFSM Leader Engine, Context Guards
├── NXKeys.StateMachines.Tests/# [C# .NET 8] Generative tests, replay sessions, invariant checks for HFSM/DFA
├── config/                    # [JSON Schemas] Canonical profile `nx2512-pro-hybrid.json` and `nx2512-state-machines.json`
├── dist/                      # [Binaries] Compiled executables and fallback profile binaries
├── docs/                      # [Markdown & HTML] Technical specifications, architecture, and interactive command-tree.html
├── roles/                     # [Siemens NX Roles] Custom `.mtx` role export profiles
├── scripts/                   # [Node.js Scripts] `validate-command-tree.mjs` verification utility
├── install-nx-ribbon-buttons.ps1 # [PowerShell] System installer script for managed packaging
└── BUILD_REPORT.md            # [Markdown] Build status and architectural specification
```

---

## 2. Subsystem Descriptions & Responsibilities

| Project / Subsystem | Language & Tech | Responsibility | Key Classes / Files |
|---|---|---|---|
| **`NX2512_HotkeyStudio`** | C# .NET 8 / WinForms / Win32 | Global keyboard hook (`User32.dll`), Leader Key activation, overlay HUD, C# CLI commands (`validate`, `scan`, `catalog`, `plan`, `apply`, `health`, `launch`), transaction deployment engine (`DeploymentEngine.cs`), launcher wrapper (`NxRuntimeService.cs`). | [Program.cs](file:///d:/Programms/NXkeys/NX2512_HotkeyStudio/Program.cs), `LeaderHudForm.cs`, `DeploymentEngine.cs` |
| **`NX2512_CommandBridge`** | C# .NET 8 (x64 DLL) | In-process Siemens NX plugin. Watches IPC `pending/` directory, checks active application/selection state, invokes exact Siemens `BUTTON ID` via NXOpen, writes status to `completed/` or `failed/`. | `NX2512_CommandBridge/Program.cs` |
| **`NX2512_ControlCenter`** | C# .NET 8 / WinForms | Visual dashboard for command matrix coverage, real-time IPC bridge status monitor, Russian search interface for NX APIs. | [ControlCenterForm.cs](file:///d:/Programms/NXkeys/NX2512_ControlCenter/ControlCenterForm.cs) |
| **`NX2512_Catalog_Studio`** | C# .NET 8 | Automated reflection tool to dump all NXOpen assemblies, public members, UFUN functions, menu bar buttons, and generate `08_ui_command_api_candidates.csv` crosswalk. | `CatalogStudioForm.cs`, `NX2512_FullFunctionCatalog.cs` |
| **`NXKeys.Protocol`** | C# .NET 8 Library | IPC Contract specifications (`NxBridgeRequest`, `NxBridgeContext`, `NxBridgeStatus`), timestamp validation, JSON serialization rules. | [NxProtocol.cs](file:///d:/Programms/NXkeys/NXKeys.Protocol/NxProtocol.cs) |
| **`NXKeys.StateMachines`** | C# .NET 8 Library | DFA sequence evaluator and HFSM state machine engine (`Idle`, `Root`, `Prefix`, `Search`, `AwaitingConfirmation`, `Dispatching`, `SwitchingModule`, `Failed`). | `LeaderStateMachines.cs`, `LeaderBehaviorProfile.cs` |
| **`NXKeys.StateMachines.Tests`** | C# .NET 8 Test Project | Unit tests verifying deterministic DFA sequence transitions, state machine invariants, replay log verification, and safety guards. | `Program.cs`, `ProtocolInvariantTests.cs` |

---

## 3. Directory Map & Dead / Obsolete Code Assessment

* **Go Language Artifacts:** `go.sum` remains in root directory. Legacy Go implementation (`cmd/nxkeys`, `internal/*`, `go.mod`) was removed in prior refactoring iterations.
* **Redundant Install Directives:** Direct installation into Siemens user profiles (`DeployToSiemensUserProfiles`) and placing DLLs in `custom/startup` were removed and prohibited by deployment invariants in `ci.yml`.
