# Конфигурация NXKeys — schema v3

Канонический профиль:

```text
config/nx2512-pro-hybrid.json
```

Он содержит только базовые глобальные сочетания, модули и параметры адаптивного Leader. Производные последовательности не хранятся в JSON.

## Верхний уровень

```json
{
  "schema_version": 3,
  "profile": {},
  "scan": {},
  "deployment": {},
  "keyboard": [],
  "modules": [],
  "workflow_controls": {},
  "performance": {},
  "role_deployment": {},
  "leader_key": {}
}
```

## Базовые сочетания

Секция `keyboard` обязана содержать ровно 12 включённых привязок:

```text
Ctrl+N, Ctrl+O, Ctrl+S, Ctrl+Shift+S,
Ctrl+Z, Ctrl+Y, Ctrl+X, Ctrl+C, Ctrl+V,
Delete, Ctrl+F, F5
```

Каждая запись требует точный `command.id`. `BasicShortcutPolicy` отклоняет дополнительное прямое сочетание, пропущенную обязательную привязку, дубликат или неправильный `BUTTON ID`.

Пример:

```json
{
  "shortcut": "Ctrl+S",
  "command": {
    "id": "UG_FILE_SAVE_PART",
    "name": "Save"
  },
  "scope": "Global",
  "enabled": true,
  "notes": "Сохранить рабочую деталь"
}
```

## Модули

Каждый элемент `modules` описывает самостоятельное приложение или контекст NX:

```json
{
  "id": "sheet_metal",
  "label": "Sheet Metal",
  "enabled": true,
  "nx_application_ids": ["UG_APP_SHEETMETAL"],
  "switch_command": {
    "id": "UG_APP_SHEETMETAL",
    "name": "Sheet Metal"
  },
  "leader_prefix": "H",
  "command_sets": [
    {
      "id": "primary",
      "label": "Primary",
      "commands": []
    }
  ]
}
```

Требования:

- уникальный `id`;
- уникальный внутренний `leader_prefix`;
- ровно восемь команд;
- слоты `N`, `NE`, `E`, `SE`, `S`, `SW`, `W`, `NW` без повторов;
- точный `command.id` каждой команды;
- контекстные флаги хранятся у команды.

Пример команды:

```json
{
  "slot": "S",
  "command": {
    "id": "UG_ASSEMBLIES_REPLACE_COMPONENT",
    "name": "Replace Component"
  },
  "requires_selection": true,
  "destructive": true,
  "confirm_before_execute": true
}
```

## Адаптивный Leader

```json
{
  "leader_key": {
    "enabled": true,
    "trigger_key": "CapsLock",
    "adaptive_module_mode": true,
    "hud_delay_ms": 120,
    "first_key_timeout_ms": 4000,
    "next_key_timeout_ms": 2500,
    "sticky_mode_on_double_tap": true,
    "hud_opacity": 0.95,
    "hook_only_when_nx_active": true,
    "slot_key_map": {
      "N": "W",
      "NE": "E",
      "E": "D",
      "SE": "C",
      "S": "X",
      "SW": "Z",
      "W": "A",
      "NW": "Q"
    }
  }
}
```

`adaptive_module_mode` всегда должен быть `true`. Клавиши сетки обязаны быть уникальными.

Внутренний DFA получает последовательность `<leader_prefix><input_key>`. Например, `MX` — позиция `S` модуля Modeling. Пользователь нажимает только `CapsLock+X`.

## Производные последовательности

После загрузки `LeaderKeyConfig.RebuildFromModules()` создаёт `LeaderSequenceItem` для каждой модульной команды:

```text
ModuleConfig + ModuleCommand + slot_key_map → LeaderSequenceItem
```

Поля `Sequences` и `RuntimeModules` имеют `[JsonIgnore]`. Поэтому JSON не содержит вторую копию карты.

## Определение активного модуля

`AdaptiveModuleResolver` проверяет по порядку:

1. нормализованный `context.module_id`;
2. `context.module_label`;
3. известное сопоставление `application_id`;
4. `modules[].nx_application_ids`.

Если контекст отсутствует или устарел, Leader не активируется.

## Guards и таймауты

Отдельный файл:

```text
config/nx2512-state-machines.json
```

задаёт:

- таймауты состояний;
- допустимые модули;
- наличие work/display part;
- минимальную достоверность;
- количество и типы выбранных объектов;
- обязательное подтверждение;
- сообщение недоступности.

Адаптивная команда другого модуля блокируется. Автоматическая скрытая смена приложения не используется; `Tab` инициирует явный переход.

## Deployment

Рекомендуемый режим:

```json
{
  "deployment": {
    "mode": "managed-wrapper",
    "managed_root": "%LOCALAPPDATA%\\NXKeys\\managed\\NX2512.6000",
    "backup_root": "%LOCALAPPDATA%\\NXKeys\\backups",
    "require_nx_stopped": true,
    "atomic_writes": true,
    "dry_run": true
  }
}
```

Для `existing-custom-dirs` путь `existing_custom_dirs_file` задаётся явно.

## Проверка

```powershell
NX2512_HotkeyStudio.exe validate --config .\config\nx2512-pro-hybrid.json
node .\scripts\validate-command-tree.mjs
```
