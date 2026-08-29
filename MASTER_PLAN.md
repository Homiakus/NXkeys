# 1. Mission

Сделать NXkeys устойчивой, проверяемой и эволюционирующей системой, где `MASTER_PLAN.md` — единственный living execution plan, а существенные изменения проходят цикл `AUDIT → PLAN → SELECT → IMPLEMENT → TEST → REVIEW → RECONCILE → COMMIT → VERIFIED PUSH → COMPRESS`.

Текущий приоритет: завершить `T-001`, восстановив полностью зелёный current-v8 baseline без возврата удалённого K3–K5/full-command-map контура и без изменения runtime semantics ради CI.

# 2. Current State

- C#/.NET 8, Windows x64, Siemens NX / Designcenter NX 2512.
- Canonical source: `config/nx2512-v8-profile.json`, schema 8; 439 raw operations / 439 unique `operation_id`, 131 executable button mappings.
- `Config : IJsonOnDeserialized` через `V8SecondaryAliasExpander` материализует `secondary_aliases` в runtime compatibility projection: 500 projected rows / 439 semantic IDs.
- IPC schema 4: HMAC authentication, replay guard, permission/context/source-process binding.
- Boundaries: HotkeyStudio → Protocol/StateMachines → BridgeCore → CommandBridge/NXOpen; ControlCenter и NxEskd — отдельные surfaces.
- Baseline `main`: `2359e85c02313f4c6227a5e9a38ef62e9b9c043f`; 5/5 push workflows тогда были red.
- Preflight branch: `audit/living-master-plan-bootstrap-20260829`; `main` не меняется до verified PASS.
- Latest preflight implementation: `d08b8b25f45af1ffef826965b2520e34e215ea75`.
- На `d08b8b25...`: Documentation PASS, Desktop UI PASS, Runtime hardening PASS; `ci` прошёл validators, DFA/HFSM, HotkeyStudio, NxEskd, desktop build/publish, NXOpen stubs и CommandBridge compile, но упал на F-015 — obsolete `Compile Remove="Models\\ConfigModels.cs"` source-text assertion.
- Live NX 2512 остаётся отдельным внешним verification layer и не подменяется stubs.

# 3. Architecture Map

```text
Keyboard / CapsLock
        ↓
NX2512_HotkeyStudio
  LeaderKeyEngine / NxContextClient
  Config(raw v8) → V8SecondaryAliasExpander → runtime projection
  NxCommandBridgeClient → NxRequestSigningPolicy
        ↓ authenticated file IPC
NXKeys.Protocol ← NXKeys.StateMachines
        ↓
NXKeys.BridgeCore (bounded background admission)
        ↓
NX2512_CommandBridge
  Context / Menu / Selection / Intervention executors
        ↓
NXOpen / NX UI / NxEskd capabilities
```

Ownership: Protocol = contracts/security/normalization; StateMachines = deterministic leader policy; HotkeyStudio = out-of-NX UI/orchestration/config translation; BridgeCore = admission/inbox; CommandBridge = in-NX effects; `V8SecondaryAliasExpander` = in-memory compatibility projection only.

# 4. Baseline

| Check | Original main | Latest preflight |
|---|---|---|
| Push workflows | 5/5 FAIL | 3 PASS, `ci` FAIL only at F-015 |
| Current Node validators | stale/masked | PASS |
| Raw v8 identity | not explicitly enforced | PASS: 439 unique |
| DFA/HFSM | FAIL missing policy | PASS |
| HotkeyStudio regressions | blocked | PASS |
| NxEskd Core | blocked | PASS: 55/55 |
| NxEskd Configurator | blocked | PASS: 27/27 |
| Protocol/BridgeCore strict build | blocked | PASS, 0 warnings/errors |
| Desktop UI | stale smoke | PASS |
| Runtime hardening | stale source ownership | PASS including NXOpen/Bridge contract |
| HotkeyStudio build/publish | downstream blocked | PASS, state policy present |
| ControlCenter build/publish | downstream blocked | PASS |
| NXOpen stubs + CommandBridge compile | downstream blocked | PASS |
| Adaptive invariants | blocked | FAIL only on obsolete ConfigModels exclusion assertion |
| Deployment invariants | not reached | pending after F-015 |
| Live NX | NOT RUN | external requirement |
| Coverage / mutation / benchmarks | unknown / absent / unknown | T-007/T-009 |

Pre-existing failures are kept separate from defects introduced by current work.

# 5. System Invariants

1. `main` never knowingly moves to a worse verified state.
2. Raw v8 schema 8 is canonical operation/profile truth.
3. Raw canonical `operation_id` values are non-empty and globally unique.
4. Runtime alias projections may repeat a canonical ID only for the same semantic operation and declared compatibility route/scope.
5. Tests distinguish raw canonical data from post-deserialization projection.
6. Independent HFSM policy ships with runtime/tests.
7. Out-of-NX code never performs NXOpen effects directly.
8. IPC admission remains authenticated, permission-bound, replay-resistant and context-bound.
9. NX UI thread performs neither unbounded inbox scanning nor cryptographic admission.
10. Selection-filter actions flow through shared protocol to the in-NX executor boundary.
11. Context normalization has one canonical Protocol implementation.
12. Significant unexpected findings are recorded/reconciled before scope expands.
13. Tests and observed behavior outrank roadmap assumptions.
14. CI asserts semantic contracts, not incidental source text or already-deleted migration files.
15. Deleted K3–K5/legacy assets are never recreated merely to satisfy stale checks.

# 6. Findings Registry

## F-001 — Latest main has no trustworthy green baseline
**Status:** Planned  
**Category:** Reliability / CI  
**Severity:** Critical  
**Confidence:** Confirmed  
**Evidence:** 5/5 baseline push workflows failed; preflight has progressively exposed blockers.  
**Files / symbols:** `.github/workflows/*`.  
**Current behavior:** current `main` has no trusted release signal.  
**Expected behavior:** current-v8 gates green.  
**Root cause:** incomplete v8 migration and stale automation.  
**Impact:** repository-wide delivery risk.  
**Blast radius:** all changes/releases.  
**Reproduction:** Actions at `2359e85...`.  
**Affected invariants:** 1,13.  
**Related findings:** F-002..F-006,F-009..F-015.  
**Affected tasks:** T-001.  
**Recommended direction:** finish baseline recovery before feature work.

## F-002 — Active CI depended on removed K3–K5/full-command-map assets
**Status:** Planned  
**Category:** CI / Migration  
**Severity:** High  
**Confidence:** Confirmed  
**Evidence:** obsolete workflows referenced absent `full-command-map`/`nx2512-pro-*`.  
**Files / symbols:** legacy map/runtime workflows/scripts.  
**Current behavior:** deterministic ENOENT in baseline.  
**Expected behavior:** active gates validate v8 only.  
**Root cause:** automation not reconciled with cleanup.  
**Impact:** permanent-red gates.  
**Blast radius:** CI/docs/profile.  
**Reproduction:** baseline Actions.  
**Affected invariants:** 1,2,15.  
**Related findings:** F-007,F-009.  
**Affected tasks:** T-001,T-004.  
**Recommended direction:** retire active obsolete gates; delete residual debt separately.

## F-003 — Independent state-machine policy was accidentally removed
**Status:** Planned  
**Category:** Correctness / Packaging  
**Severity:** High  
**Confidence:** Confirmed  
**Evidence:** runtime/tests consume `nx2512-state-machines.json`; baseline lacked it and expected `root_ms=4000`.  
**Files / symbols:** `LeaderBehaviorProfile`, HotkeyStudio/test csproj.  
**Current behavior:** baseline fell back to wrong timing.  
**Expected behavior:** schema-1 policy packaged independently of command profile.  
**Root cause:** two config contracts conflated during cleanup.  
**Impact:** leader timing/guards.  
**Blast radius:** runtime/package/tests.  
**Reproduction:** baseline DFA/HFSM.  
**Affected invariants:** 6,13.  
**Related findings:** F-001.  
**Affected tasks:** T-001.  
**Recommended direction:** restore policy only, never legacy maps.

## F-004 — Runtime hardening checked transport result in wrong file
**Status:** Planned  
**Category:** Testing / Architecture  
**Severity:** Medium  
**Confidence:** Confirmed  
**Evidence:** `NxTransportReadStatus` moved to `NxTransportReadResult.cs`; old gate searched client file.  
**Files / symbols:** runtime-hardening workflow.  
**Current behavior:** valid extraction was red.  
**Expected behavior:** check semantic owner.  
**Root cause:** brittle source-text assertion.  
**Impact:** hardening blocked.  
**Blast radius:** runtime CI.  
**Reproduction:** Wave A.  
**Affected invariants:** 1,14.  
**Related findings:** F-010,F-013,F-015.  
**Affected tasks:** T-001,T-006.  
**Recommended direction:** reconcile now, executable architecture test later.

## F-005 — Selection validator assumed effect remained in `Program.cs`
**Status:** Planned  
**Category:** Testing / Architecture  
**Severity:** Medium  
**Confidence:** Confirmed  
**Evidence:** dispatch in `Program.ProcessClaim`; effect in `NxMenuCommandExecutor.ApplySelectionCommand`.  
**Files / symbols:** command-tree validator, CommandBridge.  
**Current behavior:** false failure.  
**Expected behavior:** dispatch/effect boundaries checked independently.  
**Root cause:** validator lagged refactor.  
**Impact:** hidden red invariant.  
**Blast radius:** command-tree CI.  
**Reproduction:** pre-Wave-A validator.  
**Affected invariants:** 10,14.  
**Related findings:** F-004.  
**Affected tasks:** T-001,T-006.  
**Recommended direction:** follow executor ownership.

## F-006 — Orphan worktree gitlink committed under `.claude`
**Status:** Planned  
**Category:** Repository Integrity  
**Severity:** Medium  
**Confidence:** Confirmed  
**Evidence:** mode `160000` without `.gitmodules`; checkout cleanup warning/failure.  
**Files / symbols:** `.claude/worktrees/fix-v8-contract-paths`.  
**Current behavior:** fragile/noisy checkout.  
**Expected behavior:** no agent-local gitlink.  
**Root cause:** accidental worktree metadata commit.  
**Impact:** CI/tooling reliability.  
**Blast radius:** every checkout.  
**Reproduction:** baseline Actions cleanup.  
**Affected invariants:** 1.  
**Related findings:** F-001.  
**Affected tasks:** T-001.  
**Recommended direction:** delete gitlink, no fake submodule mapping.

## F-007 — Documentation still describes retired K3–K5 assets
**Status:** Open  
**Category:** Documentation / Migration debt  
**Severity:** Medium  
**Confidence:** Strong  
**Evidence:** README/full-map/audit narrative references absent legacy assets.  
**Files / symbols:** README, `FULL_COMMAND_MAP.md`, docs/audit, legacy scripts.  
**Current behavior:** mixed current/legacy onboarding.  
**Expected behavior:** one v8 story; history stays in Git.  
**Root cause:** cleanup debt.  
**Impact:** maintainer confusion.  
**Blast radius:** onboarding/tooling.  
**Reproduction:** docs vs tree.  
**Affected invariants:** 2,15.  
**Related findings:** F-002,F-009.  
**Affected tasks:** T-004.  
**Recommended direction:** evidence-based deletion pass.

## F-008 — Critical pure logic has no mutation baseline
**Status:** Open  
**Category:** Test-of-tests  
**Severity:** Medium  
**Confidence:** Strong  
**Evidence:** no enforced mutation score/tooling.  
**Files / symbols:** Protocol, StateMachines, validators/projection.  
**Current behavior:** coverage may overstate confidence.  
**Expected behavior:** meaningful mutants killed.  
**Root cause:** infrastructure gap.  
**Impact:** false confidence.  
**Blast radius:** auth/state/validation.  
**Reproduction:** inspect CI.  
**Affected invariants:** 8,13.  
**Related findings:** none.  
**Affected tasks:** T-007.  
**Recommended direction:** targeted Stryker.NET pilot.

## F-009 — `Sketch intent grammar` workflow is retired K3–K5 pipeline
**Status:** Planned  
**Category:** CI / Migration  
**Severity:** High  
**Confidence:** Confirmed  
**Evidence:** workflow invoked legacy map compiler and removed assets.  
**Files / symbols:** `.github/workflows/sketch-intent.yml`.  
**Current behavior:** duplicate permanent-red gate.  
**Expected behavior:** sketch grammar covered by current v8 tests.  
**Root cause:** obsolete workflow survived cleanup.  
**Impact:** red pushes.  
**Blast radius:** command/state changes.  
**Reproduction:** Wave A.  
**Affected invariants:** 1,2,15.  
**Related findings:** F-002.  
**Affected tasks:** T-001,T-004.  
**Recommended direction:** retire workflow, retain v8 regression coverage.

## F-010 — Signing assertion ignored `NxRequestSigningPolicy` extraction
**Status:** Planned  
**Category:** Testing / Architecture  
**Severity:** Medium  
**Confidence:** Confirmed  
**Evidence:** facade delegates `policy.PrepareAuthenticated`; signing/HMAC live in extracted policy.  
**Files / symbols:** client, signing policy, hardening workflow.  
**Current behavior:** valid security split failed Wave A.  
**Expected behavior:** ownership tested separately.  
**Root cause:** stale source-location contract.  
**Impact:** hardening blocked.  
**Blast radius:** security CI.  
**Reproduction:** Wave A.  
**Affected invariants:** 8,14.  
**Related findings:** F-004,F-013,F-015.  
**Affected tasks:** T-001,T-006.  
**Recommended direction:** reconcile now, architecture test later.

## F-011 — Capability-lock test and validator disagreed about optionality
**Status:** Planned  
**Category:** Testing / Contract  
**Severity:** Medium  
**Confidence:** Confirmed  
**Evidence:** JS allowed absent lock while C# demanded file existence.  
**Files / symbols:** capability validator, HotkeyStudio tests.  
**Current behavior:** deleted auxiliary file blocked suite.  
**Expected behavior:** raw v8 is truth; optional lock exact-matches if present.  
**Root cause:** stale test plus weak optional-lock validation.  
**Impact:** profile suite blocked.  
**Blast radius:** main CI.  
**Reproduction:** Wave A.  
**Affected invariants:** 2,3,13,15.  
**Related findings:** F-014.  
**Affected tasks:** T-001,T-006.  
**Recommended direction:** validate canonical profile directly.

## F-012 — Strict Protocol build exposed nullable contract mismatch
**Status:** Planned  
**Category:** Correctness / Static analysis  
**Severity:** Medium  
**Confidence:** Confirmed  
**Evidence:** nullable-enabled helper returned null through non-nullable signature; strict preflight exposed it.  
**Files / symbols:** `NxContextNormalization.NormalizeV8Module`.  
**Current behavior:** warning becomes `-warnaserror` failure.  
**Expected behavior:** explicit nullable return.  
**Root cause:** annotation mismatch.  
**Impact:** strict build blocker.  
**Blast radius:** Protocol.  
**Reproduction:** Wave A→B.  
**Affected invariants:** 1,11,13.  
**Related findings:** none.  
**Affected tasks:** T-001.  
**Recommended direction:** behavior-preserving nullable annotation.

## F-013 — Desktop UI gate hardcoded obsolete 10-row HUD contract
**Status:** Planned  
**Category:** Testing / UI Contract  
**Severity:** Medium  
**Confidence:** Confirmed  
**Evidence:** gate expected `MaximumRootRows = 10`; code/test contract is `PrimarySuggestionLimit = 8`, `MaximumRootRows = PrimarySuggestionLimit`.  
**Files / symbols:** desktop workflow, `LeaderHudForm`.  
**Current behavior:** valid compact HUD failed smoke check.  
**Expected behavior:** gate follows current public contract.  
**Root cause:** workflow not reconciled with UI change.  
**Impact:** Desktop UI red.  
**Blast radius:** UI CI.  
**Reproduction:** Wave B.  
**Affected invariants:** 1,13,14.  
**Related findings:** F-004,F-010,F-015.  
**Affected tasks:** T-001,T-006.  
**Recommended direction:** current smoke now; executable UI contract later.

## F-014 — C# identity regression confused raw operations with alias projections
**Status:** Planned  
**Category:** Testing / Data Contract  
**Severity:** Medium  
**Confidence:** Confirmed  
**Evidence:** raw validator = 439/439 unique; `IJsonOnDeserialized` intentionally expands secondary aliases to 500 rows/439 semantic IDs.  
**Files / symbols:** `V8SecondaryAliasExpander`, HotkeyStudio tests, capability validator.  
**Current behavior:** stale C# uniqueness assertion rejected intended projection.  
**Expected behavior:** raw uniqueness + projection semantic fidelity checked separately.  
**Root cause:** test asserted canonical invariant at post-projection model stage.  
**Impact:** false-red tests; naive fix would delete valid aliases.  
**Blast radius:** profile/CI.  
**Reproduction:** Wave C diagnostics.  
**Affected invariants:** 2–5,13.  
**Related findings:** F-011.  
**Affected tasks:** T-001,T-006.  
**Recommended direction:** preserve raw profile; projection-integrity regression.

## F-015 — Adaptive CI requires exclusion for an already-deleted `ConfigModels.cs`
**Status:** Planned  
**Category:** Testing / Migration debt  
**Severity:** Medium  
**Confidence:** Confirmed  
**Evidence:** latest `ci` reached adaptive invariants after every executable suite/build passed, then required literal `Compile Remove="Models\\ConfigModels.cs"`; `NX2512_HotkeyStudio/Models/ConfigModels.cs` is absent and current csproj builds/publishes successfully without an exclusion entry.  
**Files / symbols:** `.github/workflows/ci.yml`, `NX2512_HotkeyStudio.csproj`, absent `Models/ConfigModels.cs`.  
**Current behavior:** cleanup-complete project fails because CI demands obsolete migration scaffolding.  
**Expected behavior:** assert legacy file remains absent, not that an exclusion for it remains forever.  
**Root cause:** debt-deletion step removed source file but did not reconcile source-text gate.  
**Impact:** final current `ci` remains red after all prior executable checks pass.  
**Blast radius:** main CI; deployment check is skipped downstream.  
**Reproduction:** `d08b8b25...`, adaptive step 18.  
**Affected invariants:** 1,13,14,15.  
**Related findings:** F-004,F-010,F-013.  
**Affected tasks:** T-001,T-004,T-006.  
**Recommended direction:** replace literal Compile Remove assertion with fail-if-legacy-file-exists; never re-add dummy csproj exclusion.

# 7. Risk Register

| Risk | P | Impact | Mitigation |
|---|---:|---:|---|
| another stale late-stage gate appears | High | Medium | fail → Finding → reconcile; no blind skips |
| alias projection broadens semantics | Medium | High | raw uniqueness + projection fingerprint tests |
| source-text CI drifts after refactor | High | Medium | T-006 executable architecture tests |
| restored HFSM differs in live NX | Medium | High | live-NX checklist |
| NXOpen stubs differ from NX 2512 | Medium | High | explicit stub/live distinction |
| mutation suite is noisy/slow | Medium | Medium | targeted pure projects first |

# 8. Pareto Improvements

1. Finish green/fail-fast current-v8 baseline.
2. Keep raw profile and runtime compatibility projection contracts separate.
3. Make `MASTER_PLAN.md` executable.
4. Replace source-text migration checks with executable architecture/absence contracts.
5. Delete remaining legacy debt and mutation-test critical pure logic.

# 9. Dependency DAG

```text
T-001 baseline recovery
 ├─ T-002 plan gate
 ├─ T-003 verified baseline artifact
 ├─ T-004 legacy debt deletion
 ├─ T-006 architecture/projection tests ─ T-005 boundary migration
 └─ T-007 mutation pilot ─ T-008 edge-space coverage
T-003 + T-005 + T-008 ─ T-009 measured hardening ─ T-010 convergence audit
```

# 10. Implementation Phases

- Phase 0: T-001..T-003 — recover trust/governance.
- Phase 1: T-004 — debt deletion.
- Phase 2: T-006 → T-005 — semantic boundaries.
- Phase 3: T-007..T-008 — test-of-tests/edge coverage.
- Phase 4: T-009 — measured security/performance/reliability.
- Phase 5: T-010 — re-audit/delta convergence.

# 11. Atomic Tasks

## T-001 — Reconcile v8 migration fallout and restore a trustworthy baseline
**Status:** VERIFYING  
**Priority:** P0  
**Type:** FIX  
**Leverage:** HIGH
### Problem
`main` lacks green trusted baseline; latest preflight has one stale late-stage CI assertion (F-015).
### Evidence
F-001..F-006,F-009..F-015; `d08b8b25...` has three green workflows and `ci` failure only after all executable suites/builds pass.
### Goal
All applicable current-v8 Actions green without semantic weakening or resurrected legacy assets.
### Scope
HFSM policy/package; obsolete workflows; fail-fast validators; source-boundary checks; orphan gitlink; capability/raw-projection contracts; nullable fix; HUD check; F-015 absent-legacy assertion; plan reconciliation.
### Non-goals
No new feature/UX redesign, schema bump, broad refactor, alias deletion, mutation threshold or live-NX redesign.
### Files / symbols
Active workflows/validators/tests/config translation and `MASTER_PLAN.md`.
### Implementation
Replace F-015 literal csproj-exclusion requirement with an absence check for `Models/ConfigModels.cs`; run full preflight; classify any new substantive failure before scope expansion. On all-green preflight, resync remote `main`, self-review diff, recreate one logical verified tree on current main, non-force push.
### Invariants
1–15.
### Compatibility constraints
No K3–K5 resurrection; IPC/security/alias semantics preserved.
### Edge cases
Raw vs projected identity, missing policy, deleted migration files, source moves, native exit masking, strict nullable builds, downstream deployment invariants.
### Tests
All active Actions: Node validators, DFA/HFSM, HotkeyStudio, NxEskd, strict builds, desktop publish, NXOpen stubs/bridge, adaptive/deployment invariants.
### Mutation tests
Not available yet; T-007.
### Benchmarks
N/A for migration repair.
### Acceptance criteria
All applicable preflight workflows PASS; deployment step executes; raw 439 IDs remain unique; alias projection fidelity passes; no diagnostic-only artifacts; clean checkout; remote main reconciled before push.
### Verification commands
Encoded in active workflows plus validators.
### Dependencies
None.
### Blocks
T-002..T-010.
### Risk
Medium: one more late stale gate may appear.
### Rollback
Normal revert only; no force push.
### Estimated effort
Medium, iterative.

## T-002 — Enforce MASTER_PLAN structure as executable gate
**Status:** READY  
**Priority:** P1  
**Type:** HARDEN  
**Leverage:** HIGH
### Problem
Living plan can structurally drift.
### Evidence
No plan validator exists.
### Goal
Reject missing sections, duplicate IDs and invalid vocabularies.
### Scope
Pure Node validator + negative self-tests + CI.
### Non-goals
No automatic semantic truth judgment.
### Files / symbols
`MASTER_PLAN.md`, `scripts/validate-master-plan.mjs`, CI.
### Implementation
Required headings; unique F/T IDs; status/priority/type/leverage vocabularies; iteration-log presence; in-memory invalid fixtures.
### Invariants
12,13.
### Compatibility constraints
No npm dependency; Windows/Linux Node.
### Edge cases
Duplicate IDs, malformed metadata, missing log.
### Tests
Current plan + deliberate negative fixtures.
### Mutation tests
Later if useful.
### Benchmarks
Sub-second target.
### Acceptance criteria
Malformed plan fails; current plan passes.
### Verification commands
`node scripts/validate-master-plan.mjs --self-test`; `node scripts/validate-master-plan.mjs`.
### Dependencies
T-001.
### Blocks
None.
### Risk
Low.
### Rollback
Remove gate only if parser is proven defective.
### Estimated effort
Small.

## T-003 — Capture a machine-readable verified baseline
**Status:** TODO  
**Priority:** P1  
**Type:** HARDEN  
**Leverage:** HIGH
### Problem
Evidence is scattered in Actions logs.
### Evidence
Initial audit required manual correlation.
### Goal
Versioned check inventory, environment limits and classification.
### Scope
Repository-known checks; distinguish stubs/live NX; no secrets.
### Non-goals
No external telemetry.
### Files / symbols
`docs/audit/verification-baseline.*`, helper.
### Implementation
Deterministic manifest with verified SHA/checks/environment class.
### Invariants
1,13.
### Compatibility constraints
Never claim live NX from stubs.
### Edge cases
Skip/cancel/rerun/environment-only checks.
### Tests
Manifest validation.
### Mutation tests
N/A.
### Benchmarks
N/A.
### Acceptance criteria
Introduced/pre-existing failures are quickly distinguishable.
### Verification commands
CI helper.
### Dependencies
T-001.
### Blocks
T-009.
### Risk
Low.
### Rollback
Plan baseline remains fallback.
### Estimated effort
Small.

## T-004 — Delete retired K3–K5 and migration debt
**Status:** TODO  
**Priority:** P1  
**Type:** REMOVE  
**Leverage:** HIGH
### Problem
Legacy docs/scripts/assertions outlive removed assets.
### Evidence
F-002,F-007,F-009,F-015.
### Goal
One unambiguous v8 story and no eternal migration scaffolding.
### Scope
Usage-search legacy compilers/validators/docs/source-text exclusions; classify/delete/archive.
### Non-goals
Retain helpers still used by v8.
### Files / symbols
README/full-map/docs/audit/legacy scripts/workflow assertions.
### Implementation
Usage search → classify → delete/update → full verification.
### Invariants
2,14,15.
### Compatibility constraints
No unproven runtime deletion.
### Edge cases
Shared helpers/history-only docs.
### Tests
Current docs/command/profile CI.
### Mutation tests
N/A.
### Benchmarks
N/A.
### Acceptance criteria
Active docs/workflows require no absent legacy asset/scaffolding.
### Verification commands
Search + CI.
### Dependencies
T-001.
### Blocks
None.
### Risk
Medium.
### Rollback
Restore only a proven-current path.
### Estimated effort
Medium.

## T-005 — Finish remaining module-boundary adoption without semantic drift
**Status:** TODO  
**Priority:** P1  
**Type:** IMPROVE  
**Leverage:** HIGH
### Problem
Refactor status lists unresolved boundaries.
### Evidence
`docs/REFACTOR_STATUS.md`.
### Goal
Complete one behavior-preserving boundary per atomic subtask.
### Scope
Characterize → introduce boundary → dual compatibility → migrate → verify → remove legacy.
### Non-goals
No silent behavior/API transition.
### Files / symbols
ModuleContracts/executors/LeaderKeyEngine/claim path.
### Invariants
7–10,13.
### Compatibility constraints
IPC/live NX behavior preserved.
### Edge cases
Modal state, stale context, selection, cancellation, process boundary.
### Tests
Contract/architecture/live checklist when required.
### Mutation tests
Pure decisions after T-007.
### Benchmarks
Dispatch baseline where needed.
### Acceptance criteria
Explicit boundary, migrated callers, legacy deleted after verification.
### Verification commands
CI + live checklist.
### Dependencies
T-001,T-006 as applicable.
### Blocks
T-010.
### Risk
High.
### Rollback
Compatibility adapter until verified.
### Estimated effort
Large; split before execution.

## T-006 — Replace brittle source-text checks with executable architecture/projection tests
**Status:** TODO  
**Priority:** P1  
**Type:** HARDEN  
**Leverage:** HIGH
### Problem
F-004,F-005,F-010,F-013,F-014,F-015 show semantic drift in textual gates.
### Evidence
Preflight waves.
### Goal
Assert ownership/data stage/absence contracts rather than incidental text.
### Scope
Protocol/BridgeCore/CommandBridge/HotkeyStudio/UI plus raw-v8/projection boundary.
### Non-goals
No production behavior change.
### Files / symbols
Test projects/workflows/contracts.
### Implementation
Compile/reflection/behavior tests and negative fixtures; retain smoke only until equivalent executable detection exists.
### Invariants
2–5,7–10,14,15.
### Compatibility constraints
NXOpen stubs remain compile-only evidence.
### Edge cases
Internal types, conditional refs, aliases, physically deleted legacy files.
### Tests
Deliberate boundary violations must fail.
### Mutation tests
T-007 for pure logic.
### Benchmarks
N/A.
### Acceptance criteria
Valid refactor/deletion passes; real boundary regression fails.
### Verification commands
Targeted tests + CI.
### Dependencies
T-001.
### Blocks
T-005.
### Risk
Medium.
### Rollback
Keep old smoke until replacement proven.
### Estimated effort
Medium.

## T-007 — Introduce mutation testing for critical pure logic
**Status:** TODO  
**Priority:** P2  
**Type:** HARDEN  
**Leverage:** HIGH
### Problem
F-008.
### Evidence
No measured mutation baseline.
### Goal
Measure auth/state/validator/projection test strength.
### Scope
Protocol/StateMachines first; pure config projection if stable.
### Non-goals
No blind NX UI/NXOpen mutation.
### Files / symbols
Security/normalization/state/validators/projection.
### Implementation
Stryker.NET pilot; classify equivalent/surviving mutants; informational before threshold.
### Invariants
4,5,8,13.
### Compatibility constraints
Bounded CI duration.
### Edge cases
Equivalent mutants/time-sensitive tests.
### Tests
Mutation run + survivor review.
### Mutation tests
Established here.
### Benchmarks
Mutation duration.
### Acceptance criteria
Measured score; no unexplained high-risk survivor.
### Verification commands
Recorded after tool setup.
### Dependencies
T-001.
### Blocks
T-008.
### Risk
Medium.
### Rollback
Informational mode if threshold unstable.
### Estimated effort
Medium.

## T-008 — Expand multidimensional edge-case coverage
**Status:** TODO  
**Priority:** P2  
**Type:** HARDEN  
**Leverage:** HIGH
### Problem
Critical contracts span input × state × concurrency × timing × failure × permissions × configuration × external state.
### Evidence
IPC/HFSM/config architecture.
### Goal
Pairwise + high-risk N-wise + property/fuzz/metamorphic coverage.
### Scope
Auth/replay, inbox, HFSM, raw/projection, context guards.
### Non-goals
No brute-force live NX UI.
### Files / symbols
Critical tests.
### Implementation
Dimension matrix → pairwise → high-risk N-wise → reproducible fuzz/property cases.
### Invariants
2–10,13.
### Compatibility constraints
Deterministic seeds/repro.
### Edge cases
Clock skew, reorder/duplicate, corrupt JSON, stale revision, permission mismatch, alias collision, modal/cancel.
### Tests
Matrix-driven suites.
### Mutation tests
Survivors guide missing observables.
### Benchmarks
Stress duration/allocations.
### Acceptance criteria
Documented matrix + executable high-risk coverage.
### Verification commands
Targeted suites.
### Dependencies
T-007.
### Blocks
T-009.
### Risk
Medium.
### Rollback
Retain deterministic core if fuzz flakes.
### Estimated effort
Medium-large.

## T-009 — Establish performance, security and reliability baselines
**Status:** TODO  
**Priority:** P2  
**Type:** HARDEN  
**Leverage:** MEDIUM
### Problem
Coverage/mutation/performance/dependency/flaky baselines incomplete.
### Evidence
Section 4.
### Goal
Quantify release-relevant regressions.
### Scope
Pure hot paths, scans, flaky observation, warnings inventory.
### Non-goals
No live-NX performance claim from stubs.
### Files / symbols
CI/benchmark/test projects.
### Implementation
Measure first, thresholds after data.
### Invariants
1,8,13.
### Compatibility constraints
Reasonable CI duration.
### Edge cases
Cold/steady state, IO variance.
### Tests
Benchmarks/scans.
### Mutation tests
Consume T-007.
### Benchmarks
Established here.
### Acceptance criteria
Versioned measurable baselines/targets.
### Verification commands
Baseline manifest.
### Dependencies
T-003,T-008.
### Blocks
T-010.
### Risk
Medium.
### Rollback
Unstable threshold becomes informational, measurement retained.
### Estimated effort
Medium.

## T-010 — Perform final convergence re-audit and implement delta
**Status:** TODO  
**Priority:** P2  
**Type:** IMPROVE  
**Leverage:** HIGH
### Problem
Initial audit cannot prove post-change convergence.
### Evidence
Master protocol.
### Goal
Repeat architecture/correctness/security/concurrency/reliability/API/data/testing/mutation/performance/CI/docs/dependency audit until no fundamental delta.
### Scope
Whole repository.
### Non-goals
No cosmetic expansion without finding.
### Files / symbols
All.
### Implementation
Re-audit → findings → atomic delta tasks → verified iterations → repeat.
### Invariants
All.
### Compatibility constraints
Live NX release path preserved.
### Edge cases
T-008 matrix.
### Tests
All established gates + live checklist.
### Mutation tests
Critical suites.
### Benchmarks
All established baselines.
### Acceptance criteria
Section 21.
### Verification commands
Full CI + live verification where applicable.
### Dependencies
T-005,T-009.
### Blocks
Final done.
### Risk
Medium.
### Rollback
Per delta task.
### Estimated effort
Variable.

# 12. Testing Strategy

Characterization precedes behavior change. Raw config contracts are tested before `IJsonOnDeserialized` projection; projected behavior separately. Pure contracts use unit/property tests; Windows desktop/CommandBridge use integration/build checks; NXOpen stubs prove compile compatibility only; live NX is explicit separate evidence. Negative security tests are first class. Native command errors propagate immediately. Preflight branch is verification sandbox; `main` is not.

# 13. Mutation Testing Strategy

Not configured initially. T-007 measures before gating. Priority: HMAC/auth/replay → context guards → state transitions → raw/profile validators → alias projection → normalization → inbox. Surviving mutants trigger observable-contract analysis, not coverage inflation.

# 14. Performance Baselines

Unknown initially. Candidates: leader transition latency/allocations, normalization/parsing, alias projection, inbox admission/bounded poll, profile load/validation, desktop cold start/publish size. No live-NX latency claim from CI.

# 15. Security Hardening

Preserve HMAC, replay guard, permissions, context/source-process binding and bounded inbox. Raw identity and alias-projection fidelity are separate contracts; aliases must not broaden adapter/security semantics. Add dependency/code scans and mutation-resistant negative tests later. Never weaken validation to green CI.

# 16. Migration Strategy

`characterization → introduce boundary → dual compatibility → migrate callers → verify → remove legacy`. Removal is complete only when obsolete files *and* their exclusion/scaffolding assertions are deleted. v8 cleanup restores only independent HFSM policy, never K3–K5 command-map sources. Optional locks derive strictly from raw v8.

# 17. Deferred Work

- Live NX 2512 verification requiring installed NX.
- Mutation/performance thresholds until measured.
- Cosmetic UI work unrelated to findings.
- Broad refactor until T-001 green.
- Non-blocking compiler/analyzer warning cleanup to be inventoried under T-009 unless upgraded by new evidence.

# 18. Rejected Decisions

- Restore K3–K5/full-command-map to green CI — rejected: dual source of truth.
- Skip failing validators — rejected.
- Add fake capability lock — rejected.
- Delete secondary aliases/edit raw IDs to satisfy post-projection uniqueness — rejected.
- Re-add `Compile Remove="Models\\ConfigModels.cs"` after `ConfigModels.cs` was physically deleted — rejected: preserves meaningless migration scaffolding.
- Force/force-with-lease `main` — rejected.
- Treat NXOpen stubs as live NX proof — rejected.

# 19. Completed Tasks

None. `T-001` remains VERIFYING until a completely green preflight is reconciled, equivalent logical state is non-force pushed to current `main`, and main verification succeeds.

# 20. Iteration Log

## Iteration 1 — Wave A
**Task:** T-001  
**Findings addressed:** F-001..F-006  
**Unexpected findings:** F-009..F-012  
**Changes:** plan bootstrap; HFSM policy/package; current-v8 fail-fast CI; obsolete workflow retirement; executor/transport checks; orphan gitlink removal.  
**Tests:** `fab88e79...`: docs/current validators/DFA PASS; deeper signing/legacy/capability blockers exposed.  
**Plan changes:** F-009..F-012 added.  
**Commit:** `fab88e79c3a98f0eeb30ba496fc0a834cbae337a`  
**Push:** audit branch only.  
**Result:** FAIL → reconciled.

## Iteration 1 — Wave B
**Task:** T-001  
**Findings addressed:** F-009..F-012  
**Unexpected findings:** F-013,F-014  
**Changes:** retire Sketch legacy gate; signing ownership; capability validator; nullable contract.  
**Tests:** `a5f9a718...`: strict Protocol/BridgeCore and DFA PASS; Hotkey identity/Desktop UI stale checks exposed.  
**Plan changes:** F-013,F-014 added.  
**Commit:** `a5f9a718fce9dd2db9fdcd3ced56770b4f025378`  
**Push:** audit branch only.  
**Result:** FAIL → reconciled.

## Iteration 1 — Wave C
**Task:** T-001  
**Findings addressed:** F-013,F-014  
**Unexpected findings:** F-015  
**Changes:** raw uniqueness + projection-fidelity test; HUD contract reconciliation; diagnostic code removed.  
**Tests:** `d08b8b25...`: Documentation PASS; Desktop UI PASS; Runtime hardening PASS; `ci` passes validators, DFA/HFSM, HotkeyStudio, 55 NxEskd Core + 27 Configurator tests, desktop build/publish, ControlCenter, NXOpen stubs and CommandBridge compile, then FAILs only at obsolete ConfigModels exclusion assertion.  
**Plan changes:** F-014 root cause finalized; F-015 added; T-001 scope extended only to stale final adaptive gate.  
**Commit:** `d08b8b25f45af1ffef826965b2520e34e215ea75`  
**Push:** audit branch only.  
**Result:** FAIL → Wave D selected.

## Iteration 1 — Wave D
**Task:** T-001  
**Findings addressed:** F-015  
**Unexpected findings:** none yet.  
**Changes:** planned: replace legacy csproj exclusion marker with absence invariant.  
**Tests:** pending full preflight.  
**Plan changes:** this reconciliation records F-015 before implementation.  
**Commit:** pending.  
**Push:** audit branch only until PASS.  
**Result:** VERIFYING.

# 21. Definition of Final Done

- no known Critical/High findings;
- all P0/P1 DONE or evidence-backed REJECTED/DEFERRED;
- applicable CI consistently green with no masked native failures;
- raw canonical identity and runtime alias fidelity enforced separately;
- important architecture rules technically protected where feasible;
- critical security/state logic mutation-tested with no unexplained high-risk survivors;
- multidimensional critical edge space covered;
- performance/security/reliability baselines recorded and acceptable;
- no unexplained flaky tests;
- docs match current v8 code and no active gate depends on absent legacy assets/scaffolding;
- live-NX-only claims have explicit verification records;
- repeat audit finds no fundamental delta;
- `MASTER_PLAN.md`, code/tests and last verified `main` SHA agree.
