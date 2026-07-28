# Конфигурация NXKeys

## Профили

NXKeys использует два типа профилей:

```text
config/nx2512-pro-hybrid.json          базовый проверенный профиль
config/nx2512-pro-full.generated.json  локально сгенерированный полный профиль
```

Полный профиль создаётся из 1169 намерений и каталога конкретной установки NX. Его нельзя считать универсальным: число включённых команд зависит от роли, лицензий, локализации и MenuScript-расширений.

## Версии schema

| Уровень | Версия |
|---|---:|
| JSON-профиль на диске | `4` |
| Runtime-модель HotkeyStudio | `5` |
| IPC HotkeyStudio ↔ CommandBridge | `3` |
| `full_command_catalog` | `1` |

Установщик принимает source profile schema 3–4. При загрузке `Config.ApplyDefaults()` мигрирует его в runtime schema 5, нормализует пути и строит производные последовательности. При сохранении через HotkeyStudio новые runtime-поля могут быть записаны явно.

## Верхний уровень

```json
{
  "schema_version": 4,
  "profile": {},
  "scan": {},
  "deployment": {},
  "keyboard": [],
  "modules": [],
  "workflow_controls": {},
  "performance": {},
  "role_deployment": {},
  "leader_key": {},
  "full_command_catalog": {}
}
```

`full_command_catalog` присутствует только в полном сгенерированном профиле.

## Базовые прямые сочетания

Секция `keyboard` содержит ровно 12 разрешённых привязок:

```text
Ctrl+N, Ctrl+O, Ctrl+S, Ctrl+Shift+S,
Ctrl+Z, Ctrl+Y, Ctrl+X, Ctrl+C, Ctrl+V,
Delete, Ctrl+F, F5
```

Все профессиональные команды вызываются через Leader, чтобы не создавать глобальные конфликты NX.

## Команда модуля

```json
{
  "command": {
    "id": "UG_MODELING_EXTRUDED_FEATURE",
    "name": "Extrude",
    "aliases": ["Extrude", "Вытягивание"]
  },
  "path": ["C", "F", "E"],
  "path_labels": ["Create", "Feature", "Extrude"],
  "aliases": [["C", "E"]],
  "search_aliases": ["extrude", "вытянуть", "выдавливание"],
  "action": "execute_command",
  "selection_type": "none",
  "enabled": true,
  "requires_selection": false,
  "destructive": false,
  "confirm_before_execute": false,
  "icon_hint": "feature",
  "display_order": 2
}
```

### `path`

Канонический путь после `CapsLock`. Внутренний префикс модуля пользователем не вводится.

```text
Пользователь: CapsLock → C → F → E
DFA:          M → C → F → E
```

### `path_labels`

Подписи уровней HUD. Их число должно совпадать с длиной `path`.

### `aliases`

Дополнительные короткие пути. Alias удаляется, если совпадает с каноническим путём, другой командой либо создаёт конфликт префикса.

### `search_aliases`

Имена, переводы, сокращения, профессиональные термины и пользовательские формулировки для поиска.

### `action`

| Значение | Поведение |
|---|---|
| `execute_command` | Вызов точного NX `BUTTON ID` через CommandBridge. |
| `set_selection_filter` | Применение глобального NXOpen selection-фильтра без запуска `UG_SEL_*` как обычной кнопки. |

### `selection_type`

```text
none, all, reset, edge, face, body, component,
curve, datum, feature, operation
```

Поле описывает ожидаемый тип выбора. Для `set_selection_filter` оно передаётся в IPC как `selection_filter`.

## Поля полного каталога

Команды, добавленные компилятором, дополнительно содержат:

```json
{
  "catalog_refs": ["nx2512-000123"],
  "frequency": "K4",
  "resolution_status": "resolved",
  "resolution_candidates": [],
  "fallback": "catalog:nx2512-000123"
}
```

| Поле | Назначение |
|---|---|
| `catalog_refs` | Стабильные идентификаторы исходных намерений. |
| `frequency` | Экспертная частота `K1–K5`. |
| `resolution_status` | `existing`, `resolved`, `ambiguous` или `unresolved`. |
| `resolution_candidates` | Лучшие кандидаты `BUTTON ID` и оценки сходства. |
| `fallback` | Трассировка к исходной записи полного каталога. |

Правило безопасности: `enabled: true` допустим только при непустом точном `command.id`.

## Метаданные полной генерации

```json
{
  "full_command_catalog": {
    "schema_version": 1,
    "source_intents": 1169,
    "generated_utc": "2026-07-28T00:00:00Z",
    "catalog_items": 12345,
    "global_commands_duplicated": true,
    "source_files": []
  }
}
```

`generated_utc` и `catalog_items` зависят от конкретного запуска.

## Модули

Канонические модули:

```text
modeling, sketch, assembly, drafting, pmi, surface,
sheet_metal, manufacturing, simulation, routing, mold,
reuse, inspect_view, selection_object
```

```json
{
  "id": "manufacturing",
  "label": "CAM / Manufacturing",
  "enabled": true,
  "nx_application_ids": ["UG_APP_MANUFACTURING"],
  "leader_prefix": "C",
  "command_sets": []
}
```

Одинаковый пользовательский путь может существовать в разных модулях. Внутри одного модуля все канонические пути и aliases должны быть уникальными и prefix-free.

Базовый профиль содержит curated primary-команды и дополнительные command sets. Полный профиль не ограничен восемью командами на модуль: восемь физических клавиш относятся только к legacy/primary-слою быстрых aliases.

## Автоматическая миграция

При загрузке профиля:

1. нормализуются legacy `slot`, `submenu_key`, `input_key`;
2. создаются `path`, `path_labels`, `aliases`, `search_aliases`;
3. для известных ID применяются точные ручные пути;
4. остальным командам назначаются детерминированные пути;
5. устраняются коллизии и конфликты префиксов;
6. `UG_SEL_*` получают `action: set_selection_filter`;
7. выводится `selection_type`;
8. строится runtime schema 5 и DFA.

Основная реализация:

```text
NX2512_HotkeyStudio/Models/ConfigRuntimeV5.cs
NX2512_HotkeyStudio/Models/MnemonicPathGenerator.cs
NX2512_HotkeyStudio/Models/LeaderConfigV5.cs
```

## Компиляция полной карты

```powershell
node .\scripts\compile-full-command-map.mjs `
  --profile .\config\nx2512-pro-hybrid.json `
  --intents .\config\full-command-map `
  --catalog-dir "D:\NX2512_Catalog_Output" `
  --out .\config\nx2512-pro-full.generated.json `
  --report .\docs\generated\full-command-resolution.md
```

По умолчанию глобальные команды дублируются в активные модули. Флаг `--no-global-duplication` оставляет их только в специализированных областях.

## Guards и подтверждение

`config/nx2512-state-machines.json` задаёт таймауты, допустимые модули, требования к Work/Display Part, confidence, selection и подтверждению. Policy использует нормализованные последовательности с внутренним префиксом модуля.

`requires_selection` описывает workflow и ожидаемый тип выбора, но не всегда означает обязательный preselection. Жёсткий минимум задаётся policy.

## Проверка

```powershell
node .\scripts\validate-command-tree.mjs
node .\scripts\validate-full-command-map.mjs
```

Проверяется:

- 12 базовых сочетаний;
- 14 модулей;
- 1169 намерений и 32 раздела;
- `K1–K5`;
- уникальность и prefix-free пути;
- отсутствие включённых строк без точного `BUTTON ID`;
- aliases, `action`, `selection_type`;
- schema migration и policy;
- целостность HTML-карты и документации.
