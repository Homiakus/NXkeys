# ADR-0003: V8 runtime profile как default, K1–K5 pipeline как catalog/compatibility layer

- Status: Accepted
- Date: 2026-08-10
- Supersedes: ADR-0001 в части default/generated/installed runtime profile

## Context

ADR-0001 фиксировал архитектуру, в которой generated K3–K5 profile с 885 intents считался ежедневным runtime и устанавливался под compatibility filename `nx2512-pro-hybrid.json`.

После развития v8 это перестало соответствовать исполняемому коду:

- `Config.CurrentSchemaVersion = 8`;
- versioned v8 operation model хранится в `config/nx2512-v8-profile.json`;
- `install-nxkeys.ps1` без `-ConfigPath` выбирает именно этот файл;
- staging/managed installation сохраняет его как `nx2512-v8-profile.json`;
- HotkeyStudio auto-resolution отдаёт этому имени приоритет;
- при отсутствии JSON runtime создаёт hardcoded v8 fallback;
- v8 paths поддерживают `secondary_aliases`, one-token Sketch commands и отдельную workspace-key semantics, которые не описываются старым generated K3–K5 contract.

При этом полный K1–K5 catalog и compiler остаются ценными для coverage, target-catalog resolution и исторической трассировки.

## Decision

Разделить **current runtime source** и **legacy/catalog generation pipeline**.

### Current runtime source

```text
config/nx2512-v8-profile.json
```

Текущий profile schema:

```text
8
```

Installed canonical filename:

```text
%LOCALAPPDATA%\NXKeys\managed\NX2512.6000\nx2512-v8-profile.json
```

HotkeyStudio должен считать это имя первым кандидатом auto-resolution.

### Compatibility/fallback

- source schemas `3…7` могут читаться через compatibility/migration path;
- installer принимает legacy generated profile только при выполнении его K3–K5/885 invariants;
- `nx2512-pro-hybrid.json` остаётся compatibility filename/candidate, но не default current runtime source;
- отсутствие JSON допускает hardcoded v8 fallback, проверяемый CI.

### Legacy/catalog layer

```text
config/full-command-map/
config/nx2512-pro-hybrid.json
config/nx2512-pro-main.generated.json
scripts/compile-main-command-map.mjs
```

Этот слой используется для:

- полного K1–K5 intent inventory;
- frequency/coverage analysis;
- target `BUTTON ID` resolution;
- generated reports;
- migration/compatibility исследований.

Он не должен называться default runtime profile в current user/operations документации.

## Consequences

### Положительные

- documentation/install/runtime используют один versioned v8 source;
- v8 routing semantics больше не маскируются legacy K3–K5 metadata;
- current profile можно проверять независимо от полного compiler pipeline;
- Catalog Studio evidence продолжает использоваться без привязки к старой модели runtime;
- legacy artifacts сохраняют историческую ценность и traceability.

### Издержки

- репозиторий содержит два разных понятия «profile»: current v8 runtime и legacy generated/catalog profile;
- старые документы/ADR требуют явной маркировки;
- часть installer strings всё ещё может содержать историческое «K3–K5», хотя фактический source уже v8;
- tooling должно явно различать current runtime metrics и legacy 885-intent coverage.

## Compatibility rules

1. Новая user documentation показывает `nx2512-v8-profile.json`.
2. Legacy generated profile не удаляется без отдельной migration ADR.
3. K1–K5 counts не используются как размер v8 operation set.
4. Schema/version documentation получает значения из source code/validators.
5. `-CompileOnly` на default v8 profile не описывается как запуск K3–K5 compiler.
6. Explicit legacy generation выполняется `scripts/compile-main-command-map.mjs`.

## Verification

- installer source содержит default `nx2512-v8-profile.json`;
- HotkeyStudio `Program.cs` ищет v8 filename первым;
- CI валидирует schema-8 profile;
- CI валидирует hardcoded/no-profile fallback;
- HotkeyStudio tests проверяют aliases/workspace/root routing;
- documentation validator извлекает current schema/policy из source;
- current README/installation/operations не используют generated K3–K5 как default runtime.

## Rollback

При необходимости временно использовать legacy generated profile его можно передать явно через `-ConfigPath`, если он проходит supported schema и installer compatibility checks. Это compatibility path, а не изменение принятого default architecture.
