# Конфигурация NXKeys — runtime schema v5

Канонический профиль команд:

```text
config/nx2512-pro-hybrid.json
```

Профиль schema v4 полностью совместим: при загрузке NXKeys автоматически присваивает командам многоуровневые мнемонические пути и переводит модель в runtime schema v5. После сохранения через Studio новые поля записываются в JSON явно.

## Верхний уровень

```json
{
  "schema_version": 5,
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

Секция `keyboard` содержит ровно 12 прямых привязок:

```text
Ctrl+N, Ctrl+O, Ctrl+S, Ctrl+Shift+S,
Ctrl+Z, Ctrl+Y, Ctrl+X, Ctrl+C, Ctrl+V,
Delete, Ctrl+F, F5
```

Все профессиональные команды NX вызываются через Leader.

## Мнемоническая команда

```json
{
  "command": {
    "id": "UG_MODELING_EXTRUDED_FEATURE",
    "name": "Extrude"
  },
  "path": ["C", "F", "E"],
  "path_labels": ["Create", "Feature", "Extrude"],
  "aliases": [
    ["C", "E"]
  ],
  "search_aliases": [
    "extrude",
    "вытянуть",
    "выдавливание"
  ],
  "requires_selection": false,
  "action": "execute_command",
  "selection_type": "none",
  "destructive": false,
  "confirm_before_execute": false,
  "icon_hint": "feature",
  "display_order": 2
}
```

### `path`

Канонический путь после `CapsLock`. Пользователь не вводит внутренний префикс модуля.

```text
CapsLock → C → F → E
```

Внутренний DFA получает:

```text
M C F E
```

где `M` — внутренний префикс Modeling.

### `path_labels`

Подписи уровней HUD. Количество подписей должно совпадать с количеством токенов пути.

### `aliases`

Дополнительные быстрые пути к той же команде. Alias автоматически удаляется, если:

- совпадает с другим путём;
- является префиксом другой команды;
- другая команда является его префиксом;
- совпадает с каноническим путём.

### `search_aliases`

Слова для полнотекстового поиска: русские и английские названия, профессиональные сокращения и пользовательские термины.

### `action`

Способ исполнения команды в bridge:

| Значение | Назначение |
|---|---|
| `execute_command` | Обычный вызов NX `BUTTON ID` через `InvokeMenuButtonAction`. |
| `set_selection_filter` | Установка глобального selection-фильтра NXOpen без запуска `BUTTON ID` как меню-команды. |

Команды `UG_SEL_*` должны использовать `set_selection_filter`. Это убирает зависимость от состояния кнопки меню и позволяет сначала включить нужный тип выбора, а затем открыть команду NX, которая сама ведёт интерактивный selection workflow.

### `selection_type`

Тип выбора, который нужен команде или selection-фильтру:

```text
none, all, reset, edge, face, body, component,
curve, datum, feature, operation
```

Для обычных команд поле описывает ожидаемый выбор и используется HUD/policy. Для `set_selection_filter` поле передаётся в IPC как `selection_filter`; CommandBridge переводит его в набор `NXOpen.Select.FilterMember` и применяет через global selection API.

## Автоматическая миграция

При загрузке старого профиля:

1. заполняются legacy-поля `input_key` и `display_order`;
2. для известных `BUTTON ID` применяется точное сопоставление;
3. для остальных команд определяется действие;
4. определяется объект команды;
5. выбирается значимая буква операции;
6. разрешаются коллизии внутри активного модуля;
7. конфликтующие aliases удаляются;
8. primary-команды получают one-key alias по `input_key`;
9. `UG_SEL_*` переводятся в `action: "set_selection_filter"`;
10. selection-aware команды получают `selection_type`;
11. строятся последовательности DFA;
12. schema переводится в версию 5.

Точные правила находятся в:

```text
NX2512_HotkeyStudio/Models/MnemonicPathGenerator.cs
```

## Корневые категории

| Клавиша | Категория |
|---|---|
| `C` | Create |
| `E` | Edit |
| `T` | Transform |
| `X` | Remove |
| `P` | Process |
| `I` | Inspect |
| `V` | View |
| `S` | Select |
| `A` | Annotate |
| `M` | Manage |
| `F` | File |
| `G` | Go |
| `U` | Utilities |
| `H` | Help |

## Модули

Каждый модуль сохраняет собственный внутренний `leader_prefix`. Он нужен DFA и не вводится пользователем.

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

Одинаковый пользовательский путь разрешается в разных модулях, поскольку активный модуль выбирается автоматически. Внутри одного модуля пути должны быть уникальны и не могут быть префиксами друг друга.

## Полное покрытие NX 2512

Полный состав команд зависит от:

- установленной роли NX;
- приобретённых лицензий;
- локализации;
- пользовательских MenuScript-расширений;
- доступных приложений.

Поэтому полный профиль строится из фактического каталога установки. `NX2512_Catalog_Studio` извлекает UI-команды и `BUTTON ID`. Каждая найденная команда получает:

- точный канонический путь;
- поисковые псевдонимы;
- модульную область;
- guards;
- статус доступности.

Команда без реального `BUTTON ID` не добавляется как исполняемая.

## Guards и подтверждение

Файл:

```text
config/nx2512-state-machines.json
```

задаёт:

- таймауты;
- допустимые модули;
- наличие Work/Display Part;
- минимальную достоверность контекста;
- типы и количество выбранных объектов;
- обязательное подтверждение;
- сообщения недоступности.

Политики используют новые канонические последовательности, включая внутренний префикс модуля.

Пример:

```text
M E E B
```

означает:

```text
Modeling → Edit → Edge → Blend
```

## Реализация модели

Старый `Models/ConfigModels.cs` сохранён в репозитории как reference-источник schema v4, но исключён из компиляции HotkeyStudio. Runtime schema v5 разнесена по отдельным файлам:

```text
ConfigRuntimeV5.cs
BaseConfigTypesV5.cs
ModuleConfigTypesV5.cs
RuntimeSettingsTypesV5.cs
LeaderConfigV5.cs
CommandMetadataV5.cs
```

Так миграция остаётся обратимо проверяемой, а новые поля не смешиваются с legacy-моделью.

## Проверка

```powershell
node .\scripts\validate-command-tree.mjs
```

Валидатор проверяет:

- 12 базовых сочетаний;
- 14 модулей;
- наличие точных `BUTTON ID`;
- точные мнемонические сопоставления;
- наличие aliases для primary-команд;
- корректную маршрутизацию selection-фильтров;
- наличие `selection_type` у команд с выбором;
- соответствие policy новым последовательностям;
- наличие runtime schema v5;
- исключение legacy-модели из компиляции;
- целостность интерактивной карты.
