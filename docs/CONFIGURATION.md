# Конфигурация NXKeys v8

Канонический runtime profile:

```text
config/nx2512-v8-profile.json
```

Текущая profile schema — **8**. `Config.Load` принимает schemas **3…8**, применяет compatibility/default normalization и валидирует итоговый объект. Если JSON отсутствует, создаётся hardcoded v8 configuration.

Общий runtime contract: [RUNTIME_V8.md](RUNTIME_V8.md).

## Версии

| Контракт | Версия | Источник истины |
|---|---:|---|
| profile schema | **8** | `ConfigRuntimeV5.cs` |
| minimum readable profile | **3** | `ConfigRuntimeV5.cs` |
| IPC | **4** | `NXKeys.Protocol/NxProtocol.cs` |
| sequence policy | **8** | `scripts/sequence-policy.mjs` |
| v8 operation contract | v8 | `V8Models.cs` |

Не используйте старые документы со schema 6 / IPC 3 / policy 7 как current configuration reference.

## Два слоя конфигурации

### 1. v8 operation profile — current runtime source

`config/nx2512-v8-profile.json` содержит `schema_version`, `profile` и `operations`.

Упрощённый пример:

```json
{
  "schema_version": 8,
  "profile": {
    "name": "NX Adaptive Modules 2512.6000 v8",
    "nx_version": "2512.6000"
  },
  "operations": [
    {
      "operation_id": "modeling.example",
      "command_name": "Example",
      "paths": {
        "direct": null,
        "workspace_key": null,
        "leader": ["M", "X"],
        "secondary_aliases": ["M->Y"]
      },
      "adapter": {
        "kind": "button",
        "value": "UG_EXAMPLE_BUTTON",
        "status": "verified"
      },
      "availability": {
        "applications": ["modeling"],
        "requires_work_part": true,
        "blocked_in_text_input": true
      }
    }
  ]
}
```

Фактические operation fields определены в `NX2512_HotkeyStudio/Models/V8Models.cs`.

### 2. normalized/legacy runtime model

Внутренне v8 operations переводятся в существующие `ModuleConfig`, `CommandSet`, `LeaderSequence` и security permission structures. Старые schemas 3…7 поддерживаются только через migration/compatibility path.

Не проектируйте новые v8 функции вокруг устаревшей serialized schema 6 только потому, что такие поля ещё существуют во внутренних моделях.

## `paths`

### `direct`

Опциональная прямая клавиша без открытия Leader. Используйте только там, где контекст достаточно строгий и конфликт с обычным вводом исключён.

### `leader`

Основной mnemonic path после `CapsLock`.

В v8 path может быть **однотокенным**. Например в активном Sketch:

```text
L    Line
R    Rectangle
T    Trim
```

Поэтому старое универсальное правило «canonical path содержит 2–5 токенов» больше не является current runtime contract.

### `secondary_aliases`

Реальные runtime routing aliases. Формат в JSON:

```json
"secondary_aliases": ["M->L->S", "W->L->S"]
```

На десериализации aliases разворачиваются в операции до построения Leader DFA. Alias должен удовлетворять тем же prefix-free ограничениям, что и primary path.

### `workspace_key`

Клавиша, имеющая смысл **только внутри отдельного workspace state**.

Текущий runtime не проецирует operation, имеющую только `workspace_key`, в корень Leader. Это намеренное правило: иначе terminal root вроде `M` конфликтует с subtree `M → L → S`.

Пока workspace-state boundary не реализован явно, `workspace_key` нельзя документировать как обычный root shortcut.

## `adapter`

| Поле | Смысл |
|---|---|
| `kind` | тип адаптера (`button`, `internal` и т. п.) |
| `value` | точный NX command ID либо internal adapter payload |
| `status` | статус разрешения/реализации |

Не подменяйте точный NX `BUTTON ID` похожим именем.

Для новых Sheet Metal operations используйте canonical IDs NX 2512:

```text
UG_APP_SBSM
UG_SBSM_*
```

Runtime нормализует старые `UG_APP_SHEETMETAL` и `UG_SHEET_METAL_*` для совместимости, но новые профили должны использовать canonical names.

## `availability`

Текущая v8 model содержит:

```text
applications
requires_work_part
blocked_in_text_input
```

`applications` участвует не только в UI routing, но и в построении permission set для authenticated Bridge. Для switch operations security layer синтезирует разрешения по application mapping.

## Активный модуль

`AdaptiveModuleResolver` должен сначала учитывать exact NX application/module mapping, а уже потом label/heuristic aliases. Это важно для контекстов с одинаковыми первыми буквами, например Sketch / Sheet Metal / Surface / Simulation.

Пользовательский path не включает runtime module prefix.

## Modeling `M = Manage`

Пример v8 alias:

```text
M → L → S    Layer Settings
```

Если смотреть внутреннюю DFA sequence в Modeling, там может быть два `M`: первое — скрытый module prefix, второе — пользовательский Manage root. В пользовательской документации показывается только вводимый `M → L → S`.

## Sketch v8

Current Sketch model:

```text
L                 Line
R                 Rectangle
K → C             Coincident
D → Q             Rapid Dimension
C → V → …         variants
J → …             projection/derived
U → …             utilities
```

Полная таблица: [SKETCH_INTENT_LANGUAGE.md](SKETCH_INTENT_LANGUAGE.md).

## Selection Intent `0…4`

Selection Intent не хранится как обычные Leader operation rows. Это in-process Bridge behavior (`SelectionIntentHotkeys.cs`). Не пытайтесь моделировать `0…4` как глобальные `direct` shortcuts в profile: guard-логика специально привязана к active NX collector/seed.

Подробнее: [SELECTION_INTENT.md](SELECTION_INTENT.md).

## Legacy K1–K5 / K3–K5 generation pipeline

Репозиторий сохраняет:

```text
config/full-command-map/
config/nx2512-pro-hybrid.json
config/nx2512-pro-main.generated.json
scripts/compile-main-command-map.mjs
scripts/validate-main-command-map.mjs
```

Этот слой полезен для:

- intent catalog;
- frequency/coverage analysis;
- command resolution against Catalog Studio export;
- generated reports;
- regression/compatibility studies.

Он **не является default installer profile path**. `install-nxkeys.ps1` без `-ConfigPath` выбирает `config/nx2512-v8-profile.json`.

Generated K3–K5 artifacts не редактируются вручную.

### Legacy generated metadata

Старый generated profile сохраняет machine-readable поля, которые продолжают проверяться отдельным catalog validator:

```json
{
  "full_command_catalog": {
    "selected_frequencies": ["K3", "K4", "K5"],
    "selected_intents": 885
  }
}
```

`selected_frequencies`, `selected_intents`, а также уровни `K3`, `K4`, `K5` относятся **только к legacy generated profile**. Эти metadata не являются обязательной формой schema-8 `operations` profile и не определяют current runtime scope.

## Basic shortcuts и normalized defaults

Runtime сохраняет базовый allowlisted набор привычных shortcuts (`Ctrl+N`, `Ctrl+O`, `Ctrl+S`, Undo/Redo, clipboard, Delete, Fit, Refresh) через `BasicShortcutPolicy`/defaults.

Точный normalized object после `Config.Load` является источником истины для runtime, а не наличие/отсутствие каждого legacy поля в v8 source JSON.

## Environment paths

`Config.ExpandPath` поддерживает environment expansion и `~` в начале пути. Не храните secrets в profile: JSON попадает в managed deployment/backups и участвует в profile digest.

## Валидация

Основной набор:

```powershell
node .\scripts\validate-documentation.mjs
node .\scripts\validate-command-tree.mjs
node .\scripts\validate-main-command-map.mjs
node .\scripts\validate-full-command-map.mjs

dotnet run --project .\NX2512_HotkeyStudio.Tests\NX2512_HotkeyStudio.Tests.csproj -c Release
```

Для current v8 особенно проверяйте:

- schema 8 читается и нормализуется;
- missing profile даёт валидный hardcoded v8 fallback;
- `secondary_aliases` реально появляются в runtime paths;
- workspace-local key не становится terminal root;
- Modeling Manage subtree остаётся prefix-free;
- Sketch `K → …` routes остаются в Sketch context;
- Sheet Metal IDs canonicalized к `UG_SBSM_*`;
- security permission set совпадает с runtime command canonicalization.

## Изменение schema

При следующем schema bump одновременно обновите:

1. `Config.CurrentSchemaVersion` и supported range;
2. v8/vNext model classes;
3. migration/default normalization;
4. tests и CI source invariants;
5. installer/runtime acceptance;
6. `RUNTIME_V8.md`, этот документ и examples;
7. protocol/security docs, если меняется permission digest semantics.

Не оставляйте validator, который продолжает требовать старый номер schema.