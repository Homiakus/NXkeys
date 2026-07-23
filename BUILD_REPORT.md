# NXKeys — состояние сборки и поставки

Дата актуализации: 23 июля 2026 года.

Целевая платформа: Windows x64, Siemens NX 2512, .NET 8.

## Архитектурный статус

Проект переведён на единый C#-контур.

Удалены:

- Go CLI/TUI;
- Bubble Tea и Lip Gloss;
- `go.mod`;
- `cmd/nxkeys`;
- Go-пакеты `internal/*`;
- Go build-скрипты;
- `run.cmd`.

Канонический профиль перенесён в:

```text
config/nx2512-pro-hybrid.json
```

## Реализованные компоненты

### NX2512_HotkeyStudio

- WinForms-интерфейс;
- C# CLI;
- сканирование NX;
- разрешение UI-команд;
- генерация MenuScript;
- Leader Key и HUD;
- транзакционное развёртывание;
- C# launcher Siemens NX;
- резервное копирование;
- восстановление;
- health-check;
- управление Bridge.

### NX2512_CommandBridge

- NXOpen-библиотека x64;
- загрузка в процесс NX;
- файловая очередь запросов;
- выполнение точного `BUTTON ID`;
- проверка availability и sensitivity;
- переключение приложения NX;
- completed/failed результаты;
- status/context/log.

### NX2512_ControlCenter

- адаптивный обзор Leader-команд;
- состояние Bridge;
- базовые метрики покрытия;
- настройки Leader;
- NX API Explorer;
- русский поиск по экспортированному каталогу.

### NX2512_Catalog_Studio

- инвентаризация NXOpen assemblies и namespaces;
- types/members/entry points;
- UI buttons;
- UFUN functions;
- кандидатный crosswalk UI → API.

## Deployment 0.2

Новый C# `DeploymentEngine` выполняет:

1. проверку профиля и плана;
2. проверку запущенного NX;
3. формирование точного набора файлов;
4. staging;
5. SHA-256 staging-проверку;
6. backup manifest;
7. атомарную запись;
8. удаление только собственных устаревших файлов;
9. package manifest;
10. post-install SHA-256 verification;
11. автоматический rollback при исключении.

## Package manifest

Установленный пакет содержит:

```text
package-manifest.json
```

Для каждого файла сохраняются:

- относительный путь;
- SHA-256;
- размер;
- required-флаг.

Health-check сверяет установленный пакет с этим манифестом.

## Launcher

Запуск NX выполняет C# `NxRuntimeService`.

Launcher:

- разрешает абсолютный путь NX;
- проверяет `custom_dirs.dat`;
- запускает Leader Engine;
- передаёт только `UGII_CUSTOM_DIRECTORY_FILE`;
- не изменяет `PATH`;
- не изменяет `UGII_USER_DIR`;
- использует `ProcessStartInfo.ArgumentList`.

## CommandBridge placement

Единственное место установки DLL:

```text
custom/application/NX2512_CommandBridge.dll
```

Копирование Bridge DLL в `custom/startup` удалено.

## Existing custom_dirs

Для режима `existing-custom-dirs` требуется явный путь.

Массовая запись во все каталоги Siemens удалена.

При изменении сохраняются:

- кодировка;
- BOM;
- окончания строк;
- существующее содержимое.

## Build scripts

### HotkeyStudio

- требуется .NET 8;
- всегда очищается `dist`;
- используется `dotnet publish` для `win-x64`;
- копируется канонический профиль.

### CommandBridge

- требуется .NET 8;
- проверяется наличие NXOpen и NXOpenUI;
- по умолчанию требуется соответствие пути версии NX 2512;
- в `dist` копируются только собственные Bridge artifacts;
- выводится SHA-256 DLL.

### Catalog Studio

- автоматическая установка SDK удалена;
- удалён запуск скачанного `dotnet-install.ps1`;
- требуется установленный .NET 8;
- проверяется версия NXOpen path.

### Основной installer

PowerShell формирует чистый staging-набор и передаёт установку C# deployment engine. Ручное копирование файлов в managed root удалено.

## CI

`.github/workflows/ci.yml` настроен на:

- проверку отсутствия Go-кода;
- JSON validation;
- build/publish HotkeyStudio;
- C# CLI profile validation;
- build/publish Control Center;
- проверку deployment-инвариантов;
- публикацию Windows x64 artifact.

CommandBridge не собирается в GitHub-hosted CI, потому что требует проприетарные NXOpen DLL конкретной установки.

## Необходимая проверка на рабочей станции NX

Даже после успешной общей C#-сборки необходимо отдельно подтвердить:

- сборку Bridge против фактического NXOpen 2512;
- загрузку Bridge в NX;
- отсутствие второй загруженной копии DLL;
- запуск NX через generated wrapper;
- MenuScript versions 139/170;
- каждый критичный `BUTTON ID`;
- контекст выбора и рабочей детали;
- опасные команды;
- экспортированную `.mtx` роль;
- rollback при заблокированной DLL;
- обновление при наличии старого package manifest.

## Границы безопасности

NXKeys намеренно:

- не изменяет установку Siemens;
- не патчит бинарный формат `.mtx`;
- не пишет во все найденные профили Siemens;
- не изменяет глобальный PATH;
- не устанавливает SDK автоматически;
- не удаляет файлы вне собственного package manifest;
- не считает UI → API crosswalk доказанным эквивалентом.
