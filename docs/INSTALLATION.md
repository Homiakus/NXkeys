# Установка и обновление NXKeys

## Требования

- Windows 10/11 x64;
- Siemens NX / Designcenter NX 2512;
- .NET 8 SDK x64;
- NXOpen DLL целевой установки;
- права записи в `%LOCALAPPDATA%\NXKeys`.

## Установка

Закройте все процессы NX и выполните из корня репозитория:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\install-nx-ribbon-buttons.ps1 `
  -Clean `
  -NxRoot "C:\Program Files\Siemens\NX2512"
```

При нестандартном пути:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\install-nx-ribbon-buttons.ps1 `
  -Clean `
  -NxOpenDll "D:\Siemens\NX2512\NXBIN\managed\NXOpen.dll"
```

Установщик:

1. проверяет .NET 8 и остановку NX;
2. собирает HotkeyStudio;
3. собирает CommandBridge против целевых NXOpen DLL;
4. публикует Control Center;
5. валидирует schema v3;
6. разрешает только 12 базовых сочетаний;
7. проверяет 14 модулей и 112 команд;
8. создаёт staging и backup;
9. атомарно устанавливает пакет;
10. проверяет SHA-256;
11. удаляет только устаревшие файлы предыдущего package manifest;
12. выполняет rollback при ошибке;
13. запускает health-check;
14. автоматически создаёт ярлыки на Рабочем столе (`Siemens NX 2512 (NXKeys).lnk`) и в Главном меню (`NXKeys`).

## Запуск

Используйте только managed launcher:

```text
%LOCALAPPDATA%\NXKeys\managed\NX2512.6000\launch-nx2512-with-nxkeys.cmd
```

Он запускает:

```powershell
NX2512_HotkeyStudio.exe launch --config nx2512-pro-hybrid.json -- <аргументы NX>
```

Launcher передаёт только `UGII_CUSTOM_DIRECTORY_FILE`, не изменяет глобальный `PATH` и не подменяет `UGII_USER_DIR`.

## Первый тест

1. Запустите NX через managed launcher.
2. Дождитесь `status=running` в `%LOCALAPPDATA%\NXKeys\bridge\context.json`.
3. Откройте Modeling и нажмите `CapsLock`.
4. Убедитесь, что HUD показывает Modeling и сетку `QWE/A·D/ZXC`.
5. Перейдите в Sketch и повторите проверку: набор должен измениться автоматически.
6. Проверьте Sheet Metal и Drafting, если приложения доступны по лицензии.
7. Сначала тестируйте команды без destructive-флага.

## Обновление

Повторите установку с `-Clean`. Deployment создаст новый backup, сравнит package manifest и удалит только ранее управляемые устаревшие файлы.

## Dry-run

```powershell
.\NX2512_HotkeyStudio\dist\NX2512_HotkeyStudio.exe apply `
  --config .\config\nx2512-pro-hybrid.json `
  --dry-run
```

## Проверка

```powershell
.\NX2512_HotkeyStudio\dist\NX2512_HotkeyStudio.exe validate --config .\config\nx2512-pro-hybrid.json
.\NX2512_HotkeyStudio\dist\NX2512_HotkeyStudio.exe health --config .\config\nx2512-pro-hybrid.json
.\NX2512_HotkeyStudio\dist\NX2512_HotkeyStudio.exe bridge-status --config .\config\nx2512-pro-hybrid.json
node .\scripts\validate-command-tree.mjs
```

## Восстановление

```powershell
.\NX2512_HotkeyStudio\dist\NX2512_HotkeyStudio.exe backups --config .\config\nx2512-pro-hybrid.json
.\NX2512_HotkeyStudio\dist\NX2512_HotkeyStudio.exe restore `
  --config .\config\nx2512-pro-hybrid.json `
  --manifest "C:\...\manifest.json"
```

Без `--force` восстановление откажется перезаписывать файлы, изменённые после deployment.

## Production-проверка Bridge

CI компилирует Bridge против контрактных NXOpen assemblies. Перед рабочим применением необходимо собрать его против DLL фактической установки и проверить целевые `BUTTON ID` внутри Siemens NX 2512.
