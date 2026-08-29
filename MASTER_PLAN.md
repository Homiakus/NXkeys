# 1. Mission

Сделать NXkeys устойчивой, проверяемой и эволюционирующей системой, где `MASTER_PLAN.md` — единственный living execution plan, а существенные изменения проходят цикл `AUDIT → PLAN → SELECT → IMPLEMENT → TEST → REVIEW → RECONCILE → COMMIT → VERIFIED PUSH → COMPRESS`.

Текущий приоритет: завершить `T-001`, восстановив полностью зелёный current-v8 baseline без возврата удалённого K3–K5/full-command-map контура и без изменения runtime semantics ради CI.

# 2. Current State

- C#/.NET 8, Windows x64, Siemens NX / Designcenter NX 2512.
- Canonical source: `config/nx2512-v8-profile.json`, schema 8; 439 raw operations / 439 unique `operation_id`, 131 executable button mappings.
- `Config : IJsonOnDeserialized` через `V8SecondaryAliasExpander` материализует `secondary_aliases` в compatibility projection: 500 projected rows / 439 semantic IDs.
- IPC schema 4: HMAC authentication, replay guard, permission/context/source-process binding.
- Boundaries: HotkeyStudio → Protocol/StateMachines → BridgeCore → CommandBridge/NXOpen; ControlCenter и NxEskd — отдельные surfaces.
- Baseline `main`: `2359e85c02313f4c6227a5e9a38ef62e9b9c043f`; 5/5 push workflows тогда были red.
- Preflight branch: `audit/living-master-plan-bootstrap-20260829`; `main` не меняется до verified PASS.
- Wave D head `b7f461bb6f05f6c3461122d83497f837932a6441`: validators, DFA/HFSM, HotkeyStudio, NxEskd 55+27 tests, HotkeyStudio/ControlCenter build+publish, NXOpen stubs и CommandBridge compile PASS; `ci` упал только на F-016 — stale Selection Intent source-owner assertion. Deployment gate ещё не выполнен из-за fail-fast.
- Documentation, Desktop UI и Runtime hardening уже имеют полностью зелёные прогоны на текущем логическом дереве до Wave D-only CI edits.
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

| Check | Original main | Current preflight evidence |
|---|---|---|
| Push workflows | 5/5 FAIL | Documentation/Desktop UI/Runtime hardening PASS; `ci` one stale late gate |
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
| Adaptive invariants | blocked | only F-016 source-owner assertion remains |
| Deployment invariants | not reached | pending after F-016 |
| Live NX | NOT RUN | external requirement |
| Mutation / performance baselines | absent / unknown | T-007 / T-009 |

Pre-existing failures remain separated from defects introduced by current work.

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

## F-001 — Main has no trustworthy green baseline
**Status:** Planned | **Severity:** Critical | **Category:** Reliability/CI | **Confidence:** Confirmed  
Evidence: baseline `2359e85...` had 5/5 red push workflows. Root cause: incomplete v8 migration + stale automation. Impact: repository-wide delivery risk. Tasks: T-001. Direction: recover current-v8 baseline first.

## F-002 — Active CI depended on removed K3–K5/full-command-map assets
**Status:** Planned | **Severity:** High | **Category:** CI/Migration | **Confidence:** Confirmed  
Evidence: obsolete workflows referenced absent legacy maps. Impact: permanent-red gates. Invariants: 1,2,15. Tasks: T-001,T-004. Direction: retire obsolete gates; never restore dual source-of-truth.

## F-003 — Independent state-machine policy was accidentally removed
**Status:** Planned | **Severity:** High | **Category:** Correctness/Packaging | **Confidence:** Confirmed  
Evidence: runtime/tests consume `nx2512-state-machines.json`, including `root_ms=4000`. Impact: fallback behavior/timing. Tasks: T-001. Direction: restore independent policy only.

## F-004 — Runtime hardening checked transport result in wrong file
**Status:** Planned | **Severity:** Medium | **Category:** Testing/Architecture | **Confidence:** Confirmed  
Evidence: `NxTransportReadStatus` moved to its semantic owner. Impact: false-red hardening. Tasks: T-001,T-006. Direction: ownership-aware/executable test.

## F-005 — Selection validator assumed effect remained in `Program.cs`
**Status:** Planned | **Severity:** Medium | **Category:** Testing/Architecture | **Confidence:** Confirmed  
Evidence: dispatch lives in `Program.ProcessClaim`, effect in executor. Impact: false failure. Tasks: T-001,T-006. Direction: verify dispatch/effect boundaries separately.

## F-006 — Orphan `.claude` worktree gitlink
**Status:** Planned | **Severity:** Medium | **Category:** Repository Integrity | **Confidence:** Confirmed  
Evidence: mode 160000 without `.gitmodules`. Impact: checkout noise/failure. Task: T-001. Direction: delete gitlink, do not fabricate submodule metadata.

## F-007 — Documentation still describes retired K3–K5 assets
**Status:** Open | **Severity:** Medium | **Category:** Documentation/Migration | **Confidence:** Strong  
Impact: mixed current/legacy onboarding. Tasks: T-004. Direction: evidence-based deletion/update after baseline recovery.

## F-008 — Critical pure logic has no mutation baseline
**Status:** Open | **Severity:** Medium | **Category:** Test-of-tests | **Confidence:** Strong  
Impact: possible false confidence in auth/state/validation tests. Task: T-007. Direction: targeted Stryker.NET pilot.

## F-009 — `Sketch intent grammar` workflow is retired K3–K5 pipeline
**Status:** Planned | **Severity:** High | **Category:** CI/Migration | **Confidence:** Confirmed  
Impact: duplicate permanent-red gate. Tasks: T-001,T-004. Direction: retire while retaining v8 regression coverage.

## F-010 — Signing assertion ignored `NxRequestSigningPolicy` extraction
**Status:** Planned | **Severity:** Medium | **Category:** Testing/Architecture | **Confidence:** Confirmed  
Evidence: facade delegates to signing policy. Tasks: T-001,T-006. Direction: assert facade/policy ownership independently.

## F-011 — Capability-lock test and validator disagreed about optionality
**Status:** Planned | **Severity:** Medium | **Category:** Testing/Contract | **Confidence:** Confirmed  
Expected: raw v8 is truth; optional derived lock exact-matches if present. Tasks: T-001,T-006.

## F-012 — Strict Protocol build exposed nullable contract mismatch
**Status:** Planned | **Severity:** Medium | **Category:** Correctness/Static analysis | **Confidence:** Confirmed  
Symbol: `NxContextNormalization.NormalizeV8Module`. Direction: behavior-preserving nullable annotation. Task: T-001.

## F-013 — Desktop UI gate hardcoded obsolete 10-row HUD contract
**Status:** Planned | **Severity:** Medium | **Category:** Testing/UI Contract | **Confidence:** Confirmed  
Evidence: production contract is `PrimarySuggestionLimit = 8`. Tasks: T-001,T-006. Direction: semantic/current public contract.

## F-014 — C# identity regression confused raw operations with alias projections
**Status:** Planned | **Severity:** Medium | **Category:** Testing/Data Contract | **Confidence:** Confirmed  
Evidence: raw = 439 unique; post-deserialization = 500 rows/439 semantic IDs. Tasks: T-001,T-006. Direction: raw uniqueness + projection fidelity, never delete valid aliases.

## F-015 — Adaptive CI required exclusion for already-deleted `ConfigModels.cs`
**Status:** Planned | **Severity:** Medium | **Category:** Testing/Migration | **Confidence:** Confirmed  
Evidence: project builds/publishes while file is physically absent; old gate demanded literal `<Compile Remove>`. Tasks: T-001,T-004,T-006. Direction: fail if legacy file returns, not if stale exclusion disappears.

## F-016 — Selection Intent CI checks executor semantics in keyboard-hook file
**Status:** Planned | **Severity:** Medium | **Category:** Testing/Architecture | **Confidence:** Confirmed  
**Evidence:** Wave D `ci` passes all executable suites/builds through CommandBridge compile, then searches `UG_SEL_CHAINING`, `UG_SC_INFERRED_CURVE_SELECTION`, `CreateRule*` and `RequestSelections` only in `SelectionIntentHotkeys.cs`; that file now maps physical keys and delegates to `NxSelectionExecutor.TryApplyIntent`, while `NxSelectionExecutor.cs` owns the NX menu IDs, ScRuleFactory fallbacks and selection updates.  
**Current behavior:** valid hook→executor refactor is reported as missing Selection Intent runtime.  
**Expected behavior:** CI verifies delegation in the hook and NX Selection Intent semantics in the executor owner.  
**Root cause:** source-location assertion was not reconciled after extracting NX effects.  
**Impact:** final adaptive gate red; deployment gate skipped.  
**Blast radius:** main CI only; executable tests/builds are green.  
**Reproduction:** `b7f461bb...`, `Validate adaptive state and protocol invariants`, missing `UG_SEL_CHAINING`.  
**Affected invariants:** 1,10,13,14.  
**Related findings:** F-004,F-005,F-010,F-015.  
**Affected tasks:** T-001,T-006.  
**Recommended direction:** retain a hook delegation assertion and move NX semantic-token ownership check to `NxSelectionExecutor`; later replace source-text smoke with executable architecture tests under T-006.

# 7. Risk Register

| Risk | P | Impact | Mitigation |
|---|---:|---:|---|
| another stale late-stage gate appears | High | Medium | fail → finding → reconcile; no blind skip |
| alias projection broadens semantics | Medium | High | raw uniqueness + projection fidelity |
| source-text CI drifts after refactor | High | Medium | T-006 executable architecture tests |
| restored HFSM differs in live NX | Medium | High | explicit live-NX checklist |
| NXOpen stubs differ from NX 2512 | Medium | High | never treat stubs as live proof |
| mutation suite noisy/slow | Medium | Medium | targeted pure projects first |

# 8. Pareto Improvements

1. Finish green/fail-fast current-v8 baseline.
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
**Problem:** `main` has no green trusted baseline; current preflight has only F-016 after F-015 was corrected.  
**Goal:** all applicable current-v8 Actions green without semantic weakening or legacy resurrection.  
**Scope:** HFSM policy/package, obsolete workflows, validators, source-boundary checks, orphan gitlink, raw/projection contracts, nullable fix, HUD check, F-015/F-016 CI ownership reconciliation, plan.  
**Non-goals:** new features/UX, schema bump, broad refactor, alias deletion, mutation threshold, live-NX redesign.  
**Implementation:** verify `SelectionIntentHotkeys → NxSelectionExecutor` delegation and check NX Selection Intent primitives in executor; run full preflight including deployment; classify any new failure before more edits. On all-green preflight, resync `main`, self-review net diff, create one logical commit directly on current main, non-force push, verify main Actions.  
**Acceptance:** all current workflows PASS on one tree; deployment executes; raw 439 IDs unique; alias projection fidelity PASS; no diagnostic artifacts; clean checkout; main rechecked before push.  
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
F-004,F-005,F-010,F-013..F-016 demonstrate drift. Replace location-sensitive smoke with compile/reflection/behavior/negative-fixture contracts for Protocol, BridgeCore, CommandBridge, HotkeyStudio/UI and raw-v8/projection ownership. NXOpen stubs remain compile-only evidence. Depends: T-001; blocks T-005.

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

- Restore K3–K5/full-command-map to green CI — rejected: dual source of truth.
- Skip failing validators — rejected.
- Add fake capability lock — rejected.
- Delete secondary aliases/edit raw IDs to satisfy projected uniqueness — rejected.
- Re-add dummy `Compile Remove` for deleted `ConfigModels.cs` — rejected.
- Put NX Selection Intent literals back into keyboard hook merely to satisfy CI — rejected: violates ownership.
- Force/force-with-lease `main` — rejected.
- Treat NXOpen stubs as live NX proof — rejected.

# 19. Completed Tasks

None. `T-001` remains VERIFYING until a completely green preflight is reconciled, one equivalent logical commit is non-force pushed onto current `main`, and main verification succeeds.

# 20. Iteration Log

## Iteration 1 — Wave A
T-001; addressed F-001..F-006; discovered F-009..F-012. Restored HFSM policy/package, current-v8 fail-fast CI, retired obsolete workflows, corrected executor/transport checks, removed orphan gitlink. `fab88e79...`: docs/current validators/DFA PASS; deeper blockers exposed. Result: FAIL → reconciled.

## Iteration 1 — Wave B
T-001; addressed F-009..F-012; discovered F-013,F-014. Reconciled signing ownership, capability validator, nullable contract and retired Sketch legacy gate. `a5f9a718...`: strict Protocol/BridgeCore + DFA PASS; identity/Desktop stale checks exposed. Result: FAIL → reconciled.

## Iteration 1 — Wave C
T-001; addressed F-013,F-014; discovered F-015. Added raw uniqueness + projection-fidelity regression, HUD contract fix, removed diagnostics. `d08b8b25...`: Documentation/Desktop UI/Runtime hardening PASS; `ci` reached obsolete ConfigModels assertion after all executable checks. Result: FAIL → Wave D.

## Iteration 1 — Wave D
T-001; addressed F-015; discovered F-016. Replaced dummy csproj-exclusion requirement with fail-if-legacy-file-exists. `b7f461bb...`: validators, DFA/HFSM, HotkeyStudio, NxEskd 55+27, HotkeyStudio/ControlCenter build+publish, NXOpen stubs and CommandBridge compile PASS; adaptive gate then failed because Selection Intent NX semantics were searched in keyboard-hook owner. Result: FAIL → reconciled; Wave E selected.

## Iteration 1 — Wave E
T-001; addresses F-016. Planned minimal change: assert `SelectionIntentHotkeys` delegates to `NxSelectionExecutor.TryApplyIntent`; inspect NX menu IDs/ScRuleFactory/`RequestSelections` in `NxSelectionExecutor`. Production runtime unchanged. Full preflight including deployment required before main integration. Result: VERIFYING.

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
