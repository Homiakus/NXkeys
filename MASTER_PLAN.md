# 1. Mission

Сделать NXkeys устойчивой, проверяемой и эволюционирующей системой, где `MASTER_PLAN.md` — единственный living execution plan, а существенные изменения проходят цикл `AUDIT → PLAN → SELECT → IMPLEMENT → TEST → REVIEW → RECONCILE → COMMIT → VERIFIED PUSH → COMPRESS`.

Текущий приоритет: завершить governance/baseline Phase 0 без возврата удалённого K3–K5/full-command-map контура и без маскировки внешних инфраструктурных ошибок.

# 2. Current State

- C#/.NET 8, Windows x64, Siemens NX / Designcenter NX 2512.
- Canonical profile: `config/nx2512-v8-profile.json`, schema 8; 439 raw operations / 439 unique `operation_id`; runtime projection = 500 rows / 439 semantic IDs.
- Independent state policy: `config/nx2512-state-machines.json`.
- IPC schema 4: HMAC, replay guard, permission/context/source-process binding.
- Core boundaries: HotkeyStudio → Protocol/StateMachines → BridgeCore → CommandBridge/NXOpen; ControlCenter и NxEskd — separate surfaces.
- Original baseline `2359e85c02313f4c6227a5e9a38ef62e9b9c043f`: 5/5 push workflows red.
- Clean current-v8 core baseline entered `main` as `2b28da717d4325b767c240851d487ae7717a3cb0`; `ci`, Documentation, Desktop UI и Runtime hardening PASS.
- F-017 Pages path fix entered `main` as `6e1882bd8f6b61d0f67d194f96df55c245ee700f`.
- F-018 Pages dependency-contract fix entered `main` as `8425ec3739e68782dc204e94337bb5f0f6915048`.
- On `8425ec373...`: Pages run `33266648791` passes validator + static website preparation, then fails only at `actions/configure-pages@v6` because repository Pages is not enabled/configured for GitHub Actions.
- T-002 is formally closed on `main` `efc5836fac6f7228ecee01d312d805523b596fca`, tree `01c3a3d5e44dfbc9c60ccd00cc3558d38b087260`; post-push Documentation `33267519645` PASS and full `ci` `33267519676` PASS.
- T-003 preflight branch: `audit/t003-verified-baseline-20260829`; selected design is a deterministic historical snapshot whose evidence entries carry their own `subject_commit_sha`, evidence kind and environment class rather than pretending every workflow ran on the latest SHA.
- Live NX 2512 remains an external verification layer; NXOpen stubs are compile-contract evidence only.

# 3. Architecture Map

```text
Keyboard / CapsLock
        ↓
NX2512_HotkeyStudio
  Config(raw v8) → V8SecondaryAliasExpander → runtime projection
  NxCommandBridgeClient → NxRequestSigningPolicy
        ↓ authenticated file IPC
NXKeys.Protocol ← NXKeys.StateMachines
        ↓
NXKeys.BridgeCore (bounded background admission)
        ↓
NX2512_CommandBridge
  SelectionIntentHotkeys → NxSelectionExecutor
        ↓
NXOpen / NX UI / NxEskd capabilities
```

Verification ownership: `MASTER_PLAN.md` describes intent/state; `verification/baseline.json` records immutable evidence snapshots; validators enforce structure but do not invent freshness or live-NX claims.

# 4. Baseline

| Check | Original main | Current evidence |
|---|---|---|
| Core/current-v8 CI | red/blocked | PASS on `efc5836f...`, run `33267519676` |
| Documentation | red/blocked | PASS on `efc5836f...`, run `33267519645` |
| Desktop UI | red | PASS on verified ancestor `2b28da717...`, run `33265565482` |
| Runtime hardening | red | PASS on verified ancestor `2b28da717...`, run `33265565481` |
| Raw v8 identity | not explicit | PASS: 439/439 unique |
| Alias projection fidelity | conflated | PASS: 500 rows / 439 semantic IDs |
| DFA/HFSM | missing policy | PASS |
| NxEskd Core / Configurator | blocked | PASS 55/55 + 27/27 |
| Desktop build/publish | blocked | PASS |
| NXOpen stubs + CommandBridge compile | blocked | PASS in current full `ci` |
| Adaptive/deployment invariants | blocked | PASS in current full `ci` |
| Pages static package | red legacy assertions | PASS step on `8425ec373...`, run `33266648791` |
| Pages configure/deploy | not reached | BLOCKED_EXTERNAL: F-019 |
| Live NX | NOT RUN | external requirement |
| Mutation/performance baselines | absent | T-007 / T-009 |

# 5. System Invariants

1. `main` never knowingly moves to a worse verified code state.
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
12. Unexpected findings are reconciled before implementation scope expands.
13. Tests and observed behavior outrank roadmap assumptions.
14. CI asserts semantic ownership/contracts, not incidental source locations or deleted scaffolding.
15. Deleted K3–K5/legacy assets are never recreated merely to satisfy stale checks.
16. Pages asserts HTML dependencies only for files actually consumed by the page.
17. External infrastructure blockers remain explicit; they are never converted to `continue-on-error`, skipped gates or fake PASS states.
18. Living-plan structure and controlled vocabularies are executable CI contracts, including negative self-tests.
19. Machine-readable verification records are immutable historical snapshots: every evidence item identifies its subject commit and proof class; no manifest may imply live-NX or same-SHA verification that did not occur.

# 6. Findings Registry

## F-001 — Main had no trustworthy green baseline
**Status:** Addressed in code; external Pages closure remains | **Severity:** Critical | **Category:** Reliability/CI | **Confidence:** Confirmed

## F-002 — Active CI depended on removed K3–K5/full-command-map assets
**Status:** Addressed by T-001; residual cleanup T-004 | **Severity:** High | **Category:** CI/Migration | **Confidence:** Confirmed

## F-003 — Independent state-machine policy was accidentally removed
**Status:** Addressed by T-001 | **Severity:** High | **Category:** Correctness/Packaging | **Confidence:** Confirmed

## F-004 — Runtime hardening checked transport result in wrong file
**Status:** Addressed; structural replacement T-006 | **Severity:** Medium | **Category:** Testing/Architecture | **Confidence:** Confirmed

## F-005 — Selection validator assumed effect remained in `Program.cs`
**Status:** Addressed; structural replacement T-006 | **Severity:** Medium | **Category:** Testing/Architecture | **Confidence:** Confirmed

## F-006 — Orphan `.claude` worktree gitlink
**Status:** Addressed by T-001 | **Severity:** Medium | **Category:** Repository Integrity | **Confidence:** Confirmed

## F-007 — Documentation still contains retired K3–K5 narrative
**Status:** Open | **Severity:** Medium | **Category:** Documentation/Migration | **Confidence:** Strong

## F-008 — Critical pure logic has no mutation baseline
**Status:** Open | **Severity:** Medium | **Category:** Test-of-tests | **Confidence:** Strong

## F-009 — `Sketch intent grammar` workflow was retired K3–K5 pipeline
**Status:** Addressed by T-001 | **Severity:** High | **Category:** CI/Migration | **Confidence:** Confirmed

## F-010 — Signing assertion ignored `NxRequestSigningPolicy` extraction
**Status:** Addressed; structural replacement T-006 | **Severity:** Medium | **Category:** Testing/Architecture | **Confidence:** Confirmed

## F-011 — Capability-lock test and validator disagreed about optionality
**Status:** Addressed by T-001 | **Severity:** Medium | **Category:** Testing/Contract | **Confidence:** Confirmed

## F-012 — Strict Protocol build exposed nullable contract mismatch
**Status:** Addressed by T-001 | **Severity:** Medium | **Category:** Correctness/Static analysis | **Confidence:** Confirmed

## F-013 — Desktop UI gate hardcoded obsolete 10-row HUD contract
**Status:** Addressed; stronger guard T-006 | **Severity:** Medium | **Category:** Testing/UI Contract | **Confidence:** Confirmed

## F-014 — Identity regression confused raw operations with alias projections
**Status:** Addressed by T-001 | **Severity:** Medium | **Category:** Testing/Data Contract | **Confidence:** Confirmed

## F-015 — Adaptive CI required exclusion for deleted `ConfigModels.cs`
**Status:** Addressed; stronger guard T-006 | **Severity:** Medium | **Category:** Testing/Migration | **Confidence:** Confirmed

## F-016 — Selection Intent CI checked executor semantics in keyboard-hook file
**Status:** Addressed; stronger guard T-006 | **Severity:** Medium | **Category:** Testing/Architecture | **Confidence:** Confirmed

## F-017 — Pages workflow published removed hybrid profile
**Status:** Addressed on main | **Severity:** High | **Category:** CI/Documentation/Migration | **Confidence:** Confirmed

## F-018 — Pages required HTML reference to independent state-machine policy
**Status:** Addressed on main | **Severity:** Medium | **Category:** CI/Documentation Contract | **Confidence:** Confirmed

## F-019 — Repository-level GitHub Pages is not enabled for Actions deployment
**Status:** BLOCKED_EXTERNAL | **Severity:** Medium | **Category:** Infrastructure/Deployment | **Confidence:** Confirmed
**Evidence:** main Pages run `33266648791`, build job `99137769071`: validator PASS; `Prepare static website` PASS; `Configure GitHub Pages` fails `Get Pages site failed ... Not Found`; deploy is skipped.
**Root cause:** repository has no Pages site configured to build from GitHub Actions.
**Constraint:** `actions/configure-pages@v6` auto-enablement requires a token other than built-in `GITHUB_TOKEN` with repository/Pages administration rights; the connected GitHub tool exposes no Pages-setting write action.
**Required external action:** Repository → Settings → Pages → Build and deployment → Source = `GitHub Actions`, then rerun Pages workflow.
**Non-solution:** do not hide this with `continue-on-error`, disable the workflow, or add `enablement:true` with an incapable `GITHUB_TOKEN`.

## F-020 — Documentation gate pinned deprecated Node-backed actions
**Status:** Addressed by T-002 | **Severity:** Low | **Category:** CI/Maintenance | **Confidence:** Confirmed
**Evidence:** T-002 moved Documentation to `checkout@v7`, `setup-node@v7` and Node 24; post-closure main run `33267519645` PASS.

# 7. Risk Register

| Risk | P | Impact | Mitigation |
|---|---:|---:|---|
| Pages remains unavailable until one-time repo setting | High | Medium | F-019 explicit external unblock; no masking |
| evidence snapshot accidentally overclaims freshness | Medium | High | per-evidence subject SHA/kind/scope + strict validator |
| source-text CI drifts after refactor | High | Medium | T-006 executable architecture tests |
| alias projection broadens semantics | Medium | High | raw uniqueness + projection fidelity |
| NXOpen stubs differ from NX 2512 | Medium | High | separate stub/live evidence classes |
| mutation suite noisy/slow | Medium | Medium | targeted pure projects first |

# 8. Pareto Improvements

1. Capture a machine-readable verification baseline with explicit evidence classes.
2. Remove residual K3–K5 migration debt.
3. Replace source-location smoke with executable architecture contracts.
4. Mutation-test critical pure logic.
5. Expand multidimensional edge coverage before broader refactors.

# 9. Dependency DAG

```text
T-001 code baseline verified ─┬─ T-002 plan gate DONE
                              ├─ T-003 baseline manifest IN_PROGRESS
                              ├─ T-004 legacy cleanup
                              ├─ T-006 architecture tests ─ T-005 boundary adoption
                              └─ T-007 mutation ─ T-008 edge-space
F-019 external Pages setting ─┘ (required only for full T-001 deployment closure)
T-003 + T-005 + T-008 ─ T-009 measured hardening ─ T-010 convergence audit
```

# 10. Implementation Phases

- Phase 0: T-001..T-003 — recover trust/governance; T-002 done, F-019 is an explicit external T-001 sub-blocker, T-003 in progress.
- Phase 1: T-004 — delete migration debt.
- Phase 2: T-006 → T-005 — protect and finish semantic boundaries.
- Phase 3: T-007..T-008 — test-of-tests and multidimensional edge coverage.
- Phase 4: T-009 — measured security/performance/reliability.
- Phase 5: T-010 — convergence re-audit.

# 11. Atomic Tasks

## T-001 — Reconcile v8 migration fallout and restore trustworthy baseline
**Status:** BLOCKED | **Priority:** P0 | **Type:** FIX | **Leverage:** HIGH
**State:** code/current-v8 baseline and static Pages package are verified; final Pages configure/deploy is blocked only by F-019 repository setting.
**Acceptance remaining:** enable Pages source = GitHub Actions and obtain successful Pages build+deploy on a verified main SHA.
**Blocks:** no governance-only work; F-019 still blocks full deployment closure.

## T-002 — Enforce MASTER_PLAN structure as executable gate
**Status:** DONE | **Priority:** P1 | **Type:** HARDEN | **Leverage:** HIGH
**Implementation:** executable living-plan validator with negative self-tests, CRLF/LF normalization and current Node-backed actions.
**Evidence:** formal closure main `efc5836fac6f7228ecee01d312d805523b596fca`; Documentation `33267519645` PASS and full `ci` `33267519676` PASS.

## T-003 — Capture machine-readable verified baseline
**Status:** IN_PROGRESS | **Priority:** P1 | **Type:** HARDEN | **Leverage:** HIGH
**Design:** add `verification/baseline.json` schema 1 as a deterministic historical snapshot. Baseline identifies a verified main commit/tree. Each evidence item has a unique ID, evidence kind, result, `subject_commit_sha`, environment class and precise workflow/run/job/step coordinates where applicable. Historical Desktop/Runtime evidence remains attached to its actual ancestor SHA. Pages static preparation is recorded as a successful workflow step rather than falsely marking the failed Pages workflow green. F-019 is an explicit external blocker. NXOpen stub compile and live NX 2512 are separate claim-boundary evidence classes.
**Validator:** pure Node, fail-fast, no network dependency; exact repository/schema/branch, SHA formats, unique evidence IDs, enums, required coordinates, external blocker linkage, required core evidence and stub-vs-live claim boundary. `--self-test` must reject malformed SHA, duplicate evidence ID, blocked evidence without finding, missing required evidence and a false live-NX PASS.
**Acceptance:** manifest + validator + Documentation integration PASS; full `ci` PASS; no runtime code change; clean non-force main integration.

## T-004 — Delete retired K3–K5 and migration debt
**Status:** TODO | **Priority:** P1 | **Type:** REMOVE | **Leverage:** HIGH
Usage-search legacy docs/scripts/assertions → classify → delete/update → full verification. Preserve only current v8 helpers.

## T-005 — Finish remaining module-boundary adoption without semantic drift
**Status:** TODO | **Priority:** P1 | **Type:** IMPROVE | **Leverage:** HIGH
Characterize → interface → compatibility → migrate → verify → remove legacy. Preserve IPC/live-NX behavior. Depends on T-006.

## T-006 — Replace brittle source-text checks with executable architecture/projection tests
**Status:** TODO | **Priority:** P1 | **Type:** HARDEN | **Leverage:** HIGH
Replace location-sensitive smoke exposed by F-004/F-005/F-010/F-013..F-018 with compile/reflection/behavior/negative-fixture contracts. NXOpen stubs remain compile-only evidence.

## T-007 — Mutation-test critical pure logic
**Status:** TODO | **Priority:** P2 | **Type:** HARDEN | **Leverage:** HIGH
Stryker.NET pilot on Protocol/StateMachines, then projection/validators. Measure before setting threshold.

## T-008 — Expand multidimensional edge-case coverage
**Status:** TODO | **Priority:** P2 | **Type:** HARDEN | **Leverage:** HIGH
Dimensions: input × state × concurrency × timing × failure × permissions × config × external state. Pairwise + high-risk N-wise + reproducible property/fuzz/metamorphic cases.

## T-009 — Establish performance, security and reliability baselines
**Status:** TODO | **Priority:** P2 | **Type:** HARDEN | **Leverage:** MEDIUM
Measure leader transitions, normalization/parsing, alias projection, inbox admission, profile load, desktop cold start/publish size, warnings and flaky behavior. No live-NX claim from CI.

## T-010 — Final convergence re-audit and delta implementation
**Status:** TODO | **Priority:** P2 | **Type:** IMPROVE | **Leverage:** HIGH
Repeat architecture/correctness/security/concurrency/reliability/API/data/testing/mutation/performance/CI/docs/dependency audit until no fundamental delta remains.

# 12. Testing Strategy

Characterization precedes behavior change. Raw config is tested before projection; projected behavior separately. Pure contracts use unit/property tests; Windows desktop/CommandBridge use integration/build checks. NXOpen stubs prove compile compatibility only. Native failures propagate immediately. Preflight branches are verification sandboxes; `main` receives only verified trees.

# 13. Mutation Testing Strategy

T-007 measures before gating. Priority: HMAC/auth/replay → context guards → state transitions → raw/profile validators → alias projection → normalization → inbox. Surviving mutants trigger observable-contract analysis, not coverage inflation.

# 14. Performance Baselines

Initially unknown. Measure leader transition latency/allocations, normalization/parsing, projection, inbox admission/bounded poll, profile load/validation, desktop cold start and publish size. Live-NX latency requires live NX.

# 15. Security Hardening

Preserve HMAC, replay guard, permissions, context/source-process binding and bounded inbox. Aliases must not broaden adapter/security semantics. Never weaken validation merely to green CI.

# 16. Migration Strategy

`characterization → introduce boundary → dual compatibility → migrate callers → verify → remove legacy`. Removal includes obsolete files and stale scaffolding assertions. Restore independent HFSM policy only, never retired K3–K5 maps.

# 17. Deferred Work

- Live NX 2512 verification requiring installed NX.
- Mutation/performance thresholds until measured.
- Cosmetic UI work unrelated to findings.
- Repository Pages enablement until an authorized settings surface is available; tracked as F-019, not silently ignored.

# 18. Rejected Decisions

- Restore K3–K5/full-command-map/hybrid profile to green CI — rejected: dual source of truth.
- Skip or `continue-on-error` failing validators/deployment — rejected.
- Add fake capability lock or fake HTML dependency — rejected.
- Delete valid secondary aliases/edit raw IDs to satisfy projected uniqueness — rejected.
- Re-add dummy `Compile Remove` for deleted files — rejected.
- Put NX Selection Intent literals back into keyboard hook merely to satisfy CI — rejected.
- `configure-pages enablement:true` with built-in `GITHUB_TOKEN` — rejected: documented incapable token.
- Make the baseline manifest self-referential to the commit containing itself — rejected: non-deterministic circular provenance.
- Collapse NXOpen stubs and live NX into one `verified` flag — rejected: materially different proof classes.
- Force/force-with-lease `main` — rejected.

# 19. Completed Tasks

- T-002 — executable living-plan contract; formal closure main `efc5836fac6f7228ecee01d312d805523b596fca`; Documentation and full `ci` PASS.
- T-001 remains code-verified but externally blocked by F-019.

# 20. Iteration Log

## Iteration 1 — Baseline recovery Waves A–E
T-001 restored independent HFSM policy, removed obsolete workflows/orphan gitlink, reconciled ownership-sensitive gates, separated raw identity from alias projection, fixed nullable/HUD/contracts, and reached full core `ci` PASS. Verified core tree entered main as `2b28da717...`.

## Iteration 1 — Pages F-017/F-018
F-017 replaced removed hybrid profile with canonical v8 assets. F-018 removed the false HTML dependency on state-policy while preserving policy packaging/existence checks. Main `8425ec373...` passes Pages static preparation; next failure moved to repository-level Pages configuration.

## Iteration 1 — F-019 external boundary
Main run `33266648791` proves code-side Pages package PASS and repository Pages setup missing. No code workaround can honestly satisfy this with current token/tool permissions. T-001 marked BLOCKED only for external deployment closure; governance work continues.

## Iteration 2 — T-002 executable plan gate
Added pure Node plan validator and Documentation integration; fixed CRLF parsing and section-scoped self-test mutations; upgraded Documentation actions to current Node-backed majors. Formal closure main `efc5836fac6f7228ecee01d312d805523b596fca`: Documentation `33267519645` PASS and full `ci` `33267519676` PASS. Result: DONE.

## Iteration 3 — T-003 verified baseline manifest
Selected immutable historical evidence model: baseline snapshot + per-evidence subject SHA/kind/environment; step-level Pages proof; explicit F-019 blocker; separate NXOpen-stub and live-NX claim classes. Result: IN_PROGRESS.

# 21. Definition of Final Done

- no known unowned Critical/High findings;
- P0/P1 tasks DONE or explicitly evidence-backed BLOCKED/DEFERRED;
- applicable code CI green with no masked native failures;
- external blockers separated from code defects and tracked explicitly;
- raw canonical identity and runtime alias fidelity enforced separately;
- architecture rules technically protected where feasible;
- machine-readable evidence never overclaims subject SHA, environment or live-NX verification;
- critical security/state logic mutation-tested with no unexplained high-risk survivors;
- multidimensional critical edge space covered;
- performance/security/reliability baselines recorded;
- no unexplained flaky tests;
- docs match current v8 and no active gate depends on absent legacy assets or stale source ownership;
- live-NX-only claims have explicit verification records;
- repeat audit finds no fundamental delta;
- `MASTER_PLAN.md`, code/tests and last verified baseline evidence agree.
