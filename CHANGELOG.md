# Changelog

Значимые изменения NXKeys фиксируются по датам и commit-level milestones. Подтверждённой публичной release numbering scheme пока нет.

## Unreleased — 2026-08-10

### Runtime v8

- current profile schema закреплена на **8**;
- default installer/runtime profile — `config/nx2512-v8-profile.json`;
- no-profile path строит hardcoded v8 fallback и валидируется CI;
- `secondary_aliases` участвуют в реальном runtime routing;
- workspace-only keys не проецируются как root commands без explicit workspace state;
- Modeling использует `M` как Manage root; regression path `M → L → S`;
- adaptive resolver сначала использует exact NX application mapping, затем label heuristics;
- CapsLock получил physical key latch против keyboard autorepeat.

### Sketch

- frequent commands в active Sketch используют короткий v8 routing (`L`, `R`, `C`, `A`, `T` и др.);
- constraints доступны через `K → …`, включая `K → C` Coincident;
- dimensions доступны через `D → …`, включая `D → Q` Rapid Dimension;
- variants сохраняются в `C → V → …`;
- hardcoded fallback проецирует constraint overlay внутрь Sketch вместо недостижимого отдельного module.

### Sheet Metal

- runtime нормализован на реальные NX2512 IDs `UG_APP_SBSM` и `UG_SBSM_*`;
- legacy `UG_APP_SHEETMETAL` / `UG_SHEET_METAL_*` поддерживаются compatibility mapping;
- security permission canonicalization синхронизирована с runtime;
- v8 availability applications участвуют в switch permissions.

### Selection Intent

Добавлен in-process механизм быстрого выбора:

```text
0 reset
1 single
2 connected / chain
3 tangent
4 inferred path / region boundary
```

- handler работает только в NX foreground;
- сохраняет modifier/text-input/collector guards;
- использует native NX chain/path/boundary/tangent controls;
- при seed selection может расширять выбор через `ScRuleFactory` compatibility reflection;
- physical key latch защищает от autorepeat.

### Security / IPC

- current IPC schema — **4**;
- requests используют ephemeral launch capability, HMAC-SHA-256, `client_instance_id`, nonce и monotonic sequence;
- Bridge проверяет source process, anti-replay state, profile digest/permission и current context;
- unknown actions, unsigned/wrong-session requests и permission mismatch отклоняются fail-closed;
- selection fingerprint участвует в context/dispatch protection;
- file queue остаётся transport layer, а не authority.

### CI / tests

- добавлены regressions v8 aliases, workspace-root collision и Sheet Metal security canonicalization;
- CI валидирует no-profile hardcoded fallback;
- Command Bridge contract build включает Selection Intent implementation;
- documentation workflow исправлен: stale check использует exit code `git diff --quiet`, line-ending-only generated diff не создаёт false failure.

### Documentation

- создан канонический [v8 runtime contract](docs/RUNTIME_V8.md);
- создан отдельный [Selection Intent guide](docs/SELECTION_INTENT.md);
- README, docs index, cheatsheet, installation, configuration, CLI, architecture, API, safety, operations и troubleshooting переведены на current v8 model;
- HotkeyStudio, Command Bridge и Control Center READMEs обновлены;
- K1–K5 / generated K3–K5 pipeline явно классифицирован как legacy/catalog analysis, а не default runtime;
- documentation validator извлекает profile schema, IPC schema и sequence policy из source code и проверяет current docs;
- validator запрещает возврат старых Sketch line examples в current user-facing guides.

### Known live-NX limits

- CI/contract stubs не подтверждают фактическую command sensitivity/license/role;
- Selection Intent требует smoke test в реальных NX collectors;
- interactive commands требуют live-NX проверки: `DialogTester.InvokeMenuButtonAction(...)` return value не является универсальным доказательством фактического открытия dialog/collector;
- Bridge DLL требует полного restart NX для обновления.

## 2026-08-03

### Changed

- внедрена отдельная семантическая грамматика Sketch;
- историческая версия использовала пути `CGL`, `CGR`, `CGC`, `CGA`, `EGT`, `EGE`, `TGO` и ветвь `CGV…`;
- legacy positional aliases удалялись, user-locked paths сохранялись;
- добавлены Sketch regression tests и workflow.

> Эти paths зафиксированы как исторический milestone. Current user grammar описана в `docs/SKETCH_INTENT_LANGUAGE.md` и `docs/RUNTIME_V8.md`.

## 2026-07-30

### Changed

- source sequence policy обновлена до v7;
- в universal selection policy добавлены Select All (`SA`) и Deselect All (`SN`);
- добавлена модель `path_locked` / `path_source`;
- создана единая карта кодовой базы и расширен documentation set.

## 2026-07-29

### Changed

- installer принимал generated profiles до schema 6;
- legacy main runtime scope тогда был закреплён как K3–K5 / 885 intents;
- source catalog сохранён как K1–K5;
- deployment переведён на managed package, manifest, health-check и rollback;
- Bridge получил contract build против NXOpen stubs.

## До 2026-07-29

### Architecture

- проект переведён на C#/.NET 8;
- реализованы HotkeyStudio, Command Bridge, Control Center и Catalog Studio;
- protocol вынесен в `NXKeys.Protocol`;
- DFA/HFSM — в `NXKeys.StateMachines`;
- добавлена file queue `pending → processing → completed|failed`;
- добавлены backups, SHA-256 verification и managed launcher.

## Правила ведения

- Не добавлять release number без подтверждённого release/плана.
- Generated timestamp сам по себе не является changelog событием.
- Profile/IPC schema, sequence policy, Sketch grammar, Selection Intent и deployment contract должны отражаться здесь.
- Historical entries не переписываются как будто они были current v8; при необходимости добавляется явное пояснение.
- Breaking change должен иметь migration/rollback notes.
