# NXKeys Adaptive Control Center

`NX2512_ControlCenter` — единое адаптивное меню управления NXKeys для Siemens NX 2512.

## Возможности

- показывает Leader-команды, ранжированные для текущего модуля NX;
- учитывает наличие выбранных объектов, рабочей детали и активного диалога;
- объясняет, почему команда временно недоступна;
- отображает покрытие профиля и состояние Command Bridge;
- редактирует trigger, timeout и режим перехвата только в NX;
- запускает существующий `NX2512_HotkeyStudio` в фоновом Leader-режиме;
- ищет низкоуровневые команды по экспортам Catalog Studio:
  - `04_nxopen_members.csv`;
  - `05_nxopen_entry_points.csv`;
  - `06_ui_commands_buttons.csv`;
  - `07_ufun_functions.csv`;
  - `08_ui_command_api_candidates.csv`;
- принимает русские запросы, например `Как через NXOpen создать выдавливание?`.

## Сборка

```powershell
dotnet publish .\NX2512_ControlCenter\NX2512_ControlCenter.csproj `
  -c Release `
  -r win-x64 `
  --self-contained false `
  -o .\dist\control-center
```

## Запуск

```powershell
.\dist\control-center\NX2512_ControlCenter.exe `
  --config .\config\nx2512-pro-hybrid.json
```

Для явного указания каталога API:

```powershell
.\dist\control-center\NX2512_ControlCenter.exe `
  --config .\config\nx2512-pro-hybrid.json `
  --catalog "D:\NX2512_Full_Function_API_Catalog_YYYYMMDD_HHMMSS"
```

Также поддерживается переменная окружения `NXKEYS_CATALOG_DIR`.

## Метрики покрытия

Control Center отдельно показывает:

1. доступность Leader-последовательностей;
2. наличие конкретного `BUTTON ID`;
3. свежесть и контекст NX Bridge.

Само наличие последовательности больше не считается достаточным подтверждением работоспособности команды.
