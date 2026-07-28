# Документация NXKeys

Документация описывает актуальную архитектуру NXKeys для Siemens NX / Designcenter NX 2512.

## Актуальная модель проекта

NXKeys состоит из двух уровней покрытия:

- **базовый проверенный профиль** `config/nx2512-pro-hybrid.json`: 12 прямых системных сочетаний, 14 контекстных модулей, ручные `BUTTON ID`, aliases и safety-policy;
- **полная карта** `config/full-command-map/`: 1169 намерений команд в 32 разделах с частотой `K1–K5`, русскими/английскими именами и prefix-free путями.

Полный исполняемый профиль строится локально из каталога конкретной установки NX. Команда без надёжно разрешённого `BUTTON ID` остаётся в карте, но отключается и попадает в отчёт.

## Рекомендуемый порядок чтения

1. [Корневой README](../README.md) — назначение, быстрый старт и текущие цифры.
2. [Полная карта 1169 команд](../FULL_COMMAND_MAP.md) — компиляция полного профиля и модель разрешения `BUTTON ID`.
3. [Установка](INSTALLATION.md) — базовый и полный сценарии deployment.
4. [Мнемонический язык](MNEMONIC_COMMAND_LANGUAGE.md) — грамматика путей и правила конфликтов.
5. [Конфигурация](CONFIGURATION.md) — source schema 4, runtime schema 5 и поля полного каталога.
6. [Архитектура](ARCHITECTURE.md) — компоненты и поток данных.
7. [Архитектура автоматов](STATE_MACHINE_ARCHITECTURE.md) — DFA, HFSM, guards и queue semantics.
8. [Модель безопасности](SAFETY_MODEL.md) — подтверждение, контекст и deployment-инварианты.
9. [IPC API](api.md) — фактические JSON-контракты protocol schema 3.
10. [Диагностика](TROUBLESHOOTING.md) — ошибки установки, Bridge и разрешения команд.
11. [Control Center](../NX2512_ControlCenter/README.md) — обзор, покрытие и API Explorer.
12. [Спецификация профиля](NX_PRO_HYBRID_SOURCE_SPEC.md) — базовый слой и его связь с полной картой.

## Источники истины

| Данные | Источник |
|---|---|
| Базовый профиль | `config/nx2512-pro-hybrid.json` |
| Полные 1169 намерений | `config/full-command-map/nx2512-full-command-map.json.gz.b64.part1–part3` |
| Компилятор полного профиля | `scripts/compile-full-command-map.mjs` |
| Структурная проверка полной карты | `scripts/validate-full-command-map.mjs` |
| Мнемонические правила известных ID | `NX2512_HotkeyStudio/Models/MnemonicPathGenerator.cs` |
| Runtime schema | `NX2512_HotkeyStudio/Models/*V5.cs` |
| Safety-policy | `config/nx2512-state-machines.json` |
| IPC-контракт | `NXKeys.Protocol/NxProtocol.cs` |
| Каталог конкретной NX | `NX2512_Catalog_Studio`, прежде всего `06_ui_commands_buttons.csv` |
| Runtime-проверка команд | `docs/audit/runtime-command-probe-2026-07-28.json` и новые локальные probe-отчёты |

## Версии schema

Не смешивайте версии разных уровней:

| Уровень | Версия | Пояснение |
|---|---:|---|
| Исходный профиль | `schema_version: 4` | Формат базового и сгенерированного JSON, принимаемый установщиком. |
| Runtime-модель HotkeyStudio | `5` | После загрузки добавляет/нормализует `path`, `aliases`, `action`, `selection_type` и другие поля. |
| IPC между HotkeyStudio и Bridge | `3` | Контракт `NxCommandRequest`, `NxContextSnapshot`, `NxCommandResult`. |
| Метаданные полного каталога | `full_command_catalog.schema_version: 1` | Служебная информация о генерации полного профиля. |

## Мнемоническая грамматика

```text
CapsLock → действие → объект → команда → вариант
```

Корневые действия:

```text
C Create      E Edit        T Transform   X Remove
P Process     I Inspect     V View        S Select
A Annotate    M Manage      F File        G Go
U Utilities   H Help
```

Примеры:

```text
C F E  Create Feature Extrude
T F M  Transform Feature Mirror
M L C  Manage Layer Copy
P O G  Process Operation Generate Tool Path
S F    Select Face
```

Внутренний префикс активного модуля добавляется движком автоматически. Пользователь вводит только путь после `CapsLock`.

## Интерактивная карта

Из корня репозитория:

```powershell
py -m http.server 8080
```

Откройте:

```text
http://localhost:8080/docs/command-tree.html
```

Статическая карта отображает базовый профиль и policy. Полный профиль конкретной установки можно исследовать через сгенерированный JSON, отчёт разрешения и Control Center.

## Сборка полного профиля

```powershell
.\install-full-command-profile.ps1 `
  -CatalogDir "D:\NX2512_Catalog_Output" `
  -CompileOnly
```

Результаты:

```text
config/nx2512-pro-full.generated.json
docs/generated/full-command-resolution.md
```

## Проверка

```powershell
node .\scripts\validate-command-tree.mjs
node .\scripts\validate-full-command-map.mjs
dotnet run --project .\NXKeys.StateMachines.Tests\NXKeys.StateMachines.Tests.csproj -c Release
```

Валидаторы проверяют:

- 12 базовых прямых сочетаний;
- 14 контекстных модулей;
- 1169 исходных намерений и 32 раздела;
- диапазон `K1–K5`;
- уникальность и prefix-free пути;
- отсутствие включённых команд без точного `BUTTON ID`;
- schema migration, aliases, `action` и `selection_type`;
- selection-filter routing;
- DFA/HFSM, IPC и документационные инварианты.

## Статусы команд

- **existing** — точный ID уже присутствовал в базовом профиле;
- **resolved** — ID надёжно найден в каталоге конкретной установки;
- **ambiguous** — несколько кандидатов имеют близкие оценки, команда отключена;
- **unresolved** — надёжного кандидата нет, команда отключена;
- **runtime verified** — команда дополнительно подтверждена внутри целевой NX.

## Исторические аудиты

Файлы `docs/audit/00-*`…`11-*` фиксируют состояние проекта на дату соответствующего аудита. Они полезны для трассируемости, но не заменяют актуальные README, конфигурацию и исходный код. При расхождении приоритет имеют источники истины из таблицы выше.
