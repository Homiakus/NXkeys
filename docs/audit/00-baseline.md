# Audit Baseline (Stage 0) — project state before remediation

**Date:** July 24, 2026  
**Project Path:** `d:\Programms\NXkeys`  
**Target Platform:** Windows 10/11 x64, Siemens NX 2512, .NET 8.0 SDK  

---

## 1. Environment & Tooling Snapshot

| Tool / Runtime | Detected Version / Status | Notes |
|---|---|---|
| Operating System | Windows (x64) | Tested on PowerShell 7 environment |
| .NET SDK | 8.0.x x64 | Installed & confirmed via `dotnet --version` |
| Node.js | v18+ | Used for command tree validation scripts |
| C# Compiler | Roslyn (.NET 8 SDK) | Platform target set to `x64` |
| Siemens NX SDK | Contract Stubs (`NXOpen.dll`, `NXOpenUI.dll`) | Built in `artifacts/nxopen-contract` |

---

## 2. Git & File System Baseline Status

* **Active Branch:** Main working tree (`d:\Programms\NXkeys`).
* **Uncommitted Changes:** Working tree prepared for baseline documentation.
* **Go Migration Residuals:** `go.sum` exists in root folder (Go code removed per `BUILD_REPORT.md` and enforced in `ci.yml`).

---

## 3. Baseline Verification Results

| Verification Action | Command | Result | Notes |
|---|---|---|---|
| JSON & Profile Validation | `node .\scripts\validate-command-tree.mjs` | **PASS** | 12 basic shortcuts, 14 modules, 112 commands verified |
| Automaton & State Invariants | `dotnet run --project .\NXKeys.StateMachines.Tests` | **PASS** | 17 invariants & replay sessions passed |
| Contract Stubs Build | `dotnet build .\NX2512_CommandBridge.Tests\NXOpenUI` | **PASS** | Created `NXOpen.dll` and `NXOpenUI.dll` stubs |
| HotkeyStudio Build | `dotnet build .\NX2512_HotkeyStudio -c Release` | **PASS** | Executable and models built cleanly |
| ControlCenter Build | `dotnet build .\NX2512_ControlCenter -c Release` | **PASS** | WPF/WinForms dashboard built cleanly |
| CommandBridge Build | `dotnet build .\NX2512_CommandBridge -p:NXOpenDir=...` | **PASS** | In-process DLL compiled against stubs |
| CatalogStudio Build | `dotnet build .\NX2512_Catalog_Studio -p:NXOpenDir=...` | **FAIL** | `CS0117: 'Session.LibraryUnloadOption' does not contain definition for 'Immediately'` |
| CLI Schema Validation | `NX2512_HotkeyStudio.exe validate --config ...` | **PASS** | Schema v3 canonical profile passed |

---

## 4. Discovered Defect Register Summary (Stage 0)

1. **`DEF-001` (CatalogStudio Stub Mismatch):** `NX2512_Catalog_Studio` relies on `Session.LibraryUnloadOption.Immediately`, which was missing from the stub `NX2512_CommandBridge.Tests/NXOpen/Api.cs`.
2. **`DEF-002` (Residual Files):** Root folder retains `go.sum` from legacy Go implementation.
3. **`DEF-003` (Audit Docs Missing):** `docs/audit/` directory structure did not exist prior to this baseline initialization.

---

## 5. Execution Snapshot & Environment Variables

Required Environment Setup (Runtime):
* `UGII_CUSTOM_DIRECTORY_FILE` points to `%LOCALAPPDATA%\NXKeys\managed\NX2512.6000\custom_dirs.dat`.
* IPC Directory: `%LOCALAPPDATA%\NXKeys\bridge` (`pending/`, `processing/`, `completed/`, `failed/`, `context.json`, `status.json`).
