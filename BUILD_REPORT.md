# NXKeys — состояние сборки и поставки

Дата актуализации: 23 июля 2026 года.

Целевая платформа: Windows x64, Siemens NX 2512, .NET 8 и Go 1.25.

## Реализованные компоненты

### Go CLI/TUI

- Bubble Tea v2 и Lip Gloss v2;
- сканирование установки и профилей NX;
- парсер MenuScript;
- разрешение команд по ID, имени и aliases;
- планирование изменений;
- dry-run;
- атомарная запись;
- резервные копии SHA-256;
- контролируемое восстановление;
- managed-wrapper.

### NX2512_HotkeyStudio

- WinForms-интерфейс;
- редактирование горячих клавиш;
- Leader Key и HUD;
- radial-планы;
- поиск по каталогу команд;
- развёртывание;
- health-check;
- просмотр Bridge;
- резервные копии и профиль.

### NX2512_CommandBridge

- загрузка в процесс NX через NXOpen;
- файловая очередь запросов;
- выполнение точного `BUTTON ID`;
- проверка доступности и чувствительности кнопки;
- переключение приложения NX;
- completed/failed-результаты;
- статус, контекст и журнал.

### NX2512_Catalog_Studio

- инвентаризация сборок и пространств имён NXOpen;
- типы, члены и точки входа;
- UI-команды;
- функции UFUN;
- кандидатный crosswalk UI → API.

### NX2512_ControlCenter

- русскоязычный обзор профиля;
- состояние и свежесть Bridge;
- контекстно ранжированный список Leader-команд;
- основные настройки Leader;
- запуск HotkeyStudio/Leader;
- NX API Explorer по CSV-каталогу;
- русско-английское расширение простых запросов.

## Профили

- `config/nx2512-pro-hybrid.json`;
- `config/nx2512-ergo-80.json`;
- встроенные копии в `internal/defaults/`;
- модульные наборы для основных приложений NX;
- глобальные сочетания, Leader и radial-намерения.

## CI

Workflow `.github/workflows/ci.yml` настроен на `windows-latest` и выполняет:

```text
go test ./...
go vet ./...
go build ./cmd/nxkeys
dotnet build NX2512_HotkeyStudio
dotnet build NX2512_ControlCenter
dotnet publish NX2512_ControlCenter win-x64
```

Сборка `NX2512_CommandBridge` не включена в общий CI, поскольку требует NXOpen DLL целевой установки Siemens NX.

## Рекомендуемая локальная проверка

```powershell
go test ./...
go vet ./...
dotnet build .\NX2512_HotkeyStudio\NX2512_HotkeyStudio.csproj -c Release -p:Platform=x64
dotnet build .\NX2512_ControlCenter\NX2512_ControlCenter.csproj -c Release -p:Platform=x64
```

Проверка профиля:

```powershell
$studio = "$env:LOCALAPPDATA\NXKeys\managed\NX2512.6000\NX2512_HotkeyStudio.exe"
& $studio validate --config .\config\nx2512-pro-hybrid.json
& $studio plan --config .\config\nx2512-pro-hybrid.json
& $studio health --config .\config\nx2512-pro-hybrid.json
```

## Границы подтверждения

Наличие исходного кода и CI-конфигурации не означает, что каждая NX-команда проверена во всех ролях и лицензиях.

Отдельно требуется подтвердить:

- сборку Bridge против фактических NXOpen DLL;
- загрузку Bridge в целевую сборку NX;
- каждый критичный `BUTTON ID`;
- работу в нужном модуле;
- команды, требующие выбранный объект;
- опасные операции;
- экспортированную роль `.mtx`;
- установку в корпоративной среде.

## Известные ограничения текущей версии

1. Control Center рассчитывает базовую метрику точных `BUTTON ID`, а не полное runtime-покрытие.
2. AdaptiveLeaderPolicy используется Control Center для ранжирования; HotkeyStudio HUD сохраняет собственную логику.
3. Клиент Bridge поддерживает расширенный контекст, но текущая библиотека Bridge может не публиковать все поля выбора и рабочей детали.
4. API Explorer выполняет локальный токенизированный поиск и не генерирует готовый безопасный NXOpen-код.
5. Control Center публикуется отдельно и пока автоматически не устанавливается `install-nx-ribbon-buttons.ps1`.
6. Сопоставление UI → API является эвристическим.
7. `.mtx` копируется целиком и не редактируется.

## Модель безопасности

NXKeys намеренно:

- не изменяет установку Siemens;
- не патчит неизвестные бинарные форматы;
- исключает неоднозначные команды;
- создаёт резервные копии;
- использует управляемый wrapper;
- проверяет доступность команды внутри NX;
- сохраняет ошибки выполнения для диагностики.

Подробности: [docs/SAFETY_MODEL.md](docs/SAFETY_MODEL.md).

## Документация

Начальная страница: [docs/README.md](docs/README.md).