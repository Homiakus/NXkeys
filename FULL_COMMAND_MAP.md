# Полная карта команд Siemens NX 2512

NXKeys содержит полный слой из **1169 намерений команд** в **32 разделах**. Компилятор превращает этот слой в безопасный профиль конкретной установки Siemens NX 2512.

## Почему используется компиляция

Иерархический список задаёт:

- функцию;
- раздел и группу;
- английское и русское название;
- частоту `K1–K5`;
- целевой модуль;
- удобный мнемонический путь.

Но список не содержит гарантированных `BUTTON ID`. Их состав зависит от:

- сборки NX;
- лицензий;
- активной роли;
- локализации;
- Teamcenter/Managed Mode;
- корпоративных MenuScript-расширений.

Поэтому карта разделена на два слоя:

1. **Intent layer:** все 1169 функций всегда присутствуют и имеют путь.
2. **Executable layer:** точные ID разрешаются по каталогу конкретной NX; ambiguous/unresolved-команды отключаются.

## Файлы

```text
config/full-command-map/
├── nx2512-full-command-map.json.gz.b64.part1
├── nx2512-full-command-map.json.gz.b64.part2
└── nx2512-full-command-map.json.gz.b64.part3

scripts/compile-full-command-map.mjs
scripts/validate-full-command-map.mjs
install-full-command-profile.ps1
```

Снимок хранится в трёх частях только для удобства GitHub API. Компилятор объединяет, распаковывает и проверяет их автоматически.

## Состав записи

Для каждой команды сохранены:

```text
intent_id
source_index
runtime_module
frequency
source_module
group
name_en
name_ru
path
```

Составные имена сохраняются без повреждения: `DXF/DWG`, `Zoom In/Out`, `Arc/Circle`, `Faces/Edges` и подобные.

## Требования

- Node.js 20+;
- базовый профиль `config/nx2512-pro-hybrid.json`;
- экспорт `NX2512_Catalog_Studio`;
- `06_ui_commands_buttons.csv`;
- для установки — .NET 8 SDK и NXOpen DLL целевой NX.

## Быстрый запуск

```powershell
.\install-full-command-profile.ps1 `
  -CatalogDir "D:\NX2512_Catalog_Output" `
  -NxRoot "C:\Program Files\Siemens\NX2512" `
  -Clean
```

Только компиляция:

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

## Ручная компиляция

```powershell
node .\scripts\compile-full-command-map.mjs `
  --profile .\config\nx2512-pro-hybrid.json `
  --intents .\config\full-command-map `
  --catalog-dir "D:\NX2512_Catalog_Output" `
  --probe .\docs\audit\runtime-command-probe-2026-07-28.json `
  --out .\config\nx2512-pro-full.generated.json `
  --report .\docs\generated\full-command-resolution.md
```

Опции:

| Опция | Назначение |
|---|---|
| `--profile` | Базовый профиль и известные IDs. |
| `--intents` | Каталог 1169 намерений. |
| `--catalog-dir` | Экспорт конкретной NX. |
| `--probe` | Дополнительные runtime evidence. |
| `--out` | Сгенерированный JSON. |
| `--report` | Markdown-отчёт разрешения. |
| `--no-global-duplication` | Не дублировать общие команды во все активные модули. |

## Алгоритм разрешения

1. Загружается базовый профиль.
2. Из базового профиля извлекаются известные точные IDs и curated paths.
3. Загружается `06_ui_commands_buttons.csv`.
4. При наличии загружается runtime probe.
5. Для каждой intent-записи рассчитываются кандидаты по имени, aliases и module affinity.
6. Слабые и близкие результаты получают `unresolved`/`ambiguous`.
7. Надёжный результат получает точный ID.
8. Curated-команда обогащается `catalog_refs`.
9. Новая команда добавляется в целевой module command set.
10. Внутри каждого модуля резервируется prefix-free canonical path.
11. Конфликтующие aliases удаляются.
12. Формируется JSON и отчёт.

## Статусы

| Статус | Значение | Enabled |
|---|---|---|
| `existing` | Точный ID уже присутствовал в базовом профиле. | Да |
| `resolved` | Найден надёжный ID в каталоге. | Да |
| `ambiguous` | Несколько близких кандидатов. | Нет |
| `unresolved` | Надёжного кандидата нет. | Нет |

Включённая строка без точного `command.id` запрещена валидатором.

## Политика путей

```text
CapsLock → действие → объект → команда → вариант
```

Канонический путь:

- содержит 2–5 токенов;
- использует стабильный action root;
- по возможности использует смысловой object token;
- уникален внутри модуля;
- не является префиксом другой команды;
- сохраняет curated path для известных IDs;
- может иметь alias только без конфликтов.

## Частота

| K | Смысл |
|---|---|
| `K5` | многократно в течение часа |
| `K4` | обычно ежедневно |
| `K3` | несколько раз в неделю или на этапе |
| `K2` | специализированная/периодическая |
| `K1` | редкая административная |

Это экспертная оценка, а не телеметрия Siemens.

## Глобальные команды

По умолчанию общие намерения Gateway/File/View/Measure/Teamcenter и административных областей дублируются в активные модули. Это позволяет вызвать их без ручного переключения HUD.

```powershell
.\install-full-command-profile.ps1 `
  -CatalogDir "D:\NX2512_Catalog_Output" `
  -NoGlobalDuplication `
  -CompileOnly
```

используется для более компактного профиля.

## Версии schema

- generated source profile: `schema_version: 4`;
- HotkeyStudio runtime model: schema 5;
- IPC: schema 3;
- `full_command_catalog.schema_version`: 1.

## Проверка

```powershell
node .\scripts\validate-full-command-map.mjs
```

Валидатор подтверждает:

- ровно 1169 intent records;
- наличие всех 32 разделов;
- корректность `K1–K5`;
- уникальность `intent_id`;
- сохранение имён и составных разделителей;
- 2–5 токенов пути;
- отсутствие duplicate/prefix conflicts;
- сохранение каждой intent-записи в test-generated profile;
- отсутствие включённых команд без точного ID.

Дополнительно стандартный CI проверяет baseline profile, runtime schema migration, DFA/HFSM, selection routing, IPC и сборку.

## Отчёт разрешения

`docs/generated/full-command-resolution.md` содержит:

- число доступных catalog entries;
- enriched existing commands;
- добавленные строки;
- общее и enabled-количество;
- resolved/existing/ambiguous/unresolved;
- лучшие кандидаты для проблемных записей.

Этот отчёт следует сохранять вместе с версией NX, ролью и лицензиями при production-проверке.

## Ограничения

Статическая компиляция не доказывает чувствительность кнопки и семантическую эквивалентность в рабочем контексте. Перед эксплуатацией:

1. выполните dry-run;
2. изучите отчёт;
3. тестируйте сначала `existing` и `resolved` безопасные команды;
4. проверьте selection filters;
5. destructive-команды тестируйте на копии данных;
6. сформируйте runtime probe целевой установки.

См. также [README.md](README.md), [docs/CONFIGURATION.md](docs/CONFIGURATION.md) и [docs/INSTALLATION.md](docs/INSTALLATION.md).
