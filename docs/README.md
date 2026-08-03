# Документация NXKeys

Этот каталог содержит эксплуатационную, разработческую и архитектурную документацию NXKeys. Стандартный **главный профиль** охватывает **885** уникальных намерений K3–K5; полный source catalog содержит 1169 намерений K1–K5.

## С чего начать

| Задача | Документ |
|---|---|
| понять назначение проекта и запустить проверки | [корневой README](../README.md) |
| подготовить локальную среду разработки | [DEVELOPMENT.md](../DEVELOPMENT.md) |
| установить или обновить NXKeys | [INSTALLATION.md](INSTALLATION.md) |
| понять компоненты и потоки данных | [ARCHITECTURE.md](ARCHITECTURE.md) |
| изучить текущую карту путей | [Интерактивная карта команд](command-tree.html) |
| изменить профиль или мнемонический путь | [CONFIGURATION.md](CONFIGURATION.md) и [MNEMONIC_COMMAND_LANGUAGE.md](MNEMONIC_COMMAND_LANGUAGE.md) и [SKETCH_INTENT_LANGUAGE.md](SKETCH_INTENT_LANGUAGE.md) |
| использовать CLI | [CLI.md](CLI.md) |
| интегрироваться с файловым IPC | [api.md](api.md) |
| диагностировать установленную систему | [OPERATIONS.md](OPERATIONS.md) и [TROUBLESHOOTING.md](TROUBLESHOOTING.md) |
| понять ограничения безопасности | [SAFETY_MODEL.md](SAFETY_MODEL.md) |
| внести изменение в репозиторий | [CONTRIBUTING.md](../CONTRIBUTING.md) |

## Канонические документы

- [Аудит документации и карта кодовой базы](DOCUMENTATION_AUDIT.md)
- [Архитектура](ARCHITECTURE.md)
- [Установка и обновление](INSTALLATION.md)
- [Конфигурация](CONFIGURATION.md)
- [CLI HotkeyStudio](CLI.md)
- [IPC API и файловая очередь](api.md)
- [Мнемонический язык](MNEMONIC_COMMAND_LANGUAGE.md)
- [Архитектура DFA/HFSM](STATE_MACHINE_ARCHITECTURE.md)
- [Модель безопасности](SAFETY_MODEL.md)
- [Эксплуатационный runbook](OPERATIONS.md)
- [Диагностика](TROUBLESHOOTING.md)
- [Спецификация профиля](NX_PRO_HYBRID_SOURCE_SPEC.md)
- [ADR](adr/README.md)

Документация отдельных компонентов:

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
- интерактивная карта [`command-tree.html`](command-tree.html), если она пересобирается генератором.

После изменения `scripts/sequence-policy.mjs`, компиляторов профиля или каталога намерений generated-документы необходимо пересоздать и проверить в diff.

## Исторические аудиты

`docs/audit/00-*` … `docs/audit/12-*`, датированные evidence JSON и `BUILD_REPORT.md` фиксируют состояние проекта на момент проведения конкретного анализа. Они полезны для трассировки решений, но не являются текущей инструкцией по установке или разработке.

При противоречии используйте следующий приоритет:

1. исполняемый код и конфигурационные валидаторы;
2. канонические документы из списка выше;
3. generated-отчёты одного и того же commit;
4. исторические аудиты.

## Источники истины

| Область | Источник |
|---|---|
| profile schema и runtime migration | `NX2512_HotkeyStudio/Models/ConfigRuntimeV5.cs` |
| поля команд | `NX2512_HotkeyStudio/Models/ModuleConfigTypesV5.cs` |
| мнемонические пути | `scripts/sequence-policy.mjs` и `MnemonicPathGenerator.cs` |
| состав K1–K5 | `config/full-command-map/` |
| IPC | `NXKeys.Protocol/NxProtocol.cs` |
| guards и state machine | `config/nx2512-state-machines.json`, `NXKeys.StateMachines/` |
| сборка и установка | `install-nxkeys.ps1` и component `build.ps1` |
| CI | `.github/workflows/*.yml` |

## Статусы утверждений

В аудиторских документах используются метки:

- **Подтверждено кодом** — утверждение непосредственно следует из исходного кода или конфигурации;
- **Предположение** — рабочая гипотеза, которую нельзя доказать доступными файлами;
- **Требует уточнения** — необходимо проверить на лицензированной целевой установке NX или согласовать с владельцем проекта.
