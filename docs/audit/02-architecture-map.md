# Architecture Map (Stage 1) — Out-of-Process / In-Process Control & Data Flow

**Date:** July 24, 2026  
**Repository Path:** `d:\Programms\NXkeys`  

---

## 1. System Architecture Overview

NXKeys adopts an isolated, out-of-process control daemon pattern paired with an in-process C# DLL inside Siemens NX to ensure system stability.

```mermaid
flowchart TB
    subgraph OutOfProcess["Out-of-Process (.NET 8 Runtime)"]
        Hook["Win32 Keyboard Hook (User32.dll)"]
        EventQueue["Keyboard Event Queue"]
        DFA["DFA Sequence Evaluator"]
        HFSM["Leader HFSM Engine"]
        HUD["Overlay HUD (WinForms TopMost)"]
        CLI["Hotkey Studio CLI & Daemon"]
        ControlCenter["Control Center GUI Monitor"]
    end

    subgraph IPC["File-Oriented IPC Queue (%LOCALAPPDATA%\\NXKeys\\bridge)"]
        Pending["pending/*.json"]
        Processing["processing/*.json"]
        Completed["completed/*.json"]
        Failed["failed/*.json"]
        Context["context.json"]
        Status["status.json"]
    end

    subgraph InProcess["In-Process Siemens NX 2512 (.NET 8 DLL)"]
        Bridge["NX2512_CommandBridge.dll"]
        ContextMonitor["Active Module & Selection Exporter"]
        Dispatcher["NXOpen / UI Action Invoker"]
        SiemensNX["Siemens NX 2512 Process"]
    end

    Hook --> EventQueue
    EventQueue --> DFA
    DFA --> HFSM
    HFSM --> HUD
    HFSM -->|Dispatch Request| Pending
    Context -->|Read Context| HFSM
    Status -->|Bridge Status| ControlCenter
    
    Pending --> Bridge
    Bridge --> Processing
    Bridge --> ContextMonitor
    ContextMonitor --> Context
    Bridge --> Dispatcher
    Dispatcher --> SiemensNX
    SiemensNX --> Bridge
    Bridge -->|Operation Done| Completed
    Bridge -->|Operation Fail| Failed
    Completed --> HFSM
    Failed --> HFSM
```

---

## 2. HFSM State Transition Specifications

The HFSM (Hierarchical Finite State Machine) regulates user key combinations:

1. **`Idle`**: Hotkey engine listens for `CapsLock` press.
2. **`Root`**: `CapsLock` pressed. Overlay HUD displays 8 contextual commands (`QWE/A·D/ZXC`) for the currently active NX module (e.g., Modeling).
3. **`Prefix` / `Search`**: User types `Space` to initiate fuzzy search across module commands.
4. **`AwaitingConfirmation`**: Target command has destructive side-effects (e.g., Delete/Trim). System waits for `Enter` confirmation key.
5. **`Dispatching` / `AwaitingResult`**: Command JSON request written to IPC `pending/` directory. System waits for `completed/` or `failed/` notification.
6. **`SwitchingModule`**: User changes Siemens NX application (e.g. from Modeling to Sketch). `CommandBridge` updates `context.json`, resetting HFSM to the new active module scope.
7. **`Failed`**: Error captured during validation or IPC timeout. Error notification displayed on HUD; state automatically resets to `Idle`.

---

## 3. IPC Queue Mechanics & Safety Controls

* **Request Expiry:** Requests contain an `expires_utc` timestamp and `expected_context_revision`. If Siemens NX is busy or frozen, expired requests are marked `failed` without execution.
* **Context Guards:** `ContextGuardEvaluator` checks if the current NX module matches the target command's module scope. Cross-module command execution is strictly blocked.
* **Safety Boundary:** basic OS short-cuts (`Ctrl+S`, `Ctrl+Z`, `Ctrl+C`, etc.) are hard-coded in `BasicShortcutPolicy` (12 barrier shortcuts) and cannot be overridden by module JSON files.
