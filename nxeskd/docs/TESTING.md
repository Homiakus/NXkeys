# Testing strategy

## Test pyramid

1. **Core xUnit** — schema, migration, validation, planning, DAG, scale, paths and orchestration.
2. **Core smoke** — complete example profile, package-source invariants and dry-run.
3. **Structural checks** — JSON syntax, forbidden NX assemblies, version and manifest contracts.
4. **NX station integration** — actual builders, Undo, PMI, BOM, Flat Pattern, save and export.
5. **Engineering acceptance** — visual and ESKD review of generated documents.

## Core commands

```powershell
dotnet test .\tests\NxEskd.Core.Tests\NxEskd.Core.Tests.csproj -c Release --collect:"XPlat Code Coverage"
dotnet run --project .\tests\NxEskd.SmokeTests\NxEskd.SmokeTests.csproj -c Release -- .\config\active-profile.example.json
```

## Required Core regression cases

- unknown schema property;
- unsupported/future profile version;
- known migration provenance;
- duplicate sheet/view IDs;
- missing parent and dependency cycle;
- auto-scale from bounding box;
- Unicode and spaces in paths;
- dry-run does not call Execute/Export;
- atomic save creates exact backup;
- source contains no legacy `NxReflection.Commit` or double-destroy pattern.

## NX integration evidence

Every station scenario stores:

- fixture hash;
- build-info and API inventory;
- before/after object inventory;
- execution report;
- NX syslog;
- saved result hash;
- operator verdict and ManualReview closure.

See `NX2512_6000_STATION_TESTS.md`.

## Failure policy

- Any P0/P1 failure blocks release.
- An unimplemented enabled validation rule blocks acceptance.
- A rollback flag without matching before/after inventory is not proof.
- A visually plausible drawing with failed postconditions is a failed test.
- Tests must never mutate the fixture source; only isolated copies are opened.
