# Remediation Plan (Stage 10) — Package Execution Roadmap

**Date:** July 24, 2026  
**Repository Path:** `d:\Programms\NXkeys`  

---

## 1. Remediation Strategy & Ordering

Remediation work is partitioned into small, independently verifiable work packages:

1. **`R-001` (Completed):** Unify C# contract stubs for `NX2512_Catalog_Studio` compilation against NXOpen stubs (`DEF-001`).
2. **`R-002` (In Progress):** Formalize IPC API contract documentation and schema specifications in `docs/api.md` (`DEF-003`).
3. **`R-003` (In Progress):** Remove legacy `go.sum` file to strictly enforce C#-only repository invariant (`DEF-002`).
4. **`R-004` (Planned):** Audit test coverage across unit, state machine, and contract test layers; generate `docs/audit/08-test-coverage.md`.
5. **`R-005` (Planned):** Perform security audit covering input sanitization, file permissions, and IPC message expiry; generate `docs/audit/09-security-review.md`.
6. **`R-006` (Planned):** Execute final verification suite and generate `docs/audit/10-final-verification.md`.

---

## 2. Package Details

### Package R-001 — Contract Stub Alignment
* **Goal:** Enable standalone offline compilation of `NX2512_Catalog_Studio`.
* **Affected Files:** `NX2512_CommandBridge.Tests/NXOpen/Api.cs`
* **Verification:** `dotnet build .\NX2512_Catalog_Studio\NX2512_CatalogStudio.csproj ...` (Passed: 0 Errors).

### Package R-002 — IPC API Contract Formalization
* **Goal:** Document all JSON file structures (`pending/*.json`, `context.json`, `status.json`) with validation rules.
* **Affected Files:** `docs/api.md`
* **Verification:** Check C# protocol models against `docs/api.md` specification.

### Package R-003 — Clean Repository Invariant Enforcement
* **Goal:** Remove legacy `go.sum` leftover file.
* **Affected Files:** `go.sum` (delete)
* **Verification:** `ci.yml` step "Verify C#-only source tree".
