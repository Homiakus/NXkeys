# ADR 0001: Safe NX command transaction

- Status: Accepted
- Date: 2026-08-03

## Context

NX drawing automation can partially mutate a WorkPart before a later builder, update, validation, save or export fails. File operations are not automatically included in NX Undo.

## Decision

Generate and Update use this order:

```text
profile validation
→ ModelSnapshot
→ operation DAG
→ capability/license preflight
→ ownership/duplicate preflight
→ visible Undo mark
→ exact-scope reconciliation
→ controlled mutations
→ one controlled NX update
→ configured postconditions
→ rollback on every blocking diagnostic
→ save
→ verified temporary export
→ atomic publish
```

The current WorkPart is checked before and after mutation stages. A builder has exactly one lifecycle owner. Save and export do not run after a failed postcondition.

## Consequences

- Operations unsupported by the local maintenance release fail before or inside the rollback-safe scope.
- File publishing remains a separate phase and uses temporary files plus atomic move.
- A rollback flag is not release evidence; station tests compare before/after inventories.
- Commands may stop instead of using a speculative API fallback.
