# Спецификация профилей NXKeys 2512

## Цель

NXKeys сочетает два уровня:

1. **Hybrid baseline** — минимальный проверенный моторный слой для частых сценариев.
2. **Full command map** — 1169 намерений в 32 разделах с компиляцией под конкретную установку NX.

Базовый профиль остаётся безопасным fallback и источником известных `BUTTON ID`, ручных путей и aliases. Полный профиль расширяет его, а не заменяет правила безопасности.

## Базовые инварианты

- 12 прямых системных сочетаний;
- один Leader (`CapsLock` по умолчанию);
- 14 контекстных модулей;
- legacy primary-grid из восьми физических клавиш;
- многоуровневые пути длиной 2–5 токенов;
- source profile schema 4, runtime schema 5;
- IPC schema 3.

## Канонические модули

| ID | Приложение | Внутренний префикс |
|---|---|---|
| `modeling` | Modeling | `M` |
| `sketch` | Sketch | `S` |
| `assembly` | Assembly | `A` |
| `drafting` | Drafting | `D` |
| `pmi` | PMI | `P` |
| `surface` | Surface | `U` |
| `sheet_metal` | Sheet Metal | `H` |
| `manufacturing` | CAM / Manufacturing | `C` |
| `simulation` | CAE / Simulation | `X` |
| `routing` | Routing | `G` |
| `mold` | Mold / Tooling | `O` |
| `reuse` | Reuse / Templates | `R` |
| `inspect_view` | Inspect / View | `V` |
| `selection_object` | Selection / Object | `F` |

Префикс нужен DFA и не вводится пользователем.

## Legacy primary-grid

| Клавиша | Слот | Рекомендуемый смысл |
|---|---|---|
| `W` | `N` | основная create/open-команда |
| `E` | `NE` | следующий частый шаг |
| `D` | `E` | добавить объект или зависимость |
| `C` | `SE` | преобразовать или заменить |
| `X` | `S` | завершить, обработать или удалить |
| `Z` | `SW` | удалить, уменьшить или ослабить |
| `A` | `W` | структура, связь, pattern |
| `Q` | `NW` | проверить, измерить, открыть сервис |

Эти восемь клавиш являются быстрыми aliases для primary-набора. Они не ограничивают количество команд в модуле.

## Многоуровневый язык

```text
CapsLock → действие → объект → команда → вариант
```

Основные свойства:

- действие имеет стабильный смысл между модулями;
- пути внутри модуля уникальны и prefix-free;
- aliases проходят такую же проверку конфликтов;
- одинаковый путь разрешён в разных модулях;
- известные curated-пути приоритетнее автогенерации.

## Правила базового профиля

1. Команда должна иметь точный `BUTTON ID`.
2. Частые команды получают primary alias, когда это безопасно.
3. Контекстные команды получают `selection_type`.
4. `UG_SEL_*` получают `action: set_selection_filter`.
5. Разрушительные операции получают `destructive` и confirmation.
6. Команда не дублируется прямым глобальным ускорителем, кроме 12 системных действий.
7. Guards отражают Work/Display Part, module, selection и confidence.

## Полная карта

Полный слой содержит:

- 1169 intent records;
- 32 исходных раздела;
- `K1–K5`;
- английские и русские имена;
- source module и group;
- целевой runtime module;
- path hint.

Компилятор объединяет намерения с базовым профилем и `06_ui_commands_buttons.csv`.

Результат для каждой команды:

- `existing` — ID уже известен;
- `resolved` — ID найден надёжно;
- `ambiguous` — команда сохранена, но отключена;
- `unresolved` — команда сохранена, но отключена.

Включённая строка обязана иметь точный ID.

## Глобальные команды

По умолчанию намерения Gateway/File/View/Measure/Teamcenter и других общих областей могут дублироваться во все активные модули. Это делает их доступными без ручной смены HUD.

`--no-global-duplication` отключает такое дублирование.

## Критерии готовности команды

Команда считается production-ready, когда:

1. присутствует в базовом или полном профиле;
2. имеет точный `BUTTON ID`;
3. имеет prefix-free путь;
4. проходит schema validation;
5. корректно разрешяется в каталоге целевой NX;
6. guards соответствуют workflow;
7. selection routing проверен;
8. destructive confirmation проверено;
9. Bridge выполняет её в нужном приложении;
10. результат подтверждён на целевой рабочей станции.

## Поведение при смене модуля

- Bridge обновляет context;
- `AdaptiveModuleResolver` выбирает новый `ModuleConfig`;
- HUD перестраивает доступные корни и ветви;
- search ограничивается новым набором;
- незавершённый путь отменяется;
- явный switch завершается только после подтверждённой revision.

## Расширение

Добавление нового runtime-модуля требует:

- уникального `id` и `leader_prefix`;
- `nx_application_ids`;
- switch command;
- command sets;
- policy guards;
- теста resolver;
- проверки DFA/HFSM;
- проверки full-map compiler;
- обновления документации и HTML-карты.

Добавление новой команды в полный каталог требует стабильного `intent_id`, раздела, группы, `Kч`, двух языковых имён, target module и path hint.

## Проверка

```powershell
node .\scripts\validate-command-tree.mjs
node .\scripts\validate-full-command-map.mjs
```

Базовый validator проверяет runtime и curated-профиль. Full-map validator проверяет 1169 намерений, 32 раздела, частоту, уникальность и prefix-free paths.
