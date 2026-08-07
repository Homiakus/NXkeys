# Development guide

## Architectural direction

```text
NX command entrypoint
→ CommandHost
→ Profile load / migration / schema validation
→ NX ModelSnapshot
→ DrawingPlanner / operation DAG
→ capability and ownership preflight
→ Undo transaction
→ reconciliation
→ NX services
→ postconditions
→ save / atomic export
→ execution report
```

`NxEskd.Core` must not reference NXOpen, WPF, environment-specific UI or mutable NX objects.

`NxEskd.NxRuntime` owns all NX Open calls. Stable and compiled contracts should use typed NXOpen API. Reflection is permitted only in the compatibility boundary when a maintenance-release variation is documented and covered by inventory diagnostics.

`NxEskd.Configurator` runs out of process. It communicates only through versioned request/profile files and must never retain NX objects.

## Correctness priorities

1. Model and user-data safety.
2. Exact NX maintenance-release compatibility.
3. Deterministic and explainable planning.
4. Idempotency and ownership.
5. Diagnostics and rollback evidence.
6. Performance.
7. UI and style.

## Adding an NX operation

1. Add a typed desired-state model in Core.
2. Add semantic validation and dependencies.
3. Add a `DrawingOperation` with preconditions.
4. Extend capability preflight before Undo.
5. Resolve existing exact-owned objects through the registry.
6. Reject manual/foreign collisions.
7. Use a single Builder owner.
8. Verify required setters before commit.
9. Run postcondition read-back.
10. Add Core tests and an NX station scenario.
11. Update `CAPABILITY_MATRIX.md`.

## Builder rules

- Never call legacy `NxReflection.Commit`.
- Factory builders use `CommitObjectAndDestroy`.
- Edit/command builders use `CommitCommandAndDestroy`.
- A caller may destroy a builder only if ownership has not been transferred to a commit helper.
- No service may catch an NX execution error and try another side-effecting overload.

## Reflection rules

- Alias order is explicit.
- Compatible methods are resolved before invocation.
- Required parameters are never fabricated.
- A compatible method that began execution and failed terminates the operation.
- Void success is not a created NX object.
- Reflection failures include type, candidate method and requested arguments in diagnostics.

## Ownership rules

```text
profileId + scope/jobId + objectKind + logicalId
```

- Read back all required attributes.
- Do not adopt by name alone.
- Legacy objects without scope are preserved and require explicit migration.
- Duplicate keys block before Undo.
- Deletion is exact-scope only and separately confirmed.

## Testing before commit

```powershell
dotnet test .\tests\NxEskd.Core.Tests\NxEskd.Core.Tests.csproj -c Release
dotnet run --project .\tests\NxEskd.SmokeTests\NxEskd.SmokeTests.csproj -c Release -- .\config\active-profile.example.json
```

Runtime changes additionally require `docs/NX2512_6000_STATION_TESTS.md` on the target station.

## Commit policy

- One coherent defect or capability per commit.
- No Siemens assemblies, customer models, credentials or generated `dist` files.
- Do not weaken rollback, ownership, schema or signature checks to make a test pass.
- Commit messages use `fix:`, `feat:`, `test:`, `docs:`, `build:`, `security:` or `refactor:`.
