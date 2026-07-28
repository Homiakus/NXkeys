# Документация NXKeys

Документация относится к мнемонической адаптивной архитектуре NXKeys для Siemens NX 2512:

- 12 фиксированных базовых сочетаний Windows/NX;
- 14 контекстных модулей;
- многоуровневый язык `CapsLock → действие → объект → команда → вариант`;
- канонические пути и короткие экспертные aliases;
- явные `action` и `selection_type` для команд с выбором;
- selection-фильтры NXOpen для `UG_SEL_*` вместо запуска их как обычных кнопок;
- автоматическое назначение пути всем командам фактического каталога NX;
- автоматический выбор набора по контексту Command Bridge;
- DFA/HFSM, guards, подтверждение и транзакционная очередь.

## Рекомендуемый порядок чтения

1. [Корневой README](../README.md) — назначение и быстрый старт.
2. [Мнемонический язык](MNEMONIC_COMMAND_LANGUAGE.md) — грамматика, ветки и правила полного покрытия.
3. [Интерактивная карта](command-tree.html) — карта модулей, фильтры выбора, aliases, live-probe и путь исполнения.
4. [Конфигурация](CONFIGURATION.md) — профиль и миграция schema v4 → v5.
5. [Архитектура](ARCHITECTURE.md) — взаимодействие компонентов.
6. [Архитектура автоматов](STATE_MACHINE_ARCHITECTURE.md) — DFA, HFSM, guards и очередь.
7. [Установка](INSTALLATION.md) — сборка и managed deployment.
8. [Модель безопасности](SAFETY_MODEL.md) — обязательные инварианты.
9. [Диагностика](TROUBLESHOOTING.md) — поиск неисправностей.
10. [Спецификация профиля](NX_PRO_HYBRID_SOURCE_SPEC.md) — контекстные модули и покрытие.

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
P O G  Process Operation Generate Toolpath
S F    Select Face
```

При загрузке старого профиля `schema_version: 4` приложение назначает всем командам канонические пути, удаляет конфликтующие aliases, выводит `action` и `selection_type`, затем работает как runtime schema v5. Исходный JSON можно сохранить через Studio — тогда новые поля будут записаны явно.

## Интерактивная карта

Из корня репозитория выполните:

```powershell
py -m http.server 8080
```

Затем откройте:

```text
http://localhost:8080/docs/command-tree.html
```

Страница читает:

```text
config/nx2512-pro-hybrid.json
config/nx2512-state-machines.json
docs/audit/runtime-command-probe-2026-07-28.json
```

При открытии через `file://` JSON можно загрузить кнопками или перетащить в браузер. На странице доступны фильтры по типу действия, типу выбора и наличию alias, а вкладка **Команды и выбор** показывает `BUTTON ID`, путь, aliases, `action`, `selection_type` и последний probe-статус.

## Источники истины

Статический профиль хранит команды и точные `BUTTON ID`:

```text
config/nx2512-pro-hybrid.json
```

Правила назначения мнемонических путей находятся в:

```text
NX2512_HotkeyStudio/Models/MnemonicPathGenerator.cs
```

Политики безопасности и таймауты:

```text
config/nx2512-state-machines.json
```

Полный каталог конкретной установки формирует `NX2512_Catalog_Studio`. Команды, для которых нет точного ручного правила, получают детерминированный путь автоматически. Неизвестный или отсутствующий `BUTTON ID` не подменяется выдуманным значением.

## Проверка

```powershell
node .\scripts\validate-command-tree.mjs
```

Валидатор проверяет базовые сочетания, 14 модулей, точные сопоставления `BUTTON ID → mnemonic path`, политики безопасности, runtime schema v5 и HTML-карту.

После аудита сочетаний валидатор также проверяет, что primary-команды имеют one-key aliases, команды `UG_SEL_*` идут через `set_selection_filter`, все selection-aware команды имеют `selection_type`, а IPC-протокол содержит поле `selection_filter`.

## Статусы

- **реализовано** — функция присутствует в коде и проходит статическую проверку;
- **зависит от NX** — результат зависит от роли, лицензии, локализации или контекста;
- **кандидатное сопоставление** — UI/API-аналог требует проверки;
- **интеграционно проверено** — команда подтверждена внутри целевой сборки NX.
