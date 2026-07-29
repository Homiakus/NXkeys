# Safe Audit: 885 NX 2512 Functions

Date: 2026-07-29
Mode: safe full audit, no mass execution of NX commands.

## Executive Summary

- Source scope is structurally valid: the versioned catalog contains 1169 intents and the production K3-K5 scope contains exactly 885 unique intents.
- Temporary K3-K5 compilation preserved all 885 unique `catalog_refs`, but without an installed `06_ui_commands_buttons.csv` only a subset can be enabled with exact BUTTON IDs.
- The live managed runtime is healthy as a package, but it is not the generated 885-intent profile: installed `nx2512-pro-hybrid.json` has 277 module rows and no `full_command_catalog` metadata.
- CommandBridge is loaded and context is fresh; the current context at evidence capture is Modeling with no modal dialog, pending=0, processing=0.
- The saved runtime probe from 2026-07-28 is not proof of function execution: all 161 probe attempts were rejected due to modal dialog or context revision changes.
- `validate-full-command-map.mjs` fails because operational docs are stale against the newer full-map contract.

## Coverage Matrix

Machine-readable matrix: `docs/audit/885-functions-coverage-matrix-2026-07-29.csv`

| Metric | Value |
|---|---:|
| Matrix rows | 885 |
| K3 | 445 |
| K4 | 371 |
| K5 | 69 |
| Generated rows | 2144 |
| Generated enabled rows | 473 |
| Generated unique catalog refs | 885 |
| Managed runtime rows | 277 |
| Managed runtime unique catalog refs | 0 |

### Runtime Readiness Buckets

| Bucket | Intents | Meaning |
|---|---:|---|
| `blocked_unresolved_disabled` | 592 | Compiler could not resolve exact BUTTON ID; command is disabled. |
| `button_id_present_in_bootstrap_runtime_but_not_traceable_to_885` | 214 | BUTTON ID exists in current bootstrap runtime, but installed profile has no 885 catalog ref. |
| `compiled_enabled_not_installed_in_managed_runtime` | 55 | Generated K3-K5 profile can enable it, but current managed profile does not contain that BUTTON ID. |
| `blocked_ambiguous_disabled` | 24 | Compiler found ambiguous candidates; command is disabled until resolved. |

## Profile Layer Audit

| Layer | Rows | Enabled | Unique catalog refs | Metadata | Finding |
|---|---:|---:|---:|---|---|
| Repo config | 277 | 277 | 0 | none | bootstrap/source profile |
| Repo root copy | 277 | 277 | 0 | none | bootstrap/source profile |
| Temp generated K3-K5 | 2144 | 473 | 885 | selected=885 | covers production intents |
| Managed runtime | 277 | 277 | 0 | none | installed bootstrap/adaptive profile, not 885 profile |

Temporary compiler stats:

```text
[main-command-map] Source: 1169; selected K3,K4,K5: 885.
[main-command-map] Rows: 2144; enabled: 473; support: 8.
[main-command-map] Profile: C:\Windows\TEMP\nxkeys-885-audit-BynFx4\nx2512-pro-main.generated.json
[main-command-map] Report: C:\Windows\TEMP\nxkeys-885-audit-BynFx4\main-profile-resolution.md
```

## Managed Package and Bridge

- Managed root: `C:\Users\KDFX Modes\AppData\Local\NXKeys\managed\NX2512.6000`
- Manifest checked files: 31; required missing: 0; hash mismatches: 0.
- MenuScript versions: startup men=VERSION 139, application men=VERSION 139, tbr=VERSION 170, rtb=VERSION 170.
- Catalog CSV `06_ui_commands_buttons.csv`: not found under repo or %LOCALAPPDATA%\NXKeys during audit scan.
- Bridge queue: pending=0, processing=0, completed=38, failed=436.
- Live context: module=modeling, application=UG_APP_MODELING, selection=0, modal_dialog_active=false.

Health command excerpt:

```text
Managed root: C:\Users\KDFX Modes\AppData\Local\NXKeys\managed\NX2512.6000
Expected custom dirs: C:\Users\KDFX Modes\AppData\Local\NXKeys\managed\NX2512.6000\custom_dirs.dat
UGII_CUSTOM_DIRECTORY_FILE[Process]: C:\Users\KDFX Modes\AppData\Local\Programs\NxHotkeys\2512\NxCustomization\custom_dirs.dat
UGII_CUSTOM_DIRECTORY_FILE[User]: C:\Users\KDFX Modes\AppData\Local\Programs\NxHotkeys\2512\NxCustomization\custom_dirs.dat
NX запущен: да
  ugraf[29804] C:\Program Files\Siemens\DesigncenterNX2512\NXBIN\ugraf.exe
MenuScript versions: OK
Bridge loaded: да
Bridge status: C:\Users\KDFX Modes\AppData\Local\NXKeys\bridge\status.json
Bridge context: C:\Users\KDFX Modes\AppData\Local\NXKeys\bridge\context.json
Context age: 0.3s
Bridge log: C:\Users\KDFX Modes\AppData\Local\NXKeys\logs\nx-command-bridge.log
  log: 2026-07-28 06:28:48.360 [1588] rejected: 20260728132848242-f9f5d07af0846NSTRAINT - NX context changed after the shortcut was accepted. Expected revision 4199, actual 4204.
  log: 2026-07-28 06:28:48.515 [1588] rejected: 20260728132848464-d651aa26390f5GVIEWFIT - NX context changed after the shortcut was accepted. Expected revision 4199, actual 4204.
  log: 2026-07-28 06:28:48.671 [1588] rejected: 20260728132848575-f3ff33b8b6bcLSINPART - NX context changed after the shortcut was accepted. Expected revision 4199, actual 4204.
  log: 2026-07-28 06:28:48.828 [1588] rejected: 20260728132848683-d710a9e57c452ATERIALS - NX context changed after the shortcut was accepted. Expected revision 4199, actual 4204.
  log: 2026-07-28 06:28:48.982 [1588] rejected: 20260728132848903-05cca642465cbNTTFRTRI - NX context changed after the shortcut was accepted. Expected revision 4199, actual 4204.
  log: 2026-07-28 06:28:49.266 [1588] rejected: 20260728132849008-bb6d0cd362d43WREFRESH - NX has an active modal dialog.
  log: 2026-07-28 06:28:49.445 [1588] rejected: 20260728132849339-47f8ebf2b23c3DEASSIGN - NX has an active modal dialog.
  log: 2026-07-28 23:23:12.491 [29804] NXKeys Command Bridge initialized. Pending=C:\Users\KDFX Modes\AppData\Local\NXKeys\bridge\pending
Bridge queue: pending=0, completed=38, failed=436
Managed package: OK
```

Bridge status excerpt:

```text
Bridge root: C:\Users\KDFX Modes\AppData\Local\NXKeys\bridge
Context: C:\Users\KDFX Modes\AppData\Local\NXKeys\bridge\context.json
Context age: 0.6s
Context module: modeling / Modeling
Context status: running
pending: 0
processing: 0
completed: 38
failed: 436
```

## Safe Runtime Probe Sample

Sample artifact: `docs/audit/safe-runtime-probe-sample-2026-07-29.json`.

| Command ID | Result |
|---|---|
| `UG_VIEW_FIT` | completed: availability=Available; sensitivity=Sensitive |
| `UG_APP_MODELING` | completed: availability=Available; sensitivity=Sensitive |
| `UG_CREATE_SKETCH` | completed: availability=Available; sensitivity=Sensitive |
| `UG_MODELING_EXTRUDED_FEATURE` | completed: availability=Available; sensitivity=Sensitive |
| `UG_SEL_FACE_PRIORITY` | completed: availability=Available; sensitivity=Sensitive |
| `UG_EDIT_UNDO` | completed: availability=Available; sensitivity=Sensitive |
| `UG_FILE_SAVE_PART` | completed: availability=Available; sensitivity=Sensitive |
| `UG_ANALYSIS_FACE_CURVATURE` | completed: missing: UG_ANALYSIS_FACE_CURVATURE; Кнопка не существует |

Summary: 7/8 sampled commands reported available and sensitive; 1 sampled command was missing. No command was executed through `InvokeMenuButtonAction`; the sample used Bridge `probe_command` only. Post-sample queue: pending=0, processing=0, completed=54, failed=436.

## Defect Register

| ID | Severity | Layer | Evidence | Fix | Verification |
|---|---|---|---|---|---|
| D-885-001 | Critical | Managed deployment | Installed profile has 277 rows, 0 `full_command_catalog` refs; generated K3-K5 has 885 refs. | Install `nx2512-pro-main.generated.json` through `install-nxkeys.ps1` so managed compatibility filename points to the generated profile. | Re-run matrix and verify managed unique refs = 885. |
| D-885-002 | High | Catalog resolution | No `06_ui_commands_buttons.csv` found; generated profile reports many unresolved/ambiguous rows. | Export current NX catalog with `NX2512_Catalog_Studio`, then compile/install with `-CatalogDir`. | Generated report shows lower unresolved/ambiguous count and no enabled command without BUTTON ID. |
| D-885-003 | Medium | Runtime proof | Old 2026-07-28 probe captured modal/context churn, but the new safe sample completed 8/8 probe requests; 7 were available/sensitive and `UG_ANALYSIS_FACE_CURVATURE` was missing. | Expand safe probe coverage after installing the generated 885 profile; keep full execution as a separate supervised audit. | Probe sample remains `completed`; no `InvokeMenuButtonAction` is used in safe mode. |
| D-885-004 | Medium | Documentation/CI | `validate-full-command-map.mjs` fails on missing current documentation markers. | Update operational docs or relax obsolete marker checks to match the actual contract. | `node .\scripts\validate-full-command-map.mjs` exits 0. |
| D-885-005 | Medium | Operator environment | `UGII_CUSTOM_DIRECTORY_FILE` env points to old `NxHotkeys` customization path while health expects managed `custom_dirs.dat`. | Align launch/runtime environment to managed `custom_dirs.dat` or document intentional override. | Health output has no unexpected custom-dir drift and NX loads managed custom files. |

## Remediation Priority

1. Export a fresh `06_ui_commands_buttons.csv` from the target NX 2512 installation.
2. Compile and install the main K3-K5 profile with `install-nxkeys.ps1 -CatalogDir <export>`, closing/restarting NX if bridge DLL or MenuScript files are updated.
3. Re-run `health`, `bridge-status`, and this matrix audit; require managed profile metadata `selected_intents: 885`.
4. Run a limited safe `probe_command` availability sample in clean Modeling/Gateway/Sketch contexts.
5. Only after the safe audit is clean, run a separate maximum-live audit for all enabled BUTTON IDs with operator supervision.

## Validation Results

| Check | Exit | Result |
|---|---:|---|
| validate-main-command-map | 0 | PASS: 885 K3-K5 source coverage confirmed |
| validate-full-command-map | 1 | FAIL: documentation marker drift |
| temp compile-main-command-map | 0 | PASS: generated 885 unique refs |
| HotkeyStudio health | 0 | PASS command completed; see findings for runtime state |
| HotkeyStudio bridge-status | 0 | PASS command completed |

Full raw evidence: `docs/audit/885-functions-safe-audit-evidence-2026-07-29.json`.
