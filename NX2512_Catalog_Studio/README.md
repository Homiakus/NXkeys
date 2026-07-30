# NX 2512 Catalog Studio

## Назначение

Catalog Studio — NXOpen library с WinForms-панелью для инвентаризации конкретной установки Siemens NX 2512. Export используется profile compiler и Control Center для сопоставления intent records с фактическими UI command IDs и NXOpen candidates.

Catalog Studio не является runtime keyboard layer и не устанавливает main profile.

## Возможности

### Обзор и состав

Можно независимо включать:

- NX Open managed API;
- UI-команды и `BUTTON ID`;
- Open C / UFUN;
- кандидатное сопоставление UI-команда → API.

Доступны presets полного каталога, только NX Open API, только UI-команд и сброса выбора. Несовместимые output tables блокируются UI.

### Пути и запуск

- выбор output directory;
- отдельная папка с timestamp;
- настройка глубины сканирования;
- просмотр обнаруженных NX environment values;
- добавление user/site/group directories;
- background execution и progress;
- безопасная остановка через cancellation;
- повторный запуск без перезагрузки DLL.

### Результаты

- assemblies, namespaces, types и members;
- entry points;
- UI-команды;
- UFUN functions;
- UI→API candidates;
- Markdown summary и журнал.

Подтверждённые типы файлов, используемые проектом:

```text
04_nxopen_members.csv
05_nxopen_entry_points.csv
06_ui_commands_buttons.csv
07_ufun_functions.csv
08_ui_command_api_candidates.csv
```

Для main profile compiler ключевой файл:

```text
06_ui_commands_buttons.csv
```

UI→API candidates являются результатом поиска соответствий. Они не доказывают семантическую эквивалентность UI-команды и NXOpen API.

### JSON profiles

UI поддерживает сохранение и загрузку настроек export, включая output selection, depth и candidate thresholds. Эти profiles относятся к Catalog Studio и не являются NXKeys runtime command profile.

## Безопасность

Подтверждённое назначение Catalog Studio — чтение metadata и создание новых output files. Он не должен изменять:

- открытую деталь;
- пользовательскую роль;
- горячие клавиши;
- MenuScript-файлы;
- установку NX.

Перед публикацией export удалите machine/user paths, corporate names и другую чувствительную информацию. Не добавляйте proprietary NX binaries, license materials или закрытые role files в репозиторий.

## Требования

- Windows x64;
- .NET 8 SDK;
- Siemens NX / Designcenter NX 2512;
- `NXOpen.dll` target installation;
- PowerShell 5.1 или 7;
- для `-Sign` — найденные `SignDotNet.exe` и `NXSigningResource.res`.

## Сборка

Рабочая директория — корень репозитория:

```powershell
.\NX2512_Catalog_Studio\build.ps1 `
  -NxRoot "C:\Program Files\Siemens\NX2512" `
  -Clean
```

С точным NXOpen DLL:

```powershell
.\NX2512_Catalog_Studio\build.ps1 `
  -NxOpenDll "D:\Siemens\NX2512\NXBIN\managed\NXOpen.dll" `
  -Clean
```

Output:

```text
NX2512_Catalog_Studio\dist\NX2512_CatalogStudio.dll
```

Build script выводит SHA-256 DLL и по умолчанию требует path, подтверждающий expected version `2512`.

`-AllowVersionMismatch` используйте только для осознанного compatibility experiment. Export из другой версии нельзя автоматически считать достоверным для NX 2512.

## Подпись

```powershell
.\NX2512_Catalog_Studio\build.ps1 `
  -NxRoot "C:\Program Files\Siemens\NX2512" `
  -Sign `
  -Clean
```

`-Sign` завершится ошибкой, если signing tool/resource не найдены. Не добавляйте proprietary signing resources в version control.

## Запуск в NX

```text
File → Execute → NX Open...
```

Выберите динамически загружаемую library и откройте:

```text
NX2512_Catalog_Studio\dist\NX2512_CatalogStudio.dll
```

Точный UI path может зависеть от локализации и роли NX; подтвердите его на target workstation.

## Рекомендуемый workflow

1. Соберите DLL против target NXOpen.
2. Запустите Catalog Studio в целевой NX.
3. Создайте новый export directory.
4. Убедитесь, что `06_ui_commands_buttons.csv` существует и непуст.
5. Выполните main profile compilation:

```powershell
.\install-nxkeys.ps1 `
  -CatalogDir "D:\NX2512_Catalog_Output" `
  -CompileOnly
```

6. Изучите `docs/generated/main-profile-resolution.md`.
7. Не включайте ambiguous/unresolved commands вручную.

## Когда повторять export

- обновление NX build;
- изменение локализации;
- изменение role/profile;
- изменение лицензий;
- добавление corporate MenuScript extensions;
- перенос на другую workstation;
- неожиданная unavailable/insensitive command.

## Ограничения

- наличие UI ID не гарантирует sensitivity в текущем context;
- наличие NXOpen member не гарантирует лицензию;
- display labels зависят от локализации;
- API candidate score требует engineering review;
- Catalog Studio export является machine-specific evidence, а не универсальной спецификацией Siemens NX.
