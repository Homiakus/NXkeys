# 1. Mission

Сделать NXkeys устойчивой, проверяемой и эволюционирующей системой, где `MASTER_PLAN.md` — единственный living execution plan, а существенные изменения проходят цикл `AUDIT → PLAN → SELECT → IMPLEMENT → TEST → REVIEW → RECONCILE → COMMIT → VERIFIED PUSH → COMPRESS`.

Текущий приоритет — восстановить достоверный зелёный baseline после неполного перехода на канонический v8-профиль, не возвращая удалённый K3–K5/full-command-map контур как production dependency.

# 2. Current State

- C#/.NET 8, Windows x64, Siemens NX / Designcenter NX 2512.
- Канонический runtime profile: `config/nx2512-v8-profile.json`, schema 8.
- IPC: schema 4, authenticated HMAC, replay guard, permission/context binding.
- Основные boundaries: HotkeyStudio, Protocol, StateMachines, BridgeCore, CommandBridge, ControlCenter, NxEskd.
- Baseline `main` до внедрения подхода: `2359e85c02313f4c6227a5e9a38ef62e9b9c043f`.
- На этом SHA все 5 workflow, запущенные push-событием, были красными.
- `docs/REFACTOR_STATUS.md` подтверждает незавершённую модульную декомпозицию.
- Локальное клонирование из рабочего контейнера недоступно из-за сетевого ограничения среды; поэтому обязательный preflight выполняется на отдельной GitHub-ветке Actions, и только verified tree допускается в `main`.

# 3. Architecture Map

```text
keyboard / CapsLock leader
        ↓
NX2512_HotkeyStudio
  LeaderKeyEngine / NxContextClient
  NxCommandBridgeClient
  NxRequestSigningPolicy
        ↓ authenticated file IPC
NXKeys.Protocol ← NXKeys.StateMachines
        ↓
NXKeys.BridgeCore
        ↓ bounded admitted queue
NX2512_CommandBridge
  NxContextMonitor
  NxMenuCommandExecutor
  NxSelectionExecutor
  NxInterventionGuard
        ↓
NXOpen / NX UI / NxEskd capabilities
```

Ownership:
- `NXKeys.Protocol` — shared contracts/security/context normalization.
- `NXKeys.StateMachines` — deterministic leader/HFSM policy.
- `NX2512_HotkeyStudio` — out-of-NX orchestration/UI/transport facade.
- `NxRequestSigningPolicy` — request admission/signing policy; file IO is not owned here.
- `NXKeys.BridgeCore` — bounded background inbox/security gate.
- `NX2512_CommandBridge` — in-NX NXOpen effects only.

# 4. Baseline

| Check | Baseline at `2359e85c...` | Current preflight evidence |
|---|---|---|
| GitHub Actions | FAIL, 5/5 push workflows | Iteration 1 wave A exposed deeper blockers |
| current docs validators | masked/stale | PASS in preflight |
| DFA/HFSM | FAIL: missing `root_ms` policy | PASS after policy restoration |
| HotkeyStudio regressions | blocked | reaches test suite; stale capability-lock assertion found |
| Documentation | FAIL: missing `full-command-map` | PASS |
| legacy K3–K5 workflows | FAIL: missing removed assets | being retired |
| Runtime hardening | FAIL: stale source marker | deeper stale signing-policy marker found |
| checkout integrity | orphan gitlink warning | clean after gitlink removal in preflight |
| strict Protocol build | previously blocked | nullable warning identified before strict gate |
| NxEskd tests/builds | previously blocked | pending deeper CI progression |
| live NX | NOT RUN | external NX installation required |
| coverage | UNKNOWN | T-009 |
| mutation score | NOT CONFIGURED | T-007 |
| benchmarks | UNKNOWN | T-009 |
| security/dependency scans | PARTIAL / NOT ENFORCED | T-009 |

Pre-existing failures are not attributed to new code unless the iteration demonstrably worsens them.

# 5. System Invariants

1. `main` must not knowingly move to a worse verified state.
2. v8 schema 8 is the canonical command/profile source of truth.
3. Independent HFSM policy consumed by `LeaderBehaviorProfile.LoadDefault()` must ship with runtime/tests.
4. Out-of-NX code does not execute NXOpen directly.
5. Request admission remains authenticated, permission-bound, replay-resistant and context-bound.
6. NX UI thread performs neither unbounded inbox scanning nor cryptographic admission.
7. Selection-filter effects flow through shared protocol → CommandBridge executor boundary.
8. Context normalization has one canonical Protocol implementation.
9. Substantial unexpected findings are recorded and reconciled before scope expands.
10. Tests/observed behavior outrank prior plan assumptions.
11. CI assertions must follow semantic ownership, not accidental source-file location.
12. Removed legacy K3–K5 artefacts are never recreated merely to silence stale checks.

# 6. Findings Registry

## F-001 — Latest main has no trustworthy green baseline
**Status:** Planned  
**Category:** Reliability / CI  
**Severity:** Critical  
**Confidence:** Confirmed  
**Evidence:** 5/5 workflows on `2359e85c...` failed.  
**Files / symbols:** `.github/workflows/*`.  
**Current behavior:** no reliable regression signal.  
**Expected behavior:** applicable current-v8 gates pass and fail only on real violations.  
**Root cause:** incomplete migration + stale tests/workflows.  
**Impact / blast radius:** repository-wide.  
**Reproduction:** Actions on baseline SHA.  
**Affected invariants:** 1, 10.  
**Related findings:** F-002..F-006, F-009..F-012.  
**Affected tasks:** T-001.  
**Recommended direction:** recover baseline before feature work.

## F-002 — CI still depends on removed K3–K5/full-command-map artefacts
**Status:** Planned  
**Category:** CI / Migration  
**Severity:** High  
**Confidence:** Confirmed  
**Evidence:** current `config/` retained v8 profile while multiple workflows/scripts scan `config/full-command-map` or expect `nx2512-pro-*.json`.  
**Files / symbols:** old map/runtime/sketch workflows and legacy scripts.  
**Current behavior:** deterministic ENOENT.  
**Expected behavior:** v8-only gates.  
**Root cause:** cleanup removed assets without completing automation migration.  
**Impact:** several mandatory workflows fail.  
**Blast radius:** CI/docs/profile pipeline.  
**Reproduction:** baseline Actions.  
**Affected invariants:** 1, 2, 12.  
**Related findings:** F-007, F-009.  
**Affected tasks:** T-001, T-004.  
**Recommended direction:** retire obsolete push gates; later delete/archive residual tooling/docs.

## F-003 — Independent state-machine policy was accidentally removed
**Status:** Planned  
**Category:** Correctness / Packaging  
**Severity:** High  
**Confidence:** Confirmed  
**Evidence:** runtime csproj and `LeaderBehaviorProfile` consume `nx2512-state-machines.json`; tests require `root_ms=4000`; current test project copied v8 profile instead.  
**Files / symbols:** `LeaderBehaviorProfile`, HotkeyStudio csproj, StateMachines.Tests csproj.  
**Current behavior:** fallback 20s policy and red test.  
**Expected behavior:** schema-1 behavior policy shipped/tested independently of command profile.  
**Root cause:** config cleanup conflated two independent contracts.  
**Impact:** interaction timing/guards.  
**Blast radius:** leader runtime/package/tests.  
**Reproduction:** baseline state-machine suite.  
**Affected invariants:** 3, 10.  
**Related findings:** F-001.  
**Affected tasks:** T-001.  
**Recommended direction:** restore historical policy and packaging; do not restore legacy command maps.

## F-004 — Runtime-hardening asserts transport result in the wrong file
**Status:** Planned  
**Category:** Testing / Architecture  
**Severity:** Medium  
**Confidence:** Confirmed  
**Evidence:** `NxTransportReadStatus` moved to `NxTransportReadResult.cs`, old check searches client file.  
**Files / symbols:** runtime-hardening workflow.  
**Current behavior:** valid extraction fails gate.  
**Expected behavior:** semantic owner checked.  
**Root cause:** brittle textual assertion.  
**Impact:** hardening gate red.  
**Blast radius:** runtime CI.  
**Affected invariants:** 1, 11.  
**Affected tasks:** T-001, T-006.  
**Recommended direction:** reconcile now; replace text checks with architecture tests in T-006.

## F-005 — Selection-filter validator assumes NXOpen effect lives in `Program.cs`
**Status:** Planned  
**Category:** Testing / Architecture  
**Severity:** Medium  
**Confidence:** Confirmed  
**Evidence:** dispatch is in `Program.ProcessClaim`, effect is now `NxMenuCommandExecutor.ApplySelectionCommand`.  
**Current behavior:** validator reports a false architectural failure.  
**Expected behavior:** dispatch and effect ownership validated separately.  
**Root cause:** structural validator not migrated with executor extraction.  
**Impact:** hidden failure previously masked by PowerShell exit semantics.  
**Blast radius:** command-tree validation.  
**Affected invariants:** 7, 11.  
**Affected tasks:** T-001, T-006.  
**Recommended direction:** follow current executor boundary.

## F-006 — Orphan gitlink committed under `.claude/worktrees`
**Status:** Planned  
**Category:** Repository Integrity  
**Severity:** Medium  
**Confidence:** Confirmed  
**Evidence:** mode `160000` entry without `.gitmodules`; checkout cleanup reports submodule failure.  
**Files / symbols:** `.claude/worktrees/fix-v8-contract-paths`.  
**Current behavior:** checkout cleanup warning/failure.  
**Expected behavior:** no agent-local worktree metadata in repository tree.  
**Root cause:** accidental gitlink commit.  
**Impact:** CI/tooling noise and fragility.  
**Blast radius:** every checkout.  
**Affected invariants:** 1.  
**Affected tasks:** T-001.  
**Recommended direction:** delete gitlink, never fake `.gitmodules`.

## F-007 — Documentation still describes retired K3–K5 assets
**Status:** Open  
**Category:** Documentation / Migration debt  
**Severity:** Medium  
**Confidence:** Strong  
**Evidence:** README/full-map/audit narrative references absent `config/full-command-map`.  
**Current behavior:** mixed v8/legacy onboarding.  
**Expected behavior:** one current v8 story; history remains in Git.  
**Root cause:** migration debt.  
**Impact:** maintainer/operator confusion.  
**Blast radius:** docs/tooling.  
**Affected invariants:** 2, 12.  
**Affected tasks:** T-004.  
**Recommended direction:** debt deletion pass after baseline recovery.

## F-008 — Critical logic lacks measured mutation-testing baseline
**Status:** Open  
**Category:** Test-of-tests  
**Severity:** Medium  
**Confidence:** Strong  
**Evidence:** no enforced mutation score found.  
**Current behavior:** green coverage would not prove assertions distinguish critical faults.  
**Expected behavior:** mutation-tested auth/state/validation contracts.  
**Root cause:** test-infrastructure gap.  
**Impact:** false confidence risk.  
**Blast radius:** Protocol/StateMachines/validators.  
**Affected invariants:** 5, 10.  
**Affected tasks:** T-007.  
**Recommended direction:** targeted Stryker.NET pilot, then evidence-based thresholds.

## F-009 — `Sketch intent grammar` workflow is itself a legacy K3–K5 pipeline
**Status:** Planned  
**Category:** CI / Migration  
**Severity:** High  
**Confidence:** Confirmed  
**Evidence:** preflight run validates `validate-main-command-map.mjs` and generated `nx2512-pro-main.generated.json`, then fails on removed `full-command-map`.  
**Files / symbols:** `.github/workflows/sketch-intent.yml`.  
**Current behavior:** independent preflight remains red despite current v8 command-tree passing.  
**Expected behavior:** sketch grammar is covered by current v8 HotkeyStudio regressions and main CI.  
**Root cause:** obsolete workflow survived v8 cleanup.  
**Impact:** permanent red duplicate gate.  
**Blast radius:** pushes touching current command-tree/state policy.  
**Affected invariants:** 1, 2, 12.  
**Related findings:** F-002.  
**Affected tasks:** T-001, T-004.  
**Recommended direction:** retire workflow; retain v8 sketch tests in HotkeyStudio suite.

## F-010 — Signing source assertion ignores `NxRequestSigningPolicy` extraction
**Status:** Planned  
**Category:** Testing / Architecture  
**Severity:** Medium  
**Confidence:** Confirmed  
**Evidence:** preflight hardening fails looking for old `PrepareAuthenticatedRequest` in client; actual facade delegates `policy.PrepareAuthenticated`, HMAC lives in `NxRequestSigningPolicy`.  
**Files / symbols:** `NxCommandBridgeClient`, `NxRequestSigningPolicy`, runtime-hardening workflow.  
**Current behavior:** valid security boundary fails source gate.  
**Expected behavior:** orchestration and signing ownership checked separately.  
**Root cause:** stale source-location contract.  
**Impact:** hardening blocked.  
**Blast radius:** runtime security CI.  
**Affected invariants:** 5, 11.  
**Affected tasks:** T-001, T-006.  
**Recommended direction:** reconcile ownership now; architecture test later.

## F-011 — Capability-route test and validator disagree about lock-file optionality
**Status:** Planned  
**Category:** Testing / Contract  
**Severity:** Medium  
**Confidence:** Confirmed  
**Evidence:** validator explicitly succeeds if `nx2512-capability-route-lock.json` is absent, while HotkeyStudio test aborts because it requires that file.  
**Files / symbols:** `validate-capability-route-lock.mjs`, `VerifyBehavioralGuardsAndLimits`.  
**Current behavior:** canonical v8 data validates, C# suite fails on a deleted auxiliary file.  
**Expected behavior:** v8 profile itself is source of truth; optional generated lock, if present, must fully match.  
**Root cause:** stale test plus weak optional-lock validator.  
**Impact:** HotkeyStudio suite blocked.  
**Blast radius:** main CI.  
**Affected invariants:** 2, 10, 12.  
**Affected tasks:** T-001, T-006.  
**Recommended direction:** assert unique/stable canonical operations in C#; make optional lock comparison exact if file exists.

## F-012 — Strict Protocol build is blocked by a real nullable contract mismatch
**Status:** Planned  
**Category:** Correctness / Static analysis  
**Severity:** Medium  
**Confidence:** Confirmed  
**Evidence:** `NXKeys.Protocol` has `Nullable=enable`; `NormalizeV8Module` returns `null` while declared `string`, producing CS8603; hardening compiles with `-warnaserror`.  
**Files / symbols:** `NxContextNormalization.NormalizeV8Module`.  
**Current behavior:** hidden warning becomes strict-build failure once earlier blocker clears.  
**Expected behavior:** nullable return contract explicit.  
**Root cause:** annotation mismatch, not runtime redesign.  
**Impact:** strict architecture build blocked.  
**Blast radius:** Protocol build/tests.  
**Affected invariants:** 1, 8, 10.  
**Affected tasks:** T-001.  
**Recommended direction:** annotate nullable local/helper return without behavioral change.

# 7. Risk Register

| Risk | P | Impact | Mitigation |
|---|---:|---:|---|
| more hidden failures appear as blockers clear | High | High | CI wave → classify → Finding → reconcile |
| stale textual gates keep breaking valid refactors | High | Medium | T-006 executable architecture tests |
| restored HFSM policy diverges from live UX | Medium | High | executable suite + live NX release checklist |
| NXOpen stubs differ from real NX 2512 | Medium | High | never equate stub CI with live verification |
| direct main push regresses baseline | Low after preflight | High | verified temporary branch, then non-force main commit |
| mutation suite becomes noisy/slow | Medium | Medium | pure critical projects first |

# 8. Pareto Improvements

1. Restore green/fail-fast baseline.
2. Make MASTER_PLAN structure executable.
3. Replace source-text architecture checks with compiled tests.
4. Delete K3–K5 migration debt instead of maintaining dual truth.
5. Mutation-test Protocol/StateMachines before broad test expansion.

# 9. Dependency DAG

```text
T-001 baseline recovery
 ├─ T-002 plan gate
 ├─ T-003 verified baseline artifact
 ├─ T-004 legacy debt deletion
 ├─ T-006 architecture tests ─ T-005 remaining boundary migration
 └─ T-007 mutation pilot ─ T-008 edge-space coverage
T-003 + T-005 + T-008 ─ T-009 measured hardening ─ T-010 re-audit/convergence
```

# 10. Implementation Phases

- Phase 0: T-001..T-003 — recover trust/governance.
- Phase 1: T-004 — delete migration debt.
- Phase 2: T-006 then T-005 — stabilize boundaries.
- Phase 3: T-007..T-008 — strengthen test-of-tests/edge coverage.
- Phase 4: T-009 — measurable performance/security/reliability.
- Phase 5: T-010 — full re-audit and delta loop.

# 11. Atomic Tasks

## T-001 — Reconcile v8 migration fallout and restore a trustworthy baseline
**Status:** VERIFYING  
**Priority:** P0  
**Type:** FIX  
**Leverage:** HIGH

### Problem
Current `main` cannot establish a trustworthy release signal.
### Evidence
F-001..F-006 and preflight-discovered F-009..F-012.
### Goal
All applicable current-v8 Actions green, with no masked failures or resurrected legacy command-map dependency.
### Scope
Restore HFSM policy/package; retire obsolete K3–K5 workflows; fix fail-fast current validators; reconcile executor/transport/signing ownership checks; remove orphan gitlink; reconcile capability-route test contract; correct Protocol nullable annotation; keep this plan current.
### Non-goals
No new feature, UX redesign, live-NX semantic change, broad module refactor or mutation threshold.
### Files / symbols
Current workflows, state policy/test packaging, current validators, `NxContextNormalization`, HotkeyStudio regression assertions, `MASTER_PLAN.md`.
### Implementation
Minimal root-cause migration repair. Preflight every wave on audit branch; squash verified state onto current `main` without force.
### Invariants
1–12 as applicable.
### Compatibility constraints
Do not restore `nx2512-pro-*`/`full-command-map` as production dependencies. Preserve IPC schema/runtime behavior.
### Edge cases
Missing policies; stale source boundaries; missing auxiliary files; native exit masking; duplicate operation IDs; checkout gitlinks; strict nullable builds.
### Tests
Current Node validators, DFA/HFSM, HotkeyStudio regressions, NxEskd tests, strict Protocol/BridgeCore builds, desktop builds/publish, NXOpen stubs and bridge contract, documentation/runtime-hardening Actions.
### Mutation tests
Not yet available; T-007.
### Benchmarks
Not applicable to migration repair.
### Acceptance criteria
Applicable preflight workflows all PASS; no K3–K5 ENOENT; checkout clean; state policy published; strict builds and regression suites pass; final diff reviewed; current main unchanged or reconciled before final commit.
### Verification commands
Encoded in `ci.yml`, `documentation.yml`, `runtime-hardening.yml`; Actions is the authoritative available Windows environment.
### Dependencies
None.
### Blocks
T-002..T-010.
### Risk
Medium: clearing one migration blocker can expose the next pre-existing defect.
### Rollback
Normal revert commit only; never force push.
### Estimated effort
Medium, iterative CI-driven.

## T-002 — Enforce MASTER_PLAN structure as an executable gate
**Status:** READY  
**Priority:** P1  
**Type:** HARDEN  
**Leverage:** HIGH
### Problem
Living plan can drift structurally.
### Evidence
No plan validator existed.
### Goal
Reject missing sections, duplicate IDs and invalid task/finding vocabularies.
### Scope
Pure Node validator + negative self-tests + CI invocation.
### Non-goals
No attempt to judge prose truth automatically.
### Files / symbols
`MASTER_PLAN.md`, `scripts/validate-master-plan.mjs`, CI.
### Implementation
Required headings; unique F/T IDs; status/priority/type/leverage validation; iteration-log presence; self-test fixtures in memory.
### Invariants
9, 10.
### Compatibility constraints
No npm install; Windows/Linux Node.
### Edge cases
Duplicate IDs, malformed metadata, missing iteration log.
### Tests
Positive plan + deliberate invalid self-test inputs.
### Mutation tests
Later under T-007 where useful.
### Benchmarks
Sub-second target.
### Acceptance criteria
Malformed plan fails; current plan passes.
### Verification commands
`node scripts/validate-master-plan.mjs --self-test && node scripts/validate-master-plan.mjs`.
### Dependencies
T-001.
### Blocks
None.
### Risk
Low.
### Rollback
Remove gate invocation only if parser itself is proven defective.
### Estimated effort
Small.

## T-003 — Capture a machine-readable verified baseline
**Status:** TODO  
**Priority:** P1  
**Type:** HARDEN  
**Leverage:** HIGH
### Problem
Verification evidence is scattered in logs.
### Evidence
Initial audit required manual correlation of five workflows.
### Goal
Version check inventory, environment limitations and pass/fail classification.
### Scope
No secrets; distinguish stubs from live NX.
### Non-goals
No external telemetry service.
### Files / symbols
`docs/audit/verification-baseline.*`, CI helper.
### Implementation
Deterministic repository-known check manifest plus last verified state fields.
### Invariants
1, 10.
### Compatibility constraints
Never claim live NX from stub CI.
### Edge cases
skips/cancel/rerun/environment-only gates.
### Tests
manifest validation.
### Mutation tests
N/A.
### Benchmarks
N/A.
### Acceptance criteria
Future iterations can distinguish pre-existing vs introduced failures quickly.
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

## T-004 — Delete retired K3–K5 documentation/tooling debt
**Status:** TODO  
**Priority:** P1  
**Type:** REMOVE  
**Leverage:** HIGH
### Problem
Retired pipeline remains in docs/scripts.
### Evidence
F-002, F-007, F-009.
### Goal
One unambiguous v8 source-of-truth story.
### Scope
Usage-search every legacy compiler/validator/doc, classify current vs historical, delete/archive only proven dead paths.
### Non-goals
Do not delete reusable sequence-policy logic used by v8.
### Files / symbols
README, `FULL_COMMAND_MAP.md`, docs/audit, legacy map scripts.
### Implementation
usage search → classify → delete/update → validation.
### Invariants
2, 12.
### Compatibility constraints
No runtime dependency removal without evidence.
### Edge cases
shared helpers.
### Tests
current validators/docs/Hotkey suite.
### Mutation tests
N/A.
### Benchmarks
N/A.
### Acceptance criteria
No current docs or active workflows require absent legacy assets.
### Verification commands
repository search + CI.
### Dependencies
T-001.
### Blocks
None.
### Risk
Medium.
### Rollback
Restore only paths proven current.
### Estimated effort
Medium.

## T-005 — Finish remaining module-boundary adoption without semantic drift
**Status:** TODO  
**Priority:** P1  
**Type:** IMPROVE  
**Leverage:** HIGH
### Problem
`REFACTOR_STATUS.md` lists unresolved executor/selection/intervention boundaries.
### Evidence
Current architecture status.
### Goal
Complete one behavior-preserving boundary at a time.
### Scope
Characterize → adapter/new boundary → dual compatibility → migrate callers → verify → delete legacy.
### Non-goals
No throw→result or interactive behavior change without explicit migration task.
### Files / symbols
ModuleContracts, executors, LeaderKeyEngine, CommandBridge claim path.
### Invariants
4–7, 10.
### Compatibility constraints
IPC/live NX behavior preserved.
### Edge cases
modal state, stale context, selection mutation, cancellation, process boundaries.
### Tests
contract/architecture + live checklist where semantics depend on NX.
### Mutation tests
critical pure decisions after T-007.
### Benchmarks
dispatch latency before behavior-sensitive work.
### Acceptance criteria
explicit boundary, migrated callers, legacy path deleted, behavior characterized.
### Verification commands
CI + live checklist where required.
### Dependencies
T-001, T-006 as applicable.
### Blocks
T-010.
### Risk
High.
### Rollback
compatibility adapter retained until verified.
### Estimated effort
Large, split into atomic subtasks during execution.

## T-006 — Replace brittle source-text hardening with executable architecture tests
**Status:** TODO  
**Priority:** P1  
**Type:** HARDEN  
**Leverage:** HIGH
### Problem
F-004/F-005/F-010/F-011 show file-location assertions drift after valid refactors.
### Evidence
Preflight failures.
### Goal
Assert ownership/contracts rather than text placement.
### Scope
Protocol/BridgeCore/CommandBridge/HotkeyStudio dependency and behavioral boundaries.
### Non-goals
Do not remove a smoke assertion before executable replacement proves equivalent detection.
### Files / symbols
Test projects/workflows/ModuleContracts.
### Implementation
compile/reflection/architecture tests with deliberate negative test-of-tests fixtures.
### Invariants
4–7, 11.
### Compatibility constraints
No production behavior change.
### Edge cases
internal types, NXOpen stubs, conditional references.
### Tests
negative boundary violations must fail.
### Mutation tests
T-007 where pure.
### Benchmarks
N/A.
### Acceptance criteria
moving valid implementation between files does not fail; crossing architecture boundary does.
### Verification commands
targeted test project + CI.
### Dependencies
T-001.
### Blocks
T-005.
### Risk
Medium.
### Rollback
retain old smoke until replacement is demonstrated.
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
Measure test ability to detect meaningful auth/state/validator faults.
### Scope
Protocol/StateMachines first; JS separately if valuable.
### Non-goals
No blind mutation of NX UI/NXOpen integration.
### Files / symbols
security, normalization, state guards, validators.
### Implementation
Stryker.NET pilot; classify surviving/equivalent mutants; informational before gating.
### Invariants
5, 10.
### Compatibility constraints
bounded CI time.
### Edge cases
equivalent mutants/time-sensitive tests.
### Tests
mutation run + survived-mutant review.
### Mutation tests
This task establishes them.
### Benchmarks
mutation job duration.
### Acceptance criteria
measured score and no unexplained high-risk survivors in pilot scope.
### Verification commands
record after tool discovery.
### Dependencies
T-001.
### Blocks
T-008.
### Risk
Medium.
### Rollback
keep informational if threshold unstable.
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
IPC/security/HFSM architecture.
### Goal
Pairwise baseline + high-risk N-wise + property/fuzz/metamorphic coverage.
### Scope
Protocol auth/replay, Bridge inbox, HFSM, config parsing/context guards.
### Non-goals
No brute-force live NX UI combinatorics.
### Files / symbols
critical test projects.
### Implementation
partition dimensions → pairwise → high-risk N-wise → reproducible fuzz/property cases.
### Invariants
3, 5, 6, 10.
### Compatibility constraints
deterministic CI seeds/repro.
### Edge cases
clock skew, reorder/duplicate, corrupt JSON, stale revisions, permission mismatch, modal/cancel states.
### Tests
matrix-driven suites.
### Mutation tests
survivors inform missing observables.
### Benchmarks
track stress duration/allocations.
### Acceptance criteria
documented edge matrix + executable high-risk coverage.
### Verification commands
targeted suites.
### Dependencies
T-007.
### Blocks
T-009.
### Risk
Medium.
### Rollback
retain deterministic core if fuzz harness flakes.
### Estimated effort
Medium-large, split by subsystem.

## T-009 — Establish performance, security and reliability baselines
**Status:** TODO  
**Priority:** P2  
**Type:** HARDEN  
**Leverage:** MEDIUM
### Problem
Coverage/mutation/performance/dependency/flaky baselines are incomplete.
### Evidence
Section 4.
### Goal
Quantify release-relevant regressions.
### Scope
pure hot-path latency/allocations, dependency/security scanning, flaky observation, check inventory.
### Non-goals
No live-NX performance claims from CI stubs.
### Files / symbols
CI/benchmark/test projects.
### Implementation
measure first, conservative thresholds after data.
### Invariants
1, 5, 10.
### Compatibility constraints
reasonable CI duration.
### Edge cases
cold vs steady state, file-IO variance.
### Tests
benchmark regression + scans.
### Mutation tests
consume T-007 data.
### Benchmarks
this task establishes them.
### Acceptance criteria
versioned measurable baselines/targets.
### Verification commands
recorded in baseline artifact.
### Dependencies
T-003, T-008.
### Blocks
T-010.
### Risk
Medium.
### Rollback
unstable threshold → informational, measurement retained.
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
Repeat architecture/correctness/security/concurrency/reliability/API/data/testing/mutation/performance/CI/docs/dependency audit until no fundamental delta remains.
### Scope
whole repository.
### Non-goals
No cosmetic expansion without findings.
### Files / symbols
all.
### Implementation
re-audit → Findings → atomic delta tasks → verified iterations → re-audit.
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
all established baselines.
### Acceptance criteria
Section 21.
### Verification commands
full CI + live verification where required.
### Dependencies
T-005, T-009.
### Blocks
final done.
### Risk
Medium.
### Rollback
per delta task.
### Estimated effort
variable.

# 12. Testing Strategy

- Characterization/regression test before behavior-changing refactor.
- Pure unit/contract tests for Protocol, StateMachines, BridgeCore.
- Windows integration/build tests for desktop/CommandBridge.
- NXOpen stubs prove compile-time adapter compatibility only.
- Live NX checklist remains mandatory for actual NX behavior claims.
- Negative security tests are first-class.
- Native process failures propagate immediately; no last-command masking.
- Preflight branch is permitted as a verification sandbox; `main` is not.

# 13. Mutation Testing Strategy

Initial state: not configured. T-007 establishes measured baseline before thresholds. Priority: HMAC/auth/replay → context guards → state transitions → config validators → normalization → inbox policy. Survived mutants trigger observable-contract analysis, not coverage inflation.

# 14. Performance Baselines

Initial state unknown. Candidates: leader decision latency/allocations, normalization/parsing, inbox admission throughput/bounded poll cost, profile load/validation, desktop cold start/publish size. No live-NX latency claim from CI.

# 15. Security Hardening

Preserve authenticated HMAC requests, replay guard, explicit permissions, context binding, source-process constraints and bounded background inbox. Add dependency/code scans and mutation-resistant negative tests later. Never weaken validation or introduce permissive fallback to make CI green.

# 16. Migration Strategy

`characterization → introduce boundary → dual compatibility → migrate callers → verify → remove legacy`.

The v8 cleanup is treated as an incomplete migration: restore only independently required HFSM behavior policy; do not resurrect K3–K5 command-map sources. Any optional generated lock must be derived from and exactly match canonical v8 if present.

# 17. Deferred Work

- Live NX verification requiring installed NX 2512.
- Mutation thresholds until pilot data exists.
- Performance thresholds until measurements exist.
- Cosmetic UI work unrelated to findings.
- Broad refactor items until baseline is green.

# 18. Rejected Decisions

- Restore deleted K3–K5/full-command-map merely to green CI — rejected: dual source of truth.
- Skip failing validators silently — rejected: update or explicitly retire them.
- Add fake/incomplete capability lock to satisfy existence assertion — rejected: test should verify the real canonical contract.
- Force/force-with-lease main — rejected.
- Change executor semantics during baseline recovery — rejected.
- Treat NXOpen stubs as live NX proof — rejected.

# 19. Completed Tasks

None yet. T-001 remains VERIFYING until a fully green preflight is reconciled and the equivalent verified state is committed/pushed to `main`.

# 20. Iteration Log

## Iteration 1 — Wave A

**Task:** T-001  
**Findings addressed:** F-001..F-006  
**Unexpected findings:** F-009, F-010, F-011, F-012  
**Changes:** living plan bootstrap; restored HFSM policy/test packaging; current-v8/fail-fast CI; retired two obsolete K3–K5 workflows; selection/transport source checks reconciled; orphan gitlink removed.  
**Tests:** preflight `fab88e79...`: Documentation PASS; current Node validators PASS; DFA/HFSM PASS; Runtime hardening FAIL at stale signing marker; Sketch intent FAIL at legacy main-map validator; main CI reaches HotkeyStudio suite and FAILs stale capability-lock file assertion.  
**Plan changes:** T-001 scope expanded only for newly evidenced blockers; F-009..F-012 added.  
**Commit:** preflight `fab88e79c3a98f0eeb30ba496fc0a834cbae337a`  
**Push:** audit branch only; no broken `main` push  
**Result:** FAIL → classified/reconciling

## Iteration 1 — Wave B

**Task:** T-001  
**Findings addressed:** F-009..F-012  
**Unexpected findings:** pending  
**Changes:** retire legacy Sketch workflow; follow `NxRequestSigningPolicy` ownership; replace lock-file existence assertion with canonical v8 operation invariants; strengthen optional-lock validator; make nullable normalization contract explicit.  
**Tests:** pending preflight Actions.  
**Plan changes:** current document reflects Wave A evidence before Wave B code.  
**Commit:** pending  
**Push:** audit branch only until PASS  
**Result:** VERIFYING

# 21. Definition of Final Done

- no known Critical/High findings;
- all P0/P1 DONE or explicitly rejected/deferred with evidence;
- applicable CI consistently green with no masked native failures;
- important architecture invariants enforced technically where feasible;
- critical Protocol/security/state logic mutation-tested with no unexplained high-risk survivors;
- multidimensional critical edge-space covered;
- performance/security/reliability baselines recorded and acceptable;
- no unexplained flaky tests;
- docs match current v8 code and do not require absent production artefacts;
- live-NX-only claims have explicit verification records;
- repeat audit finds no fundamental delta;
- `MASTER_PLAN.md`, repository state and last verified `main` SHA agree.
