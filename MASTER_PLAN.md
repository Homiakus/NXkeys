# 1. Mission

Сделать NXkeys устойчивой, проверяемой и эволюционирующей системой, где `MASTER_PLAN.md` является единственным living execution plan, а каждое существенное изменение проходит цикл `AUDIT → PLAN → IMPLEMENT → TEST → REVIEW → RECONCILE → COMMIT → VERIFIED PUSH`.

Текущий фокус: восстановить достоверный зелёный baseline после перехода на канонический v8-профиль, затем продолжить незавершённую декомпозицию NXkeys без скрытого scope creep.

# 2. Current State

- Платформа: C#/.NET 8, Windows x64; Siemens NX / Designcenter NX 2512.
- Канонический runtime profile: `config/nx2512-v8-profile.json`, schema 8.
- IPC contract: schema 4; файловая очередь с authenticated/HMAC/replay protections.
- Основные границы: HotkeyStudio (out-of-NX orchestration/UI), CommandBridge (in-NX adapter), Protocol, StateMachines, ControlCenter, NxEskd.
- Последний baseline `main` перед этим планом: `2359e85c02313f4c6227a5e9a38ef62e9b9c043f`.
- Все 5 workflow, запущенные на этом SHA, завершились `failure`.
- Исторический K3–K5/full-command-map pipeline был удалён при cleanup v8, но часть CI/docs осталась привязана к удалённым artefacts.
- `docs/REFACTOR_STATUS.md` фиксирует незавершённую модульную декомпозицию и необходимость Windows/CI проверки.

# 3. Architecture Map

```text
Physical keyboard / CapsLock leader
        |
        v
NX2512_HotkeyStudio
  UI + LeaderKeyEngine + NxContextClient
  NxCommandBridgeClient
        |
        | authenticated file IPC
        v
NXKeys.Protocol <---- NXKeys.StateMachines
        |
        v
NXKeys.BridgeCore
        |
        v
NX2512_CommandBridge (inside NX)
  NxContextMonitor
  NxMenuCommandExecutor
  NxSelectionExecutor
  NxInterventionGuard
        |
        v
NXOpen / NX UI / capabilities (NxEskd)
```

Architectural ownership:
- `NXKeys.Protocol`: shared contracts, security, context normalization.
- `NXKeys.StateMachines`: deterministic leader/HFSM policy and context guards.
- `NX2512_HotkeyStudio`: desktop lifecycle, input orchestration, transport client and user-facing tooling.
- `NXKeys.BridgeCore`: admitted request queue/security boundary.
- `NX2512_CommandBridge`: bounded NXOpen adapter only.

# 4. Baseline

Baseline at `2359e85c...`:

| Check | State | Evidence / note |
|---|---|---|
| GitHub Actions | FAIL | 5/5 workflows on latest push failed |
| `ci` | FAIL | State-machine test: declarative `root_ms` not loaded |
| Documentation | FAIL | legacy `config/full-command-map` missing |
| Main Profile Runtime | FAIL | legacy `config/full-command-map` missing |
| Main K3-K5 Profile | FAIL | legacy `config/full-command-map` missing |
| Runtime hardening | FAIL | source check expects `NxTransportReadStatus` in wrong file |
| JSON/doc current-v8 validation | PARTIAL | some native command failures were masked by PowerShell last-exit semantics |
| Unit/invariant tests | BLOCKED | CI stops before full suite |
| Integration/live NX | NOT RUN | requires Windows + NX installation |
| Static analysis | PARTIAL | compiler warnings/build checks only |
| Security scans | NOT CONFIGURED | add dependency/code scanning policy later |
| Race/concurrency checks | PARTIAL | deterministic tests exist; no dedicated stress gate |
| Coverage | UNKNOWN | no enforced baseline found |
| Mutation score | NOT CONFIGURED | required for critical validators/security/state machines |
| Benchmarks | UNKNOWN | no enforced performance baseline found |
| Flaky tests | UNKNOWN | baseline red prevents trustworthy assessment |

Pre-existing failures are not attributed to new work unless a new change worsens them.

# 5. System Invariants

1. `main` must never knowingly move from a better verified state to a worse state.
2. Canonical user/runtime configuration is v8 schema 8; deleted K3–K5 artefacts must not silently reappear as production dependencies.
3. State-machine policy consumed by `LeaderBehaviorProfile.LoadDefault()` must be shipped with HotkeyStudio and tests.
4. Out-of-NX code does not execute NXOpen directly; in-NX CommandBridge owns NXOpen effects.
5. CommandBridge admission remains authenticated, permission-bound, replay-resistant and context-bound.
6. NX UI thread must not perform unbounded queue enumeration or cryptographic admission.
7. Selection-filter actions must be represented in shared protocol and executed through the in-NX executor boundary.
8. Context normalization has one canonical implementation in Protocol.
9. Any substantial unexpected finding is recorded before implementation scope expands.
10. Tests and observed runtime behavior outrank this plan; contradictions force plan reconciliation.

# 6. Findings Registry

## F-001 — Latest main has no green CI baseline

**Status:** Planned  
**Category:** Reliability / CI  
**Severity:** Critical  
**Confidence:** Confirmed  
**Evidence:** all five workflows triggered by `2359e85c...` concluded failure.  
**Files / symbols:** `.github/workflows/*`  
**Current behavior:** routine push cannot establish a trusted releasable state.  
**Expected behavior:** applicable workflows are green and fail for real contract violations.  
**Root cause:** multiple incomplete migrations plus stale source assertions.  
**Impact:** every subsequent change lacks a trustworthy regression signal.  
**Blast radius:** entire repository.  
**Reproduction:** inspect Actions for `2359e85c...`.  
**Affected invariants:** 1, 10.  
**Related findings:** F-002, F-003, F-004, F-005.  
**Affected tasks:** T-001.  
**Recommended direction:** repair baseline before feature/refactor work.

## F-002 — CI still depends on removed K3–K5/full-command-map artefacts

**Status:** Planned  
**Category:** CI / Migration  
**Severity:** High  
**Confidence:** Confirmed  
**Evidence:** `config` contains only the v8 profile while workflows/scripts still scan `config/full-command-map` and reference `nx2512-pro-hybrid.json`.  
**Files / symbols:** `ci.yml`, `documentation.yml`, `full-command-map.yml`, `main-profile-runtime.yml`, legacy map scripts.  
**Current behavior:** ENOENT/stale compatibility checks.  
**Expected behavior:** current CI validates v8 source of truth; legacy tooling is explicitly archived/deferred.  
**Root cause:** cleanup commit removed config assets without reconciling automation.  
**Impact:** docs/runtime/profile workflows fail; masked validator failures exist.  
**Blast radius:** CI, docs, profile build.  
**Reproduction:** run legacy map validators on current main.  
**Affected invariants:** 1, 2.  
**Related findings:** F-001, F-006.  
**Affected tasks:** T-001, T-004.  
**Recommended direction:** retire legacy push gates, keep migration history explicit.

## F-003 — Declarative state-machine policy was removed although runtime still consumes it

**Status:** Planned  
**Category:** Correctness / Packaging  
**Severity:** High  
**Confidence:** Confirmed  
**Evidence:** `LeaderBehaviorProfile` searches for `nx2512-state-machines.json`; HotkeyStudio project conditionally publishes it; StateMachines.Tests expect `root_ms=4000`, but test project currently copies the unrelated v8 profile instead.  
**Files / symbols:** `LeaderBehaviorProfile.LoadDefault`, `NX2512_HotkeyStudio.csproj`, `NXKeys.StateMachines.Tests.csproj`, `config/nx2512-state-machines.json`.  
**Current behavior:** fallback 20s timeouts load and test fails.  
**Expected behavior:** declarative policy is present, copied and tested.  
**Root cause:** config cleanup conflated canonical command profile with independent HFSM behavior policy.  
**Impact:** altered interaction timing/guards and red tests.  
**Blast radius:** leader behavior, packaging, tests.  
**Reproduction:** run StateMachines.Tests on current main.  
**Affected invariants:** 3, 10.  
**Related findings:** F-001.  
**Affected tasks:** T-001.  
**Recommended direction:** restore the independent policy file and correct test packaging.

## F-004 — Runtime-hardening source assertion crosses a refactored file boundary

**Status:** Planned  
**Category:** Testing / Architecture  
**Severity:** Medium  
**Confidence:** Confirmed  
**Evidence:** workflow searches `NxCommandBridgeClient.cs` for `NxTransportReadStatus`, but enum now lives in `NxTransportReadResult.cs`.  
**Files / symbols:** `runtime-hardening.yml`, `NxCommandBridgeClient`, `NxTransportReadResult`.  
**Current behavior:** valid refactor is reported as missing invariant.  
**Expected behavior:** checks assert semantic ownership at the correct boundary.  
**Root cause:** brittle textual test not reconciled with transport extraction.  
**Impact:** runtime-hardening workflow always fails.  
**Blast radius:** hardening gate.  
**Reproduction:** current Runtime hardening workflow.  
**Affected invariants:** 1, 4.  
**Related findings:** F-001.  
**Affected tasks:** T-001, T-006.  
**Recommended direction:** validate facade and transport result type separately; later replace brittle source checks with executable architecture tests.

## F-005 — Selection-filter validator still assumes implementation lives in CommandBridge Program.cs

**Status:** Planned  
**Category:** Testing / Architecture  
**Severity:** Medium  
**Confidence:** Confirmed  
**Evidence:** validator requires `SetEnabledGlobalFilterMembers` in `Program.cs`; implementation moved to `NxMenuCommandExecutor`.  
**Files / symbols:** `scripts/validate-command-tree.mjs`, `Program.ProcessClaim`, `NxMenuCommandExecutor.ApplySelectionCommand`.  
**Current behavior:** command-tree validator emits an error that CI currently masks.  
**Expected behavior:** validator follows the established executor boundary.  
**Root cause:** architecture refactor was not propagated into structural validator.  
**Impact:** hidden red invariant; future fail-fast CI would break.  
**Blast radius:** command-tree validation.  
**Reproduction:** run `node scripts/validate-command-tree.mjs`.  
**Affected invariants:** 4, 7, 10.  
**Related findings:** F-001.  
**Affected tasks:** T-001, T-006.  
**Recommended direction:** validate dispatch in Program and NXOpen effect in executor.

## F-006 — Repository contains an orphan gitlink under `.claude/worktrees`

**Status:** Planned  
**Category:** Repository integrity  
**Severity:** Medium  
**Confidence:** Confirmed  
**Evidence:** tree entry mode `160000` exists without matching `.gitmodules`; checkout cleanup emits fatal submodule warning.  
**Files / symbols:** `.claude/worktrees/fix-v8-contract-paths`.  
**Current behavior:** every checkout post-step warns/fails submodule cleanup.  
**Expected behavior:** repository tree contains no accidental worktree gitlink.  
**Root cause:** local agent worktree metadata was committed as a gitlink.  
**Impact:** noisy checkout, fragile tooling.  
**Blast radius:** every CI checkout.  
**Reproduction:** `git submodule foreach --recursive` after checkout.  
**Affected invariants:** 1.  
**Related findings:** F-001.  
**Affected tasks:** T-001.  
**Recommended direction:** delete the orphan gitlink; do not add a fake submodule mapping.

## F-007 — README and generated-audit narrative still describes retired K3–K5 assets

**Status:** Open  
**Category:** Documentation  
**Severity:** Medium  
**Confidence:** Strong  
**Evidence:** README describes `config/full-command-map/` although current main intentionally retains only v8 config.  
**Files / symbols:** `README.md`, `FULL_COMMAND_MAP.md`, `docs/audit/*`, legacy map scripts.  
**Current behavior:** documentation mixes current v8 architecture with retired pipeline.  
**Expected behavior:** docs distinguish current source of truth from historical tooling.  
**Root cause:** migration debt.  
**Impact:** operator/developer confusion.  
**Blast radius:** onboarding and maintenance.  
**Reproduction:** compare README config section with repository tree.  
**Affected invariants:** 2.  
**Related findings:** F-002.  
**Affected tasks:** T-004.  
**Recommended direction:** documentation debt deletion pass after baseline recovery.

## F-008 — Critical logic has no mutation-testing gate or measured mutation score

**Status:** Open  
**Category:** Test-of-tests  
**Severity:** Medium  
**Confidence:** Strong  
**Evidence:** no enforced mutation baseline discovered during initial audit.  
**Files / symbols:** Protocol security, StateMachines, validators.  
**Current behavior:** coverage can be green without proving assertions distinguish meaningful faults.  
**Expected behavior:** mutation testing protects critical pure logic with tracked score and survived-mutant review.  
**Root cause:** test infrastructure gap.  
**Impact:** false confidence in high-risk contracts.  
**Blast radius:** security/state/validation logic.  
**Reproduction:** inspect CI/test tooling.  
**Affected invariants:** 5, 10.  
**Related findings:** none.  
**Affected tasks:** T-007.  
**Recommended direction:** pilot Stryker.NET on pure libraries, then gate stable high-risk subsets.

# 7. Risk Register

| Risk | Probability | Impact | Mitigation |
|---|---:|---:|---|
| v8 migration has more hidden stale checks | High | High | fail-fast CI + iterative log classification |
| Restored HFSM policy diverges from v8 UX | Medium | High | executable tests + live NX checklist before behavior changes |
| Structural text checks give false positives/negatives | High | Medium | migrate to compiled architecture/contract tests |
| NXOpen stubs differ from live NX 2512 | Medium | High | retain live-NX verification as explicit release gate |
| Direct main change regresses current state | Low after preflight | High | preflight branch CI, then non-force fast-forward-equivalent main commit |
| Mutation suite becomes too slow/noisy | Medium | Medium | target pure critical projects; thresholds after baseline |

# 8. Pareto Improvements

1. Restore green CI and make validators fail-fast — unlocks every later change.
2. Enforce living plan structure automatically — prevents drift between audit, task and code.
3. Convert brittle source-text checks into executable architecture tests — large reliability gain.
4. Finish the 3-module refactor at explicit boundaries instead of continuing local decomposition.
5. Add mutation testing to Protocol/StateMachines — highest confidence gain per test effort.

# 9. Dependency DAG

```text
T-001 baseline recovery
  ├─> T-002 executable plan gate
  ├─> T-003 CI evidence/baseline capture
  ├─> T-004 legacy K3-K5 documentation/tooling debt deletion
  ├─> T-006 architecture-test migration
  │     └─> T-005 remaining module-boundary refactor
  └─> T-007 mutation-testing pilot
          └─> T-008 multidimensional edge-space expansion

T-005 + T-008 ─> T-009 performance/security/reliability re-audit
T-009 ─> T-010 final convergence audit
```

# 10. Implementation Phases

- **Phase 0 — Recover trust:** T-001..T-003.
- **Phase 1 — Delete migration debt:** T-004.
- **Phase 2 — Stabilize architecture boundaries:** T-005..T-006.
- **Phase 3 — Strengthen tests-of-tests:** T-007..T-008.
- **Phase 4 — Measure and harden:** T-009.
- **Phase 5 — Re-audit and converge:** T-010 plus any delta tasks created from findings.

# 11. Atomic Tasks

## T-001 — Reconcile v8 migration fallout and restore a trustworthy baseline

**Status:** VERIFYING  
**Priority:** P0  
**Type:** FIX  
**Leverage:** HIGH

### Problem
Current `main` is red because independent migration defects were allowed to accumulate after the v8 config cleanup/refactor.

### Evidence
F-001..F-006.

### Goal
Make current v8 architecture self-consistent enough for all applicable CI gates to run and report real failures.

### Scope
- restore independent `nx2512-state-machines.json` policy;
- correct StateMachines.Tests output packaging;
- reconcile command-tree validator with `NxMenuCommandExecutor` ownership;
- remove current CI dependencies on retired K3–K5 assets;
- make current invariant scripts fail-fast;
- repair runtime-hardening ownership check;
- retire obsolete K3–K5 push workflows;
- remove orphan `.claude` gitlink;
- keep this plan synchronized with verification results.

### Non-goals
No new user feature, no behavior redesign, no INxCommandExecutor adoption, no live-NX semantic change.

### Files / symbols
`config/nx2512-state-machines.json`, StateMachines test csproj, current workflows, `validate-command-tree.mjs`, orphan gitlink, `MASTER_PLAN.md`.

### Implementation
Perform minimal migration reconciliation; preserve existing v8 profile and runtime boundaries.

### Invariants
1, 2, 3, 4, 7, 10.

### Compatibility constraints
Do not restore `nx2512-pro-hybrid.json` or full-command-map as production dependencies.

### Edge cases
Missing policy file; wrong test output name; native-command exit masking; refactored source ownership; branch vs main workflow triggers; checkout cleanup.

### Tests
Current validators, StateMachines.Tests, HotkeyStudio.Tests, NxEskd tests, desktop builds, NXOpen stub/bridge contract build, hardening workflow.

### Mutation tests
Not yet available; captured as T-007 rather than faked.

### Benchmarks
Not applicable to this migration repair.

### Acceptance criteria
All applicable Actions for the verification commit are green; no legacy full-command-map ENOENT; no orphan submodule warning; published HotkeyStudio contains state policy.

### Verification commands
`node scripts/validate-documentation.mjs`; `node scripts/validate-command-tree.mjs`; `node scripts/validate-capability-route-lock.mjs`; relevant `dotnet run/test/build/publish` commands encoded in CI.

### Dependencies
None.

### Blocks
T-002..T-010.

### Risk
Medium: multiple stale contracts may surface only after earlier blockers are removed.

### Rollback
Revert the iteration commit; never restore deleted legacy K3–K5 artefacts merely to silence CI.

### Estimated effort
Small-to-medium, dominated by iterative CI discovery.

## T-002 — Enforce MASTER_PLAN structure as an executable repository gate

**Status:** READY  
**Priority:** P1  
**Type:** HARDEN  
**Leverage:** HIGH

### Problem
A living plan can drift unless its required sections/IDs/status vocabulary are machine-checked.
### Evidence
No `MASTER_PLAN.md` or plan validator existed before this audit.
### Goal
Add a deterministic plan-integrity validator and CI invocation.
### Scope
Validate required sections, unique F/T IDs, status/priority/type vocabularies and iteration log presence.
### Non-goals
Do not attempt to infer whether prose is semantically true.
### Files / symbols
`MASTER_PLAN.md`, `scripts/validate-master-plan.mjs`, CI.
### Implementation
Pure Node.js validator with no external dependency.
### Invariants
9, 10.
### Compatibility constraints
Works on Windows/Linux Node without package install.
### Edge cases
Duplicate IDs, missing headings, invalid task status, malformed iteration entry.
### Tests
Positive current plan; negative fixtures or self-test mode.
### Mutation tests
Consider mutating parser after T-007 infrastructure.
### Benchmarks
Sub-second target.
### Acceptance criteria
CI rejects malformed plan and accepts current plan.
### Verification commands
`node scripts/validate-master-plan.mjs`.
### Dependencies
T-001.
### Blocks
None.
### Risk
Low.
### Rollback
Remove validator invocation, retain plan.
### Estimated effort
Small.

## T-003 — Capture a machine-readable verified baseline

**Status:** TODO  
**Priority:** P1  
**Type:** HARDEN  
**Leverage:** HIGH

### Problem
Baseline facts are scattered across workflow logs.
### Evidence
F-001 and initial audit.
### Goal
Record tool/check status and known environmental limitations in a compact versioned baseline artifact.
### Scope
CI/check inventory, commit SHA, pass/fail/unsupported classification, no secrets.
### Non-goals
No telemetry service.
### Files / symbols
`docs/audit/verification-baseline.md` or JSON + CI helper.
### Implementation
Generate deterministically from repository-known checks where practical.
### Invariants
1, 10.
### Compatibility constraints
No live-NX claims from stub-only CI.
### Edge cases
Skipped jobs, environment-only tests, flaky reruns.
### Tests
Schema/content validator.
### Mutation tests
N/A.
### Benchmarks
N/A.
### Acceptance criteria
Future iterations can distinguish pre-existing from introduced failures.
### Verification commands
CI.
### Dependencies
T-001.
### Blocks
T-009.
### Risk
Low.
### Rollback
Manual baseline in plan remains fallback.
### Estimated effort
Small.

## T-004 — Retire legacy K3–K5 documentation and tooling debt

**Status:** TODO  
**Priority:** P1  
**Type:** REMOVE  
**Leverage:** HIGH

### Problem
Retired artefacts remain described and partially scripted.
### Evidence
F-002, F-007.
### Goal
One unambiguous v8 source-of-truth story while preserving historical traceability through Git history/docs.
### Scope
README/FULL_COMMAND_MAP/docs/audit references; unused compiler/validator scripts; obsolete generated audit files after impact analysis.
### Non-goals
Do not delete reusable sequence-policy code still used by v8 validators.
### Files / symbols
README, docs, legacy map scripts.
### Implementation
Usage search → classify current/legacy → delete or archive references → validation.
### Invariants
2.
### Compatibility constraints
No runtime dependency removal without proof.
### Edge cases
Scripts shared by current command-tree docs.
### Tests
Documentation and command-tree validators.
### Mutation tests
N/A.
### Benchmarks
N/A.
### Acceptance criteria
No current docs instruct users to depend on absent files.
### Verification commands
repository search + current docs validation.
### Dependencies
T-001.
### Blocks
None.
### Risk
Medium.
### Rollback
Restore only documentation/tooling proven current.
### Estimated effort
Medium.

## T-005 — Finish explicit module-boundary adoption without changing runtime semantics

**Status:** TODO  
**Priority:** P1  
**Type:** IMPROVE  
**Leverage:** HIGH

### Problem
`docs/REFACTOR_STATUS.md` lists unresolved `INxCommandExecutor` / `ISelectionEngine` boundary work and out-of-NX intervention constraints.
### Evidence
Current refactor status document.
### Goal
Complete boundaries only where contracts can preserve observable behavior.
### Scope
One PR-sized boundary migration at a time, starting with characterization tests.
### Non-goals
No throw→result semantic change without explicit compatibility design.
### Files / symbols
ModuleContracts, executors, LeaderKeyEngine, Program.ProcessClaim.
### Implementation
Characterize → introduce adapter → dual compatibility → migrate callers → remove old path.
### Invariants
4, 5, 6, 10.
### Compatibility constraints
Live NX behavior and IPC contracts preserved.
### Edge cases
Modal state, stale context, selection changes, process boundaries, cancellation/failure paths.
### Tests
Contract + architecture + Windows/live NX where required.
### Mutation tests
Critical pure decision logic after T-007.
### Benchmarks
Input-to-dispatch latency baseline before behavior change.
### Acceptance criteria
Boundary is explicit, callers migrated, legacy adapter deleted, tests prove semantics.
### Verification commands
CI + live verification checklist where required.
### Dependencies
T-001, T-006 where applicable.
### Blocks
T-010.
### Risk
High.
### Rollback
Retain compatibility adapter until live verification passes.
### Estimated effort
Large but split into atomic subtasks during execution.

## T-006 — Replace brittle source-text hardening checks with executable architecture tests

**Status:** TODO  
**Priority:** P1  
**Type:** HARDEN  
**Leverage:** HIGH

### Problem
F-004/F-005 show source-location assertions become stale after valid refactors.
### Evidence
Runtime hardening and command-tree failures.
### Goal
Test contracts/ownership rather than filenames/text placement.
### Scope
Protocol/BridgeCore/CommandBridge/HotkeyStudio boundaries and prohibited dependencies.
### Non-goals
Do not remove useful fast smoke checks until replacements exist.
### Files / symbols
Test projects, ModuleContracts, workflows.
### Implementation
Add compile-time/reflection/architecture assertions; migrate checks incrementally.
### Invariants
4, 5, 6, 7.
### Compatibility constraints
No production behavior change.
### Edge cases
internal types, conditional NXOpen references, test stubs.
### Tests
Tests-of-tests by deliberate local negative fixture/mutation.
### Mutation tests
After T-007.
### Benchmarks
N/A.
### Acceptance criteria
Moving a type between valid implementation files no longer breaks CI, violating boundary still does.
### Verification commands
Targeted test project + CI.
### Dependencies
T-001.
### Blocks
T-005.
### Risk
Medium.
### Rollback
Keep old smoke checks until executable test proves equivalent detection.
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
No mutation gate/baseline discovered.
### Goal
Measure whether tests detect meaningful faults in Protocol, StateMachines and validators.
### Scope
Pilot on pure projects; classify survived mutants; establish threshold only after baseline.
### Non-goals
No blind global mutation of Windows/NXOpen UI code.
### Files / symbols
Protocol security, normalization, StateMachines guards, validator logic.
### Implementation
Stryker.NET where compatible; JS mutation strategy separately if worthwhile.
### Invariants
5, 10.
### Compatibility constraints
CI runtime bounded.
### Edge cases
equivalent mutants, generated code, time-dependent tests.
### Tests
Mutation run itself + survived mutant review.
### Mutation tests
This task establishes them.
### Benchmarks
Track mutation job duration.
### Acceptance criteria
Critical projects have measured score and no unexplained high-risk survivors.
### Verification commands
Tool-specific command recorded after discovery.
### Dependencies
T-001.
### Blocks
T-008.
### Risk
Medium.
### Rollback
Informational mutation job before gating.
### Estimated effort
Medium.

## T-008 — Expand multidimensional edge-case coverage

**Status:** TODO  
**Priority:** P2  
**Type:** HARDEN  
**Leverage:** HIGH

### Problem
Critical behaviors span input × state × concurrency × timing × failure × permissions × configuration × external NX state.
### Evidence
Security/context/selection architecture.
### Goal
Systematically cover high-risk pairwise/N-wise combinations and fuzzable pure contracts.
### Scope
Protocol auth/replay, queue inbox, state machines, config parsing, context guards.
### Non-goals
No combinatorial brute force of live NX UI.
### Files / symbols
Critical test projects.
### Implementation
Partition dimensions → pairwise baseline → high-risk N-wise → fuzz/property/metamorphic cases.
### Invariants
3, 5, 6, 10.
### Compatibility constraints
Deterministic CI.
### Edge cases
Clock skew, duplicate/reordered requests, corrupt JSON, stale revision, permission mismatch, cancellation, modal state.
### Tests
Property/fuzz/randomized with reproducible seeds.
### Mutation tests
Use mutation survivors to select missing observables.
### Benchmarks
Track stress-test duration/allocations where applicable.
### Acceptance criteria
Documented edge-space matrix with executable high-risk coverage.
### Verification commands
Targeted test suites.
### Dependencies
T-007.
### Blocks
T-009.
### Risk
Medium.
### Rollback
Keep deterministic core cases if a fuzz harness is flaky.
### Estimated effort
Medium-large, split by subsystem.

## T-009 — Establish performance, security and reliability baselines

**Status:** TODO  
**Priority:** P2  
**Type:** HARDEN  
**Leverage:** MEDIUM

### Problem
Coverage/mutation/performance/dependency/race-style baseline is incomplete.
### Evidence
Section 4.
### Goal
Quantify release-relevant regressions instead of relying on qualitative checks.
### Scope
allocation/latency benchmarks for pure hot paths, dependency audit, security checks, flaky/retry observation.
### Non-goals
Do not claim live NX latency from CI stubs.
### Files / symbols
CI, benchmark/test projects.
### Implementation
Add measurements one by one with conservative non-flaky thresholds.
### Invariants
1, 5, 10.
### Compatibility constraints
Reasonable CI duration.
### Edge cases
cold start vs steady state, file IO variability.
### Tests
Benchmark regression and dependency/security scanning.
### Mutation tests
Use T-007 results.
### Benchmarks
This task establishes them.
### Acceptance criteria
Versioned baseline/targets exist for critical measurable paths.
### Verification commands
Recorded in generated baseline.
### Dependencies
T-003, T-008.
### Blocks
T-010.
### Risk
Medium.
### Rollback
Move unstable thresholds to informational mode, never delete measurements silently.
### Estimated effort
Medium.

## T-010 — Perform final convergence audit and delta implementation

**Status:** TODO  
**Priority:** P2  
**Type:** IMPROVE  
**Leverage:** HIGH

### Problem
Initial audit cannot prove convergence after major changes.
### Evidence
Master protocol requires re-audit.
### Goal
Repeat architecture/correctness/security/concurrency/reliability/API/data/testing/mutation/performance/CI/docs/dependency audit until no fundamental delta remains.
### Scope
Whole repository.
### Non-goals
No cosmetic expansion unrelated to findings.
### Files / symbols
All.
### Implementation
Re-audit → create findings/tasks → execute delta → repeat.
### Invariants
All.
### Compatibility constraints
No known regression on live NX release path.
### Edge cases
All high-risk dimensions from T-008.
### Tests
All established gates + live checklist.
### Mutation tests
Critical suites.
### Benchmarks
All established baselines.
### Acceptance criteria
Convergence criterion in section 21.
### Verification commands
Full CI + release/live verification.
### Dependencies
T-005, T-009.
### Blocks
Final done.
### Risk
Medium.
### Rollback
Per delta task.
### Estimated effort
Variable.

# 12. Testing Strategy

- Characterization before behavior-changing refactor.
- Pure unit/contract tests for Protocol, StateMachines and BridgeCore.
- Windows integration/build tests for HotkeyStudio/ControlCenter/CommandBridge.
- NXOpen contract stubs for compile-time adapter coverage.
- Live NX checklist remains mandatory for claims that depend on actual NX behavior.
- Negative security tests are first-class tests, not optional extras.
- Native command failures must propagate immediately; no last-command masking.

# 13. Mutation Testing Strategy

Initial state: not configured. T-007 will establish measured baselines before any threshold is enforced.

Priority order: auth/HMAC/replay → context guards → state transitions → config validators → normalization → queue policy. Survived mutants require observable-contract analysis, not coverage inflation.

# 14. Performance Baselines

Unknown at initial audit. Candidate measurements after functional baseline is green:
- leader decision/state transition latency and allocations;
- context parsing/normalization;
- queue admission throughput and bounded poll cost;
- config load/validation time;
- cold-start/publish size for desktop components.

# 15. Security Hardening

Current architecture contains authenticated HMAC requests, replay guard, permissions, context binding and bounded admitted queue. Future work must preserve these invariants and add dependency/code scanning plus mutation-resistant negative tests. No secrets or permissive fallback may be added to make tests pass.

# 16. Migration Strategy

For structural changes use:

`characterization → new boundary → dual compatibility → migrate callers → verify → remove legacy`.

The v8 cleanup is treated as an incomplete migration: current source of truth stays v8; only independently required HFSM policy is restored. Retired K3–K5 source assets are not resurrected as runtime dependencies.

# 17. Deferred Work

- Live NX hardware/application verification where CI stubs cannot prove semantics.
- Performance thresholds until baseline data exists.
- Broad mutation threshold until pilot data exists.
- Cosmetic UI improvements unrelated to current findings.

# 18. Rejected Decisions

- **Restore all deleted K3–K5/full-command-map files just to green CI:** rejected; contradicts intentional v8-only cleanup and perpetuates dual sources of truth.
- **Silently skip failing validators:** rejected; current validators must either be current and fail-fast or explicitly retired.
- **Force-push main:** rejected.
- **Change INxCommandExecutor semantics during baseline repair:** rejected; unrelated high-risk behavior change.
- **Treat stub CI as proof of live NX behavior:** rejected.

# 19. Completed Tasks

None at initial bootstrap. Tasks move here only after verified iteration completion and plan reconciliation.

# 20. Iteration Log

## Iteration 1

**Task:** T-001  
**Findings addressed:** F-001, F-002, F-003, F-004, F-005, F-006  
**Unexpected findings:** validator exit masking; independent HFSM config accidentally removed; transport/selection checks stale after file-boundary refactors  
**Changes:** baseline recovery prepared on preflight branch; final list is reconciled after CI.  
**Tests:** preflight Actions pending.  
**Plan changes:** initial living plan created from repository/Actions evidence.  
**Commit:** pending verification  
**Push:** preflight branch only; `main` pending PASS  
**Result:** VERIFYING

# 21. Definition of Final Done

NXkeys converges when:
- no known Critical/High findings remain;
- all P0/P1 tasks are DONE or explicitly REJECTED with evidence;
- applicable CI is consistently green with no masked native failures;
- architectural boundaries are executable/tested rather than prose-only where feasible;
- Protocol/security/state-machine critical logic has mutation-tested observables and no unexplained high-risk survivors;
- multidimensional edge-case strategy is applied to critical contracts;
- performance/reliability/security baselines are recorded and within targets;
- no unexplained flaky tests remain;
- docs describe the current v8 architecture and no absent production artefacts;
- live-NX-only claims have an explicit passed verification record;
- repeat audit finds no new fundamental defects;
- `MASTER_PLAN.md`, code, tests and last verified `main` SHA agree.
