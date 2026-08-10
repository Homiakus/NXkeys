# NX Pro Hybrid — legacy/catalog profile specification

> **Статус: compatibility / catalog-generation specification.** Этот документ описывает K1–K5 intent catalog и generated K3–K5 pipeline. Он **не** определяет current NXKeys v8 runtime. Для текущего поведения используйте [RUNTIME_V8.md](RUNTIME_V8.md) и [CONFIGURATION.md](CONFIGURATION.md).

## Почему документ сохраняется

Репозиторий всё ещё использует этот слой для:

- полного K1–K5 intent inventory;
- frequency/coverage analysis;
- resolution against NX UI catalog;
- generated K3–K5 compatibility profile;
- исторической трассировки command IDs и paths;
- regression checks отдельных legacy contracts.

Current installer без `-ConfigPath` выбирает `config/nx2512-v8-profile.json`, поэтому generated hybrid profile больше нельзя называть «основным runtime profile» без уточнения.

## Термины legacy pipeline

| Термин | Значение |
|---|---|
| intent catalog | 1169 source records K1–K5 в `config/full-command-map/` |
| bootstrap profile | `config/nx2512-pro-hybrid.json`, legacy schema 6 source |
| generated main profile | legacy K3–K5 output с 885 selected intents |
| generated compatibility profile | `config/nx2512-pro-main.generated.json` |
| current runtime profile | **не этот pipeline**; `config/nx2512-v8-profile.json`, schema 8 |

## Исторические contracts этого слоя

Legacy generation использовал:

```text
profile schema 6
K3/K4/K5 selected intents = 885
frequency policy K5<=2, K4<=3, K3<=4
full source intents = 1169 K1–K5
```

Current global contracts теперь:

```text
profile schema 8
minimum readable profile 3
IPC schema 4
sequence policy v8
```

Не переносите номера schema/policy из legacy pipeline в current runtime documentation.

## Full intent catalog

Каждая source record должна сохранять:

- stable `intent_id`;
- section/group;
- frequency K1–K5;
- English/Russian names;
- target module;
- path hint;
- traceability к source inventory.

Historical checked contract:

```text
K1=4
K2=280
K3=445
K4=371
K5=69
total=1169
```

Эти counts относятся к full intent catalog, а не к числу v8 runtime operations.

## Legacy generated K3–K5 scope

Selected legacy scope:

```text
K3 + K4 + K5 = 885 unique intents
```

Global duplication может создавать больше serialized module rows. Coverage измеряется unique `catalog_refs`, а не row count.

## Generation

Explicit compiler invocation:

```powershell
node .\scripts\compile-main-command-map.mjs `
  --profile .\config\nx2512-pro-hybrid.json `
  --intents .\config\full-command-map `
  --probe .\docs\audit\runtime-command-probe-2026-07-28.json `
  --out .\config\nx2512-pro-main.generated.json `
  --report .\docs\generated\main-profile-resolution.md
```

`install-nxkeys.ps1 -CompileOnly` на **default v8 profile** не запускает этот compiler; он только выбирает/проверяет v8 profile и завершает работу.

## Resolution rules

| Status | Смысл | Legacy generated row enabled |
|---|---|---:|
| `existing` | exact trusted ID | да |
| `resolved` | достаточно уверенное target-catalog match | да |
| `ambiguous` | несколько candidates | нет |
| `unresolved` | exact ID не найден | нет |

Similarity score/API candidate не заменяет exact UI command ID.

## Legacy module grid

Исторический pipeline использует 14 module IDs и hidden internal prefixes. User никогда не должен вводить hidden prefix вручную.

Эта часть остаётся полезной для command tree/coverage, но current v8 paths могут быть однотокенными и не обязаны следовать старой сетке 2–5 tokens.

## Что из legacy pipeline больше нельзя переносить в current v8 docs

Нельзя как current truth утверждать, что:

- default runtime profile — generated K3–K5;
- installed profile называется `nx2512-pro-hybrid.json`;
- profile schema current = 6;
- IPC current = 3;
- sequence policy current = 7;
- все user paths имеют минимум два tokens;
- старые Sketch paths `CGL`, `C→L`, `C→G→L` остаются current user grammar.

## Что остаётся полезным

- source command inventory;
- frequency metadata;
- candidate resolution evidence;
- historical IDs;
- coverage reports;
- audit reproducibility;
- exact command catalog comparisons after NX role/version changes.

## Current v8 relation

V8 operation profile имеет другой source contract:

```text
operation_id
command_name
paths.direct
paths.workspace_key
paths.leader
paths.secondary_aliases
adapter.*
availability.*
```

`secondary_aliases` участвуют в runtime routing. Workspace-only keys не должны становиться root terminals без explicit workspace state.

Sheet Metal current canonical IDs: `UG_APP_SBSM` / `UG_SBSM_*`.

Sketch current grammar: one-token frequent commands + `K→…` constraints + `D→…` dimensions.

## Validation legacy artifacts

```powershell
node .\scripts\validate-full-command-map.mjs
node .\scripts\validate-main-command-map.mjs
node .\scripts\validate-command-tree.mjs
node .\scripts\audit-command-sequences.mjs
```

Generated profile/report/audit должны быть воспроизводимы из того же commit.

## Приоритет при конфликте

1. current executable code/tests;
2. [RUNTIME_V8.md](RUNTIME_V8.md) / [CONFIGURATION.md](CONFIGURATION.md);
3. current generated artifacts;
4. этот legacy specification;
5. historical dated audits.

Этот файл сохраняется, чтобы старый K3–K5 analytical pipeline оставался понятным, но не вводил пользователя в заблуждение относительно current runtime.
