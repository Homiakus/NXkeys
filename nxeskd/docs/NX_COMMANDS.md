# NX command contracts

## Inventory

- Does not require an open WorkPart.
- Scans loaded NXOpen and NXOpen.UF assemblies.
- Produces JSON and Markdown inventories.
- Does not modify NX objects.

## Preview

- Loads, migrates and validates the profile.
- Builds `ModelSnapshot`, scale selection and operation DAG.
- Lists planned creates/updates/deletes and ManualReview items.
- Does not enter mutation services.

## Validate

- Runs profile validation and configured NX validation rules.
- Unknown enabled rules are reported as unimplemented.
- Manual/unmanaged dimensions are excluded from managed-scope associativity checks.

## Generate

Precondition: no managed objects exist in the current profile/job scope.

Generate must not adopt existing sheets, views, Flat Pattern, Parts List or balloons by name. A collision is a blocking error.

## Update

Precondition: at least one managed object exists in the current profile/job scope.

Update synchronizes desired state, preserves configured manual edits, reconciles stale exact-owned objects and blocks unsupported kind/template changes.

## Command Center

- Starts Configurator out of process.
- Uses `ArgumentList`, not a manually quoted command line.
- The request is versioned, time-limited, profile-hash-bound and target-part-bound.
- A changed WorkPart invalidates the request.

## Exit codes

| Code | Meaning |
|---:|---|
| 0 | Successful command/report |
| 1 | Bootstrap or host error |
| 2 | Completed with blocking diagnostics |

The execution report is authoritative; UI message failure falls back to Listing Window and a LocalAppData log.
