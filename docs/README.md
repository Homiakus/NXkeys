# Документация NXKeys

Этот каталог содержит пользовательскую, эксплуатационную, архитектурную и разработческую документацию NXKeys. Каноническая документация описывает текущую ветку `main`: profile schema 6, IPC schema 3, sequence policy v7 и отдельную семантическую грамматику Sketch.

## Начать отсюда

| Задача | Документ |
|---|---|
| быстро освоить ввод команд, Sketch, установку и диагностику | [CHEATSHEET.md](CHEATSHEET.md) |
| понять назначение проекта и запустить проверки | [корневой README](../README.md) |
| подготовить среду разработки | [DEVELOPMENT.md](../DEVELOPMENT.md) |
| установить или обновить NXKeys | [INSTALLATION.md](INSTALLATION.md) |
| понять компоненты и потоки данных | [ARCHITECTURE.md](ARCHITECTURE.md) |
| изучить текущую карту путей | [Интерактивная карта команд](command-tree.html) |
| изменить профиль или мнемонический путь | [CONFIGURATION.md](CONFIGURATION.md), [MNEMONIC_COMMAND_LANGUAGE.md](MNEMONIC_COMMAND_LANGUAGE.md), [SKETCH_INTENT_LANGUAGE.md](SKETCH_INTENT_LANGUAGE.md) |
| использовать CLI | [CLI.md](CLI.md) |
| интегрироваться с файловым IPC | [api.md](api.md) |
| диагностировать установленную систему | [OPERATIONS.md](OPERATIONS.md), [TROUBLESHOOTING.md](TROUBLESHOOTING.md) |
| понять ограничения безопасности | [SAFETY_MODEL.md](SAFETY_MODEL.md) |
| внести изменение в репозиторий | [CONTRIBUTING.md](../CONTRIBUTING.md) |
| проверить актуальность документации | [DOCUMENTATION_AUDIT.md](DOCUMENTATION_AUDIT.md) |

## Документы по аудитории

### Пользователь NX

- [Подробная шпаргалка](CHEATSHEET.md)
- [Мнемонический язык](MNEMONIC_COMMAND_LANGUAGE.md)
- [Язык намерений Sketch](SKETCH_INTENT_LANGUAGE.md)
- [Интерактивная карта команд](command-tree.html)
- [Установка и обновление](INSTALLATION.md)
- [Диагностика](TROUBLESHOOTING.md)

### Администратор и техническая поддержка

- [Установка и обновление](INSTALLATION.md)
- [Эксплуатационный runbook](OPERATIONS.md)
- [CLI HotkeyStudio](CLI.md)
- [Модель безопасности](SAFETY_MODEL.md)
- [IPC API и файловая очередь](api.md)

### Разработчик и reviewer

- [Локальная разработка](../DEVELOPMENT.md)
- [Внесение изменений](../CONTRIBUTING.md)
- [Архитектура](ARCHITECTURE.md)
- [Конфигурация](CONFIGURATION.md)
- [Архитектура DFA/HFSM](STATE_MACHINE_ARCHITECTURE.md)
- [Спецификация профиля](NX_PRO_HYBRID_SOURCE_SPEC.md)
- [ADR](adr/README.md)
- [Аудит документации](DOCUMENTATION_AUDIT.md)

## Канонические документы

Канонические документы должны соответствовать текущему коду и конфигурации:

- `../README.md` — назначение, быстрый старт и навигация;
- `CHEATSHEET.md` — подробная ежедневная шпаргалка;
- `../DEVELOPMENT.md` — сборка и проверки;
- `../CONTRIBUTING.md` — правила изменений;
- `ARCHITECTURE.md` — компоненты и границы;
- `INSTALLATION.md` — production compile/install/update;
- `CONFIGURATION.md` — profile schema, поля и generation;
- `CLI.md` — подтверждённые CLI-команды и flags;
- `api.md` — файловый IPC-контракт;
- `MNEMONIC_COMMAND_LANGUAGE.md` — sequence policy и карта runtime-команд;
- `SKETCH_INTENT_LANGUAGE.md` — отдельная грамматика Sketch;
- `STATE_MACHINE_ARCHITECTURE.md` — DFA/HFSM и guards;
- `SAFETY_MODEL.md` — безопасность исполнения;
- `OPERATIONS.md` — health, queue, backups и recovery;
- `TROUBLESHOOTING.md` — диагностика типовых отказов;
- `DOCUMENTATION_AUDIT.md` — дата и результат последней сверки.

Документация компонентов:

- [HotkeyStudio](../NX2512_HotkeyStudio/README.md)
- [Command Bridge](../NX2512_CommandBridge/README.md)
- [Control Center](../NX2512_ControlCenter/README.md)
- [Catalog Studio](../NX2512_Catalog_Studio/README.md)
- [Экспортированные роли](../roles/README.md)

## Generated-документы

Следующие файлы создаются скриптами и не должны редактироваться вручную:

- `generated/main-profile-resolution.md`;
- `audit/command-sequence-audit.md`;
- `audit/command-sequence-audit.json`;
- `command-tree.html`, когда карта пересобирается генератором;
- `../config/nx2512-pro-main.generated.json`.

После изменения `scripts/sequence-policy.mjs`, компиляторов профиля, `MnemonicPathGenerator*` или каталога намерений generated-документы необходимо пересоздать и проверить в diff.

## Исторические аудиты

`docs/audit/00-*` … `docs/audit/12-*`, датированные evidence JSON и старые build reports фиксируют состояние проекта на момент конкретного анализа. Они полезны для трассировки решений, но не являются текущей инструкцией.

При противоречии используйте такой приоритет:

1. исполняемый код и валидаторы;
2. канонические документы;
3. generated-отчёты, созданные тем же commit;
4. исторические аудиты.

## Источники истины

| Область | Источник |
|---|---|
| profile schema и runtime migration | `NX2512_HotkeyStudio/Models/ConfigRuntimeV5.cs` |
| поля команд | `NX2512_HotkeyStudio/Models/ModuleConfigTypesV5.cs` |
| универсальная sequence policy | `scripts/sequence-policy.mjs` |
| runtime-пути | `NX2512_HotkeyStudio/Models/MnemonicPathGenerator.cs` |
| Sketch grammar | `NX2512_HotkeyStudio/Models/MnemonicPathGenerator.Sketch.cs` |
| состав K1–K5 | `config/full-command-map/` |
| bootstrap safety/deployment | `config/nx2512-pro-hybrid.json` |
| IPC | `NXKeys.Protocol/NxProtocol.cs` |
| guards и state machine | `config/nx2512-state-machines.json`, `NXKeys.StateMachines/` |
| сборка и установка | `install-nxkeys.ps1`, component `build.ps1` |
| CI | `.github/workflows/*.yml` |
| фактические NX IDs | export Catalog Studio `06_ui_commands_buttons.csv` |

## Как поддерживать актуальность

При изменении поведения обновляйте документацию в том же PR:

| Изменение | Документация |
|---|---|
| новый пользовательский путь | `CHEATSHEET.md` при частом сценарии, `MNEMONIC_COMMAND_LANGUAGE.md` |
| изменение Sketch | `SKETCH_INTENT_LANGUAGE.md`, тесты и шпаргалка |
| profile schema | `CONFIGURATION.md`, `NX_PRO_HYBRID_SOURCE_SPEC.md`, migration notes |
| CLI или flags | `CLI.md`, при необходимости `CHEATSHEET.md` |
| IPC | `api.md`, architecture и safety docs |
| installation/deployment | `INSTALLATION.md`, `OPERATIONS.md`, `TROUBLESHOOTING.md` |
| safety/confirmation | `SAFETY_MODEL.md`, runbook и changelog |
| source/generated counts | generated reports и разделы, где число действительно необходимо |

Не копируйте большие автоматически формируемые таблицы в несколько ручных документов. Для полного списка команд используйте generated mnemonic reference и интерактивную карту.

## Статусы утверждений

В аудиторских документах используются метки:

- **Подтверждено кодом** — утверждение непосредственно следует из исходного кода или конфигурации;
- **Подтверждено тестом** — поведение зафиксировано автоматическим тестом;
- **Подтверждено CI** — проверка успешно выполняется workflow;
- **Предположение** — рабочая гипотеза, которую нельзя доказать доступными файлами;
- **Требует проверки в NX 2512** — нужна целевая лицензированная workstation.
