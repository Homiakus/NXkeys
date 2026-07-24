# Defect Register (Stage 9) — Complete Log of Project Defects

**Date:** July 24, 2026  
**Repository Path:** `d:\Programms\NXkeys`  

---

## 1. Summary Statistics

* **Total Defects Logged:** 3
* **Resolved / Fixed (`FIXED`):** 1
* **Open / Planned (`PLANNED`):** 2

---

## 2. Defect Register Table

| Defect ID | Feature ID | Severity | Description | Root Cause | Affected Files | Proposed Fix | Status |
|---|---|---|---|---|---|---|---|
| **DEF-001** | F-010 | Major | `NX2512_Catalog_Studio` failed to compile against test stubs (`CS0117`) | `Session.LibraryUnloadOption` missing `Immediately` enum member in test stub | `NX2512_CommandBridge.Tests/NXOpen/Api.cs` | Added `Immediately` and `Explicitly` values to enum | **FIXED** |
| **DEF-002** | Systems | Minor | Legacy artifact `go.sum` retained in root folder | Leftover from legacy Go implementation prior to C# migration | `go.sum` | Remove file to maintain clean C# repository invariant | **PLANNED** |
| **DEF-003** | F-006 | Minor | IPC bridge contracts lack standalone schema definition file | IPC JSON formats are documented in C# structs without standalone JSON schema | `NXKeys.Protocol/NxProtocol.cs` | Create explicit schema documentation / specification in `docs/api.md` | **PLANNED** |

---

## 3. Detailed Defect Reports

### DEF-001: CatalogStudio Compilation Failure against Stubs
* **Actual Behavior:** Compilation failed with `CS0117: 'Session.LibraryUnloadOption' does not contain a definition for 'Immediately'`.
* **Expected Behavior:** `NX2512_CatalogStudio.csproj` compiles with zero errors in automated build environments.
* **Reproduction Steps:** Run `dotnet build .\NX2512_Catalog_Studio\NX2512_CatalogStudio.csproj -c Release -p:Platform=x64 -p:NXOpenDir="<stub_path>"`.
* **Fix Verification:** Fixed via Remediation Package R-001 in `NX2512_CommandBridge.Tests/NXOpen/Api.cs`. Rebuilding produced 0 errors.

### DEF-002: Legacy Go Artifact
* **Actual Behavior:** `go.sum` remains in repository root.
* **Expected Behavior:** Repository contains exclusively C#, .NET 8, Node validation scripts, and documentation files.
* **Reproduction Steps:** File `go.sum` present in file listing.
* **Fix Plan:** Remove `go.sum` file.

### DEF-003: Formal IPC Protocol Contract Specification
* **Actual Behavior:** Protocol contracts (`NxBridgeRequest`, `NxBridgeContext`, `NxBridgeStatus`) are defined in C# source code, but lack a central API specification document.
* **Expected Behavior:** A formal API specification (`docs/api.md`) defines all message fields, validation constraints, and file queue protocol contracts.
* **Fix Plan:** Create `docs/api.md` and contract test verifications.
