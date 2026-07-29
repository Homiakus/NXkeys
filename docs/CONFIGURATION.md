# Конфигурация NXKeys

## Модель профилей

NXKeys использует два уровня конфигурации.

### Bootstrap

```text
config/nx2512-pro-hybrid.json
```

Bootstrap содержит:

- 12 прямых базовых сочетаний;
- 14 адаптивных модулей;
- проверенные вручную `BUTTON ID`;
- короткие aliases;
- selection-команды;
- deployment и scan settings;
- основу safety-policy.

Bootstrap не является заявленным полным пользовательским покрытием. Он служит безопасной основой компиляции.

### Главный профиль

```text
config/nx2512-pro-main.generated.json
```

Главный профиль генерируется из bootstrap и полного каталога, но включает только K3–K5: **885 уникальных намерений**.

```json
{
  "schema_version": 6,
  "full_command_catalog": {
    "schema_version": 2,
    "source_intents": 1169,
    "selected_intents": 885,
    "selected_frequencies": ["K3", "K4", "K5"],
    "sequence_policy_version": 6,
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

Поля `selected_frequencies` и `selected_intents` являются машинно проверяемым контрактом главного профиля. Если в него попадёт K1 или K2, CI завершится ошибкой.

## Runtime-имя

Deployment сохраняет выбранный профиль в managed root как:

```text
nx2512-pro-hybrid.json
```

Это имя оставлено для совместимости launcher, Control Center и существующих установок. Содержимое файла при стандартной установке — главный профиль K3–K5, а не исходный bootstrap.

## Схемы

- bootstrap/generated source: `schema_version: 6`;
- после загрузки: runtime schema 6;
- IPC Command Bridge: schema 3;
- `full_command_catalog.schema_version`: 2.

Runtime migration добавляет и нормализует:

```text
path
path_labels
aliases
search_aliases
action
target_module_id
support_kind
selection_type
```

## Команда в generated profile

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
  "enabled": true,
  "frequency": "K5",
  "catalog_refs": ["intent-..."],
  "resolution_status": "resolved",
  "action": "execute_command",
  "target_module_id": "",
  "support_kind": "",
  "selection_type": "feature"
}
```

Дополнительные поля generated profile:

- `catalog_refs` — исходные намерения, покрываемые строкой;
- `frequency` — K3, K4 или K5 в главном профиле;
- `resolution_status` — `existing`, `resolved`, `ambiguous`, `unresolved`;
- `resolution_candidates` — лучшие кандидаты с оценкой;
- `target_module_id` — целевой модуль для строк `action: "switch_module"`;
- `support_kind` — runtime-инфраструктура, например `selection_filter` или `module_switch`;
- `fallback: catalog:<intent_id>` — стабильная связь с источником.

## Статусы разрешения

| Статус | Значение | Исполнение |
|---|---|---|
| `existing` | точный ID уже был в bootstrap | включено |
| `resolved` | найдено надёжное соответствие в каталоге NX | включено |
| `ambiguous` | несколько близких кандидатов | отключено |
| `unresolved` | надёжного ID нет | отключено |

Команда без точного ID никогда не становится исполняемой.

## Компиляция

Главный профиль:

```powershell
node .\scripts\compile-main-command-map.mjs `
  --profile .\config\nx2512-pro-hybrid.json `
  --catalog-dir "D:\NX2512_Catalog_Output" `
  --out .\config\nx2512-pro-main.generated.json `
  --report .\docs\generated\main-profile-resolution.md
```

Область фиксирована в компиляторе: `K3,K4,K5`.

Установочный пресет один: K3/K4/K5. K1/K2 остаются в исходном каталоге `config/full-command-map/`, но не собираются в отдельный runtime profile.

## Глобальные команды

Глобальные намерения по умолчанию дублируются в активные модули. Поэтому количество строк generated profile может быть больше 885, но число уникальных `catalog_refs` главного scope всегда равно 885.

Для отключения дублирования:

```powershell
node .\scripts\compile-main-command-map.mjs --no-global-duplication
```

## Инварианты

- главный scope — только K3, K4, K5;
- все 885 намерений представлены хотя бы одной строкой;
- ни один K1/K2 `catalog_ref` не попадает в main;
- включённая команда имеет непустой реальный ID;
- путь длиной 2–5 токенов;
- K5 не длиннее 2 токенов, K4 не длиннее 3, K3 не длиннее 4;
- пути и aliases prefix-free внутри модуля;
- destructive-команда требует подтверждение;
- selection-фильтры используют фиксированные `S*` пути: `SB`, `SF`, `SE`, `ST`, `SC`, `SU`, `SD`, `SR`;
- переходы между модулями используют `G*` и доступны во всех модулях кроме Sketch.
