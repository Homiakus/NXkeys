# Feature Catalog (Stage 2) — Unified Inventory of Features & Requirements

**Date:** July 24, 2026  
**Repository Path:** `d:\Programms\NXkeys`  

---

## 1. System Features & Capabilities Catalog

| Feature ID | Feature Name | Description / Source | Entry Point | Status | Priority | Key Components |
|---|---|---|---|---|---|---|
| **F-001** | Basic Shortcuts Policy | Hard-coded enforcement of 12 barrier system hotkeys (`Ctrl+S`, `Ctrl+Z`, `Ctrl+C`, etc.) | Win32 Hook | `WORKING` | Critical | `BasicShortcutPolicy.cs`, `User32.dll` |
| **F-002** | Adaptive Leader Key | Overlay activation (`CapsLock`) with 8 ergonomic key positions (`QWE/A·D/ZXC`) | `CapsLock` | `WORKING` | Critical | `LeaderKeyEngine.cs`, `LeaderHudForm.cs` |
| **F-003** | Contextual Module Resolution | Automatic resolution of active NX module (Modeling, Sketch, Drafting, etc.) and updating of key mappings | `context.json` / Bridge | `WORKING` | Critical | `AdaptiveModuleResolver.cs`, `NxBridgeContext` |
| **F-004** | Module Search Mode | Fuzzy interactive search for commands within active module scope | `Space` key in Leader HUD | `WORKING` | High | `LeaderHudForm.cs`, `LeaderStateMachines.cs` |
| **F-005** | Destructive Guard Confirmation | Safety confirmation prompt requiring `Enter` key before executing destructive commands | `AwaitingConfirmation` state | `WORKING` | High | `ContextGuardEvaluator.cs` |
| **F-006** | Out-of-Process IPC Bridge | Asynchronous file-based request queue (`pending`, `processing`, `completed`, `failed`) | `%LOCALAPPDATA%\NXKeys\bridge` | `WORKING` | Critical | `NxProtocol.cs`, `NX2512_CommandBridge` |
| **F-007** | In-Process NX Command Execution | Invocation of Siemens NX UI `BUTTON ID` actions via NXOpen API | `NX2512_CommandBridge.dll` | `WORKING` | Critical | `NX2512_CommandBridge/Program.cs` |
| **F-008** | Transactional Deployment Engine | Managed package installation, SHA-256 staging checks, staging backup, and atomic rollback | PowerShell / C# CLI | `WORKING` | High | `DeploymentEngine.cs`, `install-nx-ribbon-buttons.ps1` |
| **F-009** | Adaptive Control Center Dashboard | Monitoring dashboard for command coverage, IPC status, and API crosswalk | `NX2512_ControlCenter.exe` | `WORKING` | High | `ControlCenterForm.cs` |
| **F-010** | NX Function & API Catalog Studio | Assembly & header inspection utility producing UI-to-API candidate mappings | `NX2512_CatalogStudio.exe` | `BROKEN` | Medium | `CatalogStudioForm.cs`, `NX2512_FullFunctionCatalog.cs` |
| **F-011** | C# CLI Management Tools | Command-line validation, scan, catalog search, planning, health checks, and backup/restore | `NX2512_HotkeyStudio.exe [args]` | `WORKING` | High | `NX2512_HotkeyStudio/Program.cs` |
| **F-012** | Command Tree Schema Validation | Node.js script for checking JSON v3 profile rules, key slot uniqueness, and command counts | `node scripts/validate-command-tree.mjs` | `WORKING` | High | `scripts/validate-command-tree.mjs` |
