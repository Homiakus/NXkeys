# NX 2512 — полный каталог функций и API

- Generated: `2026-08-04T01:17:44.1266961-07:00`
- NXOpen core assembly: `NXOpen, Version=2512.0.0.0, Culture=neutral, PublicKeyToken=null`
- NXOpen assemblies: **6**
- Public NXOpen types: **19129**
- Public NXOpen members: **151106**
- NXOpen API entry points: **33377**
- UI command definitions / BUTTON IDs: **32124**
- Open C / UFUN functions: **4591**

## Файлы

1. `00_environment_roots.csv` — найденные каталоги NX.
2. `01_nxopen_assemblies.csv` — NXOpen DLL и версии.
3. `02_nxopen_namespaces.csv` — пространства имён.
4. `03_nxopen_types.csv` — классы, интерфейсы, enum и struct.
5. `04_nxopen_members.csv` — методы, свойства, поля, события и конструкторы.
6. `05_nxopen_entry_points.csv` — Builder, Collection, Manager, Service и фабричные методы.
7. `06_ui_commands_buttons.csv` — UI-команды с `BUTTON ID`, label, accelerator и action.
8. `07_ufun_functions.csv` — Open C / UFUN функции.
9. `08_ui_command_api_candidates.csv` — кандидаты соответствия UI-команд API.
10. `09_ui_commands_without_strong_api_match.csv` — команды без сильного совпадения.

## Точность соответствия команды и API

| Уровень | Количество строк-кандидатов | Значение |
|---|---:|---|
| HIGH | 11369 | Сильное совпадение имени; всё равно проверить журналом |
| MEDIUM | 95339 | Вероятный API для дальнейшего поиска |
| LOW | 0 | Только поисковая подсказка |

> В NX нет гарантированной связи «одна кнопка = один API-метод». Для окончательной проверки конкретной операции запишите NX Journal и сопоставьте созданный Builder/Collection с таблицами этого каталога.

## Категории NXOpen типов

| Категория | Количество |
|---|---:|
| `Enum` | 8021 |
| `Object` | 5233 |
| `Builder` | 3877 |
| `ValueType` | 825 |
| `Collection` | 803 |
| `Manager` | 223 |
| `Interface` | 137 |
| `Factory` | 6 |
| `Service` | 4 |

## Важные ограничения

- Перечень managed API точен для установленных `NXOpen*.dll`.
- UFUN перечень точен для обнаруженных `uf_*.h`.
- Если отдельный модуль или Author toolkit не установлен, его DLL/headers не попадут в каталог.
- Наличие API не означает наличие runtime-лицензии соответствующего NX-модуля.
- Python, C++, Java и .NET используют близкую Common API модель, но сигнатуры и оболочки могут отличаться.
- `BUTTON ID` — внутренний идентификатор UI-команды, а не NXOpen-метод.
- Для deprecated API смотрите столбцы `is_obsolete` и `obsolete_message`, а также NXOpenReporter/What's Changed.
