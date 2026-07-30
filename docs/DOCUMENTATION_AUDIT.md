# Аудит документации NXKeys

Дата аудита: 30 июля 2026 года.  
Область: ветка, содержащая source sequence policy v7 и profile schema 6.

## Метод

Документация сверена с:

- `.csproj` и component `build.ps1`;
- `install-nxkeys.ps1`;
- `ConfigRuntimeV5.cs`, `ModuleConfigTypesV5.cs` и `MnemonicPathGenerator.cs`;
- `NxProtocol.cs`;
- `LeaderStateMachines.cs` и декларативной policy;
- Node.js-компиляторами и валидаторами;
- GitHub Actions workflows;
- текущими README и файлами `docs/`.

Названия файлов сами по себе не использовались как доказательство поведения.

## Карта кодовой базы

| Компонент | Назначение | Основные файлы | Зависимости | Статус документации |
|---|---|---|---|---|
| HotkeyStudio | WinForms UI, HUD, keyboard hook, CLI, profile runtime и deployment | `NX2512_HotkeyStudio/Program.cs`, `Services/`, `Models/`, `UI/` | .NET 8, Windows Forms; NXOpen references необязательны для базовой сборки | обновлено |
| Command Bridge | NXOpen DLL внутри процесса NX; контекст и исполнение команд | `NX2512_CommandBridge/Program.cs`, `.csproj`, `build.ps1` | .NET 8 x64, `NXOpen.dll`, `NXOpenUI.dll` | обновлено |
| Control Center | диагностика профиля, Bridge и разрешения команд | `NX2512_ControlCenter/ControlCenterForm.cs`, `.csproj` | HotkeyStudio project reference, .NET 8 WinForms | обновлено |
| Catalog Studio | инвентаризация NXOpen и экспорт UI command IDs | `NX2512_Catalog_Studio/CatalogStudioForm.cs`, `NX2512_FullFunctionCatalog.cs`, `build.ps1` | NXOpen целевой установки | обновлено |
| Protocol | общий JSON-контракт запросов, контекста и результатов | `NXKeys.Protocol/NxProtocol.cs` | `System.Text.Json` | обновлено в `api.md` |
| State machines | DFA последовательностей, HFSM взаимодействия и guards | `NXKeys.StateMachines/*.cs`, `config/nx2512-state-machines.json` | .NET 8 | обновлено |
| Tests | deterministic, randomized и protocol invariant tests | `NXKeys.StateMachines.Tests/*.cs` | .NET 8; собственный executable test runner | документировано |
| Profile compiler | выбор K3–K5, разрешение IDs и генерация профиля | `scripts/compile-main-command-map.mjs`, `compile-full-command-map.mjs` | Node.js 20+, без npm-зависимостей | обновлено |
| Sequence policy | универсальные `S*`, `G*`, длины путей и module cycle | `scripts/sequence-policy.mjs` | Node.js | обновлено; generated v6 отмечен как устаревший |
| Intent catalog | 1169 функций K1–K5 | `config/full-command-map/` | JSON/Markdown source records | документировано |
| Installer | компиляция, сборка, staging, deployment, health-check | `install-nxkeys.ps1` | PowerShell, Node 20+, .NET 8, NXOpen для Bridge | обновлено |
| CI | валидация профилей, тесты и C# build/publish | `.github/workflows/*.yml` | GitHub-hosted runners | документировано |
| Generated reports | resolution, sequence audit и HTML command map | `docs/generated/`, `docs/audit/command-sequence-audit.*` | создаются scripts | отделены от канонических docs |

## Подтверждённая модель проекта

- **Подтверждено кодом:** проект ориентирован на Windows x64 и .NET 8; основные UI-проекты используют WinForms.
- **Подтверждено кодом:** Node.js нужен для компиляции и валидации профилей; `install-nxkeys.ps1` требует Node.js 20+.
- **Подтверждено кодом:** в репозитории нет обязательного npm dependency install.
- **Подтверждено кодом:** bootstrap и runtime profile schema — 6; IPC schema — 3.
- **Подтверждено кодом:** главный installer принимает только K3/K4/K5 с 885 намерениями.
- **Подтверждено кодом:** source sequence policy — v7 и включает десять selection actions, в том числе `SA` и `SN`.
- **Подтверждено кодом:** Command Bridge production-build требует NXOpen DLL целевой установки; CI компилирует Bridge против contract stubs.
- **Требует уточнения:** фактическая доступность и чувствительность каждого `BUTTON ID` в конкретной лицензии NX.
- **Требует уточнения:** полный набор модулей и application IDs в корпоративных ролях заказчика.

## Проблемы исходной документации

| Приоритет | Документ/раздел | Проблема | Подтверждение из кодовой базы | Рекомендация |
|---|---|---|---|---|
| P1 | `README.md`, `CONFIGURATION.md`, `command-sequence-audit.md` | документация фиксировала sequence policy v6, тогда как source policy уже v7 | `SEQUENCE_POLICY_VERSION = 7` в `scripts/sequence-policy.mjs` | описывать source policy v7; generated audit пересоздавать, а не считать источником истины |
| P1 | несколько документов | перечислялись только восемь selection filters | policy содержит `UG_SEL_SELECT_ALL` и `UG_SEL_DESELECT_ALL` | добавить `SA` и `SN` во все канонические таблицы |
| P1 | `STATE_MACHINE_ARCHITECTURE.md`, `MNEMONIC_COMMAND_LANGUAGE.md`, `NX_PRO_HYBRID_SOURCE_SPEC.md` | упоминались source schema 4/runtime schema 5 | `Config.CurrentSchemaVersion = 6`, bootstrap имеет `schema_version: 6` | заменить на schema 6 и объяснить migration диапазона 3–6 |
| P1 | `Program.cs` runtime messages | сообщения об ошибке всё ещё говорят «schema v4» | `CurrentSchemaVersion = 6`, но строковые сообщения не обновлены | исправить код отдельным изменением; документация честно фиксирует расхождение |
| P1 | `BUILD_REPORT.md` | утверждалось, что Command Bridge не собирается в hosted CI | `ci.yml` собирает Bridge против NXOpen contract stubs | заменить на точное описание: contract build в CI, production build — только с реальными NXOpen DLL |
| P1 | старый README | не было полного developer quick start и различия между обычной C# сборкой и NXOpen production build | разные `.csproj` и `build.ps1` имеют разные требования | добавить `DEVELOPMENT.md` и component README |
| P2 | `docs/README.md` | исторические аудиты выглядели равнозначными эксплуатационным инструкциям | `docs/audit/*` содержат датированные snapshots и старые версии схем | явно разделить canonical, generated и historical docs |
| P2 | `docs/api.md` | отсутствовало предупреждение, что это файловый IPC, а не сетевой API; не было правил безопасной записи | реализация использует directories `pending/processing/completed/failed` | добавить контракт записи, ownership и at-most-once ограничения |
| P2 | документация CLI | команды были разбросаны по README и installation docs | `Program.RunCli` содержит 13 команд | создать единый `docs/CLI.md` с подтверждёнными flags |
| P2 | deployment docs | недостаточно ясно описывались `-NoBuild`, `-NoShortcut`, `-AllowRunningNX`, `-NoGlobalDuplication` | параметры присутствуют в `install-nxkeys.ps1` | документировать назначение и риски каждого параметра |
| P2 | architecture docs | не были явно показаны trust boundaries и различие source/generated/installed profile | compiler, installer и Bridge образуют отдельные границы | добавить Mermaid и текстовые потоки |
| P2 | contributing docs | отсутствовал обязательный процесс обновления validators/generated docs | workflows проверяют profile invariants | создать `CONTRIBUTING.md` с change matrix |
| P2 | operations | не было отдельного runbook для очереди, manifest, backups и locked Bridge DLL | health service, deployment и queue directories существуют в коде | создать `OPERATIONS.md` |
| P3 | терминология | смешивались `Hybrid`, `bootstrap`, `canonical`, `main` и `generated` | код использует bootstrap input и generated K3–K5 output | закрепить словарь терминов |
| P3 | нумерация INSTALLATION | после раздела 7 сразу следовал раздел 9 | редакторская ошибка | исправить структуру |

## Отдельные категории несоответствий

### Устаревшие команды

- **Не найдено подтверждения**, что отдельные старые wrapper-скрипты main/full/ribbon остаются поддерживаемыми. Канонический вход — `install-nxkeys.ps1`.
- `validate`, `health`, `bridge-status`, `scan`, `catalog`, `plan`, `apply`, `launch`, `leader`, `backups`, `restore`, `icons` подтверждены `Program.cs`.
- Параметр `--query` используется командой `catalog`, хотя generic parser отдельно перечисляет только некоторые value flags; пример проверяется по текущей реализации.

### Несуществующие файлы и ссылки

- Отсутствовали `CONTRIBUTING.md`, `DEVELOPMENT.md`, `SECURITY.md`, `CHANGELOG.md`, `docs/CLI.md`, `docs/OPERATIONS.md` и component README для трёх проектов. Они создаются этим изменением.
- Ссылки на `docs/generated/main-profile-resolution.md` допустимы только после компиляции; файл является generated output.

### Переменные окружения

Обязательных секретных переменных окружения нет.

Подтверждённые необязательные значения:

- `NXKEYS_CATALOG_DIR` — fallback для CatalogDir в HotkeyStudio `build.ps1`;
- `UGII_BASE_DIR`, `UGII_ROOT_DIR`, `UGOPEN` — подсказки поиска NXOpen;
- `UGII_CUSTOM_DIRECTORY_FILE` — launcher/deployment integration с NX.

### Несовпадения версий

- source/runtime config schema: 6;
- minimum accepted config schema: 3;
- IPC schema: 3;
- `full_command_catalog.schema_version`: 2;
- MenuScript: 139, toolbar: 170;
- source sequence policy: 7;
- checked-in sequence audit: v6 — generated artifact требует пересборки.

### Непоследовательные термины

| Было | Канонический термин |
|---|---|
| hybrid profile как рабочий профиль | bootstrap profile, если речь о `config/nx2512-pro-hybrid.json` в source tree |
| main/hybrid installed file | installed main profile с compatibility filename `nx2512-pro-hybrid.json` |
| full profile | full intent catalog, если речь о 1169 K1–K5; runtime profile собирается только для K3–K5 |
| shortcut map | mnemonic command profile |
| API | уточнять: NXOpen API, HotkeyStudio CLI или file IPC API |

## Рекомендуемое дерево документации

```text
README.md
CONTRIBUTING.md
DEVELOPMENT.md
SECURITY.md
CHANGELOG.md
BUILD_REPORT.md

docs/
├── README.md
├── DOCUMENTATION_AUDIT.md
├── ARCHITECTURE.md
├── INSTALLATION.md
├── CONFIGURATION.md
├── CLI.md
├── api.md
├── MNEMONIC_COMMAND_LANGUAGE.md
├── STATE_MACHINE_ARCHITECTURE.md
├── SAFETY_MODEL.md
├── OPERATIONS.md
├── TROUBLESHOOTING.md
├── NX_PRO_HYBRID_SOURCE_SPEC.md
├── adr/
│   ├── README.md
│   ├── 0001-profile-layers.md
│   └── 0002-file-queue-at-most-once.md
├── generated/
└── audit/
```

## Назначение документов

| Документ | Аудитория | Назначение | Источник данных | Приоритет |
|---|---|---|---|---|
| `README.md` | все | назначение, быстрый старт, навигация | installer, workflows, project files | P0 |
| `DEVELOPMENT.md` | разработчики | локальная среда, build/test workflow | `.csproj`, `build.ps1`, CI | P0 |
| `CONTRIBUTING.md` | contributors | безопасный change process | validators и architecture boundaries | P0 |
| `ARCHITECTURE.md` | разработчики, reviewers | компоненты, зависимости и data flow | код и config | P0 |
| `INSTALLATION.md` | пользователи, интеграторы | production compile/install/update | installer и deployment engine | P0 |
| `CONFIGURATION.md` | разработчики профиля | schemas, fields, overrides и generation | config models, compiler | P0 |
| `CLI.md` | разработчики, операторы | команды HotkeyStudio CLI | `Program.cs` | P1 |
| `api.md` | интеграторы | file IPC contract | `NxProtocol.cs`, Bridge | P1 |
| `OPERATIONS.md` | DevOps/SRE, support | health, logs, queue, backup, recovery | health/deployment/Bridge | P1 |
| `SECURITY.md` | security reporters | безопасное сообщение об уязвимости | repository policy | P1 |
| ADR | maintainers | причины ключевых архитектурных решений | фактическая архитектура | P2 |
| `CHANGELOG.md` | пользователи, maintainers | значимые изменения | commit history | P2 |

## Проверки человеком

Следующие утверждения нельзя окончательно подтвердить только репозиторием:

1. все указанные install paths соответствуют конкретной корпоративной установке NX;
2. каждый resolved `BUTTON ID` доступен по лицензии и корректно работает;
3. application/module mapping корректен для всех используемых ролей;
4. destructive classification покрывает все команды полного каталога;
5. `SA` и `SN` корректно поддерживаются конкретной версией NXOpen selection API;
6. подписывание Catalog Studio и Command Bridge требуется политикой конкретной организации;
7. GitHub Pages command tree пересобран из того же commit, что и source policy.

## Итог

Каноническая документация теперь строится вокруг проверяемых источников истины. Главный оставшийся технический долг — пересоздать generated main profile, sequence audit и interactive command tree после source policy v7, затем подтвердить результат на рабочей станции с NX 2512.
