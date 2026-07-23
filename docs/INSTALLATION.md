# Установка и обновление NXKeys

Документ описывает рекомендуемую установку NXKeys для Siemens NX 2512 на Windows x64 после перехода проекта на единый C#-контур.

## 1. Требования

- Windows 10 или Windows 11 x64;
- Siemens NX / Designcenter NX 2512;
- PowerShell 5.1 или PowerShell 7;
- .NET 8 SDK x64;
- доступ к `NXOpen.dll` и `NXOpenUI.dll` целевой установки;
- права записи в `%LOCALAPPDATA%\NXKeys`.

Go, Bubble Tea и Lip Gloss больше не используются.

Build-скрипты не устанавливают .NET SDK автоматически и не запускают удалённые install-скрипты.

## 2. Канонический профиль

Основной профиль находится по пути:

```text
config\nx2512-pro-hybrid.json
```

Перед установкой проверьте:

```powershell
Get-Content .\config\nx2512-pro-hybrid.json -Raw -Encoding UTF8 |
  ConvertFrom-Json |
  Out-Null
```

Ключевые поля:

```json
{
  "profile": {
    "nx_version": "2512.6000"
  },
  "deployment": {
    "mode": "managed-wrapper",
    "managed_root": "%LOCALAPPDATA%\\NXKeys\\managed\\NX2512.6000",
    "require_nx_stopped": true,
    "atomic_writes": true
  }
}
```

## 3. Рекомендуемая установка

Закройте NX и выполните:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\install-nx-ribbon-buttons.ps1 `
  -Clean `
  -NxRoot "C:\Program Files\Siemens\NX2512"
```

Для точного пути NXOpen:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\install-nx-ribbon-buttons.ps1 `
  -Clean `
  -NxOpenDll "D:\Siemens\NX2512\NXBIN\managed\NXOpen.dll"
```

Для другого профиля:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\install-nx-ribbon-buttons.ps1 `
  -ConfigPath ".\config\nx2512-ergo-80.json" `
  -NxRoot "C:\Program Files\Siemens\NX2512"
```

## 4. Что делает установщик

PowerShell отвечает только за оркестрацию сборки:

1. проверяет наличие .NET 8;
2. проверяет процессы `ugraf`, `run_nx` и подтверждённый Siemens `nx`;
3. собирает HotkeyStudio;
4. собирает CommandBridge против целевого NXOpen;
5. публикует Control Center;
6. создаёт новый временный staging-каталог;
7. копирует в staging только текущие артефакты;
8. запускает `NX2512_HotkeyStudio.exe apply`.

Все операции установки выполняет C# `DeploymentEngine`:

1. строит список управляемых файлов;
2. проверяет наличие обязательных EXE/DLL;
3. вычисляет SHA-256 staging-файлов;
4. читает предыдущий `package-manifest.json`;
5. определяет принадлежащие NXKeys устаревшие файлы;
6. создаёт резервную копию;
7. записывает файлы атомарно;
8. удаляет только файлы из предыдущего манифеста;
9. записывает новый package manifest последним;
10. повторно проверяет SHA-256;
11. при исключении восстанавливает backup manifest.

## 5. Структура установленного пакета

```text
%LOCALAPPDATA%\NXKeys\managed\NX2512.6000\
├─ NX2512_HotkeyStudio.exe
├─ NX2512_HotkeyStudio.dll
├─ NX2512_HotkeyStudio.deps.json
├─ NX2512_HotkeyStudio.runtimeconfig.json
├─ nx2512-pro-hybrid.json
├─ package-manifest.json
├─ custom_dirs.dat
├─ launch-nx2512-with-nxkeys.cmd
├─ resolution-report.md
├─ radial-menu-plan.md
├─ radial-menu-plan.json
├─ control-center\
│  └─ NX2512_ControlCenter.exe
└─ custom\
   ├─ application\
   │  ├─ NX2512_CommandBridge.dll
   │  └─ nxkeys_command_bridge.men
   └─ startup\
      ├─ nxkeys_generated.men
      ├─ nxkeys_ribbon.rtb
      ├─ nxkeys_toolbar.tbr
      ├─ launch-hotkeystudio-daemon.cmd
      └─ launch-hotkeystudio-gui.cmd
```

CommandBridge DLL не копируется в `custom\startup`.

## 6. Запуск NX

Используйте:

```powershell
& "$env:LOCALAPPDATA\NXKeys\managed\NX2512.6000\launch-nx2512-with-nxkeys.cmd"
```

Launcher вызывает C# CLI:

```powershell
NX2512_HotkeyStudio.exe launch `
  --config nx2512-pro-hybrid.json `
  -- <аргументы NX>
```

C# launcher устанавливает для дочернего процесса только:

```text
UGII_CUSTOM_DIRECTORY_FILE=<managed-root>\custom_dirs.dat
```

Он не изменяет:

- глобальные переменные Windows;
- `PATH`;
- `UGII_USER_DIR`;
- файлы установки Siemens.

## 7. Сборка HotkeyStudio отдельно

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\NX2512_HotkeyStudio\build.ps1 -Clean
```

Результат:

```text
NX2512_HotkeyStudio\dist\
```

Скрипт всегда очищает `dist` и выполняет framework-dependent publish под `win-x64`.

## 8. Сборка CommandBridge отдельно

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\NX2512_CommandBridge\build.ps1 `
  -NxRoot "C:\Program Files\Siemens\NX2512" `
  -Clean
```

При нескольких установках NX передавайте точный путь:

```powershell
-NxOpenDll "D:\Siemens\NX2512\NXBIN\managed\NXOpen.dll"
```

По умолчанию скрипт отклоняет путь, который не подтверждает версию `2512`. Параметр `-AllowVersionMismatch` допускается только после ручной проверки совместимости.

## 9. Сборка Catalog Studio

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\NX2512_Catalog_Studio\build.ps1 `
  -NxRoot "C:\Program Files\Siemens\NX2512" `
  -Clean
```

Скрипт не устанавливает SDK автоматически.

## 10. Обновление

1. Закройте NX.
2. Получите актуальную ветку `main`.
3. Повторите установку с `-Clean`.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\install-nx-ribbon-buttons.ps1 `
  -Clean `
  -NxRoot "C:\Program Files\Siemens\NX2512"
```

Устаревшие файлы удаляются только при наличии в предыдущем `package-manifest.json`.

## 11. Установка без повторной сборки

Параметр разрешён только при уже проверенных чистых `dist`-каталогах:

```powershell
.\install-nx-ribbon-buttons.ps1 -NoBuild
```

Перед использованием должны существовать:

```text
NX2512_HotkeyStudio\dist\NX2512_HotkeyStudio.exe
NX2512_CommandBridge\dist\NX2512_CommandBridge.dll
NX2512_ControlCenter\dist\NX2512_ControlCenter.exe
```

## 12. Режим existing-custom-dirs

В профиле необходимо явно задать:

```json
{
  "deployment": {
    "mode": "existing-custom-dirs",
    "existing_custom_dirs_file": "D:\\NX\\custom_dirs.dat",
    "patch_existing_custom_dirs": true
  }
}
```

Автоматический выбор первого найденного файла отключён. Массовая запись во все каталоги `%APPDATA%\Siemens\*` и `%LOCALAPPDATA%\Siemens\*` удалена.

При обновлении сохраняются кодировка, BOM и окончания строк исходного файла.

## 13. Проверка после установки

```powershell
$root = "$env:LOCALAPPDATA\NXKeys\managed\NX2512.6000"
& "$root\NX2512_HotkeyStudio.exe" health --config "$root\nx2512-pro-hybrid.json"
```

Ожидается:

```text
MenuScript versions: OK
Managed package: OK
```

При закрытом NX значение `Bridge loaded: нет` нормально.

## 14. Восстановление

Список резервных копий:

```powershell
& "$root\NX2512_HotkeyStudio.exe" backups --config "$root\nx2512-pro-hybrid.json"
```

Восстановление конкретного manifest:

```powershell
& "$root\NX2512_HotkeyStudio.exe" restore `
  --config "$root\nx2512-pro-hybrid.json" `
  --manifest "$env:LOCALAPPDATA\NXKeys\backups\<timestamp>\manifest.json"
```

`--force` применяется только после проверки, если файлы были изменены после установки.

## 15. Удаление

Закройте NX и сохраните нужные пользовательские профили. Затем удалите managed root:

```powershell
Remove-Item "$env:LOCALAPPDATA\NXKeys\managed\NX2512.6000" -Recurse -Force
```

При `managed-wrapper` установка Siemens не изменяется, поэтому дополнительных действий не требуется.
