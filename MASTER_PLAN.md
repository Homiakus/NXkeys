# 1. Mission

Сделать NXkeys устойчивой, проверяемой и эволюционирующей системой, где `MASTER_PLAN.md` является единственным living execution plan, а каждое существенное изменение проходит цикл `AUDIT → PLAN → SELECT → IMPLEMENT → TEST → REVIEW → RECONCILE → COMMIT → VERIFIED PUSH → COMPRESS`.

Текущий приоритет — восстановить достоверный зелёный baseline после неполного перехода на канонический v8-профиль, не возвращая удалённый K3–K5/full-command-map контур как production dependency и не изменяя runtime semantics ради зелёного CI.

# 2. Current State

- C#/.NET 8, Windows x64, Siemens NX / Designcenter NX 2512.
- Канонический source profile: `config/nx2512-v8-profile.json`, schema 8.
- Raw v8 source содержит 439 операций с 439 уникальными `operation_id`; текущий Node validator подтверждает это до .NET runtime translation.
- `Config` реализует `IJsonOnDeserialized`: `V8SecondaryAliasExpander` материализует `secondary_aliases` как дополнительные in-memory `OperationContract`, поэтому runtime projection содержит 500 записей, но всё ещё 439 семантических operation IDs. Это намеренная compatibility projection, а не дубликаты в raw source.
- IPC: schema 4, HMAC authentication, replay guard, permission/context binding.
- Boundaries: HotkeyStudio → Protocol/StateMachines → BridgeCore → CommandBridge/NXOpen; ControlCenter и NxEskd — отдельные capability/UI surfaces.
- Baseline `main` до внедрения подхода: `2359e85c02313f4c6227a5e9a38ef62e9b9c043f`; все 5 workflow, запущенные тем push, были красными.
- Preflight branch: `audit/living-master-plan-bootstrap-20260829`; `main` остаётся неизменным до полностью зелёного verified state.
- Wave A `fab88e79...`: восстановила HFSM policy и вскрыла более глубокие migration blockers.
- Wave B `a5f9a718...`: strict Protocol/BridgeCore build, source hardening и DFA/HFSM проходят; обнаружены F-013/F-014.
- Wave C diagnostics `248b5f77...` → `cfc3950...`: доказали raw uniqueness и alias projection semantics; production profile не должен изменяться для устранения F-014.
- Windows GitHub Actions является authoritative preflight-средой; live NX 2512 остаётся отдельным внешним verification layer.

# 3. Architecture Map

```text
Keyboard / CapsLock leader
        ↓
NX2512_HotkeyStudio
  LeaderKeyEngine / NxContextClient
  NxCommandBridgeClient → NxRequestSigningPolicy
  Config(raw v8) → V8SecondaryAliasExpander → legacy runtime projection
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

Ownership rules:
- `NXKeys.Protocol` — shared contracts, authentication/security and canonical context normalization.
- `NXKeys.StateMachines` — deterministic leader/HFSM policy.
- `NX2512_HotkeyStudio` — out-of-NX orchestration/UI/config translation.
- `V8SecondaryAliasExpander` — in-memory compatibility projection only; it must not redefine raw canonical identity.
- `NxRequestSigningPolicy` — request signing/admission preparation.
- `NXKeys.BridgeCore` — bounded background inbox/security admission.
- `NX2512_CommandBridge` — in-NX NXOpen effects.

# 4. Baseline

| Check | Baseline `2359e85...` | Current preflight evidence |
|---|---|---|
| Push workflows | 5/5 FAIL | obsolete K3–K5 gates retired; remaining current workflows still verifying |
| Current Node validators | stale/masked | PASS; raw v8 = 439 unique operations |
| DFA/HFSM | FAIL missing policy | PASS after policy restoration |
| Protocol strict build | blocked | PASS, 0 warnings/errors |
| BridgeCore strict build | blocked | PASS, 0 warnings/errors |
| Runtime source hardening | stale locations | source checks PASS after ownership reconciliation |
| HotkeyStudio regressions | blocked | FAIL on stale uniqueness assertion against alias-expanded runtime projection |
| Desktop UI | stale smoke contract | FAIL on obsolete literal `MaximumRootRows = 10` |
| Documentation | FAIL legacy map | PASS on current v8 docs validation |
| checkout integrity | orphan gitlink | clean after gitlink removal |
| NxEskd / desktop / NXOpen bridge builds | downstream blocked | must run after current blockers clear |
| live NX | NOT RUN | external NX 2512 installation required |
| coverage | UNKNOWN | T-009 |
| mutation score | NOT CONFIGURED | T-007 |
| benchmarks | UNKNOWN | T-009 |

Pre-existing failures remain separated from defects introduced by this work.

# 5. System Invariants

1. `main` never knowingly moves to a worse verified state.
2. Raw v8 schema 8 is the canonical operation/profile source of truth.
3. Raw canonical `operation_id` values are non-empty and globally unique.
4. Runtime alias projections may share the canonical `operation_id` only when they represent the same semantic operation and differ only by an explicitly declared secondary route/scope projection.
5. `V8SecondaryAliasExpander` is an in-memory compatibility transformation; tests must distinguish raw identity from projected routes.
6. Independent HFSM policy consumed by `LeaderBehaviorProfile` ships with runtime/tests.
7. Out-of-NX code never performs NXOpen effects directly.
8. IPC admission remains authenticated, permission-bound, replay-resistant and context-bound.
9. NX UI thread never performs unbounded queue scanning or cryptographic admission.
10. Selection-filter actions flow through shared protocol to the in-NX executor boundary.
11. Context normalization has one canonical Protocol implementation.
12. Significant unexpected findings are recorded/reconciled before scope expands.
13. Tests and observed behavior outrank prior roadmap assumptions.
14. CI asserts semantic contracts, not incidental source-file locations.
15. Deleted K3–K5 assets are not recreated merely to silence stale checks.

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
**Affected invariants:** 1,13.  
**Related findings:** F-002..F-006, F-009..F-014.  
**Affected tasks:** T-001.  
**Recommended direction:** recover baseline before feature/refactor work.

## F-002 — Active CI depended on removed K3–K5/full-command-map assets
**Status:** Planned  
**Category:** CI / Migration  
**Severity:** High  
**Confidence:** Confirmed  
**Evidence:** v8-only `config/` while old workflows referenced `full-command-map`/`nx2512-pro-*`.  
**Files / symbols:** legacy map/runtime/sketch workflows and scripts.  
**Current behavior:** baseline produced deterministic ENOENT/permanent-red gates.  
**Expected behavior:** active gates validate current v8 only.  
**Root cause:** cleanup removed assets without reconciling automation.  
**Impact:** multiple workflows could never pass.  
**Blast radius:** CI/docs/profile pipeline.  
**Reproduction:** baseline Actions.  
**Affected invariants:** 1,2,15.  
**Related findings:** F-007,F-009.  
**Affected tasks:** T-001,T-004.  
**Recommended direction:** retire obsolete active gates; delete/archive residual debt separately.

## F-003 — Independent state-machine policy was accidentally removed
**Status:** Planned  
**Category:** Correctness / Packaging  
**Severity:** High  
**Confidence:** Confirmed  
**Evidence:** runtime/tests consume `nx2512-state-machines.json`; tests require `root_ms=4000`, but cleanup removed the file.  
**Files / symbols:** `LeaderBehaviorProfile`, HotkeyStudio/StateMachines test csproj.  
**Current behavior:** fallback timing and red tests.  
**Expected behavior:** schema-1 behavior policy packaged independently of v8 command profile.  
**Root cause:** two independent config contracts were conflated.  
**Impact:** leader behavior/timing.  
**Blast radius:** runtime/package/tests.  
**Reproduction:** baseline DFA/HFSM suite.  
**Affected invariants:** 6,13.  
**Related findings:** F-001.  
**Affected tasks:** T-001.  
**Recommended direction:** restore policy only, never legacy command maps.

## F-004 — Runtime hardening checked transport result in wrong source file
**Status:** Planned  
**Category:** Testing / Architecture  
**Severity:** Medium  
**Confidence:** Confirmed  
**Evidence:** `NxTransportReadStatus` lives in `NxTransportReadResult.cs`; old gate searched client source.  
**Files / symbols:** `runtime-hardening.yml`.  
**Current behavior:** valid extraction was reported as defect.  
**Expected behavior:** semantic owner checked.  
**Root cause:** brittle source-text assertion.  
**Impact:** hardening gate red.  
**Blast radius:** runtime CI.  
**Reproduction:** Wave A.  
**Affected invariants:** 1,14.  
**Related findings:** F-010,F-013.  
**Affected tasks:** T-001,T-006.  
**Recommended direction:** reconcile source gate now; replace with executable architecture test later.

## F-005 — Selection validator assumed NXOpen effect remained in `Program.cs`
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
**Reproduction:** validator before Wave A fix.  
**Affected invariants:** 10,14.  
**Related findings:** F-004.  
**Affected tasks:** T-001,T-006.  
**Recommended direction:** follow executor boundary.

## F-006 — Orphan worktree gitlink committed under `.claude`
**Status:** Planned  
**Category:** Repository Integrity  
**Severity:** Medium  
**Confidence:** Confirmed  
**Evidence:** mode `160000` entry with no `.gitmodules`; checkout cleanup emitted submodule failure/noise.  
**Files / symbols:** `.claude/worktrees/fix-v8-contract-paths`.  
**Current behavior:** fragile/noisy checkout.  
**Expected behavior:** no agent-local worktree gitlink in source tree.  
**Root cause:** accidental worktree metadata commit.  
**Impact:** CI/tooling reliability.  
**Blast radius:** every checkout.  
**Reproduction:** baseline checkout cleanup.  
**Affected invariants:** 1.  
**Related findings:** F-001.  
**Affected tasks:** T-001.  
**Recommended direction:** delete gitlink; never fabricate `.gitmodules`.

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
**Reproduction:** compare docs with repository tree.  
**Affected invariants:** 2,15.  
**Related findings:** F-002,F-009.  
**Affected tasks:** T-004.  
**Recommended direction:** evidence-based debt deletion pass after baseline recovery.

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
**Affected invariants:** 8,13.  
**Related findings:** none.  
**Affected tasks:** T-007.  
**Recommended direction:** targeted Stryker.NET pilot then evidence-based threshold.

## F-009 — `Sketch intent grammar` workflow is a retired K3–K5 pipeline
**Status:** Planned  
**Category:** CI / Migration  
**Severity:** High  
**Confidence:** Confirmed  
**Evidence:** Wave A invoked legacy map compiler/validator and failed on removed map files.  
**Files / symbols:** `.github/workflows/sketch-intent.yml`.  
**Current behavior:** duplicate permanent-red gate.  
**Expected behavior:** current v8 sketch grammar covered by HotkeyStudio/current CI.  
**Root cause:** obsolete workflow survived cleanup.  
**Impact:** push gate red.  
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
**Evidence:** client delegates `policy.PrepareAuthenticated`; HMAC/permissions live in extracted signing policy.  
**Files / symbols:** client, signing policy, hardening workflow.  
**Current behavior:** valid security boundary failed Wave A.  
**Expected behavior:** facade and policy ownership asserted independently.  
**Root cause:** stale source-location contract.  
**Impact:** hardening blocked.  
**Blast radius:** runtime security CI.  
**Reproduction:** Wave A hardening.  
**Affected invariants:** 8,14.  
**Related findings:** F-004,F-013.  
**Affected tasks:** T-001,T-006.  
**Recommended direction:** reconcile now; replace with executable architecture test later.

## F-011 — Capability-lock test and validator disagreed about lock optionality
**Status:** Planned  
**Category:** Testing / Contract  
**Severity:** Medium  
**Confidence:** Confirmed  
**Evidence:** validator allowed absent lock while C# regression required file existence.  
**Files / symbols:** `validate-capability-route-lock.mjs`, HotkeyStudio tests.  
**Current behavior:** deleted auxiliary file blocked suite.  
**Expected behavior:** raw canonical v8 is source of truth; optional derived lock must match exactly if present.  
**Root cause:** stale test plus weak optional-lock validation.  
**Impact:** main CI blocked.  
**Blast radius:** profile tests.  
**Reproduction:** Wave A main CI.  
**Affected invariants:** 2,3,13,15.  
**Related findings:** F-014.  
**Affected tasks:** T-001,T-006.  
**Recommended direction:** validate raw canonical profile directly; exact-compare optional lock.

## F-012 — Strict Protocol build exposed nullable contract mismatch
**Status:** Planned  
**Category:** Correctness / Static analysis  
**Severity:** Medium  
**Confidence:** Confirmed  
**Evidence:** nullable-enabled Protocol helper returned `null` from non-nullable `string`; strict Wave B build passes after annotation repair.  
**Files / symbols:** `NxContextNormalization.NormalizeV8Module`.  
**Current behavior:** baseline warning would fail `-warnaserror`.  
**Expected behavior:** explicit nullable contract.  
**Root cause:** annotation mismatch.  
**Impact:** strict-build blocker.  
**Blast radius:** Protocol.  
**Reproduction:** strict build before Wave B.  
**Affected invariants:** 1,11,13.  
**Related findings:** none.  
**Affected tasks:** T-001.  
**Recommended direction:** behavior-preserving nullable annotation.

## F-013 — Desktop UI gate hardcodes obsolete 10-row HUD contract
**Status:** Planned  
**Category:** Testing / UI Contract  
**Severity:** Medium  
**Confidence:** Confirmed  
**Evidence:** Desktop UI expects literal `MaximumRootRows = 10`; actual code defines `PrimarySuggestionLimit = 8` and `MaximumRootRows = PrimarySuggestionLimit`; executable regression also expects 8.  
**Files / symbols:** `.github/workflows/desktop-ui.yml`, `LeaderHudForm.PrimarySuggestionLimit`.  
**Current behavior:** valid current compact HUD fails before tests.  
**Expected behavior:** smoke check follows tested public contract or is replaced by executable test.  
**Root cause:** UI constant changed without workflow reconciliation.  
**Impact:** Desktop UI permanently red.  
**Blast radius:** UI workflow.  
**Reproduction:** Wave B Desktop UI.  
**Affected invariants:** 1,13,14.  
**Related findings:** F-004,F-010.  
**Affected tasks:** T-001,T-006.  
**Recommended direction:** reconcile smoke check to `PrimarySuggestionLimit = 8` plus alias constant; migrate to executable UI/architecture test under T-006.

## F-014 — C# identity regression confuses canonical operations with alias-expanded runtime projections
**Status:** Planned  
**Category:** Testing / Data Contract  
**Severity:** Medium  
**Confidence:** Confirmed  
**Evidence:** raw Node validator reports 439/439 unique canonical operations; `Config : IJsonOnDeserialized` invokes `ExpandSecondaryAliasesForLegacyRuntime()`, cloning declared `secondary_aliases` into additional `OperationContract` routes; after callback the in-memory projection is 500 records / 439 semantic IDs. Diagnostics show collision groups are route families such as W/M, Q/I and R/P and preserve adapter/name semantics.  
**Files / symbols:** `V8SecondaryAliasExpander`, `OperationContract.OperationID`, HotkeyStudio `VerifyBehavioralGuardsAndLimits`, `validate-capability-route-lock.mjs`.  
**Current behavior:** a C# regression asserts global uniqueness on the alias-expanded compatibility projection and therefore rejects intended behavior.  
**Expected behavior:** raw profile uniqueness is enforced before projection; runtime projection tests assert every duplicate ID is a faithful alias projection of one canonical operation, not an unrelated semantic collision.  
**Root cause:** test used the post-deserialization compatibility model as if it were the raw canonical data model.  
**Impact:** false-red HotkeyStudio/main/hardening workflows; changing raw profile to satisfy it would incorrectly delete supported aliases.  
**Blast radius:** CI/test contract; no evidence of raw-profile corruption.  
**Reproduction:** Node validator → 439 unique; `JsonSerializer.Deserialize<Config>` → callback-expanded 500/439.  
**Affected invariants:** 2,3,4,5,13.  
**Related findings:** F-011.  
**Affected tasks:** T-001,T-006.  
**Recommended direction:** keep raw uniqueness validator; replace the false C# uniqueness assertion with projection-integrity assertions; remove temporary diagnostics after regression is characterized.

# 7. Risk Register

| Risk | Probability | Impact | Mitigation |
|---|---:|---:|---|
| further blockers appear as CI progresses | High | High | wave → classify → Finding → reconcile |
| alias projection accidentally changes semantic adapter/scope | Medium | High | projection-integrity regression + raw uniqueness validator |
| source-text checks drift after refactor | High | Medium | T-006 executable architecture tests |
| restored HFSM differs from live UX | Medium | High | executable tests + live NX checklist |
| NXOpen stubs diverge from NX 2512 | Medium | High | never claim live behavior from stub CI |
| mutation suite becomes noisy/slow | Medium | Medium | critical pure projects first |

# 8. Pareto Improvements

1. Recover green/fail-fast current-v8 baseline without weakening semantics.
2. Separate canonical raw profile contracts from runtime compatibility projections in tests.
3. Make living plan structure executable.
4. Replace brittle source-text gates with executable architecture tests.
5. Delete legacy K3–K5 migration debt and mutation-test critical pure logic.

# 9. Dependency DAG

```text
T-001 baseline recovery
 ├─ T-002 plan gate
 ├─ T-003 verified baseline artifact
 ├─ T-004 legacy debt deletion
 ├─ T-006 architecture/projection tests ─ T-005 remaining boundary migration
 └─ T-007 mutation pilot ─ T-008 multidimensional edge coverage
T-003 + T-005 + T-008 ─ T-009 measured hardening ─ T-010 re-audit/convergence
```

# 10. Implementation Phases

- Phase 0: T-001..T-003 — recover trust/governance.
- Phase 1: T-004 — delete migration debt.
- Phase 2: T-006 → T-005 — stabilize semantic boundaries.
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
`main` cannot establish a trustworthy release signal; remaining blockers are stale HUD and alias-projection test contracts.
### Evidence
F-001..F-006, F-009..F-014.
### Goal
All applicable current-v8 Actions green with raw canonical identity and runtime alias semantics both correctly enforced.
### Scope
HFSM policy/package; obsolete workflow retirement; fail-fast validators; executor/transport/signing/UI checks; orphan gitlink; capability contract; Protocol nullable contract; F-013 HUD smoke reconciliation; F-014 raw-vs-projection regression; remove diagnostic-only code; plan reconciliation.
### Non-goals
No new feature/UX redesign, broad module refactor, raw alias deletion, schema bump, live-NX behavior redesign or mutation threshold.
### Files / symbols
current workflows/validators/tests, `V8SecondaryAliasExpander`, HotkeyStudio regression tests, `MASTER_PLAN.md`.
### Implementation
Preserve raw 439-operation source. Keep Node raw uniqueness check. Replace invalid post-deserialization uniqueness assertion with projection-integrity checks. Reconcile Desktop UI smoke assertion to current 8-item contract. Remove `OperationIdentityDiagnostics.cs`. Run full preflight; every newly exposed substantive failure triggers a Finding before scope expansion. After full green preflight, recreate one logical verified iteration commit on current `main` and push non-force.
### Invariants
1–15.
### Compatibility constraints
No restored K3–K5 production dependency; IPC/security semantics preserved; declared secondary aliases preserved.
### Edge cases
raw vs projected identity; global Manage aliases re-scoped to Modeling; canonical-path alias equality; duplicate route dedupe; missing/stale policy; source refactors; native exit masking; strict nullable builds.
### Tests
Node raw validators; projection-integrity regression; DFA/HFSM; HotkeyStudio; NxEskd; strict Protocol/BridgeCore; desktop build/publish; NXOpen stubs/bridge contract; Documentation/Desktop/Hardening Actions.
### Mutation tests
Deferred to T-007; absence explicitly recorded.
### Benchmarks
N/A for migration recovery.
### Acceptance criteria
Every applicable preflight workflow PASS; raw profile remains 439 unique operation IDs; alias projections are proven faithful; no diagnostic-only file remains; no legacy ENOENT; clean checkout; state policy shipped; all downstream tests/builds execute; remote `main` re-synced and final diff reviewed.
### Verification commands
Commands encoded in active Actions plus current validators.
### Dependencies
None.
### Blocks
T-002..T-010.
### Risk
Medium: deeper downstream failures may appear after current blockers clear.
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
`MASTER_PLAN.md`, `scripts/validate-master-plan.mjs`, CI.
### Implementation
Required headings; unique F/T IDs; valid Finding/Task vocabularies; iteration-log presence; in-memory negative fixtures.
### Invariants
12,13.
### Compatibility constraints
No npm dependencies; Windows/Linux Node.
### Edge cases
Duplicate IDs, missing metadata, malformed log.
### Tests
Positive current plan + negative fixtures.
### Mutation tests
Later if useful.
### Benchmarks
Sub-second target.
### Acceptance criteria
Malformed plan rejected; current plan accepted.
### Verification commands
`node scripts/validate-master-plan.mjs --self-test` and normal invocation.
### Dependencies
T-001.
### Blocks
None.
### Risk
Low.
### Rollback
Remove gate only if parser itself is proven defective.
### Estimated effort
Small.

## T-003 — Capture machine-readable verified baseline
**Status:** TODO  
**Priority:** P1  
**Type:** HARDEN  
**Leverage:** HIGH
### Problem
Verification evidence is scattered in Actions logs.
### Evidence
Initial/wave audit required manual correlation.
### Goal
Version check inventory, environment limits and pass/fail classification.
### Scope
Repository-known checks; no secrets; distinguish stubs/live NX.
### Non-goals
No telemetry service.
### Files / symbols
`docs/audit/verification-baseline.*`, helper.
### Implementation
Deterministic manifest with last verified state and environment classification.
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
Introduced vs pre-existing failures become obvious.
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
Retired pipeline remains referenced in docs/scripts.
### Evidence
F-002,F-007,F-009.
### Goal
One unambiguous current v8 source-of-truth story.
### Scope
Usage-search legacy compilers/validators/docs; classify/delete/archive.
### Non-goals
Retain helpers still used by v8.
### Files / symbols
README/full map/docs/audit/legacy scripts.
### Implementation
Usage search → classify → delete/update → verify.
### Invariants
2,15.
### Compatibility constraints
No unproven runtime deletions.
### Edge cases
Shared helpers.
### Tests
Current docs/command/profile suites.
### Mutation tests
N/A.
### Benchmarks
N/A.
### Acceptance criteria
Active docs/workflows do not require absent legacy assets.
### Verification commands
Repository search + CI.
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
Refactor status lists unresolved executor/selection/intervention boundaries.
### Evidence
`docs/REFACTOR_STATUS.md`.
### Goal
Complete one behavior-preserving boundary per atomic subtask.
### Scope
Characterize → boundary/adapter → dual compatibility → migrate → verify → delete legacy.
### Non-goals
No silent behavior/API transition.
### Files / symbols
ModuleContracts/executors/LeaderKeyEngine/claim path.
### Invariants
7–10,13.
### Compatibility constraints
IPC/live NX behavior preserved.
### Edge cases
Modal/stale context/selection/cancellation/process boundary.
### Tests
Contract/architecture/live checklist when required.
### Mutation tests
Pure decisions after T-007.
### Benchmarks
Dispatch baseline where behavior-sensitive.
### Acceptance criteria
Explicit boundary, migrated callers, old path deleted after verification.
### Verification commands
CI + live checklist.
### Dependencies
T-001,T-006 where applicable.
### Blocks
T-010.
### Risk
High.
### Rollback
Compatibility adapter until verified.
### Estimated effort
Large; split further before implementation.

## T-006 — Replace brittle source-text checks with executable architecture/projection tests
**Status:** TODO  
**Priority:** P1  
**Type:** HARDEN  
**Leverage:** HIGH
### Problem
F-004,F-005,F-010,F-013,F-014 show that tests can drift from semantic ownership/model stage.
### Evidence
Preflight waves and alias-expansion diagnostics.
### Goal
Assert semantic ownership, dependencies, raw contracts and compatibility projections.
### Scope
Protocol/BridgeCore/CommandBridge/HotkeyStudio/UI boundaries plus raw-v8/projection boundary.
### Non-goals
Remove no smoke gate before equivalent executable detection exists.
### Files / symbols
Test projects/workflows/contracts/`V8SecondaryAliasExpander`.
### Implementation
Compile/reflection/behavior tests plus deliberate negative test-of-tests fixtures.
### Invariants
2–5,7–10,14.
### Compatibility constraints
No production behavior change.
### Edge cases
Internal types, conditional NXOpen/stubs, alias scope projections.
### Tests
Negative boundary/model-stage violations must fail.
### Mutation tests
T-007 where pure.
### Benchmarks
N/A.
### Acceptance criteria
Valid file moves/alias projections pass; true semantic boundary violations fail.
### Verification commands
Targeted tests + CI.
### Dependencies
T-001.
### Blocks
T-005.
### Risk
Medium.
### Rollback
Retain old smoke until replacement proven.
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
Measure test ability to detect auth/state/validator/projection faults.
### Scope
Protocol/StateMachines first; pure config projection if stable.
### Non-goals
No blind NX UI/NXOpen mutation.
### Files / symbols
Security/normalization/state/validators/projection logic.
### Implementation
Stryker.NET pilot; classify survivors; informational before threshold.
### Invariants
4,5,8,13.
### Compatibility constraints
Bounded CI duration.
### Edge cases
Equivalent mutants/time-sensitive tests.
### Tests
Mutation run + survivor review.
### Mutation tests
This task establishes them.
### Benchmarks
Mutation job duration.
### Acceptance criteria
Measured score; no unexplained high-risk survivors.
### Verification commands
Recorded after tool discovery.
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
IPC/HFSM/security/config architecture.
### Goal
Pairwise + high-risk N-wise + property/fuzz/metamorphic coverage.
### Scope
Protocol auth/replay, inbox, HFSM, raw/profile projection, context guards.
### Non-goals
No brute-force live NX UI.
### Files / symbols
Critical test projects.
### Implementation
Dimension matrix → pairwise → N-wise → reproducible fuzz/property cases.
### Invariants
2–10,13.
### Compatibility constraints
Deterministic seeds/reproduction.
### Edge cases
Clock skew, reorder/duplicate, corrupt JSON, stale revision, permission mismatch, alias collision, modal/cancel.
### Tests
Matrix-driven suites.
### Mutation tests
Survivors guide missing observables.
### Benchmarks
Stress duration/allocations.
### Acceptance criteria
Documented matrix and executable high-risk cases.
### Verification commands
Targeted suites.
### Dependencies
T-007.
### Blocks
T-009.
### Risk
Medium.
### Rollback
Retain deterministic core if fuzz harness flakes.
### Estimated effort
Medium-large.

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
Pure hot paths, dependency/security scans, flaky observation.
### Non-goals
No live-NX performance claims from stubs.
### Files / symbols
CI/benchmark/test projects.
### Implementation
Measure first; conservative thresholds only after data.
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
Versioned baselines/targets.
### Verification commands
Baseline manifest.
### Dependencies
T-003,T-008.
### Blocks
T-010.
### Risk
Medium.
### Rollback
Unstable threshold becomes informational; measurement retained.
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
Repeat full architecture/correctness/security/concurrency/reliability/API/data/testing/mutation/performance/CI/docs/dependency audit until no fundamental delta.
### Scope
Whole repository.
### Non-goals
No cosmetic expansion without findings.
### Files / symbols
All.
### Implementation
Re-audit → findings → atomic deltas → verified iterations → repeat.
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
All baselines.
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

Characterization precedes behavior change. Raw config contracts are tested before `IJsonOnDeserialized` compatibility projection; projected runtime behavior is tested separately. Pure contracts get unit/property tests; Windows desktop/CommandBridge get integration/build checks; NXOpen stubs prove compile compatibility only; live NX remains explicit release evidence. Negative security tests are first class. Native command failures propagate immediately. Preflight branch is the verification sandbox; `main` is not.

# 13. Mutation Testing Strategy

Not configured initially. T-007 establishes measurement before threshold. Priority: HMAC/auth/replay → context guards → state transitions → raw/profile validators → alias projection → normalization → inbox. Survived mutants trigger observable-contract analysis, not coverage inflation.

# 14. Performance Baselines

Unknown initially. Candidate measurements: leader transition latency/allocations, normalization/parsing, alias projection cost, inbox admission throughput/bounded poll cost, profile load/validation, desktop cold-start/publish size. No live-NX latency claim from CI.

# 15. Security Hardening

Preserve HMAC, replay guard, explicit permissions, context/source-process constraints and bounded inbox. Raw identity uniqueness and alias projection fidelity are separate contracts; projected duplicate IDs must not broaden adapter/permission semantics. Add dependency/code scanning and mutation-resistant negative tests later. Never weaken validation to green CI.

# 16. Migration Strategy

Use `characterization → introduce boundary → dual compatibility → migrate callers → verify → remove legacy`. The v8 cleanup is an incomplete migration: restore only independent HFSM policy; do not resurrect K3–K5 sources. `secondary_aliases` remain canonical raw metadata and may be expanded in memory for legacy runtime compatibility. Optional generated locks are strictly derived from raw canonical v8.

# 17. Deferred Work

- Live NX verification requiring NX 2512 installation.
- Mutation/performance thresholds until measured.
- Cosmetic UI work unrelated to findings.
- Broad refactors until T-001 is green.

# 18. Rejected Decisions

- Restore deleted K3–K5/full-command-map merely to green CI — rejected.
- Skip failing validators — rejected.
- Add fake capability lock only to satisfy existence — rejected.
- Edit raw v8 IDs/remove secondary aliases to satisfy the stale post-deserialization uniqueness assertion — rejected; diagnostics prove raw IDs are already unique and aliases are intentional.
- Treat post-`IJsonOnDeserialized` `Operations.Count` as canonical source cardinality — rejected.
- Force/force-with-lease `main` — rejected.
- Treat NXOpen stubs as live NX proof — rejected.

# 19. Completed Tasks

None. T-001 remains `VERIFYING`; all preflight commits are diagnostic/verification history until a completely green current-v8 state is reconciled and pushed to `main`.

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
**Changes:** retired Sketch workflow; signing ownership checks; canonical capability validator/optional-lock strictness; nullable Protocol contract.  
**Tests:** `a5f9a718...`: current validator PASS; runtime source hardening PASS; strict Protocol/BridgeCore PASS; DFA/HFSM PASS; HotkeyStudio FAIL on identity assertion; Desktop UI FAIL on obsolete 10-row smoke assertion.  
**Plan changes:** added F-013/F-014.  
**Commit:** `a5f9a718fce9dd2db9fdcd3ced56770b4f025378`  
**Push:** audit branch only.  
**Result:** FAIL → reconciled.

## Iteration 1 — Wave C diagnostics
**Task:** T-001  
**Findings addressed:** F-014  
**Unexpected findings:** none; F-014 root cause materially revised by evidence.  
**Changes:** strengthened raw validator diagnostics; added temporary C# identity instrumentation; compared raw source and post-deserialization model.  
**Tests:** Node validator repeatedly reports 439 unique raw operations / 131 executable. C# diagnostics report 500 projected rows / 439 IDs after `JsonSerializer.Deserialize<Config>`. Inspection proves `V8SecondaryAliasExpander : IJsonOnDeserialized` intentionally clones `secondary_aliases` into compatibility routes.  
**Plan changes:** F-014 reclassified from High data-model defect to Medium test/data-contract defect; raw-profile edits rejected; T-001 now targets projection-integrity regression.  
**Commits:** `248b5f77...`, `8d099265...`, `98080045...`, `cfc3950f...` plus this planning reconciliation.  
**Push:** audit branch only.  
**Result:** DIAGNOSIS PASS → implementation next.

# 21. Definition of Final Done

- no known Critical/High findings;
- all P0/P1 DONE or evidence-backed rejected/deferred;
- applicable CI consistently green with no masked native failures;
- raw canonical operation identity unique/enforced and runtime alias projection fidelity tested separately;
- important architecture rules technically protected where feasible;
- critical security/state logic mutation-tested with no unexplained high-risk survivors;
- multidimensional critical edge space covered;
- performance/security/reliability baselines recorded and acceptable;
- no unexplained flaky tests;
- docs match current v8 code and do not require absent production assets;
- live-NX-only claims have explicit verification records;
- repeat audit finds no fundamental delta;
- `MASTER_PLAN.md`, code/tests and last verified `main` SHA agree.
