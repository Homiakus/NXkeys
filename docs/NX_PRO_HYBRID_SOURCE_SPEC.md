# Спецификация профилей NXKeys 2512

## Термины

| Термин | Значение |
|---|---|
| intent catalog | 1169 source records K1–K5 в `config/full-command-map/` |
| bootstrap profile | `config/nx2512-pro-hybrid.json`, schema 6, safety/deployment/known IDs |
| generated main profile | K3–K5 output с 885 selected intents и resolution metadata |
| installed profile | generated main profile под compatibility filename `nx2512-pro-hybrid.json` |
| runtime profile | загруженный и мигрированный объект schema 6 |

Слово `hybrid` в filename не означает, что source bootstrap является рекомендуемым полным runtime profile.

## Базовые контракты

- 12 direct basic shortcuts;
- один Leader key (`CapsLock` по умолчанию);
- 14 context modules;
- legacy primary aliases `W/E/D/C/X/Z/A/Q`;
- canonical paths 2–5 tokens;
- source/runtime profile schema 6;
- minimum readable schema 3;
- IPC schema 3;
- source sequence policy 7;
- main runtime scope K3/K4/K5 = 885 intents.

## Модули

| ID | Область | Internal prefix |
|---|---|---|
| `modeling` | Modeling | `M` |
| `sketch` | Sketch | `S` |
| `assembly` | Assembly | `A` |
| `drafting` | Drafting | `D` |
| `pmi` | PMI | `P` |
| `surface` | Surface | `U` |
| `sheet_metal` | Sheet Metal | `H` |
| `manufacturing` | CAM / Manufacturing | `C` |
| `simulation` | CAE / Simulation | `X` |
| `routing` | Routing | `G` |
| `mold` | Mold / Tooling | `O` |
| `reuse` | Reuse / Templates | `R` |
| `inspect_view` | Inspect / View | `V` |
| `selection_object` | Selection / Object | `F` |

Internal prefix нужен DFA и пользователем не вводится.

## Legacy primary grid

| Key | Slot | Роль |
|---|---|---|
| W | N | основная create/open command |
| E | NE | следующий частый шаг |
| D | E | добавить object/dependency |
| C | SE | transform/replace |
| X | S | process/remove |
| Z | SW | remove/reduce |
| A | W | structure/link/pattern |
| Q | NW | inspect/service |

Эти keys являются optional aliases. Они не ограничивают module восемью commands и сохраняются только без path conflicts.

## Source sequence policy v7

Частотные цели:

```text
K5 <= 2
K4 <= 3
K3 <= 4
K2/K1 <= 5
support = 2
```

Selection support:

```text
SB SF SE ST SC SU SD SR SA SN
```

Module switches:

```text
GM GA GD GP GU GH GC GN GR GO GL GV
```

Switches не добавляются в Sketch и Selection/Object module.

## Bootstrap requirements

Bootstrap должен содержать:

- valid profile name/version;
- deployment managed/backup roots;
- 12 exact basic bindings;
- 14 enabled modules с unique IDs/prefixes;
- known exact IDs;
- curated paths/aliases;
- workflow controls;
- safety flags;
- adaptive Leader enabled.

Bootstrap может содержать неполное command coverage и не должен выдавать себя за generated main profile.

## Full intent catalog

Каждая source record должна иметь:

- stable `intent_id`;
- source section/group;
- frequency K1–K5;
- English/Russian names;
- runtime module;
- path hint;
- traceability к source inventory.

Количество records и frequencies являются машинно проверяемым contract:

```text
K1=4, K2=280, K3=445, K4=371, K5=69, total=1169
```

## Generation

Compiler объединяет intent catalog, bootstrap, target UI catalog и runtime probe.

Generated row получает:

- exact command ID либо disabled status;
- canonical path и aliases;
- frequency;
- `catalog_refs`;
- resolution status/candidates;
- action/selection/module metadata;
- safety fields.

Global intents могут дублироваться в active modules. Coverage измеряется unique `catalog_refs`, не module row count.

## Resolution rules

| Status | Условие | Enabled |
|---|---|---:|
| existing | exact trusted bootstrap ID | да |
| resolved | confident target catalog match | да |
| ambiguous | несколько близких candidates | нет |
| unresolved | ID отсутствует | нет |

Command name или API candidate не заменяет exact UI ID.

## Path source и locking

Schema 6 содержит `path_locked` и `path_source`. Они предназначены для provenance/locking metadata.

Текущий source не подтверждает отдельный полностью завершённый user override file/UI pipeline. До его появления path customization выполняется через versioned source configuration/generator и validators.

## Production-ready command

Command считается готовой после:

1. source intent/known command существует;
2. exact ID подтверждён target NX catalog;
3. path и aliases prefix-free;
4. action/selection semantics заданы;
5. context guards соответствуют workflow;
6. destructive classification/confirmation проверены;
7. profile validators проходят;
8. Bridge contract build проходит;
9. runtime execution подтверждено в target NX;
10. evidence не содержит production data/secrets.

Coverage или compilation без runtime test не достаточны.

## Module change

Новый module требует:

- unique `id` и prefix;
- application IDs;
- switch command при применимости;
- command sets;
- resolver mapping;
- declarative guards;
- support-command policy;
- DFA/HFSM tests;
- compiler validation;
- docs и command tree update.

## Profile schema change

При изменении schema:

- обновить current/minimum versions;
- реализовать defaults/migration;
- обновить installer range;
- обновить CI checks;
- добавить compatibility tests;
- обновить examples/config docs;
- исправить runtime error messages;
- описать migration/rollback в changelog/ADR при breaking change.

## Validation

```powershell
node .\scripts\validate-full-command-map.mjs
node .\scripts\validate-main-command-map.mjs
node .\scripts\validate-command-tree.mjs
node .\scripts\audit-command-sequences.mjs

dotnet run --project .\NXKeys.StateMachines.Tests\NXKeys.StateMachines.Tests.csproj -c Release
```

Generated profile/report/audit должны быть созданы тем же source commit, который их публикует.

## Требует проверки на workstation

- application IDs конкретной role;
- licensing каждого module;
- sensitivity command IDs;
- selection behavior;
- modal workflows;
- destructive classification;
- code-signing policy;
- custom directory interaction.
