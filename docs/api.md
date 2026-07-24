# NXKeys IPC API & File Queue Specification

**Version:** 3.0  
**Protocol Format:** JSON (UTF-8)  
**IPC Queue Location:** `%LOCALAPPDATA%\NXKeys\bridge`  

---

## 1. Overview

Communication between the out-of-process daemon (`NX2512_HotkeyStudio.exe`) and the in-process Siemens NX plugin (`NX2512_CommandBridge.dll`) is mediated via atomic file operations in a shared IPC directory.

```text
%LOCALAPPDATA%\NXKeys\bridge\
├── pending/        # Request files written by Hotkey Studio (e.g., req_1721800000000_a1b2.json)
├── processing/     # Requests currently being executed by CommandBridge
├── completed/      # Successfully completed request results
├── failed/         # Failed request results with diagnostic error details
├── context.json    # Active Siemens NX context published by CommandBridge
└── status.json     # CommandBridge health and heartbeat metrics
```

---

## 2. IPC Request Format (`pending/*.json`)

Written by `NX2512_HotkeyStudio` when a user triggers a Leader Key command.

```json
{
  "request_id": "req_1721800000000_a1b2",
  "created_utc": "2026-07-24T07:30:00.0000000Z",
  "expires_utc": "2026-07-24T07:30:05.0000000Z",
  "module_id": "MOD_MODELING",
  "button_id": "UG_MODELING_EXTRUDE",
  "action_name": "Extrude",
  "expected_context_revision": 14,
  "expected_selection_count": 0,
  "confirmation_accepted": true
}
```

### Field Definitions

| Field | Type | Description | Mandatory | Validation Rules |
|---|---|---|---|---|
| `request_id` | String | Unique request identifier | Yes | Non-empty alphanumeric string |
| `created_utc` | String (ISO-8601) | UTC timestamp of creation | Yes | Must be valid ISO-8601 timestamp |
| `expires_utc` | String (ISO-8601) | Request expiration time | Yes | Expiration timeout (default: 5000 ms) |
| `module_id` | String | Active module ID | Yes | Must match registered module ID |
| `button_id` | String | Target Siemens NX `BUTTON ID` | Yes | Non-empty string matching menu command |
| `action_name` | String | Human-readable action name | Yes | Informational label |
| `expected_context_revision` | Integer | Expected context revision counter | Yes | Must match `context.json` revision |
| `expected_selection_count` | Integer | Minimum required object selection | No | Default `0` |
| `confirmation_accepted` | Boolean | Confirmation flag for destructive ops | Yes | `true` if operation required & accepted |

---

## 3. IPC Response Formats (`completed/*.json` / `failed/*.json`)

Written by `NX2512_CommandBridge` upon operation completion or failure.

```json
{
  "request_id": "req_1721800000000_a1b2",
  "status": "completed",
  "button_id": "UG_MODELING_EXTRUDE",
  "duration_ms": 12,
  "executed_utc": "2026-07-24T07:30:00.0120000Z",
  "error_message": null,
  "interrupted_unknown": false
}
```

If execution fails or is interrupted:

```json
{
  "request_id": "req_1721800000000_a1b2",
  "status": "failed",
  "button_id": "UG_MODELING_EXTRUDE",
  "duration_ms": 5000,
  "executed_utc": "2026-07-24T07:30:05.0000000Z",
  "error_message": "Request expired prior to execution in NX",
  "interrupted_unknown": true
}
```

---

## 4. Context Export Format (`context.json`)

Continuously updated by `NX2512_CommandBridge` when the Siemens NX active application or selection state changes.

```json
{
  "revision": 14,
  "timestamp_utc": "2026-07-24T07:30:00.0000000Z",
  "application_id": "UG_APP_MODELING",
  "module_id": "MOD_MODELING",
  "selection_count": 0,
  "active_part_name": "model1.prt",
  "has_active_part": true,
  "is_modal_dialog_open": false,
  "bridge_version": "2.0.0"
}
```

---

## 5. Bridge Status Format (`status.json`)

Heartbeat and diagnostic status written by `NX2512_CommandBridge`.

```json
{
  "bridge_active": true,
  "heartbeat_utc": "2026-07-24T07:30:00.0000000Z",
  "processed_requests_count": 42,
  "failed_requests_count": 0,
  "nx_process_id": 12344,
  "nx_version": "2512.6000"
}
```
