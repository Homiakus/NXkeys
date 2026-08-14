# CLI NX2512_HotkeyStudio v8

`NX2512_HotkeyStudio.exe` совмещает desktop/tray runtime и CLI для validation, diagnostics, deployment и Bridge operations.

Источник истины: `NX2512_HotkeyStudio/Program.cs`.

## Profile resolution

Явный profile:

```powershell
NX2512_HotkeyStudio.exe validate --config .\config\nx2512-v8-profile.json
```

`--config` можно поставить до или после команды.

Если `--config` не задан, runtime ищет в таком порядке имён:

```text
nx2512-v8-profile.json
nx2512-pro-hybrid.json
```

в каталоге executable и соседних `config` locations. Если ни один файл не найден, `Config.Load` создаёт hardcoded v8 fallback.

Для production используйте installed `nx2512-v8-profile.json`, а fallback — как resilience/test path.

## Первичный лаунчер `run-nxkeys.cmd`

Для пользователей и скриптов развертывания предоставляется единый лаунчер:

```powershell
.\run-nxkeys.cmd [команда/опции]
```

Опции лаунчера:
- `--daemon` / `--minimized` — запуск в фоне (в системном трее);
- `--apply` — применить профиль и развернуть ribbon/overlay в NX;
- `--verify` — проверить валидность профиля и инвариантов;
- `--repair` — восстановить канонический профиль и развернуть в NX;
- `--profile <путь>` — указать путь к пользовательскому профилю;
- `--help` — показать справку по всем командам.

## Команды CLI HotkeyStudio

```text
validate / verify / --verify
apply / --apply
repair / --repair
scan
catalog
plan
launch
leader
backups
restore
bridge-status
health
icons / export-icons
doc-map / generate-docs
help / --help / -h
```

## `validate` / `verify` / `--verify`

```powershell
.\NX2512_HotkeyStudio.exe verify `
  --config .\nx2512-v8-profile.json
```

Загружает profile, применяет defaults/normalization и model validation. Проверка не доказывает доступность `BUTTON ID` в живой NX.

## `repair` / `--repair`

```powershell
.\NX2512_HotkeyStudio.exe repair
```

Восстанавливает канонический профиль v8 из встроенного шаблона и выполняет автоматическое развертывание в NX.

Current schema — 8; source range — 3…8.

## `scan`

```powershell
.\NX2512_HotkeyStudio.exe scan `
  --config .\nx2512-v8-profile.json `
  --catalog "D:\NX2512_Catalog_Output"
```

JSON:

```powershell
.\NX2512_HotkeyStudio.exe scan `
  --config .\nx2512-v8-profile.json `
  --catalog "D:\NX2512_Catalog_Output" `
  --json
```

Вывод содержит discovered roots, MenuScript/role/launcher counts, commands, API catalog location и warnings.

## `catalog`

```powershell
.\NX2512_HotkeyStudio.exe catalog `
  --config .\nx2512-v8-profile.json `
  --catalog "D:\NX2512_Catalog_Output" `
  --query "extrude"
```

До 30 candidates. Similarity score помогает искать, но не является доказательством UI→API equivalence.

## `plan`

```powershell
.\NX2512_HotkeyStudio.exe plan `
  --config .\nx2512-v8-profile.json `
  --catalog "D:\NX2512_Catalog_Output"
```

Строит deployment plan и resolution summary без обязательного применения.

## `apply`

Dry-run:

```powershell
.\NX2512_HotkeyStudio.exe apply `
  --config .\nx2512-v8-profile.json `
  --dry-run
```

Применение:

```powershell
.\NX2512_HotkeyStudio.exe apply `
  --config .\nx2512-v8-profile.json `
  --yes
```

`-y` — короткий эквивалент `--yes`.

Override running-NX guard:

```powershell
.\NX2512_HotkeyStudio.exe apply `
  --config .\nx2512-v8-profile.json `
  --yes `
  --allow-running-nx
```

Это **не** обновляет уже загруженную Bridge DLL. Для полноценного update закройте NX.

Для production deployment предпочтителен `install-nxkeys.ps1`.

## `launch`

```powershell
.\NX2512_HotkeyStudio.exe launch `
  --config .\nx2512-v8-profile.json
```

Это особенно важно при IPC schema 4: managed launch path создаёт authenticated session capability.

Аргументы после `--` передаются NX:

```powershell
.\NX2512_HotkeyStudio.exe launch `
  --config .\nx2512-v8-profile.json `
  -- <NX arguments>
```

Используйте только подтверждённые вашей NX installation flags.

## `leader`

Foreground Leader:

```powershell
.\NX2512_HotkeyStudio.exe leader `
  --config .\nx2512-v8-profile.json
```

Остановка — `Ctrl+C`.

## `backups`

```powershell
.\NX2512_HotkeyStudio.exe backups `
  --config .\nx2512-v8-profile.json
```

Показывает backup manifests.

## `restore`

Последний backup:

```powershell
.\NX2512_HotkeyStudio.exe restore `
  --config .\nx2512-v8-profile.json
```

Конкретный manifest:

```powershell
.\NX2512_HotkeyStudio.exe restore `
  --config .\nx2512-v8-profile.json `
  --manifest "C:\path\to\manifest.json"
```

`--force` используйте только после ручной проверки причины отказа обычного restore.

## `bridge-status`

```powershell
.\NX2512_HotkeyStudio.exe bridge-status `
  --config .\nx2512-v8-profile.json
```

Показывает Bridge root, context path/age/module/status и queue counts.

При закрытом NX отсутствие свежего context нормально.

## `health`

```powershell
.\NX2512_HotkeyStudio.exe health `
  --config .\nx2512-v8-profile.json
```

Проверяет managed package, manifest/hash, custom dirs, NX processes, Bridge status/context/log и queue counters.

`Bridge OFFLINE` при закрытом NX само по себе не означает повреждённую установку.

## `icons` / `export-icons`

```powershell
.\NX2512_HotkeyStudio.exe icons `
  --config .\nx2512-v8-profile.json
```

Обе команды используют один handler operation thumbnail export/cache.

## Desktop flags

| Flag | Назначение |
|---|---|
| `--config <path>` | выбрать profile |
| `--background` | background mode |
| `--tray` | tray/background alias |
| `--daemon` | background alias |
| `--ensure-background` | запустить или сигнализировать существующему instance |
| `--start` | обеспечить запуск Leader |
| `--toggle` | переключить Leader существующего instance |
| `--gui` | открыть UI вместе с background mode |

HotkeyStudio использует single-instance mutex + named events. Второй instance сигнализирует первому вместо создания второго keyboard hook.

## Installed package

```powershell
$root = "$env:LOCALAPPDATA\NXKeys\managed\NX2512.6000"
$studio = "$root\NX2512_HotkeyStudio.exe"
$config = "$root\nx2512-v8-profile.json"

& $studio validate --config $config
& $studio health --config $config
```

После запуска NX через managed launcher:

```powershell
& $studio bridge-status --config $config
```

## Exit codes

- `0` — CLI command завершилась без обработанной failure;
- `1` — argument/validation/IO/deployment error.

Error text выводится как `[ERROR] ...`.

## Ограничения

- CLI validation не заменяет live NX integration test;
- `catalog` выдаёт candidates;
- `apply --allow-running-nx` не делает hot reload;
- Selection Intent `0…4` — не CLI feature, а in-process Bridge behavior;
- параметры, отсутствующие в `Program.cs`, не считаются поддерживаемыми.
