# NXKeys Adaptive Control Center

`NX2512_ControlCenter` — WinForms observer/diagnostic UI для current v8 profile, Command Bridge, NX context, Catalog Studio outputs и legacy coverage artifacts.

Target: .NET 8, `net8.0-windows`, x64. Проект переиспользует HotkeyStudio models через `ProjectReference`.

## Current profile

Для source запуска:

```text
config/nx2512-v8-profile.json
```

Installed profile:

```text
%LOCALAPPDATA%\NXKeys\managed\NX2512.6000\nx2512-v8-profile.json
```

Current schema — **8**.

Generated `nx2512-pro-main.generated.json` / K3–K5 metadata всё ещё можно открывать для legacy catalog coverage analysis, но это не default runtime profile.

## Возможности

- profile load/schema diagnostics;
- normalized modules/Leader paths;
- enabled operations/IDs;
- Bridge ONLINE/STALE/OFFLINE;
- active application/module;
- selection/context/security state;
- search by name/ID/path/aliases;
- запуск HotkeyStudio/Leader;
- локальная usage statistics;
- NX API Explorer по Catalog Studio outputs;
- legacy `existing/resolved/ambiguous/unresolved` coverage, когда открыт generated catalog profile.

## Запуск из репозитория

```powershell
dotnet run --project .\NX2512_ControlCenter\NX2512_ControlCenter.csproj -- `
  --config .\config\nx2512-v8-profile.json `
  --catalog "D:\NX2512_Catalog_Output"
```

`--catalog` нужен для Explorer/дополнительных данных, но не обязателен для базового runtime view.

## Installed version

```powershell
$root = "$env:LOCALAPPDATA\NXKeys\managed\NX2512.6000"
& "$root\control-center\NX2512_ControlCenter.exe" `
  --config "$root\nx2512-v8-profile.json"
```

## Bridge/security context

Control Center читает:

```text
%LOCALAPPDATA%\NXKeys\bridge\status.json
%LOCALAPPDATA%\NXKeys\bridge\context.json
```

Current context schema 4 включает не только module/selection, но и security status/session/profile digest.

- ONLINE — fresh context;
- STALE — context существует, но старый;
- OFFLINE — context/status отсутствует или NX закрыт;
- `authentication_required` — нет valid managed launch session.

`selection_count = -1` означает unknown.

## Legacy coverage metrics

Если открыт generated K3–K5 profile, могут отображаться:

```text
source intents: 1169 K1–K5
selected legacy main intents: 885 K3–K5
resolution coverage
serialized/executable rows
```

Эти числа описывают **catalog generation pipeline**, а не размер current v8 operations profile.

Не смешивайте две метрики в одном отчёте.

## NX API Explorer

При наличии Catalog Studio export используются файлы вроде:

```text
04_nxopen_members.csv
05_nxopen_entry_points.csv
06_ui_commands_buttons.csv
07_ufun_functions.csv
08_ui_command_api_candidates.csv
```

Candidate mapping не доказывает UI→API equivalence и не даёт права автоматически разрешать command.

## Сборка

```powershell
dotnet build .\NX2512_ControlCenter\NX2512_ControlCenter.csproj `
  -c Release -p:Platform=x64 --nologo
```

Publish:

```powershell
dotnet publish .\NX2512_ControlCenter\NX2512_ControlCenter.csproj `
  -c Release -r win-x64 --self-contained false -p:Platform=x64 `
  -o .\NX2512_ControlCenter\dist
```

## Диагностика

Если current runtime metrics выглядят как legacy K3–K5:

1. проверьте фактический `--config`;
2. откройте `nx2512-v8-profile.json`;
3. выполните `NX2512_HotkeyStudio.exe validate`;
4. проверьте Bridge context/security state.

Если вы намеренно анализируете K3–K5 coverage, используйте generated profile и явно помечайте отчёт как legacy/catalog analysis.

## Ограничения

- Control Center — observer, а не execution authority;
- live command effect он не подтверждает;
- usage statistics локальны;
- фактические IDs/modules зависят от NX role/license;
- live Selection Intent/dialog semantics требуют проверки внутри NX.

Current runtime contract: [`docs/RUNTIME_V8.md`](../docs/RUNTIME_V8.md).
