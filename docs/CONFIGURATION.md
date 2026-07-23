# Справочник JSON-конфигурации NXKeys

JSON-профиль является единственным редактируемым источником истины. Сгенерированные `.men`, `.tbr`, `.rtb`, CMD-файлы, отчёты и package manifest вручную не редактируются.

## 1. Профили

Основной профиль:

```text
config/nx2512-pro-hybrid.json
```

Альтернативный профиль:

```text
config/nx2512-ergo-80.json
```

Копии в `internal/defaults` удалены вместе с Go-контуром. C# build и installer используют файлы из `config/`.

## 2. Корневые поля

| Поле | Назначение |
|---|---|
| `schema_version` | версия структуры профиля |
| `profile` | имя, версия NX и описание |
| `scan` | корни поиска и ограничения сканирования |
| `deployment` | managed root, backup, launcher и политика записи |
| `keyboard` | сочетания клавиш |
| `radials` | совместимый общий слой radial-намерений |
| `modules` | модульные наборы команд |
| `workflow_controls` | OK, Apply, Cancel, Back |
| `performance` | кэш и watcher-параметры |
| `role_deployment` | копирование проверенной `.mtx` роли |
| `leader_key` | trigger, HUD, таймауты и последовательности |

## 3. `profile`

```json
{
  "profile": {
    "name": "NX Pro Hybrid 2512.6000",
    "nx_version": "2512.6000",
    "description": "Адаптивный профиль NXKeys"
  }
}
```

`nx_version` используется:

- при выборе установки NX;
- в имени managed root;
- в ключе кэша;
- в package manifest;
- при проверке NXOpen build-скриптами.

## 4. `scan`

```json
{
  "scan": {
    "roots": [
      "%LOCALAPPDATA%\\Siemens",
      "%APPDATA%\\Siemens"
    ],
    "install_hints": [
      "%ProgramFiles%\\Siemens\\NX2512"
    ],
    "profile_hints": [],
    "menu_extensions": [".men", ".tbr", ".rtb", ".gly", ".abr"],
    "role_extensions": [".mtx"],
    "launcher_extensions": [".bat", ".cmd", ".ps1"],
    "max_depth": 9,
    "max_files": 30000,
    "follow_symlinks": false
  }
}
```

Рекомендации:

- не добавляйте корень всего диска;
- оставляйте `follow_symlinks: false`, если нет обоснованной необходимости;
- увеличивайте `max_depth` и `max_files` только после анализа scan report;
- сетевые каталоги указывайте явно.

`NxScanner` также учитывает переменные NX:

```text
UGII_BASE_DIR
UGII_ROOT_DIR
UGII_USER_PROFILE_DIR
UGII_SITE_DIR
UGOPEN
UGII_CUSTOM_DIRECTORY_FILE
```

## 5. `deployment`

### Рекомендуемый managed-wrapper

```json
{
  "deployment": {
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
}
```

### Поля deployment

| Поле | Значение |
|---|---|
| `mode` | `managed-wrapper` или `existing-custom-dirs` |
| `managed_root` | изолированный установленный пакет |
| `backup_root` | каталог backup manifests |
| `overlay_filename` | имя generated MenuScript |
| `menuscript_version` | версия `.men`, для NX 2512 используется `139` |
| `main_menubar_id` | корневой menubar для overlay |
| `nx_executable` | точный путь `ugraf.exe`; пустое значение разрешает C# auto-discovery |
| `existing_custom_dirs_file` | явный existing `custom_dirs.dat` |
| `patch_existing_custom_dirs` | разрешение добавить managed custom root |
| `require_nx_stopped` | блокировать apply при запущенном NX |
| `clear_detected_conflicts` | генерировать очистку найденных accelerators |
| `atomic_writes` | включить temporary-file commit и локальный rollback |
| `dry_run` | не изменять файлы |

### Existing custom dirs

```json
{
  "deployment": {
    "mode": "existing-custom-dirs",
    "existing_custom_dirs_file": "D:\\NX\\custom_dirs.dat",
    "patch_existing_custom_dirs": true
  }
}
```

Путь обязателен. Неявный выбор первого найденного файла отключён.

C# `TextFileCodec` сохраняет исходную кодировку, BOM и тип строк.

## 6. `keyboard[]`

```json
{
  "shortcut": "Ctrl+4",
  "command": {
    "id": "UG_MODELING_EXTRUDED_FEATURE",
    "name": "Extrude",
    "aliases": ["Выдавливание"]
  },
  "scope": "Modeling",
  "enabled": true,
  "notes": "Основная операция"
}
```

Порядок разрешения:

1. точный `command.id`;
2. точная нормализованная метка;
3. alias;
4. token/fuzzy candidate;
5. отказ при слабом или неоднозначном результате.

Для production-профиля предпочтителен точный `BUTTON ID`.

## 7. `modules[]`

```json
{
  "id": "modeling",
  "label": "Моделирование",
  "enabled": true,
  "nx_application_ids": ["UG_APP_MODELING"],
  "switch_command": {
    "id": "UG_APP_MODELING",
    "name": "Modeling"
  },
  "leader_prefix": "M",
  "selection_priorities": [],
  "command_sets": [
    {
      "id": "primary",
      "label": "Основные",
      "commands": [
        {
          "slot": "N",
          "command": {
            "id": "UG_SKETCH_NEW",
            "name": "Sketch"
          },
          "requires_selection": false,
          "destructive": false,
          "confirm_before_execute": false,
          "fallback": ""
        }
      ]
    }
  ],
  "radials": []
}
```

Правила:

- `id` активного модуля уникален;
- один slot не повторяется в command set;
- команда содержит `id` или `name`;
- slot должен отражать одинаковую семантику в разных модулях.

## 8. `leader_key`

```json
{
  "leader_key": {
    "enabled": true,
    "trigger_key": "CapsLock",
    "hud_delay_ms": 120,
    "first_key_timeout_ms": 20000,
    "next_key_timeout_ms": 20000,
    "sticky_mode": false,
    "hud_opacity": 0.96,
    "hook_only_when_nx_active": true,
    "sequences": []
  }
}
```

Пример последовательности:

```json
{
  "sequence": "M E",
  "category": "Modeling",
  "module_id": "modeling",
  "label": "Extrude",
  "command": {
    "id": "UG_MODELING_EXTRUDED_FEATURE",
    "name": "Extrude"
  },
  "requires_selection": false,
  "destructive": false,
  "confirm_before_execute": false,
  "enabled": true
}
```

`requires_selection`, `destructive` и `confirm_before_execute` участвуют в доступности и подтверждении.

## 9. `workflow_controls`

```json
{
  "workflow_controls": {
    "accept_ok": {
      "name": "OK",
      "aliases": ["Accept", "Finish"]
    },
    "apply": {
      "name": "Apply"
    },
    "cancel": {
      "name": "Cancel",
      "aliases": ["Deselect"]
    },
    "back_previous_step": {
      "name": "Back",
      "aliases": ["Previous Step"]
    },
    "confirm_dangerous": true
  }
}
```

## 10. `performance`

```json
{
  "performance": {
    "catalog_cache_enabled": true,
    "lazy_studio_scan": true,
    "bridge_watcher": true
  }
}
```

Ключ C#-кэша включает:

- NX version;
- MenuScript file path/size/mtime;
- API CSV path/size/mtime;
- scanner schema.

После изменения API-каталога создаётся новый ключ.

## 11. `role_deployment`

```json
{
  "role_deployment": {
    "enabled": false,
    "source_mtx": "",
    "target_directory": "",
    "target_name": "NX_Pro_Hybrid_2512.mtx",
    "set_as_default": false,
    "default_role_env": "UGII_DEFAULT_ROLE"
  }
}
```

Правила:

- `.mtx` создаётся и тестируется внутри целевой версии NX;
- NXKeys копирует файл целиком;
- бинарное содержимое не анализируется и не патчится;
- target directory задаётся явно.

## 12. Переменные путей

Поддерживаются конструкции:

```text
%LOCALAPPDATA%
%APPDATA%
%ProgramFiles%
~\
```

Пути расширяются при загрузке профиля.

## 13. Dry-run и применение

Проверка без записи:

```powershell
NX2512_HotkeyStudio.exe apply `
  --config .\config\nx2512-pro-hybrid.json `
  --dry-run
```

Применение:

```powershell
NX2512_HotkeyStudio.exe apply `
  --config .\config\nx2512-pro-hybrid.json `
  --yes
```

`--yes` временно отключает `dry_run` для текущего процесса и не изменяет исходный JSON автоматически.

## 14. Package manifest

`package-manifest.json` не задаётся в профиле. Его формирует deployment engine.

В манифест попадают только файлы внутри `managed_root`. Внешний existing `custom_dirs.dat` и роль защищаются backup manifest, но не считаются частью managed package.

## 15. Проверка профиля

```powershell
NX2512_HotkeyStudio.exe validate `
  --config .\config\nx2512-pro-hybrid.json
```

Перед организационным развёртыванием дополнительно проверяются:

- точные `BUTTON ID`;
- конфликтующие accelerators;
- версия NXOpen;
- путь NX executable;
- роль и лицензия;
- команды, требующие выбор;
- опасные команды;
- rollback на тестовой машине.
