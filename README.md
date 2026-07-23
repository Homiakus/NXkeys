# NXKeys для Siemens NX 2512

NXKeys — русскоязычный набор C#-инструментов для настройки горячих клавиш, адаптивного Leader-меню, радиальных меню и безопасного выполнения UI-команд в Siemens NX 2512 под Windows x64.

Проект больше не содержит Go CLI/TUI. Сканирование, конфигурация, развёртывание, запуск NX, диагностика, резервное копирование и восстановление выполняются единым C#-контуром на .NET 8.

## Компоненты

| Компонент | Назначение |
|---|---|
| `NX2512_HotkeyStudio` | основной WinForms-интерфейс, CLI, Leader Engine, сканирование, deployment, запуск NX, health-check и восстановление |
| `NX2512_ControlCenter` | адаптивный русскоязычный обзор команд, контекста NX Bridge, покрытия и API-каталога |
| `NX2512_CommandBridge` | NXOpen-библиотека внутри процесса NX для выполнения точного `BUTTON ID` |
| `NX2512_Catalog_Studio` | извлечение UI-команд, NXOpen members/entry points, UFUN и кандидатного crosswalk UI → API |

Канонический профиль:

```text
config/nx2512-pro-hybrid.json
```

Целевая конфигурация профиля: **NX Pro Hybrid 2512.6000**.

> NXKeys не является продуктом Siemens. Доступность команды зависит от сборки NX, лицензии, роли, локализации, активного приложения, открытой детали и выбранных объектов.

## Текущий статус

Реализованы:

- JSON-профиль схемы v2;
- модульные наборы команд;
- горячие клавиши через MenuScript;
- Leader Key и HUD;
- Adaptive Control Center;
- NXOpen Command Bridge;
- поиск по NXOpen/UFUN-каталогу;
- транзакционное C#-развёртывание;
- SHA-256 package manifest;
- резервное копирование и автоматический rollback;
- единый C#-запуск Siemens NX;
- CI для .NET 8 и Windows x64.

## Требования

- Windows 10 или Windows 11 x64;
- Siemens NX / Designcenter NX 2512;
- .NET 8 SDK x64 для сборки;
- NXOpen DLL целевой установки для сборки `NX2512_CommandBridge`;
- права записи в `%LOCALAPPDATA%\NXKeys`.

Автоматическая загрузка и установка .NET SDK из build-скриптов отключена. Зависимости устанавливаются пользователем или администратором заранее.

## Быстрая установка

Закройте Siemens NX и выполните из корня репозитория:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\install-nx-ribbon-buttons.ps1 `
  -Clean `
  -NxRoot "C:\Program Files\Siemens\NX2512"
```

При нестандартном расположении NXOpen:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\install-nx-ribbon-buttons.ps1 `
  -Clean `
  -NxOpenDll "D:\Siemens\NX2512\NXBIN\managed\NXOpen.dll"
```

Установщик:

1. проверяет .NET 8;
2. проверяет, что NX закрыт;
3. собирает HotkeyStudio;
4. собирает CommandBridge против указанного NXOpen;
5. публикует Control Center;
6. создаёт чистый staging-набор;
7. запускает C# deployment;
8. создаёт резервную копию;
9. устанавливает файлы атомарно;
10. удаляет только устаревшие файлы, перечисленные в предыдущем манифесте;
11. проверяет SHA-256;
12. при ошибке выполняет автоматический rollback;
13. запускает `health` для установленного пакета.

## Запуск NX

После установки используйте только сгенерированную обёртку:

```text
%LOCALAPPDATA%\NXKeys\managed\NX2512.6000\launch-nx2512-with-nxkeys.cmd
```

CMD-файл является тонкой оболочкой и передаёт управление C#-команде:

```powershell
NX2512_HotkeyStudio.exe launch --config nx2512-pro-hybrid.json -- <аргументы NX>
```

C# launcher:

- разрешает абсолютный путь к `ugraf.exe`;
- проверяет существование `custom_dirs.dat`;
- запускает Leader Engine идемпотентно;
- передаёт дочернему процессу только `UGII_CUSTOM_DIRECTORY_FILE`;
- не подменяет `PATH`;
- не подменяет `UGII_USER_DIR`;
- передаёт аргументы непосредственно через `ProcessStartInfo.ArgumentList`.

## Managed-пакет

Стандартный путь:

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

`NX2512_CommandBridge.dll` устанавливается только в `custom\application`. В `custom\startup` вторая копия DLL не создаётся.

## Транзакционная установка

Основные гарантии C# deployment:

- staging формируется отдельно от рабочего пакета;
- каждый файл проверяется по SHA-256 до commit-фазы;
- перед изменением создаётся резервная копия;
- запись выполняется через временный файл в том же каталоге;
- при ошибке исходный файл восстанавливается;
- package manifest записывается последним;
- после установки все хэши проверяются повторно;
- при общей ошибке выполняется восстановление из backup manifest;
- устаревшие файлы удаляются только если принадлежали предыдущему NXKeys package manifest.

Файл:

```text
package-manifest.json
```

содержит версию пакета, целевую версию NX, относительные пути, размер, обязательность и SHA-256 каждого управляемого файла.

## Режимы развёртывания

### `managed-wrapper`

Рекомендуемый режим. NXKeys не изменяет системную установку Siemens и активируется только через собственный launcher.

```json
{
  "deployment": {
    "mode": "managed-wrapper",
    "managed_root": "%LOCALAPPDATA%\\NXKeys\\managed\\NX2512.6000"
  }
}
```

### `existing-custom-dirs`

Используется только при осознанном подключении к существующему `custom_dirs.dat`.

```json
{
  "deployment": {
    "mode": "existing-custom-dirs",
    "existing_custom_dirs_file": "D:\\NX\\custom_dirs.dat",
    "patch_existing_custom_dirs": true
  }
}
```

Путь должен быть указан явно. NXKeys больше не перебирает и не изменяет все найденные подпапки Siemens.

При добавлении пути сохраняются:

- исходная кодировка;
- BOM;
- тип окончания строк;
- существующие строки и комментарии.

## Сборка отдельных компонентов

### HotkeyStudio

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\NX2512_HotkeyStudio\build.ps1 -Clean
```

Скрипт выполняет чистый `dotnet publish` под `win-x64` и копирует канонический профиль.

### CommandBridge

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\NX2512_CommandBridge\build.ps1 `
  -NxRoot "C:\Program Files\Siemens\NX2512" `
  -Clean
```

Скрипт:

- требует .NET 8;
- ищет `NXOpen.dll` и `NXOpenUI.dll` в одной установке;
- по умолчанию требует подтверждение версии `2512` в пути;
- не копирует NXOpen DLL в distributable;
- выводит SHA-256 Bridge DLL.

### Catalog Studio

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\NX2512_Catalog_Studio\build.ps1 `
  -NxRoot "C:\Program Files\Siemens\NX2512" `
  -Clean
```

Build-скрипт не скачивает и не запускает удалённые install-скрипты.

## C# CLI

Все операционные команды предоставляет `NX2512_HotkeyStudio.exe`:

```powershell
$exe = ".\NX2512_HotkeyStudio\dist\NX2512_HotkeyStudio.exe"
$config = ".\config\nx2512-pro-hybrid.json"

& $exe validate --config $config
& $exe scan --config $config --json
& $exe catalog --config $config --query "Extrude"
& $exe plan --config $config
& $exe apply --config $config --dry-run
& $exe apply --config $config --yes
& $exe health --config $config
& $exe bridge-status --config $config
& $exe backups --config $config
& $exe restore --config $config --manifest "...\manifest.json"
& $exe launch --config $config -- -nx
```

## Leader Key

По умолчанию последовательность выглядит так:

```text
CapsLock → модуль → команда
```

Управление HUD:

| Клавиша | Действие |
|---|---|
| `Space` | поиск |
| `Tab` / `Shift+Tab` | смена модуля при недоступном контексте |
| `Backspace` | уровень назад |
| `Esc` | отмена |
| `Enter` | подтверждение опасной команды |

Adaptive Control Center ранжирует команды по:

- активному модулю;
- наличию выбора;
- рабочей детали;
- модальному диалогу;
- частоте и давности использования;
- разрушительности операции.

## NX Command Bridge

Bridge использует файловую очередь:

```text
%LOCALAPPDATA%\NXKeys\bridge\pending
%LOCALAPPDATA%\NXKeys\bridge\completed
%LOCALAPPDATA%\NXKeys\bridge\failed
%LOCALAPPDATA%\NXKeys\bridge\context.json
%LOCALAPPDATA%\NXKeys\bridge\status.json
```

Перед выполнением точного `BUTTON ID` Bridge проверяет:

- наличие кнопки;
- доступность в текущем приложении;
- чувствительность в текущем контексте;
- срок действия запроса;
- ожидаемую ревизию контекста, если она доступна.

## Покрытие 80%

«80%» означает покрытие большинства высокочастотных операций типового механического CAD-потока, а не 80% всех тысяч команд Siemens NX.

Команда считается подтверждённой только при сочетании нескольких признаков:

1. она присутствует в профиле;
2. задан или разрешён точный `BUTTON ID`;
3. команда существует в каталоге целевой версии NX;
4. Bridge может выполнить её в нужном модуле;
5. требования выбора и рабочей детали соблюдены;
6. опасные операции требуют подтверждения.

## Безопасность

NXKeys не должен:

- изменять файлы установки Siemens;
- редактировать внутренний бинарный формат `.mtx`;
- автоматически писать во все профили Siemens;
- добавлять каталоги NXKeys в глобальный `PATH`;
- подменять `UGII_USER_DIR`;
- устанавливать SDK или выполнять скачанный install-скрипт без отдельного решения пользователя;
- применять неоднозначную команду как подтверждённую.

Роль `.mtx` копируется только целиком, если она предварительно экспортирована и проверена в целевой версии NX.

## CI

Workflow `.github/workflows/ci.yml` выполняется на `windows-latest`:

- проверяет отсутствие Go-исходников и `go.mod`;
- валидирует JSON-профили;
- собирает и публикует HotkeyStudio;
- проверяет профиль через C# CLI;
- собирает и публикует Control Center;
- проверяет архитектурные инварианты launcher/deployment;
- сохраняет Windows x64 artifact.

CommandBridge не собирается в публичном CI без NXOpen DLL. Он собирается локально против конкретной установки NX.

## Документация

- [Оглавление документации](docs/README.md)
- [Установка](docs/INSTALLATION.md)
- [Конфигурация](docs/CONFIGURATION.md)
- [Архитектура](docs/ARCHITECTURE.md)
- [Модель безопасности](docs/SAFETY_MODEL.md)
- [Диагностика](docs/TROUBLESHOOTING.md)
- [Спецификация NX Pro Hybrid](docs/NX_PRO_HYBRID_SOURCE_SPEC.md)
- [Control Center](NX2512_ControlCenter/README.md)
- [Роли NX](roles/README.md)
- [Состояние сборки](BUILD_REPORT.md)

## Лицензия

MIT.
