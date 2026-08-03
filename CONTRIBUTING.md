# Внесение изменений в NXKeys

## Общие правила

- Код, конфигурационные модели и валидаторы являются источником истины.
- Не добавляйте выдуманные `BUTTON ID`, application IDs или NXOpen contracts.
- Не включайте `ambiguous` или `unresolved` команду только ради формального покрытия.
- Не редактируйте generated profile, resolution report, sequence audit или command tree без запуска соответствующего генератора.
- Не добавляйте machine-specific paths, лицензии, токены, персональные данные и внутренние каталоги заказчика.
- Изменение поведения должно сопровождаться тестом или машинно проверяемым инвариантом, когда это возможно.
- Изменение пользовательского языка команд должно сопровождаться обновлением документации в том же PR.

## Ветки и commits

Рекомендуемый формат ветки:

```text
agent/<краткое-описание>
```

Commit должен описывать одно логическое изменение. Generated-результаты допустимо включать в тот же commit, если они непосредственно получены изменённым генератором.

## Перед началом

1. Прочитайте [README.md](README.md), [подробную шпаргалку](docs/CHEATSHEET.md), [DEVELOPMENT.md](DEVELOPMENT.md) и [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).
2. Определите источник истины для изменяемой области.
3. Проверьте, не является ли файл generated или historical snapshot.
4. Зафиксируйте, требует ли изменение реальной NX 2512.
5. Для Sketch отдельно прочитайте [SKETCH_INTENT_LANGUAGE.md](docs/SKETCH_INTENT_LANGUAGE.md).

## Матрица изменений

| Изменяется | Обязательно проверить | Обязательно обновить |
|---|---|---|
| `config/full-command-map/` | full/main validators, coverage и traceability | generated profile, resolution report, command map при изменении путей |
| `scripts/sequence-policy.mjs` | main/tree validators, sequence audit, prefix-free paths, оба test runner | mnemonic/config docs и generated sequence audit |
| `MnemonicPathGenerator.cs` | state-machine tests, HotkeyStudio tests, tree/main validators, HotkeyStudio build | mnemonic language и architecture docs |
| `MnemonicPathGenerator.Sketch.cs` | Sketch regression tests, main/tree validators, pollution checks | Sketch language, шпаргалка и changelog |
| profile schema/models | config migration tests, HotkeyStudio/Control Center build | `CONFIGURATION.md`, schema references, examples |
| `NxProtocol.cs` | protocol invariant tests, Bridge contract build | `docs/api.md`, architecture и safety docs |
| Bridge execution/context | Bridge contract build и runtime test в NX | component README, API/operations/troubleshooting |
| deployment | CI deployment invariants, dry-run, health, rollback | INSTALLATION, OPERATIONS, SECURITY при необходимости |
| keyboard/HUD | state-machine tests, HotkeyStudio tests и ручной UX test | README, CLI, шпаргалка или mnemonic docs |
| документация only | ссылки, команды, пути и соответствие текущему коду | `docs/DOCUMENTATION_AUDIT.md` и changelog при существенном изменении |

## Добавление команды в полный каталог

Новая запись должна иметь:

- стабильный `intent_id`;
- исходный раздел и группу;
- частоту K1–K5;
- английское и русское название;
- подтверждённый target module;
- path hint, не нарушающий смысл корневого алфавита;
- доказательство `BUTTON ID` либо статус, при котором команда останется disabled.

После изменения:

```powershell
node .\scripts\validate-full-command-map.mjs
node .\scripts\validate-main-command-map.mjs
node .\scripts\audit-command-sequences.mjs
```

## Изменение мнемонического пути

1. Сначала определите, является путь user override, curated source или generated fallback.
2. Сохраняйте стабильное значение action root между модулями.
3. Проверяйте canonical path и все aliases на prefix conflicts.
4. Для обычных команд учитывайте частотную цель: K5 ≤ 2, K4 ≤ 3, K3 ≤ 4, если policy не изменена явно.
5. Универсальные `S*` и `G*` резервируются policy и не должны использоваться обычными командами.
6. Изменение curated mapping отражайте в `MNEMONIC_COMMAND_LANGUAGE.md`.
7. Часто используемый новый путь добавляйте в `docs/CHEATSHEET.md`.

### Исключение Sketch

Sketch не подчиняется механическому сокращению пути по K-частоте. Для него действует отдельная грамматика:

```text
действие → объект/область → операция → вариант
```

Обязательные правила:

- сохраняйте семейства `CG`, `EG`, `TG`, `CK`, `AD`, `IS`, `XG`, `MS`;
- варианты размещайте в prefix-free ветви `CGV…`;
- путь может содержать до пяти токенов;
- не переносите команду в случайный корень при коллизии;
- не восстанавливайте legacy positional aliases;
- сохраняйте `path_locked: true` и `path_source: user`;
- не добавляйте файловые, сборочные, материальные и другие чужие команды в Sketch;
- не включайте строку без точного ID.

После изменения Sketch выполните:

```powershell
node .\scripts\validate-main-command-map.mjs
node .\scripts\validate-command-tree.mjs

dotnet run --project .\NX2512_HotkeyStudio.Tests\NX2512_HotkeyStudio.Tests.csproj -c Release

dotnet build .\NX2512_HotkeyStudio\NX2512_HotkeyStudio.csproj `
  -c Release -p:Platform=x64 --nologo
```

## Изменение profile schema

- Увеличивайте schema только при несовместимом или требующем migration изменении.
- Обновляйте `CurrentSchemaVersion`, supported range, installer acceptance и CI checks согласованно.
- Добавляйте defaults/migration до validation.
- Обновляйте JSON examples и список fields.
- Проверяйте загрузку minimum supported schema и сохранение current schema.

Не оставляйте runtime-сообщения со старым номером schema.

## Изменение IPC

`NXKeys.Protocol/NxProtocol.cs` является общим source-файлом для HotkeyStudio, Bridge и tests.

При добавлении поля:

1. используйте явный `JsonPropertyName`;
2. задайте безопасное default-поведение;
3. обновите validation и round-trip tests;
4. определите совместимость со старым reader/writer;
5. обновите `docs/api.md`;
6. не передавайте profile-only metadata в Bridge без необходимости.

Повышение protocol schema требует согласованного обновления обеих сторон IPC.

## Изменение deployment

Deployment не должен:

- писать в системные файлы Siemens;
- массово изменять найденные user profiles;
- переопределять глобальный `PATH` или `UGII_USER_DIR`;
- удалять файлы вне package manifest;
- обновлять загруженную Bridge DLL без явного предупреждения;
- выполнять destructive cleanup без backup.

Для изменений deployment обязательны dry-run, повторная установка, health-check и rollback test.

## Документация

Используйте следующие статусы, когда доказательность ограничена:

- `Подтверждено кодом`;
- `Подтверждено тестом`;
- `Подтверждено CI`;
- `Предположение`;
- `Требует проверки в NX 2512`.

Команды должны быть готовы к копированию. Указывайте рабочую директорию, если она отличается от корня репозитория. Не описывайте API или flags, отсутствующие в коде.

Generated и historical docs должны иметь явную маркировку. Не копируйте большие generated-таблицы вручную в несколько документов.

### Обязательная проверка документации

- все ссылки ведут на существующие файлы;
- примеры используют текущие имена проектов и скриптов;
- версии schema/policy не противоречат коду;
- Sketch описан как отдельная грамматика, а не обычное K-сокращение;
- CLI flags подтверждены `Program.cs`;
- installer flags подтверждены `install-nxkeys.ps1`;
- generated counts не выдаются за вечные константы;
- ограничения, требующие NX workstation, отмечены явно.

## Обязательные локальные проверки

Минимальный набор для большинства изменений:

```powershell
node .\scripts\validate-main-command-map.mjs
node .\scripts\validate-command-tree.mjs
node .\scripts\validate-full-command-map.mjs

dotnet run --project .\NXKeys.StateMachines.Tests\NXKeys.StateMachines.Tests.csproj -c Release
dotnet run --project .\NX2512_HotkeyStudio.Tests\NX2512_HotkeyStudio.Tests.csproj -c Release

dotnet build .\NX2512_HotkeyStudio\NX2512_HotkeyStudio.csproj -c Release -p:Platform=x64 --nologo
dotnet build .\NX2512_ControlCenter\NX2512_ControlCenter.csproj -c Release -p:Platform=x64 --nologo
```

При изменении путей дополнительно:

```powershell
node .\scripts\audit-command-sequences.mjs
```

При изменении Bridge — contract build из [DEVELOPMENT.md](DEVELOPMENT.md).

## Pull request

Описание PR должно содержать:

- что изменено;
- почему;
- пользовательское или developer влияние;
- источники истины;
- выполненные проверки;
- какие проверки требуют NX workstation;
- generated-файлы, если они изменены;
- известные ограничения и rollback для рискованных изменений.

## Review checklist

- [ ] нет выдуманных IDs и неподтверждённых API;
- [ ] enabled-команды имеют точный ID;
- [ ] paths и aliases prefix-free;
- [ ] Sketch-команды остаются в смысловых семействах;
- [ ] user-locked пути не перезаписаны;
- [ ] destructive workflow не обходит confirmation;
- [ ] protocol/profile schema согласованы;
- [ ] документация соответствует коду;
- [ ] generated diff воспроизводим;
- [ ] secrets и персональные данные отсутствуют;
- [ ] указаны проверки, невозможные без NX.
