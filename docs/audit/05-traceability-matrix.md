# Traceability Matrix (Stage 3) — Requirement to Test Traceability

**Date:** July 24, 2026  
**Repository Path:** `d:\Programms\NXkeys`  

---

## 1. Feature Traceability Chain

```text
Requirement -> UI / Entry Point -> Frontend / Leader Hook -> IPC Protocol -> Command Bridge -> NXOpen API -> Validation Test -> Status
```

---

## 2. Comprehensive Traceability Matrix

| Feature ID | Requirement / Description | UI / Key Trigger | Leader / Hook Handler | IPC Queue / Message | Backend / Bridge Exec | Data / Model | Automated Test | Status |
|---|---|---|---|---|---|---|---|---|
| **F-001** | Basic Shortcuts Policy | Win32 Hook | `KeyboardHook.cs` | None (Direct OS passthrough) | Direct Windows Dispatch | `BasicShortcutPolicy` | `ProtocolInvariantTests.cs` | `WORKING` |
| **F-002** | Adaptive Leader Key | `CapsLock` | `LeaderKeyEngine` | None | `LeaderHudForm.cs` | `LeaderBehaviorProfile` | `ReplayAndRandomizedTests.cs` | `WORKING` |
| **F-003** | Contextual Module Resolution | Active NX App change | `AdaptiveModuleResolver` | `context.json` | `CommandBridge.ExportContext()` | `NxBridgeContext` | `DeclarativePolicyTests.cs` | `WORKING` |
| **F-004** | Module Search Mode | `Space` in HUD | `LeaderHudForm.OnKeyDown` | None | Filtered Command List | `ModuleCommand` | `validate-command-tree.mjs` | `WORKING` |
| **F-005** | Destructive Guard Confirmation | Destructive Key (`Delete`, `Trim`) | `ContextGuardEvaluator` | `AwaitingConfirmation` state | Pause IPC Request | `NxBridgeRequest` | `ReplayAndRandomizedTests.cs` | `WORKING` |
| **F-006** | Out-of-Process IPC Bridge | Key slot select (`QWE/A·D/ZXC`) | `LeaderKeyEngine.Dispatch` | `pending/*.json` | File System Queue Watcher | `NxBridgeRequest` | `ProtocolInvariantTests.cs` | `WORKING` |
| **F-007** | In-Process NX Command Execution | Request file in `pending/` | `CommandBridge.ProcessQueue` | `completed/*.json` or `failed/*.json` | `UI.GetUI().Menu...Execute()` | `BUTTON ID` | `NX2512_CommandBridge.Tests` | `WORKING` |
| **F-008** | Transactional Deployment Engine | `install-nx-ribbon-buttons.ps1` | `DeploymentEngine` | `package-manifest.json` | Backup / Staging / Copy | SHA-256 Manifest | `ci.yml (Validate deployment)` | `WORKING` |
| **F-009** | Adaptive Control Center | GUI Application | `ControlCenterForm` | `status.json`, `context.json` | Visual Status Display | `NxBridgeStatus` | `ci.yml (Build Control Center)` | `WORKING` |
| **F-010** | NX Catalog Studio | Utility GUI | `CatalogStudioForm` | None | Reflection over `NXOpen.dll` | `08_ui_command_api_candidates.csv` | `CatalogStudio.csproj` compilation | `BROKEN` |
| **F-011** | C# CLI Management | Command Line | `Program.Main()` | `nx2512-pro-hybrid.json` | Execution of CLI verb | `ConfigModels.cs` | `ci.yml (Validate profile CLI)` | `WORKING` |
| **F-012** | Command Tree Schema Check | `validate-command-tree.mjs` | Node.js Runtime | `config/*.json` | Profile AST inspection | JSON Schema v3 | `validate-command-tree.mjs` | `WORKING` |

---

## 3. Discovered Gaps & Traceability Defect Summary

* **Defect `DEF-001` (CatalogStudio CS0117):** Feature **F-010** (`NX2512_Catalog_Studio`) broke compilation when compiled against test stubs due to a missing enum definition in `NX2512_CommandBridge.Tests/NXOpen/Api.cs`. Target fix: Remediation Package `R-001`.
