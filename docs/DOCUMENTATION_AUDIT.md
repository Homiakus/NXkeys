# Аудит актуальности документации NXKeys

Дата сверки: **3 августа 2026 года**.  
Базовая реализация: `main` после объединения Sketch intent taxonomy.  
Область: пользовательская документация, developer guides, generated reference, component boundaries и GitHub Actions.

## Итог

Каноническая документация приведена к текущей архитектуре:

- profile schema 6;
- IPC schema 3;
- sequence policy v7;
- main scope K3–K5;
- полный source catalog K1–K5;
- 14 контекстных модулей;
- отдельная смысловая грамматика Sketch;
- два исполняемых C# test runner: StateMachines и HotkeyStudio;
- постоянный workflow `Sketch intent grammar`;
- managed deployment с backup, manifest, health-check и rollback.

Добавлена единая [подробная шпаргалка](CHEATSHEET.md), которая связывает пользовательские пути, Sketch, установку, CLI, диагностику и developer workflow.

## Метод сверки

Документация сопоставлена с:

- `.csproj` и component `build.ps1`;
- `install-nxkeys.ps1`;
- `ConfigRuntimeV5.cs` и `ModuleConfigTypesV5.cs`;
- `MnemonicPathGenerator.cs` и `MnemonicPathGenerator.Sketch.cs`;
- `NxProtocol.cs`;
- `NXKeys.StateMachines/` и декларативной policy;
- Node.js-компиляторами и валидаторами;
- `NX2512_HotkeyStudio.Tests`;
- `.github/workflows/*.yml`;
- bootstrap, generated profile и full intent catalog.

Название файла само по себе не считается доказательством поведения. При конфликте приоритет имеют исполняемый код и валидаторы.

## Текущая карта документов

| Документ | Аудитория | Статус | Основной источник истины |
|---|---|---|---|
| `README.md` | все | актуализирован | installer, workflows, project structure |
| `docs/CHEATSHEET.md` | пользователь, support, разработчик | создан | mnemonic policy, CLI, installation, Sketch tests |
| `DEVELOPMENT.md` | разработчик | актуализирован | `.csproj`, CI, build scripts |
| `CONTRIBUTING.md` | contributor, reviewer | актуализирован | validators, tests, architecture boundaries |
| `docs/README.md` | все | актуализирован | структура документации |
| `docs/ARCHITECTURE.md` | разработчик, reviewer | канонический | component code и data flow |
| `docs/INSTALLATION.md` | пользователь, интегратор | канонический | `install-nxkeys.ps1`, deployment engine |
| `docs/CONFIGURATION.md` | разработчик профиля | канонический | config models и compiler |
| `docs/CLI.md` | оператор, разработчик | канонический | `NX2512_HotkeyStudio/Program.cs` |
| `docs/api.md` | интегратор | канонический | `NXKeys.Protocol/NxProtocol.cs` |
| `docs/MNEMONIC_COMMAND_LANGUAGE.md` | пользователь, разработчик | generated/reference | sequence policy и runtime profile |
| `docs/SKETCH_INTENT_LANGUAGE.md` | пользователь, разработчик | актуальный | Sketch allocator и regression tests |
| `docs/STATE_MACHINE_ARCHITECTURE.md` | разработчик | канонический | state-machine code/config |
| `docs/SAFETY_MODEL.md` | reviewer, support | канонический | guards, confirmation и Bridge lifecycle |
| `docs/OPERATIONS.md` | support, administrator | канонический | health, queue, backups, deployment |
| `docs/TROUBLESHOOTING.md` | пользователь, support | канонический | runtime diagnostics |
| `docs/audit/command-sequence-audit.*` | maintainer | generated | `audit-command-sequences.mjs` |
| `docs/audit/00-*` … `12-*` | maintainer | historical | снимки старых состояний |

## Подтверждённые технические факты

| Факт | Статус |
|---|---|
| проект ориентирован на Windows x64 и .NET 8 | подтверждено project files |
| Node.js 20+ используется для profile compiler и validators | подтверждено scripts и installer |
| обязательного `npm install` нет | подтверждено структурой репозитория |
| runtime profile schema равна 6 | подтверждено config runtime |
| IPC schema равна 3 | подтверждено protocol source |
| source sequence policy равна v7 | подтверждено `sequence-policy.mjs` |
| основной installer использует K3/K4/K5 scope | подтверждено compiler/installer validators |
| Command Bridge production build требует NXOpen DLL | подтверждено build scripts |
| CI выполняет contract build Bridge без proprietary DLL | подтверждено workflow |
| Sketch использует отдельную семантическую грамматику | подтверждено кодом и HotkeyStudio tests |
| Sketch допускает пути до пяти токенов | подтверждено policy и tests |
| неоднозначные/неразрешённые команды не должны исполняться | подтверждено compiler и safety model |
| фактическая доступность каждого ID требует NX 2512 | требует workstation |

## Изменения Sketch, отражённые в документации

Каноническая документация теперь фиксирует:

- базовые пути `CGL`, `CGR`, `CGC`, `CGA`, `EGT`, `EGE`, `TGO`;
- ветвь вариантов `CGV…`;
- семейства `CG`, `EG`, `TG`, `CK`, `AD`, `IS`, `XG`, `MS`;
- исключение Sketch из механического сокращения K5/K4/K3;
- удаление legacy positional aliases;
- сохранение user-locked paths;
- запрет попадания файловых, сборочных, материальных и других чужих команд в Sketch;
- запрет включения строки без точного ID;
- обязательный HotkeyStudio regression test runner.

## Классы документации

### Каноническая

Описывает текущее поведение и редактируется вручную. При изменении кода обновляется в том же PR.

### Generated

Создаётся скриптами. Не редактируется вручную. Generated-файл считается актуальным только если:

1. создан текущей версией генератора;
2. соответствует тому же commit;
3. прошёл валидаторы;
4. его diff проверен человеком.

### Historical

Фиксирует прошлое состояние. Такой файл может содержать старые schema, counts и выводы. Он обязан быть явно отделён от текущих инструкций.

## Исправленные несоответствия

| Проблема | Решение |
|---|---|
| документация не выделяла единую точку входа | создана `CHEATSHEET.md`, обновлены README и docs index |
| contributor guide применял K-длину к Sketch без явного исключения | добавлено отдельное правило Sketch до пяти токенов |
| developer quick start не запускал HotkeyStudio regression tests | test runner добавлен в README, DEVELOPMENT и CONTRIBUTING |
| структура Sketch была описана только в одном специализированном файле | основные пути добавлены в README и шпаргалку |
| generated sequence audit оставался на policy v6 | добавлена автоматическая регенерация и проверка v7 |
| источники путей были описаны неполно | добавлен `MnemonicPathGenerator.Sketch.cs` |
| пользовательская и developer информация были смешаны | документы сгруппированы по аудитории |
| исторические аудиты выглядели равнозначными текущим инструкциям | закреплён приоритет canonical/generated/historical |

## Проверки документационного изменения

Обязательный минимум:

- относительные Markdown-ссылки разрешаются;
- README указывает на шпаргалку;
- docs index указывает на все канонические документы;
- команды копируются без исправления путей;
- `validate-command-tree.mjs` проходит;
- `validate-main-command-map.mjs` проходит;
- `NXKeys.StateMachines.Tests` проходит;
- `NX2512_HotkeyStudio.Tests` проходит;
- HotkeyStudio собирается Release/x64;
- generated sequence audit сообщает policy v7;
- workflow Sketch проверяет базовое ядро и отсутствие загрязнения контекста.

## Что нельзя подтвердить только репозиторием

Следующие проверки остаются обязательными на целевой workstation:

1. чувствительность каждого `BUTTON ID` в установленной роли;
2. наличие команды при конкретной лицензии;
3. application/module mapping корпоративной конфигурации;
4. загрузка подписанного или неподписанного Bridge согласно политике организации;
5. работа destructive-команд на реальных данных;
6. совместимость с конкретным maintenance release NX 2512.

Такие утверждения в документации помечаются как **требующие проверки в NX 2512**.

## Правило дальнейшей поддержки

Документация считается частью реализации. PR не завершён, если изменение пользовательского поведения не отражено в соответствующем документе и не имеет проверяемого примера.

| Изменение | Обновить |
|---|---|
| пользовательский путь | mnemonic reference; шпаргалка для частых команд |
| Sketch grammar | Sketch doc, tests, шпаргалка |
| profile schema | configuration/spec/migration docs |
| CLI | CLI reference и шпаргалка при операционном сценарии |
| IPC | API, architecture, safety |
| deployment | installation, operations, troubleshooting |
| generated counts | только generated reference и действительно необходимые сводки |

Последняя проверка документации должна обновлять дату этого файла и перечислять фактически выполненные автоматические проверки.
