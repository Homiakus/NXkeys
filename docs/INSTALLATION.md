# Установка и обновление NXKeys

## Требования

### Для базового профиля

- Windows 10/11 x64;
- Siemens NX / Designcenter NX 2512;
- .NET 8 SDK x64;
- NXOpen DLL целевой установки;
- права записи в `%LOCALAPPDATA%\NXKeys`.

### Для полного профиля 1169 команд

Дополнительно:

- Node.js 20+;
- экспорт `NX2512_Catalog_Studio`;
- файл `06_ui_commands_buttons.csv` в каталоге экспорта.

## Варианты установки

| Вариант | Когда использовать | Команда |
|---|---|---|
| Базовый профиль | Быстрая установка проверенного набора | `install-nx-ribbon-buttons.ps1` |
| Полный профиль | Максимальное покрытие конкретной установки NX | `install-full-command-profile.ps1` |

## Установка базового профиля

Закройте Siemens NX и выполните из корня репозитория:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\install-nx-ribbon-buttons.ps1 `
  -Clean `
  -NxRoot "C:\Program Files\Siemens\NX2512"
```

Для Designcenter NX:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\install-nx-ribbon-buttons.ps1 `
  -Clean `
  -NxRoot "C:\Program Files\Siemens\DesigncenterNX2512"
```

При нестандартном расположении NXOpen:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\install-nx-ribbon-buttons.ps1 `
  -Clean `
  -NxOpenDll "D:\Siemens\NX2512\NXBIN\managed\NXOpen.dll"
```

## Установка полного профиля

Сначала сформируйте каталог через `NX2512_Catalog_Studio`. Затем:

```powershell
.\install-full-command-profile.ps1 `
  -CatalogDir "D:\NX2512_Catalog_Output" `
  -NxRoot "C:\Program Files\Siemens\NX2512" `
  -Clean
```

Скрипт:

1. проверяет Node.js 20+;
2. валидирует 1169 намерений и 32 раздела;
3. читает `06_ui_commands_buttons.csv`;
4. разрешает названия в реальные `BUTTON ID`;
5. сохраняет полный профиль и отчёт;
6. отключает ambiguous/unresolved-команды;
7. передаёт профиль стандартному transactional installer.

Только компиляция без установки:

```powershell
.\install-full-command-profile.ps1 `
  -CatalogDir "D:\NX2512_Catalog_Output" `
  -CompileOnly
```

Без дублирования глобальных намерений во все модули:

```powershell
.\install-full-command-profile.ps1 `
  -CatalogDir "D:\NX2512_Catalog_Output" `
  -NoGlobalDuplication `
  -CompileOnly
```

Результаты:

```text
config/nx2512-pro-full.generated.json
docs/generated/full-command-resolution.md
```

Число исполняемых строк выводится после компиляции и зависит от конкретной NX.

## Что делает стандартный установщик

1. проверяет .NET 8 и состояние процессов NX;
2. проверяет source profile schema 3–4 и runtime migration;
3. запускает структурные валидаторы;
4. собирает HotkeyStudio;
5. собирает CommandBridge против DLL целевой NX;
6. публикует Control Center;
7. строит deployment plan;
8. создаёт staging и backup;
9. проверяет SHA-256;
10. атомарно устанавливает managed package;
11. удаляет только файлы предыдущего package manifest;
12. выполняет rollback при ошибке;
13. запускает health-check;
14. при необходимости создаёт ярлыки.

## Размещение файлов

Стандартный managed root:

```text
%LOCALAPPDATA%\NXKeys\managed\NX2512.6000
```

Ключевые пути:

```text
managed-root\NX2512_HotkeyStudio.exe
managed-root\control-center\NX2512_ControlCenter.exe
managed-root\custom\startup\nxkeys_generated.men
managed-root\custom\startup\nxkeys_ribbon.rtb
managed-root\custom\startup\nxkeys_toolbar.tbr
managed-root\custom\application\NX2512_CommandBridge.dll
managed-root\custom\application\nxkeys_command_bridge.men
managed-root\nx2512-pro-hybrid.json
managed-root\package-manifest.json
```

В зависимости от package layout Bridge artifacts могут дополнительно присутствовать в управляемом startup-слое. Источником истины является `package-manifest.json`; не удаляйте и не копируйте DLL вручную.

## Запуск

Используйте managed launcher:

```text
%LOCALAPPDATA%\NXKeys\managed\NX2512.6000\launch-nx2512-with-nxkeys.cmd
```

Он запускает HotkeyStudio с активным профилем и передаёт NX отдельный `UGII_CUSTOM_DIRECTORY_FILE`. Глобальный `PATH` и `UGII_USER_DIR` не изменяются.

## Первый тест

1. Запустите NX через managed launcher.
2. Проверьте `status=running` в `%LOCALAPPDATA%\NXKeys\bridge\context.json`.
3. Откройте Modeling и нажмите `CapsLock`.
4. Проверьте имя активного модуля и доступные корневые действия.
5. Вызовите безопасную команду, например Fit или Measure.
6. Проверьте смену набора в Sketch, Drafting или Manufacturing.
7. Для полного профиля откройте `docs/generated/full-command-resolution.md` и тестируйте сначала строки `existing`/`resolved`.
8. Destructive-команды проверяйте только на копии данных.

## Dry-run

Для базового профиля:

```powershell
.\NX2512_HotkeyStudio\dist\NX2512_HotkeyStudio.exe apply `
  --config .\config\nx2512-pro-hybrid.json `
  --dry-run
```

Для полного профиля сначала выполните `-CompileOnly`, затем укажите сгенерированный JSON в `apply --dry-run`.

## Проверка

```powershell
node .\scripts\validate-command-tree.mjs
node .\scripts\validate-full-command-map.mjs

.\NX2512_HotkeyStudio\dist\NX2512_HotkeyStudio.exe validate `
  --config .\config\nx2512-pro-hybrid.json

.\NX2512_HotkeyStudio\dist\NX2512_HotkeyStudio.exe health `
  --config .\config\nx2512-pro-hybrid.json

.\NX2512_HotkeyStudio\dist\NX2512_HotkeyStudio.exe bridge-status `
  --config .\config\nx2512-pro-hybrid.json
```

## Обновление

Закройте NX и повторите установку с `-Clean`. Новый deployment:

- создаёт отдельный backup;
- сравнивает новый и предыдущий package manifest;
- заменяет только управляемые файлы;
- удаляет только устаревшие файлы предыдущего пакета;
- выполняет rollback при ошибке.

## Восстановление

```powershell
.\NX2512_HotkeyStudio\dist\NX2512_HotkeyStudio.exe backups `
  --config .\config\nx2512-pro-hybrid.json

.\NX2512_HotkeyStudio\dist\NX2512_HotkeyStudio.exe restore `
  --config .\config\nx2512-pro-hybrid.json `
  --manifest "C:\...\manifest.json"
```

Без `--force` восстановление не перезаписывает файлы, изменённые после deployment.

## Production-проверка

CI компилирует код против NXOpen contract stubs, но не заменяет тест в реальной NX. Перед эксплуатацией проверьте:

- фактическую доступность `BUTTON ID`;
- лицензии и роль;
- selection-фильтры;
- команды с собственным диалогом выбора;
- destructive confirmation;
- постпроцессоры и корпоративные расширения.
