# NXKeys

[![CI](https://github.com/Homiakus/NXkeys/actions/workflows/ci.yml/badge.svg)](https://github.com/Homiakus/NXkeys/actions/workflows/ci.yml)
[![Mnemonic Command Language](https://github.com/Homiakus/NXkeys/actions/workflows/mnemonic-command-language.yml/badge.svg)](https://github.com/Homiakus/NXkeys/actions/workflows/mnemonic-command-language.yml)
[![Main K3–K5 Profile](https://github.com/Homiakus/NXkeys/actions/workflows/full-command-map.yml/badge.svg)](https://github.com/Homiakus/NXkeys/actions/workflows/full-command-map.yml)
[![Command Map](https://github.com/Homiakus/NXkeys/actions/workflows/pages.yml/badge.svg)](https://github.com/Homiakus/NXkeys/actions/workflows/pages.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4.svg)](https://dotnet.microsoft.com/)

NXKeys — клавиатурный слой управления Siemens NX / Designcenter NX 2512. Вместо сотен конфликтующих глобальных сочетаний используется контекстный мнемонический язык:

```text
CapsLock → действие → объект → команда → вариант
```

```text
CapsLock → C → E        Create → Extrude
CapsLock → T → E        Transform → Mirror Feature
CapsLock → P → P        Process → Generate Tool Path
CapsLock → S → F        Select → Face Selection Priority
CapsLock → G → D        Go → Drafting
```

**Интерактивная карта:** https://homiakus.github.io/NXkeys/

> NXKeys — сторонний проект. Фактическая доступность команды зависит от сборки NX 2512, лицензий, роли, локализации и корпоративных MenuScript-расширений.

## Главный профиль: K3–K5

Рабочим профилем NXKeys является **главный профиль K3–K5**, охватывающий все функции трёх приоритетных уровней исходного каталога:

| Уровень | Команд-намерений |
|---|---:|
| `K5` | 69 |
| `K4` | 371 |
| `K3` | 445 |
| **Итого** | **885** |

`K1–K2` остаются в исходном каталоге для аудита и трассировки, но не имеют отдельного установочного пресета и не перегружают HUD, поиск и основной runtime.

### Источники профиля

| Файл | Роль |
|---|---|
| `config/full-command-map/` | Полный источник из 1169 функций и 32 разделов с уровнями `K1–K5`. |
| `config/nx2512-pro-hybrid.json` | Bootstrap-профиль: проверенные `BUTTON ID`, модули, базовые сочетания и safety-policy. Это не главный рабочий набор команд. |
| `config/nx2512-pro-main.generated.json` | Генерируемый главный профиль K3–K5 для конкретной установки NX. |
| `docs/generated/main-profile-resolution.md` | Отчёт разрешения команд в реальные `BUTTON ID`. |

Managed-пакет сохраняет главный профиль под совместимым runtime-именем `nx2512-pro-hybrid.json`, чтобы старые launcher-команды и пользовательские установки продолжали работать. Содержимое этого файла после основной установки — профиль K3–K5, а не старый bootstrap-набор.

## Что означает «покрывает все K3–K5»

Для каждой из 885 функций главный профиль обязательно содержит:

- исходный раздел и группу;
- уровень частоты `K3`, `K4` или `K5`;
- английское и русское название;
- целевой контекстный модуль;
- prefix-free путь с частотной оптимизацией: `K5` — до 2 клавиш, `K4` — до 3, `K3` — до 4;
- поисковые aliases;
- статус разрешения в `BUTTON ID`.

Команда становится исполняемой только после надёжного сопоставления с реальным `BUTTON ID` целевой установки. Состояния `ambiguous` и `unresolved` сохраняются в профиле, но отключаются. NXKeys не подставляет выдуманные идентификаторы.

Runtime-инфраструктура не считается покрытием каталога:

- во всех 14 модулях доступны одинаковые фильтры выбора `S*`: `SB` Body, `SF` Face, `SE` Edge, `ST` Feature, `SC` Component, `SU` Curve, `SD` Datum, `SR` Reset;
- во всех модулях кроме Sketch доступны переходы `G*`: `GM` Modeling, `GA` Assembly, `GD` Drafting, `GP` PMI, `GU` Surface, `GH` Sheet Metal, `GC` CAM, `GN` Simulation, `GR` Routing, `GO` Mold, `GL` Reuse, `GV` Inspect/View;
- Sketch не получает общий switch-menu: вход в эскиз остаётся контекстной командой `UG_CREATE_SKETCH`.

## Быстрая установка главного профиля

### Требования

- Windows 10/11 x64;
- Siemens NX или Designcenter NX 2512;
- .NET 8 SDK x64;
- Node.js 20+;
- `NXOpen.dll` и `NXOpenUI.dll` целевой установки;
- экспорт `NX2512_Catalog_Studio` с файлом `06_ui_commands_buttons.csv` — рекомендуется для максимального числа исполняемых команд.

Закройте NX и выполните из корня репозитория:

```powershell
.\install-nxkeys.ps1 `
  -CatalogDir "D:\NX2512_Catalog_Output" `
  -NxRoot "C:\Program Files\Siemens\NX2512" `
  -Clean
```

Для Designcenter NX:

```powershell
.\install-nxkeys.ps1 `
  -CatalogDir "D:\NX2512_Catalog_Output" `
  -NxRoot "C:\Program Files\Siemens\DesigncenterNX2512" `
  -Clean
```

Только скомпилировать профиль и отчёт:

```powershell
.\install-nxkeys.ps1 `
  -CatalogDir "D:\NX2512_Catalog_Output" `
  -CompileOnly
```

Без `CatalogDir` профиль также будет создан, но исполняемыми станут только команды с уже известными точными IDs; остальные функции K3–K5 останутся видимыми в карте и отчёте как безопасно отключённые.

В корне проекта больше нет отдельных wrapper-скриптов для main/full/ribbon сценариев: `install-nxkeys.ps1` — единственный поддерживаемый вход установки и компиляции.

## Как работает ввод

1. `CapsLock` открывает HUD активного приложения NX.
2. Первая клавиша задаёт действие: Create, Edit, Transform, Process и т.д.
3. Следующая клавиша задаёт объект: Feature, Body, Surface, Operation, Tool и т.д.
4. Остальные клавиши уточняют команду и вариант.
5. Внутренний префикс модуля добавляется движком автоматически.

HUD показывает допустимые продолжения в **3 колонки**. Канонические пути и aliases внутри активного модуля уникальны и не являются префиксами друг друга.

| Клавиша | Действие |
|---|---|
| `CapsLock` | Открыть HUD |
| Буква или цифра | Перейти по ветви или выполнить команду |
| `Space` | Поиск по главному профилю активного модуля |
| `Enter` | Выполнить найденную команду или подтвердить опасную операцию |
| `Backspace` | Сбросить текущий путь |
| `Esc` | Закрыть HUD |
| `Tab` / `Shift+Tab` | Явно переключить модуль |
| Двойной `CapsLock` | Закрепить HUD |

`S*` фильтры не зависят от активного модуля и отправляются в Command Bridge как `set_selection_filter`. `G*` переходы отправляются как `switch_module` с явным `target_module_id`; обычные команды не маскируются под переключение приложения.

## Контекстные модули

```text
modeling       sketch          assembly       drafting
pmi            surface         sheet_metal    manufacturing
simulation     routing         mold           reuse
inspect_view   selection_object
```

Command Bridge публикует активное приложение, модуль, Work/Display Part, выбранные типы и количество объектов, модальное состояние, ревизию контекста и результат последней команды.

## Безопасность

Перед dispatch проверяются:

- свежесть и достоверность контекста;
- активный модуль и приложение;
- Work Part и Display Part;
- модальный диалог и активная команда NX;
- типы и количество выбранных объектов;
- наличие точного `BUTTON ID`;
- destructive-флаг и подтверждение.

`UG_SEL_*` выполняются через `set_selection_filter`, а не как обычные псевдокнопки. Разрушительные операции требуют `Enter`. Запрос с неизвестным результатом получает `interrupted_unknown` и автоматически не повторяется.

## Проверка

```powershell
node .\scripts\validate-main-command-map.mjs
node .\scripts\validate-command-tree.mjs
node .\scripts\validate-full-command-map.mjs
node .\scripts\audit-command-sequences.mjs
dotnet run --project .\NXKeys.StateMachines.Tests\NXKeys.StateMachines.Tests.csproj -c Release
```

CI проверяет:

- полный исходный каталог: 1169 функций, 32 раздела;
- точные количества `K1–K5`;
- главный scope: ровно 885 уникальных функций K3–K5;
- отсутствие K1–K2 в главном профиле;
- prefix-free пути и aliases;
- частотные цели маршрутов: `K5 <= 2`, `K4 <= 3`, `K3 <= 4`;
- 112 универсальных фильтров выбора и 132 перехода между модулями;
- отсутствие включённых команд без точного `BUTTON ID`;
- единый установочный пресет K3–K5 без второго full/all runtime-профиля;
- 12 базовых сочетаний, 14 модулей, DFA/HFSM и CommandBridge contract.

Аудит последовательностей сохраняется в:

```text
docs/audit/command-sequence-audit.json
docs/audit/command-sequence-audit.md
```

Последняя локальная проверка нового профиля: `schema_version = 6`, `sequence_policy_version = 6`, `support_commands = 244`, `selection_filter_support_commands = 112`, `module_switch_support_commands = 132`.

## Запуск после установки

```text
%LOCALAPPDATA%\NXKeys\managed\NX2512.6000\launch-nx2512-with-nxkeys.cmd
```

## Документация

- [Главный профиль и полный каталог](FULL_COMMAND_MAP.md)
- [Оглавление документации](docs/README.md)
- [Установка](docs/INSTALLATION.md)
- [Конфигурация](docs/CONFIGURATION.md)
- [Мнемонический язык](docs/MNEMONIC_COMMAND_LANGUAGE.md)
- [Архитектура](docs/ARCHITECTURE.md)
- [Конечные автоматы](docs/STATE_MACHINE_ARCHITECTURE.md)
- [Безопасность](docs/SAFETY_MODEL.md)
- [Диагностика](docs/TROUBLESHOOTING.md)

## Лицензия

[MIT](LICENSE)
