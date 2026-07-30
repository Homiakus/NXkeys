# Внесение изменений в NXKeys

## Общие правила

- Код и конфигурационные валидаторы являются источником истины.
- Не добавляйте выдуманные `BUTTON ID`, application IDs или NXOpen contracts.
- Не включайте `ambiguous` или `unresolved` команду только ради формального покрытия.
- Не редактируйте generated profile, resolution report или sequence audit без запуска соответствующего генератора.
- Не добавляйте machine-specific paths, лицензии, токены, персональные данные и внутренние каталоги заказчика.
- Изменение поведения должно сопровождаться тестом или машинно проверяемым инвариантом, когда это возможно.

## Ветки и commits

Рекомендуемый формат ветки:

```text
agent/<краткое-описание>
```

Commit должен описывать одно логическое изменение. Generated-результаты допустимо включать в тот же commit, если они непосредственно получены изменённым генератором.

## Перед началом

1. Прочитайте [README.md](README.md), [DEVELOPMENT.md](DEVELOPMENT.md) и [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).
2. Определите источник истины для изменяемой области.
3. Проверьте, не является ли файл generated или historical snapshot.
4. Зафиксируйте, требует ли изменение реальной NX 2512.

## Матрица изменений

| Изменяется | Обязательно проверить | Обязательно обновить |
|---|---|---|
| `config/full-command-map/` | full/main validators, coverage 1169/885 | generated profile, resolution report, command map при изменении путей |
| `scripts/sequence-policy.mjs` | main/tree validators, sequence audit, prefix-free paths | mnemonic/config docs и generated sequence audit |
| `MnemonicPathGenerator.cs` | state-machine tests, tree/main validators, HotkeyStudio build | mnemonic language и architecture docs |
| profile schema/models | config migration tests, HotkeyStudio/Control Center build | `CONFIGURATION.md`, schema references, examples |
| `NxProtocol.cs` | protocol invariant tests, Bridge contract build | `docs/api.md`, architecture и safety docs |
| Bridge execution/context | Bridge contract build и runtime test в NX | component README, API/operations/troubleshooting |
| deployment | CI deployment invariants, dry-run, health, rollback | INSTALLATION, OPERATIONS, SECURITY при необходимости |
| keyboard/HUD | state-machine tests и ручной UX test | README, CLI или mnemonic docs |
| documentation only | ссылки, команды и соответствие текущему коду | `docs/DOCUMENTATION_AUDIT.md`, если меняется статус проблемы |

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
4. Учитывайте частотную цель: K5 ≤ 2, K4 ≤ 3, K3 ≤ 4, если текущая policy не изменена явно.
5. Универсальные `S*` и `G*` резервируются policy и не должны использоваться обычными командами.
6. `Sketch` не получает обычное module-switch menu.
7. Изменение curated mapping должно быть отражено в `MNEMONIC_COMMAND_LANGUAGE.md`.

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
- `Предположение`;
- `Требует уточнения`.

Команды должны быть готовы к копированию. Указывайте рабочую директорию, если она отличается от корня репозитория. Не описывайте API или flags, отсутствующие в коде.

Generated и historical docs должны иметь явную маркировку.

## Обязательные локальные проверки

Минимальный набор для большинства изменений:

```powershell
node .\scripts\validate-main-command-map.mjs
node .\scripts\validate-command-tree.mjs
node .\scripts\validate-full-command-map.mjs

dotnet run --project .\NXKeys.StateMachines.Tests\NXKeys.StateMachines.Tests.csproj -c Release
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
- [ ] destructive workflow не обходит confirmation;
- [ ] protocol/profile schema согласованы;
- [ ] документация соответствует коду;
- [ ] generated diff воспроизводим;
- [ ] secrets и персональные данные отсутствуют;
- [ ] указаны проверки, невозможные без NX.
