# Главный профиль K3–K5 и полный каталог NX 2512

## Область

Source catalog NXKeys содержит **1169 намерений** Siemens NX 2512 в **32 разделах**. Стандартный runtime profile включает приоритетные уровни K3–K5. Уровни **K1–K2** остаются source-only и не входят в стандартную установку.

| Уровень | Количество |
|---|---:|
| K5 | 69 |
| K4 | 371 |
| K3 | 445 |
| **Главный профиль** | **885** |
| K2 | 280 |
| K1 | 4 |
| **Полный источник** | **1169** |

K1/K2 остаются в versioned source catalog, но отдельный installable full/all profile текущим installer не поддерживается.

## Файлы

```text
config/full-command-map/
  nx2512-full-command-map.json.gz.b64.part1
  nx2512-full-command-map.json.gz.b64.part2
  nx2512-full-command-map.json.gz.b64.part3

config/nx2512-pro-hybrid.json             bootstrap schema 6
config/nx2512-pro-main.generated.json     generated main K3-K5

docs/generated/main-profile-resolution.md
docs/audit/command-sequence-audit.md
```

Generated files нельзя редактировать вручную. Если source compiler или sequence policy изменены, их необходимо пересоздать.

## Запись намерения

Для функции сохраняются:

- стабильный `intent_id`;
- source section/group;
- `runtime_module`;
- frequency K1–K5;
- английское и русское имя;
- path hint;
- source traceability.

Intent record не является исполняемой командой, пока compiler не разрешит точный `BUTTON ID`.

## Компиляция

Рекомендуемый способ:

```powershell
.\install-nxkeys.ps1 `
  -CatalogDir "D:\NX2512_Catalog_Output" `
  -CompileOnly
```

Прямой вызов:

```powershell
node .\scripts\compile-main-command-map.mjs `
  --profile .\config\nx2512-pro-hybrid.json `
  --intents .\config\full-command-map `
  --probe .\docs\audit\runtime-command-probe-2026-07-28.json `
  --catalog-dir "D:\NX2512_Catalog_Output" `
  --out .\config\nx2512-pro-main.generated.json `
  --report .\docs\generated\main-profile-resolution.md
```

Main compiler фиксирует scope K3/K4/K5.

## Разрешение IDs

Compiler объединяет:

1. known IDs bootstrap;
2. `06_ui_commands_buttons.csv` target NX;
3. runtime probe;
4. command names/aliases;
5. module-aware scoring;
6. curated mnemonic definitions.

Статусы:

| Status | Значение | Enabled |
|---|---|---:|
| `existing` | exact bootstrap ID | да |
| `resolved` | надёжный target catalog match | да |
| `ambiguous` | несколько близких candidates | нет |
| `unresolved` | ID не найден | нет |

Similarity score не является достаточным доказательством для включения ambiguous command.

## Metadata main profile

После regeneration текущим source ожидается:

```json
{
  "full_command_catalog": {
    "schema_version": 2,
    "source_intents": 1169,
    "selected_intents": 885,
    "selected_frequencies": ["K3", "K4", "K5"],
    "sequence_policy_version": 7,
    "selection_filter_support_commands": 140,
    "module_switch_support_commands": 132,
    "frequency_counts": {
      "K1": 4,
      "K2": 280,
      "K3": 445,
      "K4": 371,
      "K5": 69
    }
  }
}
```

Почему selection support = 140: десять selection actions добавляются в 14 enabled modules. Module switches формируются в 12 обычных switchable modules, по 11 переходов без self-switch.

`Select All` является одновременно catalog intent и universal support action. Generated row помечается `catalog_backed_support: true`: frequency остаётся `support`, а `catalog_refs` сохраняют покрытие intent.

**Состояние репозитория:** source policy уже v7, но checked-in generated audit может всё ещё показывать v6/112 selection rows до запуска генераторов. Это несоответствие считается stale generated artifact, а не альтернативной policy.

## Политика путей v7

- canonical path: 2–5 буквенно-цифровых tokens;
- paths и aliases prefix-free внутри module;
- K5 ≤ 2, K4 ≤ 3, K3 ≤ 4;
- support command length = 2;
- action/object semantics предпочтительны случайному распределению;
- known explicit path обрабатывается раньше generated fallback;
- enabled command обязана иметь exact ID;
- глобальные намерения по умолчанию дублируются по модулям;
- `--no-global-duplication` отключает такое дублирование.

### Selection paths

```text
SB Body       SF Face       SE Edge       ST Feature
SC Component  SU Curve      SD Datum      SR Reset
SA Select All SN Deselect All
```

### Module switches

```text
GM Modeling       GA Assembly      GD Drafting
GP PMI            GU Surface       GH Sheet Metal
GC Manufacturing  GN Simulation    GR Routing
GO Mold           GL Reuse         GV Inspect/View
```

Switch rows не добавляются в Sketch и Selection/Object module.

## Coverage metrics

Различайте:

1. **Source intents** — 1169.
2. **Selected main intents** — 885 unique `catalog_refs`.
3. **Serialized module rows** — может быть больше 885 из-за global duplication.
4. **Executable rows** — enabled rows с exact ID.
5. **Runtime verified commands** — фактически проверенные в target NX.

Число rows не заменяет coverage unique intents.

## Проверка

```powershell
node .\scripts\validate-full-command-map.mjs
node .\scripts\validate-main-command-map.mjs
node .\scripts\validate-command-tree.mjs
node .\scripts\audit-command-sequences.mjs
```

Проверяются:

- 1169 intents и 32 sections;
- точное frequency distribution;
- 885 selected K3–K5 intents;
- отсутствие K1/K2 leakage;
- все selected `catalog_refs`;
- path/alias conflicts;
- frequency length targets;
- universal `S*` и `G*` paths;
- disabled ambiguous/unresolved;
- отсутствие enabled row без ID;
- документационные инварианты.

После изменения compiler/policy проверьте diff generated profile, resolution report, sequence audit и interactive command map.
