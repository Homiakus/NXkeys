# Конфигурация NXKeys

## Уровни конфигурации

NXKeys использует четыре уровня:

1. `config/full-command-map/` — каталог 1169 намерений K1–K5;
2. `config/nx2512-pro-hybrid.json` — bootstrap-профиль;
3. `config/nx2512-pro-main.generated.json` — generated main profile K3–K5;
4. установленный `nx2512-pro-hybrid.json` — compatibility filename generated profile.

Не редактируйте generated и installed profile как источник конфигурации.

## Версии контрактов

| Контракт | Версия | Источник истины |
|---|---:|---|
| profile schema | 6 | `ConfigRuntimeV5.cs` |
| minimum readable profile | 3 | `ConfigRuntimeV5.cs` |
| IPC | 3 | `NxProtocol.cs` |
| `full_command_catalog` | 2 | compiler output |
| source sequence policy | 7 | `scripts/sequence-policy.mjs` |

Runtime принимает schema 3–6, применяет defaults и migration, затем нормализует объект к schema 6.

## Bootstrap

```text
config/nx2512-pro-hybrid.json
```

Bootstrap содержит:

- `profile`, `scan`, `deployment`;
- ровно 12 enabled basic shortcuts;
- 14 адаптивных модулей;
- `workflow_controls`, `performance`, `role_deployment`, `leader_key`;
- известные command IDs и curated commands.

Bootstrap служит входом compiler, а не полным daily runtime profile.

## Generated main profile

```text
config/nx2512-pro-main.generated.json
```

Ожидаемая metadata после generation текущим compiler:

```json
{
  "schema_version": 6,
  "full_command_catalog": {
    "schema_version": 2,
    "source_intents": 1169,
    "selected_intents": 885,
    "selected_frequencies": ["K3", "K4", "K5"],
    "sequence_policy_version": 7,
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

Если checked-in generated file или sequence audit содержит policy 6, artifact устарел относительно source policy и должен быть пересоздан.

## Верхнеуровневые разделы

### `profile`

Содержит имя, целевую версию NX и описание. `nx_version` используется defaults, но не заменяет проверку фактического NXOpen DLL.

### `scan`

Определяет:

- roots и install/profile hints;
- расширения MenuScript, role и launcher;
- `max_depth`, `max_files`, `follow_symlinks`.

Environment placeholders вида `%LOCALAPPDATA%` разворачиваются runtime.

### `deployment`

Основные поля:

```json
{
  "mode": "managed-wrapper",
  "managed_root": "%LOCALAPPDATA%\\NXKeys\\managed\\NX2512.6000",
  "backup_root": "%LOCALAPPDATA%\\NXKeys\\backups",
  "overlay_filename": "nxkeys_generated.men",
  "menuscript_version": 139,
  "main_menubar_id": "UG_GATEWAY_MAIN_MENUBAR",
  "nx_executable": "",
  "existing_custom_dirs_file": "",
  "patch_existing_custom_dirs": false,
  "require_nx_stopped": true,
  "clear_detected_conflicts": false,
  "atomic_writes": true,
  "dry_run": true
}
```

Допустимые modes: `managed-wrapper` и `existing-custom-dirs`.

### `keyboard`

`BasicShortcutPolicy` разрешает только:

```text
Ctrl+N, Ctrl+O, Ctrl+S, Ctrl+Shift+S,
Ctrl+Z, Ctrl+Y, Ctrl+X, Ctrl+C, Ctrl+V,
Delete, Ctrl+F, F5
```

Validator требует точные IDs и ровно 12 enabled bindings. Остальные команды должны использовать Leader paths.

## Модуль

```json
{
  "id": "modeling",
  "label": "Modeling",
  "enabled": true,
  "nx_application_ids": ["UG_APP_MODELING"],
  "switch_command": {
    "id": "UG_APP_MODELING",
    "name": "Modeling"
  },
  "leader_prefix": "M",
  "selection_priorities": [],
  "command_sets": []
}
```

Для enabled module требуются уникальные `id` и внутренний `leader_prefix`, команды и подтверждённые application mappings. Пользователь не вводит module prefix.

## Команда

Пример сокращённой command row:

```json
{
  "path": ["C", "F", "E"],
  "path_labels": ["Create", "Feature", "Extrude"],
  "aliases": [["C", "E"]],
  "search_aliases": ["Extrude", "Выдавливание"],
  "command": {
    "id": "UG_MODELING_EXTRUDED_FEATURE",
    "name": "Extrude"
  },
  "action": "execute_command",
  "target_module_id": "",
  "support_kind": "",
  "selection_type": "feature",
  "enabled": true,
  "requires_selection": false,
  "destructive": false,
  "confirm_before_execute": false,
  "frequency": "K5",
  "catalog_backed_support": false,
  "path_locked": false,
  "path_source": "curated"
}
```

Подтверждённые поля модели:

| Группа | Поля |
|---|---|
| legacy/UI | `slot`, `submenu_key`, `submenu_label`, `input_key`, `icon_hint`, `display_order` |
| routing | `path`, `path_labels`, `aliases`, `search_aliases`, `action`, `target_module_id`, `support_kind`, `selection_type` |
| command | `command.id`, `command.name` |
| safety | `enabled`, `requires_selection`, `destructive`, `confirm_before_execute` |
| traceability | `fallback`, `notes`, `frequency`, `catalog_backed_support`, generated resolution metadata |
| path metadata | `path_locked`, `path_source` |

### Paths

- canonical path содержит 2–5 токенов;
- aliases проходят ту же prefix-free проверку;
- `search_aliases` используются для текста поиска и не являются routing paths;
- K5 должен укладываться в 2 токена, K4 — в 3, K3 — в 4 при текущей policy;
- support paths резервируются раньше обычных команд.

### Resolution

Generated rows получают `catalog_refs`, `resolution_status` и `resolution_candidates`.

| Статус | Исполнение |
|---|---|
| `existing` | разрешено при валидном ID |
| `resolved` | разрешено при надёжном ID |
| `ambiguous` | disabled |
| `unresolved` | disabled |

Не меняйте status вручную без evidence target catalog.

### `catalog_backed_support`

Большинство universal support rows являются чистой runtime-инфраструктурой: у них `frequency: support` и нет `catalog_refs`. Исключение возникает, когда та же функция уже входит в выбранные 885 catalog intents. Подтверждённый пример — `Select All`, нормализованный к пути `SA`.

Для такой строки compiler задаёт:

```json
{
  "support_kind": "selection_filter",
  "frequency": "support",
  "catalog_backed_support": true,
  "catalog_refs": ["nx2512-00-L0060"]
}
```

Семантика:

- путь и execution action остаются universal support;
- `frequency` остаётся `support`, поэтому к пути применяется support policy;
- `catalog_refs` сохраняют coverage и трассировку исходного K3–K5 intent;
- validator разрешает catalog coverage только при явном `catalog_backed_support: true`;
- чистая support row не должна получать этот флаг или catalog reference.

Поле не передаётся в IPC: оно относится к profile/coverage layer.

### `path_locked` и `path_source`

Поля уже присутствуют в schema 6:

- `path_locked` — metadata для закрепления явно выбранного пути;
- `path_source` — происхождение пути, например curated или generated.

**Ограничение:** наличие этих полей не означает, что отдельный user override file и полный UI-workflow кастомизации уже реализованы end-to-end. До появления такого механизма source paths меняются через versioned profile/policy и проходят validators.

## Workflow controls

Schema содержит:

```json
{
  "workflow_controls": {
    "accept_ok": { "id": "", "name": "OK" },
    "apply": { "id": "", "name": "Apply" },
    "cancel": { "id": "", "name": "Cancel" },
    "back_previous_step": { "id": "", "name": "Back" },
    "confirm_dangerous": true
  }
}
```

Default names не являются подтверждёнными исполняемыми IDs. Конкретное modal behavior необходимо подтверждать Bridge/runtime кодом и целевой NX.

## Универсальные selection actions

Source policy v7 добавляет во все enabled modules:

```text
SB body       SF face       SE edge       ST feature
SC component  SU curve      SD datum      SR reset
SA all        SN none
```

Они используют `action: set_selection_filter` и `support_kind: selection_filter`. Если universal action одновременно представляет selected catalog intent, compiler сохраняет его coverage через `catalog_backed_support`.

## Module switches

Резервируются:

```text
GM modeling       GA assembly      GD drafting
GP pmi            GU surface       GH sheet metal
GC manufacturing  GN simulation    GR routing
GO mold           GL reuse         GV inspect/view
```

Switch rows не добавляются в `sketch` и `selection_object`. Модуль не получает switch на самого себя.

Source policy также экспортирует default cycle `modeling → assembly → drafting → manufacturing`. Фактическое использование списка UI/runtime должно подтверждаться Leader engine; наличие constant само по себе не является доказательством поведения.

## Environment expansion

`Config.ExpandPath` поддерживает `%VAR%`, стандартное expansion environment variables и `~` в начале пути. Не храните секреты в profile: JSON попадает в managed package и backups.

## Компиляция

Рекомендуемый вход:

```powershell
.\install-nxkeys.ps1 `
  -CatalogDir "D:\NX2512_Catalog_Output" `
  -CompileOnly
```

Прямой compiler:

```powershell
node .\scripts\compile-main-command-map.mjs `
  --profile .\config\nx2512-pro-hybrid.json `
  --intents .\config\full-command-map `
  --probe .\docs\audit\runtime-command-probe-2026-07-28.json `
  --catalog-dir "D:\NX2512_Catalog_Output" `
  --out .\config\nx2512-pro-main.generated.json `
  --report .\docs\generated\main-profile-resolution.md
```

Без global duplication:

```powershell
node .\scripts\compile-main-command-map.mjs --no-global-duplication
```

## Валидация

```powershell
node .\scripts\validate-main-command-map.mjs
node .\scripts\validate-command-tree.mjs
node .\scripts\validate-full-command-map.mjs
node .\scripts\audit-command-sequences.mjs

dotnet run --project .\NXKeys.StateMachines.Tests\NXKeys.StateMachines.Tests.csproj -c Release
```

Основные инварианты:

- current schema 6;
- main scope K3/K4/K5, 885 intents;
- 12 basic shortcuts и 14 modules;
- enabled command имеет exact ID;
- paths и aliases prefix-free;
- destructive command требует confirmation;
- support paths не используются обычными командами;
- catalog-backed support сохраняет intent coverage только при явном флаге.

## Порядок изменения

1. Изменяйте bootstrap, intent catalog, sequence policy или curated generator.
2. Запустите validators.
3. Пересоздайте generated profile, resolution report и sequence audit.
4. Проверьте diff paths, support counts и resolution statuses.
5. Соберите HotkeyStudio и Control Center.
6. Для Bridge/command changes выполните test на target NX.
7. Обновите документацию и changelog при изменении contract.
