# Справочник JSON-конфигурации NXKeys

JSON-профиль является единственным редактируемым источником истины. Сгенерированные файлы NX (`.men`, `.tbr`, `.rtb`), отчёты и планы не следует изменять вручную: следующее применение профиля заменит их.

## 1. Основные профили

```text
config/nx2512-pro-hybrid.json
config/nx2512-ergo-80.json
internal/defaults/nx2512-pro-hybrid.json
internal/defaults/nx2512-ergo-80.json
```

Файлы из `config/` предназначены для распространения и редактирования. Копии в `internal/defaults/` встраиваются в Go-приложение и должны оставаться синхронизированными.

## 2. Корневые поля

| Поле | Назначение |
|---|---|
| `schema_version` | версия структуры: `1` — устаревший плоский профиль, `2` — модульный профиль |
| `profile` | имя, версия NX и описание профиля |
| `scan` | корни поиска, подсказки и ограничения сканера |
| `deployment` | режим установки, пути, резервные копии и политика записи |
| `keyboard` | глобальные и контекстные сочетания клавиш |
| `radials` | устаревший общий слой радиальных меню |
| `modules` | модульные наборы команд, переключение приложений и radial-наборы |
| `workflow_controls` | общие команды OK, Apply, Cancel и Back |
| `performance` | кэш каталога, ленивое сканирование и watcher Bridge |
| `role_deployment` | необязательное копирование проверенной роли `.mtx` |
| `leader_key` | trigger, таймауты, HUD и последовательности Leader |

## 3. Миграция схемы v1 → v2

NXKeys продолжает читать схему v1. После сохранения через HotkeyStudio профиль записывается как схема v2, при этом `keyboard[]` и `radials[]` сохраняются для совместимости.

Предпочтительным источником модульного поведения являются `modules[]` и `leader_key.sequences[]`.

## 4. `profile`

```json
{
  "profile": {
    "name": "NX Pro Hybrid 2512.6000",
    "nx_version": "2512.6000",
    "description": "Модульный профиль NXKeys"
  }
}
```

Версия используется для путей управляемого пакета и кэша. Она должна соответствовать целевой установке NX.

## 5. `scan`

```json
{
  "scan": {
    "roots": [],
    "install_hints": [],
    "profile_hints": [],
    "menu_extensions": [".men", ".tbr", ".rtb", ".gly", ".abr"],
    "role_extensions": [".mtx"],
    "launcher_extensions": [".bat", ".cmd", ".ps1"],
    "max_depth": 8,
    "max_files": 25000,
    "follow_symlinks": false
  }
}
```

Не увеличивайте `max_depth` и `max_files` без необходимости: сканирование системных и сетевых дисков может быть медленным.

## 6. `keyboard[]`

```json
{
  "shortcut": "Ctrl+4",
  "command": {
    "id": "UG_MODELING_EXTRUDE",
    "name": "Extrude",
    "aliases": ["Выдавливание"]
  },
  "scope": "Modeling",
  "enabled": true,
  "notes": "Основная операция моделирования"
}
```

Порядок разрешения команды:

1. точный `command.id`;
2. точное нормализованное имя;
3. псевдоним;
4. нечёткое совпадение по токенам и расстоянию редактирования;
5. отказ, если оценка слабая или два кандидата слишком близки.

Для производственного профиля задавайте точный `BUTTON ID`. Имя и псевдонимы следует использовать для поиска и читаемости, а не как единственную гарантию выполнения.

## 7. `modules[]`

Модуль описывает команды, доступные в конкретном приложении NX.

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
  "command_sets": [],
  "radials": []
}
```

Встроенные значения охватывают:

- Modeling;
- Sketch;
- Assembly;
- Drafting;
- PMI;
- Surface;
- Sheet Metal;
- Manufacturing/CAM;
- Simulation/CAE;
- Routing;
- Mold/Tooling;
- Reuse/Templates;
- Inspect/View;
- Selection/Object.

Наличие модуля в профиле не означает наличие лицензии на соответствующее приложение NX.

### Наборы команд

```json
{
  "id": "primary",
  "label": "Основные команды",
  "slot_semantics": {
    "N": "начать или создать",
    "NE": "следующий основной шаг",
    "E": "добавить",
    "SE": "преобразовать или заменить",
    "S": "завершить или удалить",
    "SW": "убрать или уменьшить",
    "W": "структура или размножение",
    "NW": "проверка или служебная команда"
  },
  "commands": [
    {
      "slot": "N",
      "command": {
        "id": "UG_CREATE_SKETCH",
        "name": "Sketch"
      },
      "requires_selection": false,
      "destructive": false,
      "confirm_before_execute": false,
      "fallback": ""
    }
  ]
}
```

Правила:

- включённые модули должны иметь уникальный `id`;
- в одном `command_set` слот не повторяется;
- команда содержит `id` или `name`;
- направление radial ограничено `N NE E SE S SW W NW`;
- разрушительные операции помечаются явно.

## 8. `leader_key`

```json
{
  "leader_key": {
    "enabled": true,
    "trigger_key": "CapsLock",
    "hud_delay_ms": 150,
    "first_key_timeout_ms": 20000,
    "next_key_timeout_ms": 20000,
    "sticky_mode_on_double_tap": true,
    "hud_opacity": 0.95,
    "hook_only_when_nx_active": true,
    "sequences": []
  }
}
```

### Последовательность

```json
{
  "sequence": "M E",
  "category": "Modeling",
  "module_id": "modeling",
  "enabled": true,
  "command": {
    "id": "UG_MODELING_EXTRUDE",
    "name": "Extrude",
    "aliases": ["Выдавливание"]
  },
  "requires_selection": false,
  "destructive": false,
  "confirm_before_execute": false,
  "fallback": "",
  "notes": "Основное выдавливание"
}
```

Последовательности должны быть уникальными после удаления пробелов и нормализации регистра. Короткая последовательность не должна случайно полностью перекрывать другую ветку.

### Контекстная оценка

`AdaptiveLeaderPolicy` может учитывать:

- совпадение активного модуля;
- общие модули `selection_object`, `inspect_view`, `reuse`;
- требование выбранного объекта;
- активный модальный диалог;
- наличие рабочей детали;
- разрушительность;
- историю использования.

Control Center применяет эту оценку для ранжированного просмотра. Полный Leader HUD HotkeyStudio использует собственный индекс последовательностей и модульный контекст.

## 9. `workflow_controls`

```json
{
  "workflow_controls": {
    "accept_ok": {"name": "OK", "aliases": ["Accept", "Finish"]},
    "apply": {"name": "Apply"},
    "cancel": {"name": "Cancel", "aliases": ["Deselect"]},
    "back_previous_step": {"name": "Back", "aliases": ["Previous Step"]},
    "confirm_dangerous": true
  }
}
```

`Esc` отменяет, `Backspace` возвращает на предыдущий уровень, `Enter` подтверждает ожидающую опасную команду.

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

Кэш каталога хранится под `%LOCALAPPDATA%\NXKeys\cache`. При сомнениях после обновления NX или каталога удалите кэш и выполните повторное сканирование.

## 11. `deployment`

### Рекомендуемый режим `managed-wrapper`

```json
{
  "deployment": {
    "mode": "managed-wrapper",
    "require_nx_stopped": true,
    "atomic_writes": true,
    "dry_run": true,
    "clear_detected_conflicts": false
  }
}
```

Он создаёт приватный custom directory и запускает NX с отдельным `UGII_CUSTOM_DIRECTORY_FILE`.

### Режим `existing-custom-dirs`

```json
{
  "deployment": {
    "mode": "existing-custom-dirs",
    "existing_custom_dirs_file": "D:\\NX\\custom_dirs.dat",
    "patch_existing_custom_dirs": true
  }
}
```

Используйте только после проверки плана и резервной копии.

### Политика конфликтов

`clear_detected_conflicts` по умолчанию равен `false`. При включении NXKeys может вывести пустой `ACCELERATOR` для обнаруженного конфликтующего `BUTTON ID`, однако не может гарантированно найти конфликты внутри непрозрачной роли `.mtx`.

## 12. Радиальные меню

Для схемы v2 используйте `modules[].radials`. Общий `radials[]` сохранён для совместимости.

```text
N NE E SE S SW W NW
```

Автоматическое применение требует:

```json
{
  "role_deployment": {
    "enabled": true,
    "source_mtx": "roles\\nx2512-tested-role.mtx"
  }
}
```

NXKeys копирует роль целиком и не изменяет её внутреннюю структуру.

## 13. Валидация

```powershell
$studio = "$env:LOCALAPPDATA\NXKeys\managed\NX2512.6000\NX2512_HotkeyStudio.exe"
& $studio validate --config .\config\nx2512-pro-hybrid.json
& $studio plan --config .\config\nx2512-pro-hybrid.json
```

Перед `apply` устраните:

- пустые обязательные поля;
- повторяющиеся сочетания;
- повторяющиеся Leader-последовательности;
- команды без ID и имени;
- неоднозначные сопоставления;
- неправильные направления radial;
- несуществующие пути роли или custom directories.

## 14. Рекомендации по сопровождению

1. Храните эталонный профиль в Git.
2. Локальные пользовательские варианты держите вне репозитория.
3. После обновления NX повторно создавайте каталог команд.
4. Сравнивайте точные `BUTTON ID` с целевой сборкой NX.
5. Применяйте сначала на тестовом профиле и рабочей станции.
6. Не считайте непустой `BUTTON ID` доказательством доступности команды в любом контексте.