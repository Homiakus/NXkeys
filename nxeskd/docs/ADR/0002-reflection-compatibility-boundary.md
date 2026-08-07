# ADR 0002: Restrict reflection to a compatibility boundary

- Status: Accepted
- Date: 2026-08-03

## Context

NX 2512 maintenance releases may expose different builder names or overloads. Broad reflection across all Runtime services delays errors until execution and can invoke the wrong side-effecting overload.

## Decision

- Use typed NXOpen contracts whenever the exact API is compiled and station-verified.
- Use reflection only through `NxReflection` and explicit API-map aliases.
- Resolve a compatible signature before invoking it.
- Never fabricate required arguments.
- Stop after a compatible side-effecting invocation begins and fails.
- Distinguish command success from factory object return.
- Record actual station signatures through API Inventory.

## Consequences

- Unknown maintenance-release APIs fail with capability diagnostics instead of speculative retries.
- API-map changes require station evidence.
- Some functionality remains blocked until exact signatures are verified, which is safer than creating a partially valid drawing.
