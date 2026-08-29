# 1. Mission

Сделать NXkeys устойчивой, проверяемой и эволюционирующей системой, где `MASTER_PLAN.md` — единственный living execution plan, а существенные изменения проходят цикл `AUDIT → PLAN → SELECT → IMPLEMENT → TEST → REVIEW → RECONCILE → COMMIT → VERIFIED PUSH → COMPRESS`.

Текущий приоритет: завершить `T-001`, восстановив полностью зелёный current-v8 baseline без возврата удалённого K3–K5/full-command-map контура и без изменения runtime semantics ради CI.

# 2. Current State

- C#/.NET 8, Windows x64, Siemens NX / Designcenter NX 2512.
- Canonical source: `config/nx2512-v8-profile.json`, schema 8; 439 raw operations / 439 unique `operation_id`, 131 executable button mappings.
- `Config : IJsonOnDeserialized` через `V8SecondaryAliasExpander` материализует `secondary_aliases` в compatibility projection: 500 projected rows / 439 semantic IDs.
- IPC schema 4: HMAC authentication, replay guard, permission/context/source-process binding.
- Boundaries: HotkeyStudio → Protocol/StateMachines → BridgeCore → CommandBridge/NXOpen; ControlCenter и NxEskd — отдельные surfaces.
- Original baseline `main`: `2359e85c02313f4c6227a5e9a38ef62e9b9c043f`; 5/5 push workflows были red.
- Clean T-001 core tree был non-force fast-forwarded в `main` как `2b28da717d4325b767c240851d487ae7717a3cb0`; на нём `ci`, Documentation, Desktop UI и Runtime hardening полностью PASS.
- Main-only Pages run `33265565463` выявил F-017: workflow всё ещё копировал удалённый `nx2512-pro-hybrid.json`.
- F-017 preflight branch: `audit/t001-pages-f017-20260829`, основана ровно на `2b28da717...`.
- F-017 implementation head `73a5fed49ef1eeb7880979ee24e7b5539dcbb2df` полностью PASS по full `ci`, включая validators, DFA/HFSM, HotkeyStudio, NxEskd 55+27, desktop build/publish, NXOpen stubs, CommandBridge compile, adaptive/deployment invariants и artifacts.
- F-017 delta меняет только `.github/workflows/pages.yml` и этот living plan; runtime/test production code не меняется.
- Live NX 2512 — отдельный внешний verification layer; stubs не считаются live-NX proof.

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
  SelectionIntentHotkeys → NxSelectionExecutor
  Context / Menu / Selection / Intervention executors
        ↓
NXOpen / NX UI / NxEskd capabilities
```

Ownership: Protocol = contracts/security/normalization; StateMachines = deterministic leader policy; HotkeyStudio = out-of-NX UI/orchestration/config translation; BridgeCore = admission/inbox; CommandBridge = in-NX effects; `SelectionIntentHotkeys` = physical keyboard hook/admission; `NxSelectionExecutor` = NX Selection Intent semantics; `V8SecondaryAliasExpander` = in-memory compatibility projection only.

# 4. Baseline

| Check | Original main | Current evidence |
|---|---|---|
| Push workflows | 5/5 FAIL | 4/5 PASS on `2b28da717...`; Pages F-017 fixed on green preflight |
| Current Node validators | stale/masked | PASS |
| Raw v8 identity | not explicit | PASS: 439/439 unique |
| DFA/HFSM | FAIL missing policy | PASS |
| HotkeyStudio regressions | blocked | PASS |
| NxEskd Core | blocked | PASS 55/55 |
| NxEskd Configurator | blocked | PASS 27/27 |
| Protocol/BridgeCore strict build | blocked | PASS |
| HotkeyStudio build/publish | blocked | PASS |
| ControlCenter build/publish | blocked | PASS |
| NXOpen stubs + CommandBridge compile | blocked | PASS |
| Adaptive invariants | blocked | PASS |
| Deployment invariants | not reached | PASS |
| Pages static package | FAIL legacy hybrid path | F-017 preflight PASS; main proof pending |
| Live NX | NOT RUN | external requirement |
| Mutation / performance baselines | absent / unknown | T-007 / T-009 |

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
10. Selection-filter actions flow through the in-NX selection executor boundary.
11. Context normalization has one canonical Protocol implementation.
12. Significant unexpected findings are reconciled before implementation scope expands.
13. Tests and observed behavior outrank roadmap assumptions.
14. CI asserts semantic ownership/contracts, not incidental source locations or deleted scaffolding.
15. Deleted K3–K5/legacy assets are never recreated merely to satisfy stale checks.

# 6. Findings Registry

## F-001 — Main had no trustworthy green baseline
**Status:** Addressed by T-001 | **Severity:** Critical | **Category:** Reliability/CI | **Confidence:** Confirmed  
Original `2359e85...` had 5/5 red workflows. Core/current-v8 baseline is now green; final Pages proof remains before T-001 closure.

## F-002 — Active CI depended on removed K3–K5/full-command-map assets
**Status:** Addressed by T-001; cleanup continues in T-004 | **Severity:** High | **Category:** CI/Migration | **Confidence:** Confirmed  
Obsolete active workflows were retired rather than restoring a second source of truth.

## F-003 — Independent state-machine policy was accidentally removed
**Status:** Addressed by T-001 | **Severity:** High | **Category:** Correctness/Packaging | **Confidence:** Confirmed  
`nx2512-state-machines.json` restored as an independent schema-1 behavior policy; it is not a legacy command map.

## F-004 — Runtime hardening checked transport result in wrong file
**Status:** Addressed; structural replacement in T-006 | **Severity:** Medium | **Category:** Testing/Architecture | **Confidence:** Confirmed  
Ownership-aware check restored; executable architecture guard remains T-006.

## F-005 — Selection validator assumed effect remained in `Program.cs`
**Status:** Addressed; structural replacement in T-006 | **Severity:** Medium | **Category:** Testing/Architecture | **Confidence:** Confirmed  
Dispatch/effect ownership now follows executor boundary.

## F-006 — Orphan `.claude` worktree gitlink
**Status:** Addressed by T-001 | **Severity:** Medium | **Category:** Repository Integrity | **Confidence:** Confirmed  
Mode-160000 orphan removed; no fake `.gitmodules` added.

## F-007 — Documentation still describes retired K3–K5 assets
**Status:** Open | **Severity:** Medium | **Category:** Documentation/Migration | **Confidence:** Strong  
Mixed legacy/current narrative remains T-004 cleanup scope.

## F-008 — Critical pure logic has no mutation baseline
**Status:** Open | **Severity:** Medium | **Category:** Test-of-tests | **Confidence:** Strong  
Targeted Stryker.NET baseline is T-007.

## F-009 — `Sketch intent grammar` workflow was retired K3–K5 pipeline
**Status:** Addressed by T-001 | **Severity:** High | **Category:** CI/Migration | **Confidence:** Confirmed  
Retired while current v8 sketch grammar coverage remains in active suites.

## F-010 — Signing assertion ignored `NxRequestSigningPolicy` extraction
**Status:** Addressed; structural replacement in T-006 | **Severity:** Medium | **Category:** Testing/Architecture | **Confidence:** Confirmed  
Facade and signing-policy ownership reconciled.

## F-011 — Capability-lock test and validator disagreed about optionality
**Status:** Addressed by T-001 | **Severity:** Medium | **Category:** Testing/Contract | **Confidence:** Confirmed  
Raw v8 is truth; optional derived lock must exact-match if present.

## F-012 — Strict Protocol build exposed nullable contract mismatch
**Status:** Addressed by T-001 | **Severity:** Medium | **Category:** Correctness/Static analysis | **Confidence:** Confirmed  
`NxContextNormalization.NormalizeV8Module` received behavior-preserving nullable contract correction.

## F-013 — Desktop UI gate hardcoded obsolete 10-row HUD contract
**Status:** Addressed; stronger guard in T-006 | **Severity:** Medium | **Category:** Testing/UI Contract | **Confidence:** Confirmed  
Current production contract `PrimarySuggestionLimit = 8` is reflected by active verification.

## F-014 — C# identity regression confused raw operations with alias projections
**Status:** Addressed by T-001 | **Severity:** Medium | **Category:** Testing/Data Contract | **Confidence:** Confirmed  
Raw = 439 unique IDs; projected runtime = 500 rows / 439 semantic IDs. Raw uniqueness and projection fidelity are now separate invariants.

## F-015 — Adaptive CI required exclusion for already-deleted `ConfigModels.cs`
**Status:** Addressed; stronger guard in T-006 | **Severity:** Medium | **Category:** Testing/Migration | **Confidence:** Confirmed  
CI now fails if the legacy file returns rather than requiring dead `<Compile Remove>` scaffolding.

## F-016 — Selection Intent CI checked executor semantics in keyboard-hook file
**Status:** Addressed; stronger guard in T-006 | **Severity:** Medium | **Category:** Testing/Architecture | **Confidence:** Confirmed  
Hook delegation and `NxSelectionExecutor` NX semantics are checked in their actual owners.

## F-017 — Pages workflow still published removed hybrid profile
**Status:** Preflight PASS; main verification pending | **Severity:** High | **Category:** CI/Documentation/Migration | **Confidence:** Confirmed  
**Evidence:** main run `33265565463` failed only at `cp config/nx2512-pro-hybrid.json`; HTML already loaded `../config/nx2512-v8-profile.json`.  
**Fix:** Pages trigger/copy/test/grep references were changed to canonical `nx2512-v8-profile.json`, keeping `nx2512-state-machines.json` separate.  
**Verification:** full branch `ci` on `73a5fed49ef1eeb7880979ee24e7b5539dcbb2df` PASS.  
**Remaining proof:** main-only Pages build/deploy after non-force integration.  
**Affected tasks:** T-001,T-004.

# 7. Risk Register

| Risk | P | Impact | Mitigation |
|---|---:|---:|---|
| another main-only stale gate appears | Medium | Medium | fail → finding → reconcile; no blind skip |
| alias projection broadens semantics | Medium | High | raw uniqueness + projection fidelity |
| source-text CI drifts after refactor | High | Medium | T-006 executable architecture tests |
| restored HFSM differs in live NX | Medium | High | explicit live-NX checklist |
| NXOpen stubs differ from NX 2512 | Medium | High | never treat stubs as live proof |
| mutation suite noisy/slow | Medium | Medium | targeted pure projects first |

# 8. Pareto Improvements

1. Finish all-five-workflow green main baseline.
2. Keep raw profile and runtime compatibility projection contracts separate.
3. Make the living plan executable.
4. Replace source-location checks with executable architecture contracts.
5. Remove remaining migration debt, then mutation-test critical logic.

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
- Phase 1: T-004 — delete legacy debt.
- Phase 2: T-006 → T-005 — semantic boundaries.
- Phase 3: T-007..T-008 — test-of-tests/edge coverage.
- Phase 4: T-009 — measured security/performance/reliability.
- Phase 5: T-010 — convergence re-audit.

# 11. Atomic Tasks

## T-001 — Reconcile v8 migration fallout and restore trustworthy baseline
**Status:** VERIFYING | **Priority:** P0 | **Type:** FIX | **Leverage:** HIGH  
**Problem:** four core/current-v8 workflows are green on `main`; F-017 Pages fix is green in preflight but Pages is main-only.  
**Goal:** all five applicable main workflows green without semantic weakening or legacy resurrection.  
**Scope:** HFSM policy/package, obsolete workflows, validators, ownership checks, orphan gitlink, raw/projection contracts, nullable fix, HUD check, F-015/F-016 reconciliation, F-017 Pages v8 publication, plan.  
**Non-goals:** new features/UX, schema bump, broad refactor, alias deletion, mutation threshold, live-NX redesign.  
**Implementation:** verify this post-F-017 plan-only tree; recheck current `main`; create one clean commit whose parent is current main and whose tree equals verified F-017 branch tree; `force=false`; then verify all workflows on the new main SHA.  
**Acceptance:** all five current workflows PASS on one main tree; Pages build/deploy succeeds; raw 439 IDs unique; alias projection fidelity PASS; no diagnostic artifacts; clean checkout.  
**Dependencies:** none. **Blocks:** T-002..T-010. **Risk:** Medium. **Rollback:** normal revert only.

## T-002 — Enforce MASTER_PLAN structure as executable gate
**Status:** READY | **Priority:** P1 | **Type:** HARDEN | **Leverage:** HIGH  
Pure Node validator: required headings, unique F/T IDs, vocabularies, iteration log, negative self-tests. Current plan and invalid fixtures must behave deterministically. Depends: T-001.

## T-003 — Capture machine-readable verified baseline
**Status:** TODO | **Priority:** P1 | **Type:** HARDEN | **Leverage:** HIGH  
Create deterministic manifest of verified SHA/checks/environment class, explicitly separating stubs from live NX. Depends: T-001; blocks T-009.

## T-004 — Delete retired K3–K5 and migration debt
**Status:** TODO | **Priority:** P1 | **Type:** REMOVE | **Leverage:** HIGH  
Usage-search legacy docs/scripts/assertions → classify → delete/update → full verification. Retain only proven-current helpers. Depends: T-001.

## T-005 — Finish remaining module-boundary adoption without semantic drift
**Status:** TODO | **Priority:** P1 | **Type:** IMPROVE | **Leverage:** HIGH  
For each boundary: characterize → introduce interface → compatibility → migrate → verify → remove legacy. Preserve IPC/live-NX behavior. Depends: T-001,T-006; blocks T-010.

## T-006 — Replace brittle source-text checks with executable architecture/projection tests
**Status:** TODO | **Priority:** P1 | **Type:** HARDEN | **Leverage:** HIGH  
F-004,F-005,F-010,F-013..F-017 demonstrate drift. Replace location-sensitive smoke with compile/reflection/behavior/negative-fixture contracts for Protocol, BridgeCore, CommandBridge, HotkeyStudio/UI and raw-v8/projection ownership. NXOpen stubs remain compile-only evidence. Depends: T-001; blocks T-005.

## T-007 — Mutation-test critical pure logic
**Status:** TODO | **Priority:** P2 | **Type:** HARDEN | **Leverage:** HIGH  
Stryker.NET pilot on Protocol/StateMachines, then pure projection/validators. Measure before setting threshold. Depends: T-001; blocks T-008.

## T-008 — Expand multidimensional edge-case coverage
**Status:** TODO | **Priority:** P2 | **Type:** HARDEN | **Leverage:** HIGH  
Dimensions: input × state × concurrency × timing × failure × permissions × config × external state. Pairwise + high-risk N-wise + reproducible property/fuzz/metamorphic cases. Depends: T-007; blocks T-009.

## T-009 — Establish performance, security and reliability baselines
**Status:** TODO | **Priority:** P2 | **Type:** HARDEN | **Leverage:** MEDIUM  
Measure leader transitions, normalization/parsing, alias projection, inbox admission, profile load, desktop cold start/publish size, warnings/flaky behavior. No live-NX claim from CI. Depends: T-003,T-008; blocks T-010.

## T-010 — Final convergence re-audit and delta implementation
**Status:** TODO | **Priority:** P2 | **Type:** IMPROVE | **Leverage:** HIGH  
Repeat architecture/correctness/security/concurrency/reliability/API/data/testing/mutation/performance/CI/docs/dependency audit until no fundamental delta. Depends: T-005,T-009.

# 12. Testing Strategy

Characterization precedes behavior change. Raw config is tested before `IJsonOnDeserialized` projection; projected behavior separately. Pure contracts use unit/property tests; Windows desktop/CommandBridge use integration/build checks; NXOpen stubs prove compile compatibility only; live NX is separate evidence. Negative security tests are first class. Native command errors propagate immediately. Preflight is the verification sandbox; `main` is not.

# 13. Mutation Testing Strategy

T-007 measures before gating. Priority: HMAC/auth/replay → context guards → state transitions → raw/profile validators → alias projection → normalization → inbox. Surviving mutants trigger observable-contract analysis, not coverage inflation.

# 14. Performance Baselines

Initially unknown. Candidate baselines: leader transition latency/allocations, normalization/parsing, projection, inbox admission/bounded poll, profile load/validation, desktop cold start/publish size. Live-NX latency requires live NX.

# 15. Security Hardening

Preserve HMAC, replay guard, permissions, context/source-process binding and bounded inbox. Aliases must not broaden adapter/security semantics. Never weaken validation merely to green CI.

# 16. Migration Strategy

`characterization → introduce boundary → dual compatibility → migrate callers → verify → remove legacy`. Removal includes deleting obsolete files and their scaffolding assertions. Restore independent HFSM policy only, never retired K3–K5 maps. Optional locks derive strictly from raw v8.

# 17. Deferred Work

- Live NX 2512 verification requiring installed NX.
- Mutation/performance thresholds until measured.
- Cosmetic UI work unrelated to findings.
- Broad refactor until T-001 green.
- Non-blocking compiler/analyzer warnings inventory under T-009 unless upgraded by evidence.

# 18. Rejected Decisions

- Restore K3–K5/full-command-map/hybrid profile to green CI — rejected: dual source of truth.
- Skip failing validators — rejected.
- Add fake capability lock — rejected.
- Delete secondary aliases/edit raw IDs to satisfy projected uniqueness — rejected.
- Re-add dummy `Compile Remove` for deleted `ConfigModels.cs` — rejected.
- Put NX Selection Intent literals back into keyboard hook merely to satisfy CI — rejected.
- Force/force-with-lease `main` — rejected.
- Treat NXOpen stubs as live NX proof — rejected.

# 19. Completed Tasks

None. `T-001` remains VERIFYING until the F-017 fix is non-force integrated and all five workflows pass on one `main` SHA.

# 20. Iteration Log

## Iteration 1 — Wave A
T-001; addressed F-001..F-006; discovered F-009..F-012. Restored HFSM policy/package, current-v8 fail-fast CI, retired obsolete workflows, corrected executor/transport checks, removed orphan gitlink. `fab88e79...`: docs/current validators/DFA PASS; deeper blockers exposed. Result: FAIL → reconciled.

## Iteration 1 — Wave B
T-001; addressed F-009..F-012; discovered F-013,F-014. Reconciled signing ownership, capability validator, nullable contract and retired Sketch legacy gate. `a5f9a718...`: strict Protocol/BridgeCore + DFA PASS; identity/Desktop stale checks exposed. Result: FAIL → reconciled.

## Iteration 1 — Wave C
T-001; addressed F-013,F-014; discovered F-015. Added raw uniqueness + projection-fidelity regression, HUD contract fix, removed diagnostics. `d08b8b25...`: Documentation/Desktop UI/Runtime hardening PASS; `ci` reached obsolete ConfigModels assertion. Result: FAIL → Wave D.

## Iteration 1 — Wave D
T-001; addressed F-015; discovered F-016. `b7f461bb...`: all executable stages through CommandBridge PASS; stale Selection Intent owner check failed. Result: FAIL → reconciled.

## Iteration 1 — Wave E
T-001; addressed F-016. `709fa114e4fa03ab82f86caf02cd1bdc4a893491`: full `ci` PASS including adaptive/deployment/artifacts. Result: PASS.

## Iteration 1 — Clean main integration
Verified tree was condensed to one commit `2b28da717d4325b767c240851d487ae7717a3cb0` with parent `2359e85...`, then non-force fast-forwarded. On that main SHA `ci`, Documentation, Desktop UI and Runtime hardening PASS; Pages exposed F-017. Result: 4/5 PASS → F-017 branch.

## Iteration 1 — F-017 Pages preflight
T-001; changed Pages trigger/copy/test/grep from removed `nx2512-pro-hybrid.json` to canonical `nx2512-v8-profile.json`, preserving independent state-machine policy. Full `ci` on `73a5fed49ef1eeb7880979ee24e7b5539dcbb2df` PASS. Production/runtime code unchanged. Result: PASS → post-verification plan reconciliation and clean main integration selected.

# 21. Definition of Final Done

- no known Critical/High findings;
- all P0/P1 DONE or evidence-backed REJECTED/DEFERRED;
- applicable CI consistently green with no masked native failures;
- raw canonical identity and runtime alias fidelity enforced separately;
- architecture rules technically protected where feasible;
- critical security/state logic mutation-tested with no unexplained high-risk survivors;
- multidimensional critical edge space covered;
- performance/security/reliability baselines recorded and acceptable;
- no unexplained flaky tests;
- docs match current v8 and no active gate depends on absent legacy assets/scaffolding or stale source ownership;
- live-NX-only claims have explicit verification records;
- repeat audit finds no fundamental delta;
- `MASTER_PLAN.md`, code/tests and last verified `main` SHA agree.
