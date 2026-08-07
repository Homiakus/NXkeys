# Configuration and profile contract

## Load sequence

```text
read JSON
→ explicit migration
→ JSON Schema
→ semantic validation
→ environment validation
→ model snapshot
→ resolved DrawingPlan
```

The source file is not silently rewritten during migration. A user save is atomic and creates `.bak`.

## Versioning

Current schema version: `1.0.0`.

- unknown major versions are rejected;
- future versions are rejected;
- known legacy versions are migrated explicitly;
- migration provenance is included in the execution report.

## Unknown properties

`execution.allowUnknownJsonProperties=false` is the safe default. Unknown fields are errors in strict schema objects. Extension fields must be placed in an explicitly documented extension section rather than added beside stable properties.

## Required identity

```json
{
  "profileId": "ORGANIZATION_PROFILE",
  "job": {
    "jobId": "UNIQUE_DOCUMENT_JOB",
    "document": {
      "designation": "...",
      "name": "..."
    }
  }
}
```

`jobId` scopes managed NX objects. Reusing it for an unrelated drawing is prohibited.

## Safety settings

The runtime requires:

```json
{
  "execution": {
    "singleUndoTransaction": true,
    "rollbackOnError": true,
    "preserveManualObjects": true,
    "allowUnknownJsonProperties": false
  }
}
```

Stale deletion additionally requires:

```json
{
  "execution": {
    "idempotency": {
      "deleteManagedObjectsMissingFromConfig": true,
      "confirmManagedDeletion": true
    }
  }
}
```

Use confirmation only after Preview shows the exact stale list.

## Views

Every view must have a unique stable `id`. Projected, section and detail views must reference a valid earlier parent. Circular dependencies are rejected.

Use explicit `placement.preferredAnchor` and `direction`; do not encode behavior in localized names.

## Templates

Every sheet references `templateId`. A production template must satisfy `docs/TEMPLATE_CONTRACT.md`. Changing the template of an existing managed sheet requires an explicit recreate policy; silent replacement is blocked.

## Validation

`validation.checks` is executable configuration. An enabled unknown rule produces `VAL_RULE_UNIMPLEMENTED` and is not treated as passed.

## Planned fields

A JSON field is not an implementation guarantee. Consult `docs/CAPABILITY_MATRIX.md`. Planned fields must be labelled in Configurator and produce diagnostics rather than being silently ignored.
