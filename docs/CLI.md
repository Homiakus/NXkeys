# CLI NX2512_HotkeyStudio

## Назначение

`NX2512_HotkeyStudio.exe` совмещает desktop-приложение и набор CLI-команд для проверки профиля, сканирования NX, deployment и диагностики Bridge.

Источник истины: `NX2512_HotkeyStudio/Program.cs`.

> В текущем коде не подтверждена отдельная команда `--help`. Используйте этот документ и сообщения об ошибках CLI.

## Общий синтаксис

```powershell
NX2512_HotkeyStudio.exe <command> [options]
```

Параметр конфигурации можно передать до или после команды:

```powershell
NX2512_HotkeyStudio.exe validate --config .\config\nx2512-pro-hybrid.json
NX2512_HotkeyStudio.exe --config .\config\nx2512-pro-hybrid.json validate
```

Если `--config` не задан, приложение ищет `nx2512-pro-hybrid.json` рядом с executable и в соседних `config`-каталогах.

## Команды

### `validate`

Загружает профиль, применяет defaults/migration и выполняет model validation.

```powershell
.\NX2512_HotkeyStudio.exe validate `
  --config .\nx2512-pro-hybrid.json
```

Успешный результат содержит имя профиля, число включённых basic shortcuts, модулей и Leader-команд.

Проверка не доказывает доступность `BUTTON ID` в запущенной NX.

### `scan`

Сканирует configured roots, MenuScript/role/launcher files и доступный command catalog.

```powershell
.\NX2512_HotkeyStudio.exe scan `
  --config .\nx2512-pro-hybrid.json `
  --catalog "D:\NX2512_Catalog_Output"
```

JSON-вывод:

```powershell
.\NX2512_HotkeyStudio.exe scan `
  --config .\nx2512-pro-hybrid.json `
  --catalog "D:\NX2512_Catalog_Output" `
  --json
```

Подтверждённые поля JSON: `roots`, `menu_files`, `role_files`, `launcher_files`, `commands`, `api_catalog`, `warnings`.

### `catalog`

Ищет команды в обнаруженном каталоге по строке запроса.

```powershell
.\NX2512_HotkeyStudio.exe catalog `
  --config .\nx2512-pro-hybrid.json `
  --catalog "D:\NX2512_Catalog_Output" `
  --query "extrude"
```

CLI выводит до 30 совпадений с score, `BUTTON ID`, display label и первым API candidate. Score не доказывает эквивалентность UI-команды и NXOpen API.

### `plan`

Строит deployment plan без обязательного применения.

```powershell
.\NX2512_HotkeyStudio.exe plan `
  --config .\nx2512-pro-hybrid.json `
  --catalog "D:\NX2512_Catalog_Output"
```

Вывод включает action summary и resolution report.

### `apply`

Применяет deployment plan.

Dry-run определяется профилем либо принудительно:

```powershell
.\NX2512_HotkeyStudio.exe apply `
  --config .\nx2512-pro-hybrid.json `
  --dry-run
```

Фактическое применение:

```powershell
.\NX2512_HotkeyStudio.exe apply `
  --config .\nx2512-pro-hybrid.json `
  --yes
```

Короткий эквивалент: `-y`.

Разрешить применение при запущенной NX:

```powershell
.\NX2512_HotkeyStudio.exe apply `
  --config .\nx2512-pro-hybrid.json `
  --yes `
  --allow-running-nx
```

`--allow-running-nx` не обновляет уже загруженную Bridge DLL в памяти. Новая версия начнёт работать только после перезапуска NX.

Для production рекомендуется использовать `install-nxkeys.ps1`, который формирует чистый staging-набор и выполняет post-install health-check.

### `launch`

Запускает Siemens NX через `NxRuntimeService`.

```powershell
.\NX2512_HotkeyStudio.exe launch `
  --config .\nx2512-pro-hybrid.json
```

Аргументы после `--` передаются запускаемому процессу NX:

```powershell
.\NX2512_HotkeyStudio.exe launch `
  --config .\nx2512-pro-hybrid.json `
  -- <аргументы NX>
```

Используйте только аргументы, подтверждённые документацией вашей установки NX.

### `leader`

Запускает Leader Engine в foreground до `Ctrl+C`.

```powershell
.\NX2512_HotkeyStudio.exe leader `
  --config .\nx2512-pro-hybrid.json
```

### `backups`

Показывает backup manifests из `deployment.backup_root`.

```powershell
.\NX2512_HotkeyStudio.exe backups `
  --config .\nx2512-pro-hybrid.json
```

Строка содержит timestamp, profile name и количество файлов.

### `restore`

Восстанавливает последнюю резервную копию:

```powershell
.\NX2512_HotkeyStudio.exe restore `
  --config .\nx2512-pro-hybrid.json
```

Восстановление указанного manifest:

```powershell
.\NX2512_HotkeyStudio.exe restore `
  --config .\nx2512-pro-hybrid.json `
  --manifest "C:\path\to\manifest.json"
```

Принудительное восстановление:

```powershell
.\NX2512_HotkeyStudio.exe restore `
  --config .\nx2512-pro-hybrid.json `
  --manifest "C:\path\to\manifest.json" `
  --force
```

Перед `--force` закройте NX и сохраните текущий managed package. Этот флаг ослабляет защиту восстановления и требует ручной проверки результата.

### `bridge-status`

Показывает IPC root, наличие `context.json`, возраст/модуль контекста и число файлов очереди.

```powershell
.\NX2512_HotkeyStudio.exe bridge-status `
  --config .\nx2512-pro-hybrid.json
```

Когда NX закрыт, отсутствие свежего context является ожидаемым.

### `health`

Проверяет managed package, manifest/hash, custom dirs, NX processes, Bridge status/context/log и queue counters.

```powershell
.\NX2512_HotkeyStudio.exe health `
  --config .\nx2512-pro-hybrid.json
```

Health-check может завершиться ошибкой при повреждённом package или отсутствующих required files. OFFLINE Bridge при закрытой NX сам по себе не означает повреждение установки.

### `icons` / `export-icons`

Обе команды вызывают один и тот же обработчик icon cache/export.

```powershell
.\NX2512_HotkeyStudio.exe icons `
  --config .\nx2512-pro-hybrid.json
```

Текущая реализация очищает cache и использует внешние изображения из `assets/nx-operation-icons`.

## Desktop flags

При отсутствии CLI-команды запускается desktop mode.

Подтверждённые flags:

| Flag | Назначение |
|---|---|
| `--config <path>` | профиль |
| `--background` | запуск без обязательного main window |
| `--tray` | background/tray mode |
| `--daemon` | alias background mode |
| `--ensure-background` | запустить или сигнализировать существующему instance |
| `--start` | обеспечить запуск Leader Engine |
| `--toggle` | переключить состояние Leader в существующем instance |
| `--gui` | открыть UI вместе с background mode |

HotkeyStudio использует single-instance mutex и named events. Второй процесс обычно сигнализирует уже работающему instance вместо запуска второго keyboard hook.

## Exit codes

- `0` — команда завершилась без необработанной ошибки;
- `1` — validation, IO, deployment или argument error.

Точный текст ошибки выводится в stderr как `[ERROR] ...`.

## Примеры установленного пакета

```powershell
$root = "$env:LOCALAPPDATA\NXKeys\managed\NX2512.6000"
$studio = "$root\NX2512_HotkeyStudio.exe"
$config = "$root\nx2512-pro-hybrid.json"

& $studio validate --config $config
& $studio health --config $config
& $studio bridge-status --config $config
```

## Ограничения

- CLI не заменяет integration test внутри NX.
- `catalog` возвращает candidates, а не доказанный UI→API mapping.
- `validate` проверяет profile contract, но не чувствительность кнопки.
- `apply --allow-running-nx` не делает hot reload Bridge DLL.
- Параметры без подтверждения в `Program.cs` не считаются поддерживаемыми.
