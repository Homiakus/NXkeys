# Test Coverage Report (Stage 12) — Verification & Invariant Analysis

**Date:** July 24, 2026  
**Repository Path:** `d:\Programms\NXkeys`  

---

## 1. Test Pyramid Coverage Summary

| Test Level | Target Component | Test Runner / Command | Passed Tests | Status |
|---|---|---|---|---|
| **Unit / Invariants** | State Machine DFA/HFSM (`NXKeys.StateMachines`) | `dotnet run --project .\NXKeys.StateMachines.Tests` | 17 invariants | **PASS** |
| **Profile Validation** | JSON Schema v3 Profile (`config/nx2512-pro-hybrid.json`) | `node .\scripts\validate-command-tree.mjs` | 112 commands, 14 modules | **PASS** |
| **CLI Contract Validation** | Hotkey Studio CLI (`NX2512_HotkeyStudio.exe`) | `NX2512_HotkeyStudio.exe validate --config ...` | Schema v3 check | **PASS** |
| **Contract Assembly Stubs** | NXOpen / NXOpenUI C# Contract Stubs | `dotnet build .\NX2512_CommandBridge.Tests\NXOpenUI` | Stubs compiled clean | **PASS** |
| **Component Build** | All C# Projects (`HotkeyStudio`, `CommandBridge`, `ControlCenter`, `CatalogStudio`) | `dotnet build` (Release x64) | 4 assemblies compiled clean | **PASS** |

---

## 2. Tested Invariants

1. **Basic Shortcut Policy:** 12 barrier system hotkeys are hardcoded and preserved.
2. **Deterministic Sequence Automaton (DFA):** DFA recognizes exact key sequences, rejects invalid branches, and blocks duplicate key registrations.
3. **HFSM State Transitions:** `Esc` key and timeouts reliably return system to `Idle`. Destructive commands require `Enter` key confirmation.
4. **Context Guard Safety:** Commands from inactive NX modules are strictly blocked.
5. **IPC Queue Invariants:** Requests include `expires_utc` and `expected_context_revision`; expired or stale requests are marked `failed`.
6. **Managed Deployment Invariants:** SHA-256 staging validation, staging backup manifest, and atomic rollback on failure.
