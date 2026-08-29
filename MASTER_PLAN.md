# 1. Mission

Сделать NXkeys устойчивой, проверяемой и эволюционирующей системой, где `MASTER_PLAN.md` — единственный living execution plan, а существенные изменения проходят цикл `AUDIT → PLAN → SELECT → IMPLEMENT → TEST → REVIEW → RECONCILE → COMMIT → VERIFIED PUSH → COMPRESS`.

Текущий приоритет: восстановить достоверный зелёный baseline после неполного перехода на канонический v8-профиль, не возвращая удалённый K3–K5/full-command-map контур как production dependency.

# 2. Current State

- C#/.NET 8, Windows x64, Siemens NX / Designcenter NX 2512.
- Канонический runtime profile: `config/nx2512-v8-profile.json`, schema 8.
- IPC: schema 4, HMAC authentication, replay guard, permission/context binding.
- Boundaries: HotkeyStudio → Protocol/StateMachines → BridgeCore → CommandBridge/NXOpen; ControlCenter и NxEskd — отдельные capability/UI surfaces.
- Baseline `main` до внедрения подхода: `2359e85c02313f4c6227a5e9a38ef62e9b9c043f`; все 5 workflow, запущенные тем push, были красными.
- Preflight branch: `audit/living-master-plan-bootstrap-20260829`; `main` остаётся неизменным до PASS.
- Wave A `fab88e79...`: раскрыл более глубокие blockers.
- Wave B `a5f9a718...`: strict Protocol/BridgeCore builds, source hardening и DFA/HFSM уже PASS; выявлены stale Desktop UI assertion и реальные duplicate `operation_id` в v8.
- Локальный GitHub clone в текущем контейнере недоступен из-за network/DNS ограничения; Windows GitHub Actions является доступной authoritative preflight-средой.

# 3. Architecture Map

```text
Keyboard / CapsLock leader
        ↓
NX2512_HotkeyStudio
  LeaderKeyEngine / NxContextClient
  NxCommandBridgeClient → NxRequestSigningPolicy
        ↓ authenticated file IPC
NXKeys.Protocol ← NXKeys.StateMachines
        ↓
NXKeys.BridgeCore (bounded admitted inbox)
        ↓
NX2512_CommandBridge
  NxContextMonitor / NxMenuCommandExecutor /
  NxSelectionExecutor / NxInterventionGuard
        ↓
NXOpen / NX UI / NxEskd capabilities
```

Ownership rules: Protocol owns shared contracts/security/normalization; StateMachines owns deterministic leader policy; HotkeyStudio owns out-of-NX orchestration/UI; signing policy owns request signing/admission preparation; BridgeCore owns background admission; CommandBridge owns in-NX NXOpen effects.

# 4. Baseline

| Check | Baseline `2359e85...` | Latest preflight evidence |
|---|---|---|
| Push workflows | 5/5 FAIL | Wave B: 1 PASS, 3 FAIL; obsolete workflows already retired |
| Current Node validators | masked/stale | PASS through CI step |
| DFA/HFSM | FAIL missing policy | PASS |
| Protocol strict build | previously blocked | PASS, 0 warnings/errors |
| BridgeCore strict build | previously blocked | PASS, 0 warnings/errors |
| Runtime source hardening | stale locations | PASS after ownership reconciliation |
| HotkeyStudio regressions | blocked | FAIL on newly exposed duplicate `operation_id` |
| Desktop UI | not in initial 5 | FAIL on stale `MaximumRootRows = 10` assertion |
| Documentation | FAIL legacy map | PASS in Wave A; no new docs defect observed |
| checkout integrity | orphan gitlink warning | clean after removal |
| NxEskd / desktop / NXOpen bridge builds | blocked downstream | pending deeper progression |
| live NX | NOT RUN | requires installed NX 2512 |
| coverage / mutation / benchmarks | unknown / none / unknown | T-007/T-009 |

Pre-existing defects remain classified separately from changes introduced by this iteration.

# 5. System Invariants

1. `main` never knowingly moves to a worse verified state.
2. v8 schema 8 is the canonical operation/profile source of truth.
3. Independent HFSM policy consumed by `LeaderBehaviorProfile` must ship with runtime/tests.
4. Out-of-NX code never performs NXOpen effects directly.
5. IPC admission remains authenticated, permission-bound, replay-resistant and context-bound.
6. NX UI thread never performs unbounded queue scanning or cryptographic admission.
7. Selection-filter actions flow through shared protocol to the in-NX executor boundary.
8. Context normalization has one canonical Protocol implementation.
9. Significant unexpected findings are recorded/reconciled before scope expands.
10. Tests and observed behavior outrank prior roadmap assumptions.
11. CI asserts semantic contracts, not incidental source-file locations.
12. Deleted K3–K5 assets are not recreated merely to silence stale checks.
13. Canonical operation identity must be deterministic: either `operation_id` is globally unique, or a stronger explicitly documented composite identity must be enforced end-to-end.

# 6. Findings Registry

## F-001 — Latest main has no trustworthy green baseline
**Status:** Planned  
**Category:** Reliability / CI  
**Severity:** Critical  
**Confidence:** Confirmed  
**Evidence:** all five push workflows on baseline SHA failed.  
**Files / symbols:** `.github/workflows/*`.  
**Current behavior:** no trustworthy regression signal.  
**Expected behavior:** applicable current-v8 gates green.  
**Root cause:** incomplete v8 migration plus stale automation.  
**Impact:** repository-wide delivery risk.  
**Blast radius:** all changes/releases.  
**Reproduction:** inspect Actions on `2359e85...`.  
**Affected invariants:** 1, 10.  
**Related findings:** F-002..F-006, F-009..F-014.  
**Affected tasks:** T-001.  
**Recommended direction:** recover baseline before feature/refactor work.

## F-002 — Active CI depends on removed K3–K5/full-command-map assets
**Status:** Planned  
**Category:** CI / Migration  
**Severity:** High  
**Confidence:** Confirmed  
**Evidence:** v8-only `config/` while old workflows referenced `full-command-map`/`nx2512-pro-*`.  
**Files / symbols:** old map/runtime/sketch workflows and legacy scripts.  
**Current behavior:** ENOENT/permanent red gates.  
**Expected behavior:** v8-only active gates.  
**Root cause:** automation not reconciled with cleanup.  
**Impact:** multiple workflows fail.  
**Blast radius:** CI/docs/profile pipeline.  
**Reproduction:** baseline Actions.  
**Affected invariants:** 1,2,12.  
**Related findings:** F-007,F-009.  
**Affected tasks:** T-001,T-004.  
**Recommended direction:** retire active obsolete gates; delete/archive remaining debt separately.

## F-003 — Independent state-machine policy was accidentally removed
**Status:** Planned  
**Category:** Correctness / Packaging  
**Severity:** High  
**Confidence:** Confirmed  
**Evidence:** runtime/test code consumes `nx2512-state-machines.json`; tests require `root_ms=4000`, but cleanup removed the file.  
**Files / symbols:** `LeaderBehaviorProfile`, HotkeyStudio/StateMachines test csproj.  
**Current behavior:** fallback timing and red tests.  
**Expected behavior:** schema-1 policy is packaged independently of v8 command profile.  
**Root cause:** two independent config contracts conflated during cleanup.  
**Impact:** leader behavior/timing.  
**Blast radius:** runtime/package/tests.  
**Reproduction:** baseline DFA/HFSM suite.  
**Affected invariants:** 3,10.  
**Related findings:** F-001.  
**Affected tasks:** T-001.  
**Recommended direction:** restore policy only, not retired command maps.

## F-004 — Runtime hardening checked transport result in wrong source file
**Status:** Planned  
**Category:** Testing / Architecture  
**Severity:** Medium  
**Confidence:** Confirmed  
**Evidence:** `NxTransportReadStatus` lives in `NxTransportReadResult.cs`; old gate searched client.  
**Files / symbols:** `runtime-hardening.yml`.  
**Current behavior:** valid extraction reported as defect.  
**Expected behavior:** semantic owner checked.  
**Root cause:** brittle source-text assertion.  
**Impact:** hardening gate red.  
**Blast radius:** runtime CI.  
**Reproduction:** Wave A.  
**Affected invariants:** 1,11.  
**Related findings:** F-010,F-013.  
**Affected tasks:** T-001,T-006.  
**Recommended direction:** reconcile now; architecture tests later.

## F-005 — Selection validator assumes NXOpen effect remains in `Program.cs`
**Status:** Planned  
**Category:** Testing / Architecture  
**Severity:** Medium  
**Confidence:** Confirmed  
**Evidence:** dispatch is in `Program.ProcessClaim`; effect moved to `NxMenuCommandExecutor.ApplySelectionCommand`.  
**Files / symbols:** `validate-command-tree.mjs`, CommandBridge.  
**Current behavior:** false failure previously masked.  
**Expected behavior:** dispatch/effect ownership tested separately.  
**Root cause:** validator lagged refactor.  
**Impact:** hidden contract failure.  
**Blast radius:** command-tree gate.  
**Reproduction:** current validator before Wave A fix.  
**Affected invariants:** 7,11.  
**Related findings:** F-004.  
**Affected tasks:** T-001,T-006.  
**Recommended direction:** follow executor boundary.

## F-006 — Orphan worktree gitlink committed under `.claude`
**Status:** Planned  
**Category:** Repository Integrity  
**Severity:** Medium  
**Confidence:** Confirmed  
**Evidence:** mode `160000` entry with no `.gitmodules`; checkout cleanup warns/fails submodule operation.  
**Files / symbols:** `.claude/worktrees/fix-v8-contract-paths`.  
**Current behavior:** checkout noise/fragility.  
**Expected behavior:** no agent-local gitlink.  
**Root cause:** accidental worktree metadata commit.  
**Impact:** CI/tooling reliability.  
**Blast radius:** every checkout.  
**Reproduction:** Actions checkout cleanup.  
**Affected invariants:** 1.  
**Related findings:** F-001.  
**Affected tasks:** T-001.  
**Recommended direction:** delete gitlink; never fabricate submodule config.

## F-007 — Documentation still describes retired K3–K5 assets
**Status:** Open  
**Category:** Documentation / Migration debt  
**Severity:** Medium  
**Confidence:** Strong  
**Evidence:** README/full-map/audit narrative references absent legacy assets.  
**Files / symbols:** README, `FULL_COMMAND_MAP.md`, docs/audit, legacy scripts.  
**Current behavior:** mixed current/legacy onboarding.  
**Expected behavior:** one current v8 story with Git history for legacy.  
**Root cause:** cleanup debt.  
**Impact:** maintainer confusion.  
**Blast radius:** onboarding/tooling.  
**Reproduction:** compare docs to repository tree.  
**Affected invariants:** 2,12.  
**Related findings:** F-002,F-009.  
**Affected tasks:** T-004.  
**Recommended direction:** evidence-based debt deletion pass.

## F-008 — Critical pure logic has no mutation baseline
**Status:** Open  
**Category:** Test-of-tests  
**Severity:** Medium  
**Confidence:** Strong  
**Evidence:** no enforced mutation tooling/score discovered.  
**Files / symbols:** Protocol security, StateMachines, validators.  
**Current behavior:** coverage can miss weak assertions.  
**Expected behavior:** meaningful mutants killed for critical contracts.  
**Root cause:** test infrastructure gap.  
**Impact:** false confidence.  
**Blast radius:** auth/state/validation logic.  
**Reproduction:** inspect CI/tooling.  
**Affected invariants:** 5,10.  
**Related findings:** none.  
**Affected tasks:** T-007.  
**Recommended direction:** targeted Stryker.NET pilot then evidence-based threshold.

## F-009 — `Sketch intent grammar` workflow is a retired K3–K5 pipeline
**Status:** Planned  
**Category:** CI / Migration  
**Severity:** High  
**Confidence:** Confirmed  
**Evidence:** Wave A invokes legacy map compiler/validator and fails on removed map files.  
**Files / symbols:** `.github/workflows/sketch-intent.yml`.  
**Current behavior:** duplicate permanent-red gate.  
**Expected behavior:** current v8 sketch grammar tested by HotkeyStudio/current CI.  
**Root cause:** obsolete workflow survived cleanup.  
**Impact:** push gate red.  
**Blast radius:** command/state changes.  
**Reproduction:** Wave A.  
**Affected invariants:** 1,2,12.  
**Related findings:** F-002.  
**Affected tasks:** T-001,T-004.  
**Recommended direction:** retire workflow, retain v8 regression coverage.

## F-010 — Signing assertion ignores `NxRequestSigningPolicy` extraction
**Status:** Planned  
**Category:** Testing / Architecture  
**Severity:** Medium  
**Confidence:** Confirmed  
**Evidence:** client delegates `policy.PrepareAuthenticated`; HMAC/permissions live in extracted signing policy.  
**Files / symbols:** client, signing policy, hardening workflow.  
**Current behavior:** valid security boundary failed Wave A.  
**Expected behavior:** facade and policy ownership asserted independently.  
**Root cause:** stale file-location contract.  
**Impact:** hardening blocked.  
**Blast radius:** runtime security CI.  
**Reproduction:** Wave A hardening.  
**Affected invariants:** 5,11.  
**Related findings:** F-004,F-013.  
**Affected tasks:** T-001,T-006.  
**Recommended direction:** reconcile source gate now; replace with architecture test later.

## F-011 — Capability lock test and validator disagree about lock optionality
**Status:** Planned  
**Category:** Testing / Contract  
**Severity:** Medium  
**Confidence:** Confirmed  
**Evidence:** validator allows absent lock while C# regression required file existence.  
**Files / symbols:** `validate-capability-route-lock.mjs`, HotkeyStudio tests.  
**Current behavior:** deleted auxiliary file blocks suite.  
**Expected behavior:** canonical v8 is source of truth; optional derived lock must match exactly if present.  
**Root cause:** stale test + weak optional-lock validation.  
**Impact:** main CI blocked.  
**Blast radius:** profile tests.  
**Reproduction:** Wave A main CI.  
**Affected invariants:** 2,10,12.  
**Related findings:** F-014.  
**Affected tasks:** T-001,T-006.  
**Recommended direction:** test canonical profile directly; exact-compare optional lock.

## F-012 — Strict Protocol build exposed nullable contract mismatch
**Status:** Planned  
**Category:** Correctness / Static analysis  
**Severity:** Medium  
**Confidence:** Confirmed  
**Evidence:** nullable-enabled Protocol helper returned `null` from non-nullable `string`; Wave B strict build passes after annotation repair.  
**Files / symbols:** `NxContextNormalization.NormalizeV8Module`.  
**Current behavior:** baseline warning would fail `-warnaserror`.  
**Expected behavior:** explicit nullable contract.  
**Root cause:** annotation mismatch.  
**Impact:** strict build blocker.  
**Blast radius:** Protocol.  
**Reproduction:** strict build before Wave B.  
**Affected invariants:** 1,8,10.  
**Related findings:** none.  
**Affected tasks:** T-001.  
**Recommended direction:** behavior-preserving nullable annotation.

## F-013 — Desktop UI gate hardcodes obsolete 10-row HUD contract
**Status:** Planned  
**Category:** Testing / UI Contract  
**Severity:** Medium  
**Confidence:** Confirmed  
**Evidence:** Wave B Desktop UI expects literal `MaximumRootRows = 10`; actual code defines public `PrimarySuggestionLimit = 8` and `MaximumRootRows = PrimarySuggestionLimit`; executable HotkeyStudio regression also expects 8.  
**Files / symbols:** `.github/workflows/desktop-ui.yml`, `LeaderHudForm.PrimarySuggestionLimit`.  
**Current behavior:** valid current 8-item compact HUD fails before tests.  
**Expected behavior:** source smoke check follows tested public contract or is replaced by executable test.  
**Root cause:** UI constant changed without workflow reconciliation.  
**Impact:** Desktop UI permanently red.  
**Blast radius:** UI workflow.  
**Reproduction:** Wave B run `33255530117`.  
**Affected invariants:** 1,10,11.  
**Related findings:** F-004,F-010.  
**Affected tasks:** T-001,T-006.  
**Recommended direction:** reconcile smoke check to 8/alias now; migrate to executable architecture/UI test later.

## F-014 — Canonical v8 contains duplicate `operation_id` values
**Status:** Planned  
**Category:** Data Model / Correctness  
**Severity:** High  
**Confidence:** Confirmed  
**Evidence:** new canonical-profile regression in Wave B fails uniqueness; `Config.TranslateV8OperationsToLegacy()` iterates every operation, while Bridge permission derivation also iterates the v8 operations list.  
**Files / symbols:** `config/nx2512-v8-profile.json`, `OperationContract.OperationID`, v8 translator, `NxBridgePermissionSet.FromV8Operations`.  
**Current behavior:** operation identity is ambiguous unless duplicates are intentional under an undocumented composite identity.  
**Expected behavior:** globally unique stable operation IDs, or an explicitly modeled/enforced composite identity used consistently by UI, routing, security and docs.  
**Root cause:** not yet proven; possible duplicated generated rows or identity semantics omitted from schema.  
**Impact:** potential ambiguous lookup/routing, duplicated or overwritten permission/presentation state, unstable generated locks/docs.  
**Blast radius:** profile translation, UI operation lookup, bridge allowlist, generated artifacts.  
**Reproduction:** Wave B HotkeyStudio regression; next diagnostic must list duplicate groups with app/adapter/path.  
**Affected invariants:** 2,10,13.  
**Related findings:** F-011.  
**Affected tasks:** T-001; may create foundational task if identity model requires architecture change.  
**Recommended direction:** diagnose exact groups first; never weaken uniqueness assertion until identity semantics are evidenced.

# 7. Risk Register

| Risk | Probability | Impact | Mitigation |
|---|---:|---:|---|
| additional blockers appear as CI progresses | High | High | wave → classify → finding → reconcile |
| F-014 is architectural rather than data cleanup | Medium | High | inspect duplicate contexts and all consumers before edit |
| source-text checks keep drifting | High | Medium | T-006 architecture tests |
| restored HFSM differs from live UX | Medium | High | executable tests + live NX checklist |
| NXOpen stubs diverge from NX 2512 | Medium | High | never claim live behavior from stub CI |
| mutation suite noisy/slow | Medium | Medium | critical pure projects first |

# 8. Pareto Improvements

1. Recover green/fail-fast baseline, including deterministic operation identity.
2. Make living plan structure executable.
3. Replace brittle source-text gates with executable architecture tests.
4. Delete legacy K3–K5 migration debt.
5. Mutation-test Protocol/StateMachines before broad test expansion.

# 9. Dependency DAG

```text
T-001 baseline + identity recovery
 ├─ T-002 plan gate
 ├─ T-003 verified baseline artifact
 ├─ T-004 legacy debt deletion
 ├─ T-006 architecture tests ─ T-005 remaining boundary migration
 └─ T-007 mutation pilot ─ T-008 multidimensional edge coverage
T-003 + T-005 + T-008 ─ T-009 measured hardening ─ T-010 re-audit/convergence
```

# 10. Implementation Phases

- Phase 0: T-001..T-003 — recover trust/governance.
- Phase 1: T-004 — delete migration debt.
- Phase 2: T-006 → T-005 — stabilize architecture boundaries.
- Phase 3: T-007..T-008 — test-of-tests/edge space.
- Phase 4: T-009 — measurable performance/security/reliability.
- Phase 5: T-010 — full re-audit and delta loop.

# 11. Atomic Tasks

## T-001 — Reconcile v8 migration fallout and restore a trustworthy baseline
**Status:** VERIFYING  
**Priority:** P0  
**Type:** FIX  
**Leverage:** HIGH
### Problem
`main` cannot establish a trustworthy release signal and Wave B exposed ambiguous operation identity.
### Evidence
F-001..F-006, F-009..F-014.
### Goal
All applicable current-v8 Actions green, no masked/legacy failures, deterministic operation identity.
### Scope
HFSM policy/package; obsolete workflow retirement; fail-fast validators; executor/transport/signing/UI ownership checks; orphan gitlink; capability contract; Protocol nullable contract; diagnose and minimally resolve F-014; plan reconciliation.
### Non-goals
No new feature/UX redesign, broad module refactor, live-NX semantic redesign, mutation threshold.
### Files / symbols
current workflows/validators/tests, v8 profile/identity consumers, `MASTER_PLAN.md`.
### Implementation
Preflight waves only; for F-014: diagnostic evidence → impact analysis → data fix or explicit composite-identity task; never bypass assertion. After a fully green preflight, recreate one logical verified iteration commit on current `main` and push non-force.
### Invariants
1–13.
### Compatibility constraints
No restored K3–K5 production dependency; IPC schema/security semantics preserved.
### Edge cases
missing/stale policy; duplicate IDs across apps/routes/adapters; source refactors; native exit masking; checkout gitlinks; strict nullable builds.
### Tests
Node validators; DFA/HFSM; HotkeyStudio regressions; NxEskd tests; strict Protocol/BridgeCore; desktop builds/publish; NXOpen stubs/bridge contract; Docs/Desktop/Hardening Actions.
### Mutation tests
Deferred to T-007; absence explicitly recorded.
### Benchmarks
N/A for migration recovery.
### Acceptance criteria
Every applicable preflight workflow PASS; duplicate identity resolved/enforced; no legacy ENOENT; clean checkout; state policy shipped; downstream builds/tests run; current main re-synced; final diff reviewed.
### Verification commands
Commands encoded in active Actions plus targeted diagnostic validator.
### Dependencies
None.
### Blocks
T-002..T-010.
### Risk
High until F-014 semantics are known.
### Rollback
Normal revert only; never force push or restore stale assets.
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
No validator before this effort.
### Goal
Machine-check required sections, unique IDs and vocabularies.
### Scope
Pure Node validator + negative self-tests + CI.
### Non-goals
No automated semantic truth judgment.
### Files / symbols
plan, `scripts/validate-master-plan.mjs`, CI.
### Implementation
required headings; unique F/T IDs; status/priority/type/leverage; iteration log; self-test.
### Invariants
9,10.
### Compatibility constraints
no npm dependencies.
### Edge cases
duplicate IDs/missing metadata/malformed log.
### Tests
positive current plan + negative fixtures.
### Mutation tests
later if valuable.
### Benchmarks
sub-second.
### Acceptance criteria
malformed plan rejected, current plan accepted.
### Verification commands
`node scripts/validate-master-plan.mjs --self-test`; normal invocation.
### Dependencies
T-001.
### Blocks
none.
### Risk
Low.
### Rollback
remove gate only if parser proven defective.
### Estimated effort
Small.

## T-003 — Capture machine-readable verified baseline
**Status:** TODO  
**Priority:** P1  
**Type:** HARDEN  
**Leverage:** HIGH
### Problem
Evidence is scattered in Actions logs.
### Evidence
initial/wave audit.
### Goal
Version check inventory, environment limits and pass/fail classification.
### Scope
repository-known checks; no secrets; distinguish stubs/live NX.
### Non-goals
no telemetry service.
### Files / symbols
`docs/audit/verification-baseline.*`, helper.
### Implementation
deterministic manifest.
### Invariants
1,10.
### Compatibility constraints
never claim live NX from stubs.
### Edge cases
skip/cancel/rerun/environment-only.
### Tests
manifest validation.
### Mutation tests
N/A.
### Benchmarks
N/A.
### Acceptance criteria
introduced vs pre-existing failures become obvious.
### Verification commands
CI helper.
### Dependencies
T-001.
### Blocks
T-009.
### Risk
Low.
### Rollback
plan baseline remains fallback.
### Estimated effort
Small.

## T-004 — Delete retired K3–K5 documentation/tooling debt
**Status:** TODO  
**Priority:** P1  
**Type:** REMOVE  
**Leverage:** HIGH
### Problem
retired pipeline remains referenced.
### Evidence
F-002,F-007,F-009.
### Goal
one v8 source-of-truth story.
### Scope
usage-search legacy compilers/validators/docs; classify/delete/archive.
### Non-goals
retain helpers still used by v8.
### Files / symbols
README/full map/docs/audit/legacy scripts.
### Implementation
usage search → classify → delete/update → verify.
### Invariants
2,12.
### Compatibility constraints
no unproven runtime deletions.
### Edge cases
shared helpers.
### Tests
current docs/command/profile suites.
### Mutation tests
N/A.
### Benchmarks
N/A.
### Acceptance criteria
active docs/workflows do not require absent legacy assets.
### Verification commands
repo search + CI.
### Dependencies
T-001.
### Blocks
none.
### Risk
Medium.
### Rollback
restore only proven-current path.
### Estimated effort
Medium.

## T-005 — Finish remaining module-boundary adoption without semantic drift
**Status:** TODO  
**Priority:** P1  
**Type:** IMPROVE  
**Leverage:** HIGH
### Problem
refactor status lists unresolved executor/selection/intervention boundaries.
### Evidence
`docs/REFACTOR_STATUS.md`.
### Goal
complete one behavior-preserving boundary per atomic subtask.
### Scope
characterize → boundary/adapter → dual compatibility → migrate → verify → delete legacy.
### Non-goals
no silent behavior/API transition.
### Files / symbols
ModuleContracts/executors/LeaderKeyEngine/claim path.
### Invariants
4–7,10.
### Compatibility constraints
IPC/live NX behavior preserved.
### Edge cases
modal/stale context/selection/cancellation/process boundary.
### Tests
contract/architecture/live checklist when required.
### Mutation tests
pure decisions after T-007.
### Benchmarks
dispatch baseline where behavior-sensitive.
### Acceptance criteria
explicit boundary, migrated callers, old path deleted after verification.
### Verification commands
CI + live checklist.
### Dependencies
T-001,T-006 where applicable.
### Blocks
T-010.
### Risk
High.
### Rollback
compatibility adapter until verified.
### Estimated effort
Large, split further.

## T-006 — Replace brittle source-text checks with executable architecture tests
**Status:** TODO  
**Priority:** P1  
**Type:** HARDEN  
**Leverage:** HIGH
### Problem
F-004,F-005,F-010,F-013 prove source-text checks drift.
### Evidence
preflight waves.
### Goal
assert semantic ownership/dependencies/contracts.
### Scope
Protocol/BridgeCore/CommandBridge/HotkeyStudio/UI boundaries.
### Non-goals
remove no smoke gate before equivalent executable detection exists.
### Files / symbols
test projects/workflows/contracts.
### Implementation
compile/reflection/architecture tests + deliberate negative test-of-tests.
### Invariants
4–7,11.
### Compatibility constraints
no production behavior change.
### Edge cases
internal types, conditional NXOpen/stubs.
### Tests
negative boundary violations must fail.
### Mutation tests
T-007 where pure.
### Benchmarks
N/A.
### Acceptance criteria
valid file moves pass; boundary violations fail.
### Verification commands
targeted tests + CI.
### Dependencies
T-001.
### Blocks
T-005.
### Risk
Medium.
### Rollback
retain old smoke until replacement proven.
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
no measured mutation baseline.
### Goal
measure test ability to detect auth/state/validator faults.
### Scope
Protocol/StateMachines first; JS separately if useful.
### Non-goals
no blind NX UI/NXOpen mutation.
### Files / symbols
security/normalization/state/validators.
### Implementation
Stryker.NET pilot; classify survivors; informational before threshold.
### Invariants
5,10.
### Compatibility constraints
bounded CI duration.
### Edge cases
equivalent mutants/time-sensitive tests.
### Tests
mutation run + survivor review.
### Mutation tests
this task establishes them.
### Benchmarks
mutation job duration.
### Acceptance criteria
measured score; no unexplained high-risk survivors.
### Verification commands
record after tool discovery.
### Dependencies
T-001.
### Blocks
T-008.
### Risk
Medium.
### Rollback
informational mode if threshold unstable.
### Estimated effort
Medium.

## T-008 — Expand multidimensional edge-case coverage
**Status:** TODO  
**Priority:** P2  
**Type:** HARDEN  
**Leverage:** HIGH
### Problem
critical contracts span input × state × concurrency × timing × failure × permissions × configuration × external state.
### Evidence
IPC/HFSM/security architecture.
### Goal
pairwise + high-risk N-wise + property/fuzz/metamorphic coverage.
### Scope
Protocol auth/replay, inbox, HFSM, config/context guards.
### Non-goals
no brute-force live NX UI.
### Files / symbols
critical test projects.
### Implementation
dimension matrix → pairwise → N-wise → reproducible fuzz/property.
### Invariants
3,5,6,10,13.
### Compatibility constraints
deterministic seeds.
### Edge cases
clock skew/reorder/duplicate/corrupt/stale/permissions/modal/cancel.
### Tests
matrix-driven suites.
### Mutation tests
survivors guide missing observables.
### Benchmarks
stress duration/allocations.
### Acceptance criteria
documented matrix and executable high-risk cases.
### Verification commands
targeted suites.
### Dependencies
T-007.
### Blocks
T-009.
### Risk
Medium.
### Rollback
retain deterministic core if fuzz flaky.
### Estimated effort
Medium-large.

## T-009 — Establish performance, security and reliability baselines
**Status:** TODO  
**Priority:** P2  
**Type:** HARDEN  
**Leverage:** MEDIUM
### Problem
coverage/mutation/performance/dependency/flaky baselines incomplete.
### Evidence
section 4.
### Goal
quantify release-relevant regressions.
### Scope
pure hot paths, dependency/security scans, flaky observation.
### Non-goals
no live-NX performance claims from stubs.
### Files / symbols
CI/benchmark/test projects.
### Implementation
measure first, conservative thresholds after data.
### Invariants
1,5,10.
### Compatibility constraints
reasonable CI duration.
### Edge cases
cold/steady state, IO variance.
### Tests
benchmarks/scans.
### Mutation tests
consume T-007.
### Benchmarks
established here.
### Acceptance criteria
versioned baselines/targets.
### Verification commands
baseline manifest.
### Dependencies
T-003,T-008.
### Blocks
T-010.
### Risk
Medium.
### Rollback
unstable threshold informational, measurement retained.
### Estimated effort
Medium.

## T-010 — Perform final convergence re-audit and implement delta
**Status:** TODO  
**Priority:** P2  
**Type:** IMPROVE  
**Leverage:** HIGH
### Problem
initial audit cannot prove post-change convergence.
### Evidence
master protocol.
### Goal
repeat full architecture/correctness/security/concurrency/reliability/API/data/testing/mutation/performance/CI/docs/dependency audit until no fundamental delta.
### Scope
whole repository.
### Non-goals
no cosmetic expansion without findings.
### Files / symbols
all.
### Implementation
re-audit → findings → atomic deltas → verified iterations → repeat.
### Invariants
all.
### Compatibility constraints
live NX release path preserved.
### Edge cases
T-008 matrix.
### Tests
all established gates + live checklist.
### Mutation tests
critical suites.
### Benchmarks
all baselines.
### Acceptance criteria
section 21.
### Verification commands
full CI + live verification where applicable.
### Dependencies
T-005,T-009.
### Blocks
final done.
### Risk
Medium.
### Rollback
per delta task.
### Estimated effort
variable.

# 12. Testing Strategy

Characterization precedes behavior change. Pure contracts get unit/property tests; Windows desktop/CommandBridge get integration/build checks; NXOpen stubs prove compile compatibility only; live NX remains explicit release evidence. Negative security tests are first class. Native command failures propagate immediately. Preflight branch is the verification sandbox; `main` is not.

# 13. Mutation Testing Strategy

Not configured initially. T-007 establishes measurement before threshold. Priority: HMAC/auth/replay → context guards → state transitions → config/identity validators → normalization → inbox. Survived mutants trigger observable-contract analysis, not coverage inflation.

# 14. Performance Baselines

Unknown initially. Candidate measurements: leader transition latency/allocations, normalization/parsing, inbox admission throughput/bounded poll cost, profile load/validation, desktop cold-start/publish size. No live-NX latency claim from CI.

# 15. Security Hardening

Preserve HMAC, replay guard, explicit permissions, context/source-process constraints and bounded inbox. Duplicate operation identity must not create implicit permission ambiguity. Add dependency/code scanning and mutation-resistant negative tests later. Never weaken validation to green CI.

# 16. Migration Strategy

Use `characterization → introduce boundary → dual compatibility → migrate callers → verify → remove legacy`. v8 cleanup is an incomplete migration: restore only independent HFSM policy; do not resurrect K3–K5 sources. Optional generated locks are strictly derived from canonical v8.

# 17. Deferred Work

- Live NX verification requiring NX 2512 installation.
- Mutation/performance thresholds until measured.
- Cosmetic UI work unrelated to findings.
- Broad refactors until T-001 is green.

# 18. Rejected Decisions

- Restore deleted K3–K5/full-command-map just to green CI — rejected.
- Skip failing validators — rejected.
- Add fake capability lock just to satisfy existence — rejected.
- Remove `operation_id` uniqueness assertion before understanding F-014 — rejected.
- Force/force-with-lease `main` — rejected.
- Treat NXOpen stubs as live NX proof — rejected.

# 19. Completed Tasks

None. T-001 remains `VERIFYING`; preflight commits are diagnostic/verification history, not successful iteration completion.

# 20. Iteration Log

## Iteration 1 — Wave A
**Task:** T-001  
**Findings addressed:** F-001..F-006  
**Unexpected findings:** F-009,F-010,F-011,F-012  
**Changes:** plan bootstrap; HFSM policy/package; v8/fail-fast CI; retire two obsolete workflows; selection/transport checks; orphan gitlink removal.  
**Tests:** `fab88e79...`: Documentation PASS; current validators PASS; DFA/HFSM PASS; hardening/signing FAIL; Sketch legacy FAIL; Hotkey suite capability-lock FAIL.  
**Plan changes:** added F-009..F-012.  
**Commit:** `fab88e79c3a98f0eeb30ba496fc0a834cbae337a`  
**Push:** audit branch only.  
**Result:** FAIL → reconciled.

## Iteration 1 — Wave B
**Task:** T-001  
**Findings addressed:** F-009..F-012  
**Unexpected findings:** F-013,F-014  
**Changes:** retired Sketch workflow; signing ownership checks; canonical capability assertions/optional lock strictness; nullable Protocol contract.  
**Tests:** `a5f9a718...`: current validator step PASS; runtime source hardening PASS; strict Protocol/BridgeCore PASS with 0 warnings/errors; DFA/HFSM PASS; HotkeyStudio FAIL on duplicate canonical `operation_id`; Desktop UI FAIL on obsolete 10-row textual assertion; CI stops at same HotkeyStudio identity failure.  
**Plan changes:** added invariant 13, F-013/F-014; T-001 expanded only to diagnose/resolve these blockers.  
**Commit:** `a5f9a718fce9dd2db9fdcd3ced56770b4f025378`  
**Push:** audit branch only.  
**Result:** FAIL → reconciled.

## Iteration 1 — Wave C
**Task:** T-001  
**Findings addressed:** F-013,F-014  
**Unexpected findings:** pending  
**Changes:** plan reconciled before implementation; next change will fix the stale HUD smoke assertion and add duplicate-ID diagnostics without weakening the identity contract.  
**Tests:** pending.  
**Plan changes:** Wave B evidence recorded before Wave C implementation.  
**Commit:** pending  
**Push:** audit branch only until PASS.  
**Result:** VERIFYING.

# 21. Definition of Final Done

- no known Critical/High findings;
- all P0/P1 DONE or evidence-backed rejected/deferred;
- applicable CI consistently green with no masked native failures;
- canonical operation identity deterministic/enforced;
- important architecture rules technically protected where feasible;
- critical security/state logic mutation-tested with no unexplained high-risk survivors;
- multidimensional critical edge space covered;
- performance/security/reliability baselines recorded and acceptable;
- no unexplained flaky tests;
- docs match current v8 code and do not require absent production assets;
- live-NX-only claims have explicit verification records;
- repeat audit finds no fundamental delta;
- `MASTER_PLAN.md`, code/tests and last verified `main` SHA agree.
