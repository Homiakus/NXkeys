# NXKeys Adaptive Control Center

## Назначение

`NX2512_ControlCenter` — WinForms-приложение для просмотра main profile, состояния Command Bridge, активного NX context, resolution coverage и Catalog Studio outputs.

Target: .NET 8, `net8.0-windows`, x64. Проект имеет `ProjectReference` на HotkeyStudio и переиспользует его profile/runtime models.

Control Center не исполняет роль Catalog Studio, installer или Command Bridge.

## Профиль

Для source запуска используйте generated main profile:

```text
config/nx2512-pro-main.generated.json
```

В managed package тот же профиль хранится под compatibility filename:

```text
%LOCALAPPDATA%\NXKeys\managed\NX2512.6000\nx2512-pro-hybrid.json
```

Main scope:

```text
K5=69, K4=371, K3=445, selected intents=885
```

Не открывайте source bootstrap для оценки full runtime coverage.

## Возможности

- profile schema/load validation;
- selected frequencies/intents;
- 14 modules и Leader paths;
- enabled commands с IDs;
- `existing`, `resolved`, `ambiguous`, `unresolved`;
- Bridge ONLINE/STALE/OFFLINE;
- active application/module;
- selection, Work/Display Part, modal state и last result;
- поиск по name, ID, path, aliases и catalog metadata;
- запуск HotkeyStudio/Leader;
- локальная usage statistics;
- NX API Explorer по Catalog Studio outputs.

## Запуск из репозитория

Сначала создайте main profile:

```powershell
.\install-nxkeys.ps1 `
  -CatalogDir "D:\NX2512_Catalog_Output" `
  -CompileOnly
```

Затем:

```powershell
dotnet run --project .\NX2512_ControlCenter\NX2512_ControlCenter.csproj -- `
  --config .\config\nx2512-pro-main.generated.json `
  --catalog "D:\NX2512_Catalog_Output"
```

`--catalog` нужен для Explorer/дополнительных данных; profile view может работать без полного export.

## Запуск installed version

```powershell
$root = "$env:LOCALAPPDATA\NXKeys\managed\NX2512.6000"
& "$root\control-center\NX2512_ControlCenter.exe" `
  --config "$root\nx2512-pro-hybrid.json"
```

## Метрики

| Метрика | Значение |
|---|---|
| Source intents | 1169 K1–K5 |
| Selected main intents | 885 unique K3–K5 `catalog_refs` |
| Resolution coverage | existing + resolved среди selected intents/rows |
| Executable rows | enabled module rows с exact ID |
| Runtime verified | команды, проверенные в target NX |

Serialized rows могут превышать 885 из-за global duplication. `ambiguous` и `unresolved` не считаются executable.

## Bridge context

Control Center читает local IPC:

```text
%LOCALAPPDATA%\NXKeys\bridge\status.json
%LOCALAPPDATA%\NXKeys\bridge\context.json
```

- ONLINE — fresh context;
- STALE — context существует, но старый;
- OFFLINE — context/status отсутствует или NX закрыт.

`selection_count = -1` означает неизвестно, а не ноль.

## NX API Explorer

Explorer использует, если доступны:

```text
04_nxopen_members.csv
05_nxopen_entry_points.csv
06_ui_commands_buttons.csv
07_ufun_functions.csv
08_ui_command_api_candidates.csv
```

Candidate mapping не доказывает, что API call эквивалентен UI command. Не используйте его для автоматического включения ambiguous command.

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

HotkeyStudio должен собираться, поскольку Control Center ссылается на его project.

## Диагностика

Если coverage выглядит неверно:

1. убедитесь, что открыт generated main profile;
2. проверьте `full_command_catalog`;
3. сравните selected intents/frequencies;
4. проверьте stale generated policy metadata;
5. изучите resolution report;
6. пересоберите profile текущим compiler.

Если Bridge OFFLINE при открытом NX, используйте `NX2512_HotkeyStudio.exe bridge-status` и `health`.

## Ограничения

- Control Center является observer/diagnostic UI;
- он не доказывает runtime effect команды;
- usage statistics локальны и не являются официальной частотой Siemens;
- generated profile может устареть относительно source policy;
- фактические modules/IDs зависят от NX role/license/localization.
