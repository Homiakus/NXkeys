# Siemens NX 2512 — Ergonomic 80

> Профессиональная раскладка горячих клавиш и радиальных меню для повседневной механической разработки в Siemens NX 2512.
>
> **Цель профиля:** дать прямой доступ минимум к 80% высокочастотных операций типового конструктора, сохранив удобство нажатия, моторную логику, стандартные команды NX и совместимость с разными раскладками клавиатуры.
>
> **Версия документа:** 3.0
> **Целевая версия NX:** 2512
> **Платформа:** Windows, полноразмерная или компактная клавиатура, мышь с тремя кнопками.

---

## 1. Что означает «покрытие 80%»

NX содержит тысячи команд и специализированные приложения: Modeling, Assemblies, Drafting, PMI, Sheet Metal, Routing, CAM, CAE, Mold Wizard и другие. Одна универсальная раскладка не может разумно покрыть 80% **всех** команд NX без превращения клавиатуры в труднозапоминаемый пульт.

В этом документе «80%» означает прямой доступ к большинству операций типового механического CAD-потока:

```text
создание/открытие модели
→ построение эскиза
→ создание базовой формы
→ отверстия и обработка кромок
→ паттерны и симметрия
→ редактирование фич и граней
→ сборка
→ размеры, PMI и чертёж
→ проверка, измерение и повторное использование
```

### Расчётное покрытие профиля

| Рабочая область | Вес в типовом потоке | Покрыто профилем |
|---|---:|---:|
| Файлы, отмена, сохранение | 8% | 8% |
| Вид, навигация, выбор | 10% | 10% |
| Sketch | 18% | 16% |
| Solid Modeling | 20% | 18% |
| Редактирование и Synchronous Modeling | 12% | 10% |
| Assemblies | 12% | 10% |
| Drafting и PMI | 8% | 5% |
| Expressions, WAVE, Reuse | 7% | 6% |
| Измерение, отображение, диагностика | 5% | 5% |
| **Итого** | **100%** | **88% расчётного покрытия** |

Показатель 88% — это проектная оценка по набору типовых задач, а не телеметрия Siemens. Профиль специально имеет небольшой запас над требованием 80%.

Не входят в основной показатель:

- сложное поверхностное моделирование;
- Sheet Metal;
- Routing;
- Mold Wizard;
- NX CAM;
- Simcenter/CAE;
- специализированная MBD/PMI-разметка;
- администрирование Teamcenter.

Для этих областей в конце документа предложены отдельные расширения.

---

## 2. Главный принцип: не назначать горячую клавишу каждой команде

Оптимальная система NX состоит из четырёх уровней:

| Уровень | Для чего применяется |
|---|---|
| **Штатное ядро NX** | файлы, отмена, вид, системные диалоги |
| **12 пользовательских сочетаний** | самые частые глобальные операции |
| **Контекстные клавиши Sketch** | геометрия и ограничения внутри эскиза |
| **Application/Object Radial** | команды рядом с курсором и выбранным объектом |

Такой подход быстрее и легче запоминается, чем 50–80 глобальных сочетаний.

### 2.1. Модульное меню вместо одной общей панели

В NXKeys v2 активный модуль NX выбирает видимый набор команд. В Modeling пользователь видит команды построения и редактирования тела; в Sketch — линии, окружности, ограничения и завершение эскиза; в CAM, CAE, Routing или Mold — команды соответствующего рабочего процесса. Это снижает визуальный шум и ускоряет выбор, потому что один и тот же жест не конкурирует со всеми командами NX сразу.

При этом принципы направлений остаются одинаковыми во всех модулях:

| Сектор | Постоянный смысл |
|---|---|
| `N` | начать, создать или открыть главный объект |
| `NE` | следующий основной шаг |
| `E` | добавить объект, материал или зависимость |
| `SE` | преобразовать или заменить |
| `S` | завершить, удалить или выполнить вторичную обработку |
| `SW` | убрать, ослабить или уменьшить |
| `W` | структура, связь или размножение |
| `NW` | проверка, измерение или служебная команда |

Общий слой выбора геометрии доступен из любого модуля: Body, Face, Edge, Feature, Component, Curve, Point, Datum, Sheet Body и Reset Filter. Опасные действия, например удаление, замена компонента, постпроцессинг CAM или запуск расчёта CAE, должны требовать подтверждения.

### 2.2. Переключение и подтверждение

HotkeyStudio/Leader HUD берёт активный модуль из NX Command Bridge. Если контекст NX недоступен, пользователь переключает модуль вручную через `Tab`/`Shift+Tab`. `Esc` отменяет текущий выбор, `Backspace` возвращает на уровень назад, `Enter` подтверждает ожидающую команду. В окне Studio несохранённые изменения закрываются через выбор: принять и сохранить, отменить закрытие или закрыть без сохранения.

### Почему не используются `Ctrl+Alt+буква`

- `Ctrl+Alt` может интерпретироваться Windows как `AltGr`;
- это создаёт проблемы на немецкой, польской и других европейских раскладках;
- буквенные команды сложнее переносить между корпоративными профилями;
- три клавиши труднее нажимать одной рукой.

### Почему основное ядро использует `Alt+1…5`

- левый `Alt` удобно удерживается большим пальцем;
- клавиши `1…5` доступны левой рукой;
- цифры не зависят от языка ввода;
- ряд отражает последовательность построения детали;
- сочетания не требуют одновременного нажатия `Ctrl+Shift`.

> Перед внедрением всё равно необходимо проверить корпоративные назначения в `Ctrl+1 → Customize → Keyboard`.

---

## 3. Штатное ядро NX — сохранить

### 3.1. Файлы и редактирование

| Сочетание | Команда NX | Основное имя EN | Статус |
|---|---|---|---|
| `Ctrl+N` | Новый файл | `New` | сохранить |
| `Ctrl+O` | Открыть | `Open` | сохранить |
| `Ctrl+S` | Сохранить | `Save` | сохранить |
| `Ctrl+Shift+A` | Сохранить как | `Save As` | проверить в роли |
| `Ctrl+Z` | Отмена | `Undo` | сохранить |
| `Ctrl+Y` | Повтор | `Redo` | сохранить |
| `Ctrl+X` | Вырезать | `Cut` | сохранить |
| `Ctrl+C` | Копировать | `Copy` | сохранить |
| `Ctrl+V` | Вставить | `Paste` | сохранить |
| `Delete` | Удалить выбранное | `Delete` | сохранить |
| `Esc` | Отмена команды/очистка выбора | `Cancel`, `Deselect` | контекстная |

### 3.2. Вид

| Сочетание | Команда NX | Основное имя EN |
|---|---|---|
| `Ctrl+F` | Вписать модель | `Fit`, `Fit View` |
| `Home` | Триметрический вид | `Trimetric` |
| `End` | Изометрический вид | `Isometric` |
| `F8` | Ближайший ортогональный вид | `Closest Orthographic` |
| `Shift+F8` | Нормаль к эскизу | `Normal to Sketch` |
| `W` | Показать/скрыть WCS | `WCS Display` |
| `F5` | Обновить отображение | `Refresh` |
| `Ctrl+MB3` | View Shortcut Menu | системное меню вида |

### 3.3. Системные команды

| Сочетание | Команда NX | Основное имя EN |
|---|---|---|
| `Ctrl+1` | Настройка интерфейса | `Customize` |
| `Ctrl+2` | Настройки UI | `User Interface Preferences` |
| `Ctrl+E` | Выражения | `Expressions` |
| `Ctrl+I` | Информация об объекте | `Object Information` |
| `Ctrl+J` | Отображение объекта | `Object Display` |
| `Ctrl+L` | Слои | `Layer Settings` |
| `Ctrl+M` | Modeling | `Modeling` |
| `Ctrl+Shift+D` | Drafting | `Drafting` |
| `Ctrl+Q` | Завершить Sketch | `Finish Sketch` |
| `F1` | Контекстная справка | `Context Help` |

### 3.4. Приоритет выбора

| Сочетание | Приоритет |
|---|---|
| `Shift+F` | Feature |
| `Shift+G` | Face |
| `Shift+B` | Body |
| `Shift+E` | Edge |
| `Shift+C` | Component |

Эти клавиши не следует занимать другими командами.

---

## 4. Пользовательское ядро «Ergonomic 80»

### 4.1. Главный ряд создания: `Alt+1…5`

| Сочетание | Команда | Основное имя EN | Почему здесь |
|---|---|---|---|
| `Alt+1` | Создать Sketch | `Sketch`, `Create Sketch` | начало построения |
| `Alt+2` | Extrude | `Extrude` | базовая объёмная операция |
| `Alt+3` | Hole | `Hole` | самая частая специализированная фича |
| `Alt+4` | Edge Blend | `Edge Blend` | обработка кромки |
| `Alt+5` | Chamfer | `Chamfer` | рядом со скруглением |

Моторная последовательность:

```text
Alt+1 → Alt+2 → Alt+3 → Alt+4/Alt+5
Sketch → Extrude → Hole → обработка кромок
```

### 4.2. Второй ряд формы: `Ctrl+3…7`

| Сочетание | Команда | Основное имя EN | Логика |
|---|---|---|---|
| `Ctrl+3` | Revolve | `Revolve` | альтернативное создание формы |
| `Ctrl+4` | Pattern Feature | `Pattern Feature` | повторение |
| `Ctrl+5` | Mirror Feature | `Mirror Feature` | симметрия |
| `Ctrl+6` | Datum Plane | `Datum Plane` | опорная геометрия |
| `Ctrl+7` | Unite | `Unite`, `Boolean Unite` | объединение тел |

`Ctrl+3…7` используется реже, поэтому допускает чуть большее перемещение руки.

### 4.3. Универсальные команды быстрого доступа

| Сочетание | Команда | Основное имя EN | Статус |
|---|---|---|---|
| `F2` | Поиск команды | `Command Finder` | рекомендуемая |
| `F4` | Измерение | `Measure` | рекомендуемая |

`F2` и `F4` следует назначать только после проверки, что корпоративная роль не использует их для других задач.

### 4.4. Почему здесь нет Subtract и Intersect

Булевы операции требуют осознанного выбора target/tool body. Чтобы не вызывать потенциально разрушительную команду случайно:

- `Unite` доступен с клавиатуры;
- `Subtract` и `Intersect` находятся в Body Object Radial;
- перед выполнением пользователь видит выбранные тела.

---

## 5. Sketch: одноклавишный локальный слой

Эти клавиши следует назначать только командам, активным внутри Sketch. Они не должны менять глобальное поведение Modeling.

| Клавиша | Команда | Основное имя EN | Частота |
|---|---|---|---|
| `L` | Линия | `Line` | очень высокая |
| `R` | Прямоугольник | `Rectangle` | высокая |
| `O` | Окружность | `Circle` | высокая |
| `A` | Дуга | `Arc` | высокая, историческая NX |
| `T` | Обрезать | `Trim` | очень высокая |
| `D` | Быстрый размер | `Rapid Dimension` | очень высокая, историческая NX |
| `C` | Геометрические ограничения | `Geometric Constraints` | высокая, историческая NX |
| `F` | Скругление эскиза | `Sketch Fillet`, `Fillet` | средняя |
| `M` | Зеркало кривых | `Mirror Curve`, `Mirror` | средняя |
| `P` | Паттерн кривых | `Pattern Curve`, `Pattern` | средняя |
| `Z` | Sketch/Profile | `Sketch`, `Profile` | историческая NX |
| `Ctrl+Q` | Завершить эскиз | `Finish Sketch` | очень высокая |

### Не назначать в Sketch

| Клавиша | Причина |
|---|---|
| `W` | штатный WCS Display |
| `X` | может использоваться NX для Extrude |
| `E` | исторически связано с Design Feature menu |
| `S` | часто используется меню/фильтрами и плохо различается с Save |
| `B` | конфликтует с приоритетом Body при использовании Shift |

---

## 6. Application Radial — постоянная пространственная логика

NX позволяет вызвать три Application Radial:

| Вызов | Название |
|---|---|
| `Ctrl+Shift+MB1` | `Application Radial 1` |
| `Ctrl+Shift+MB2` | `Application Radial 2` |
| `Ctrl+Shift+MB3` | `Application Radial 3` |

### Семантика направлений

| Направление | Постоянный смысл |
|---|---|
| **N** | начать/создать главное |
| **NE** | следующий шаг процесса |
| **E** | добавить |
| **SE** | преобразовать/заменить |
| **S** | завершить/удалить |
| **SW** | уменьшить/убрать |
| **W** | связать/размножить/структура |
| **NW** | проверить/служебная команда |

Одно направление должно иметь сходный смысл во всех приложениях. Это ускоряет формирование моторной памяти.

---

# 7. Modeling — три радиальных меню

## 7.1. Modeling Radial 1 — CREATE

**Вызов:** `Ctrl+Shift+MB1`

| Сектор | Команда | Поиск EN |
|---|---|---|
| **N** | Sketch | `Sketch`, `Create Sketch` |
| **NE** | Extrude | `Extrude` |
| **E** | Hole | `Hole` |
| **SE** | Revolve | `Revolve` |
| **S** | Edge Blend | `Edge Blend` |
| **SW** | Chamfer | `Chamfer` |
| **W** | Pattern Feature | `Pattern Feature` |
| **NW** | Mirror Feature | `Mirror Feature` |

Это меню дублирует самые частые клавиши намеренно: пользователь может работать либо двумя руками, либо почти полностью мышью.

## 7.2. Modeling Radial 2 — EDIT

**Вызов:** `Ctrl+Shift+MB2`

| Сектор | Команда | Поиск EN |
|---|---|---|
| **N** | Edit Parameters | `Edit Parameters` |
| **NE** | Edit Sketch | `Edit Sketch` |
| **E** | Move Face | `Move Face` |
| **SE** | Replace Face | `Replace Face` |
| **S** | Delete Face | `Delete Face` |
| **SW** | Resize Hole | `Resize Hole` |
| **W** | Resize Pattern | `Resize Pattern` |
| **NW** | Feature Playback | `Feature Playback`, `Playback` |

`Delete Face` находится снизу, а не в лёгком северном секторе, чтобы уменьшить риск случайного жеста.

## 7.3. Modeling Radial 3 — CONTROL

**Вызов:** `Ctrl+Shift+MB3`

| Сектор | Команда | Поиск EN |
|---|---|---|
| **N** | Expressions | `Expressions` |
| **NE** | Measure | `Measure` |
| **E** | Part Navigator | `Part Navigator` |
| **SE** | Reuse Library | `Reuse Library` |
| **S** | WAVE Geometry Linker | `WAVE Geometry Linker`, `WAVE` |
| **SW** | Layer Settings | `Layer Settings` |
| **W** | Object Display | `Object Display` |
| **NW** | Command Finder | `Command Finder` |

---

# 8. Sketch — три радиальных меню

Application Radial следует настраивать отдельно для активного приложения Sketch.

## 8.1. Sketch Radial 1 — DRAW

| Сектор | Команда | Поиск EN |
|---|---|---|
| **N** | Line | `Line` |
| **NE** | Rectangle | `Rectangle` |
| **E** | Circle | `Circle` |
| **SE** | Arc | `Arc` |
| **S** | Trim | `Trim` |
| **SW** | Extend | `Extend` |
| **W** | Offset Curve | `Offset Curve`, `Offset` |
| **NW** | Studio Spline | `Studio Spline`, `Spline` |

## 8.2. Sketch Radial 2 — CONSTRAIN/EDIT

| Сектор | Команда | Поиск EN |
|---|---|---|
| **N** | Rapid Dimension | `Rapid Dimension` |
| **NE** | Geometric Constraints | `Geometric Constraints` |
| **E** | Move Curve | `Move Curve` |
| **SE** | Mirror Curve | `Mirror Curve` |
| **S** | Delete | `Delete` |
| **SW** | Make Reference | `Convert to Reference`, `Make Reference` |
| **W** | Pattern Curve | `Pattern Curve` |
| **NW** | Sketch Checker | `Sketch Checker` |

## 8.3. Sketch Radial 3 — CONTROL

| Сектор | Команда | Поиск EN |
|---|---|---|
| **N** | Finish Sketch | `Finish Sketch` |
| **NE** | Normal to Sketch | `Normal to Sketch` |
| **E** | Show/Remove Constraints | `Show Constraints`, `Remove Constraints` |
| **SE** | Auto Constrain | `Auto Constrain` |
| **S** | Fix | `Fix`, `Fix Constraint` |
| **SW** | Relax Dimensions | `Relax Dimensions` |
| **W** | Sketch Navigator | `Sketch Navigator` |
| **NW** | Command Finder | `Command Finder` |

---

# 9. Assemblies — три радиальных меню

## 9.1. Assembly Radial 1 — COMPONENTS

| Сектор | Команда | Поиск EN |
|---|---|---|
| **N** | Add Component | `Add Component`, `Place Component` |
| **NE** | Create New Component | `Create New Component` |
| **E** | Move Component | `Move Component` |
| **SE** | Assembly Constraints | `Assembly Constraints`, `Constraints` |
| **S** | Replace Component | `Replace Component` |
| **SW** | Remove Component | `Remove Component` |
| **W** | Pattern Component | `Pattern Component` |
| **NW** | Mirror Assembly | `Mirror Assembly`, `Mirror Component` |

Опасные `Replace` и `Remove` расположены в нижней половине.

## 9.2. Assembly Radial 2 — POSITION/CONTEXT

| Сектор | Команда | Поиск EN |
|---|---|---|
| **N** | Make Work Part | `Make Work Part` |
| **NE** | Make Displayed Part | `Make Displayed Part` |
| **E** | Touch Align | `Touch Align` |
| **SE** | Align/Lock | `Align`, `Lock` |
| **S** | Suppress Component | `Suppress Component` |
| **SW** | Unsuppress Component | `Unsuppress Component` |
| **W** | Show Only | `Show Only` |
| **NW** | Component Properties | `Component Properties`, `Properties` |

## 9.3. Assembly Radial 3 — STRUCTURE

| Сектор | Команда | Поиск EN |
|---|---|---|
| **N** | Assembly Navigator | `Assembly Navigator` |
| **NE** | Find Component | `Find Component`, `Find in Navigator` |
| **E** | Arrangements | `Arrangements` |
| **SE** | Reference Set | `Reference Set` |
| **S** | Update Assembly | `Update Assembly`, `Update` |
| **SW** | Load Options | `Load Options` |
| **W** | WAVE Geometry Linker | `WAVE Geometry Linker` |
| **NW** | Measure | `Measure` |

---

# 10. Drafting и PMI — три радиальных меню

Команды Drafting могут отличаться по лицензии, стандарту оформления и корпоративному шаблону. Назначения ниже являются базовым механическим профилем.

## 10.1. Drafting Radial 1 — DIMENSIONS

| Сектор | Команда | Поиск EN |
|---|---|---|
| **N** | Rapid Dimension | `Rapid Dimension` |
| **NE** | Horizontal Dimension | `Horizontal Dimension` |
| **E** | Vertical Dimension | `Vertical Dimension` |
| **SE** | Angular Dimension | `Angular Dimension` |
| **S** | Radius Dimension | `Radius Dimension` |
| **SW** | Diameter Dimension | `Diameter Dimension` |
| **W** | Ordinate Dimension | `Ordinate Dimension` |
| **NW** | Edit Dimension | `Edit Dimension` |

## 10.2. Drafting Radial 2 — ANNOTATE

| Сектор | Команда | Поиск EN |
|---|---|---|
| **N** | Note | `Note` |
| **NE** | Label | `Label` |
| **E** | Surface Finish Symbol | `Surface Finish Symbol` |
| **SE** | Feature Control Frame | `Feature Control Frame` |
| **S** | Centerline | `Centerline` |
| **SW** | Center Mark | `Center Mark` |
| **W** | Parts List | `Parts List` |
| **NW** | Balloon | `Balloon`, `Auto Balloon` |

## 10.3. Drafting Radial 3 — VIEWS/UPDATE

| Сектор | Команда | Поиск EN |
|---|---|---|
| **N** | Base View | `Base View` |
| **NE** | Projected View | `Projected View` |
| **E** | Section View | `Section View` |
| **SE** | Detail View | `Detail View` |
| **S** | Update Views | `Update Views` |
| **SW** | View Style | `View Style`, `Settings` |
| **W** | Break View | `Break View` |
| **NW** | Drafting Preferences | `Drafting Preferences` |

---

# 11. Object Radial — команды по выбранному объекту

Object Radial важнее дополнительных глобальных клавиш: пользователь сначала показывает NX, **с чем** он работает, а затем выбирает допустимое действие.

## 11.1. Выбрана грань

| Сектор | Команда |
|---|---|
| **N** | Move Face |
| **NE** | Offset Region |
| **E** | Replace Face |
| **SE** | Delete Face |
| **S** | Object Display |
| **SW** | Hide |
| **W** | Measure |
| **NW** | Select Similar Faces |

## 11.2. Выбрана кромка

| Сектор | Команда |
|---|---|
| **N** | Edge Blend |
| **NE** | Chamfer |
| **E** | Measure |
| **SE** | Delete |
| **S** | Object Display |
| **SW** | Hide |
| **W** | Select Similar Edges |
| **NW** | Object Information |

## 11.3. Выбрана фича

| Сектор | Команда |
|---|---|
| **N** | Edit Parameters |
| **NE** | Edit with Rollback |
| **E** | Show Parents |
| **SE** | Show Children |
| **S** | Suppress Feature |
| **SW** | Unsuppress Feature |
| **W** | Rename |
| **NW** | Object Information |

## 11.4. Выбрано тело

| Сектор | Команда |
|---|---|
| **N** | Unite |
| **NE** | Subtract |
| **E** | Intersect |
| **SE** | Move Object |
| **S** | Delete |
| **SW** | Hide |
| **W** | Measure Bodies |
| **NW** | Object Display |

## 11.5. Выбран компонент

| Сектор | Команда |
|---|---|
| **N** | Make Work Part |
| **NE** | Make Displayed Part |
| **E** | Move Component |
| **SE** | Assembly Constraints |
| **S** | Replace Component |
| **SW** | Hide |
| **W** | Show Only |
| **NW** | Component Properties |

---

# 12. Feature Templates, Reuse Library и WAVE

Эти команды важны для корпоративной параметризации, но применяются реже обычного моделирования. Поэтому они не занимают главный ряд `Alt+1…5`.

| Команда | Основное имя EN | Доступ |
|---|---|---|
| Expressions | `Expressions` | `Ctrl+E`, Modeling Radial 3 — N |
| WAVE Geometry Linker | `WAVE Geometry Linker` | Modeling/Assembly Radial 3 |
| Reuse Library | `Reuse Library` | Modeling Radial 3 — SE |
| Create Feature Template | `Create Feature Template`, `Template Author` | QAT/собственная Ribbon-группа |
| Edit Feature Template | `Edit Feature Template` | контекст Feature Template |
| Replace Feature Template | `Replace Feature Template` | контекст экземпляра |
| Parameter Table | `Parameter Table`, `Table from Expressions` | внутри Template Author |
| Validate Template | `Validate Template` | только внутри диалога |
| Part Navigator | `Part Navigator` | Modeling Radial 3 — E |

### Рекомендуемая собственная группа Ribbon

```text
NX ERGO 80 / REUSE
├─ Expressions
├─ WAVE Geometry Linker
├─ Reuse Library
├─ Create Feature Template
├─ Edit Feature Template
├─ Replace Feature Template
└─ Parameter Table
```

Не следует назначать глобальные клавиши на `Validate Template`, `Define Feature Reference` и `Configure User Interface`: эти действия имеют смысл только внутри Template Author.

---

# 12.1. Все NX области — стартовые модульные наборы

Эти наборы являются рекомендуемой первой версией для `modules[]`. Они не заменяют лицензии и корпоративные роли: если команда не находится в Customize или не резолвится по `BUTTON ID`, NXKeys оставляет её нерешённой и не подставляет похожую.

| Модуль | Префикс Leader | `N` | `NE` | `E` | `SE` | `S` | `SW` | `W` | `NW` |
|---|---|---|---|---|---|---|---|---|---|
| Modeling | `M` | Sketch | Extrude | Hole | Revolve | Edge Blend | Chamfer | Pattern Feature | Mirror Feature |
| Sketch | `S` | Line | Rectangle | Circle | Arc | Trim | Extend | Offset Curve | Sketch Checker |
| Assembly | `A` | Add Component | Create New Component | Move Component | Constraints | Replace Component | Remove Component | Pattern Component | Assembly Navigator |
| Drafting | `D` | Base View | Projected View | Section View | Detail View | Update Views | View Style | Parts List | Rapid Dimension |
| PMI | `P` | Rapid Dimension | Datum Feature Symbol | Feature Control Frame | Surface Finish | PMI Note | Edit PMI | Model View | Validate PMI |
| Surface | `U` | Through Curves | Swept | Studio Surface | Trim Sheet | Sew | Untrim | Extract Geometry | Face Curvature |
| Sheet Metal | `H` | Base Tab | Flange | Contour Flange | Bend | Unbend | Rebend | Flat Pattern | Preferences |
| CAM/Manufacturing | `C` | Create Operation | Create Tool | Generate Tool Path | Verify Tool Path | Postprocess | Delete Operation | Operation Navigator | Tool Path Information |
| CAE/Simulation | `X` | Create Solution | Create Load | Create Constraint | Mesh | Solve | Delete Simulation Object | Simulation Navigator | Results |
| Routing | `G` | Create Route | Place Part | Add Stock | Edit Route | Delete Route Object | Remove Part | Routing Navigator | Validate Route |
| Mold/Tooling | `O` | Initialize Project | Parting | Mold Base | Gate | Cooling | Ejector | Mold Library | Validate Mold Design |
| Reuse/Templates | `R` | Expressions | WAVE Geometry Linker | Reuse Library | Create Feature Template | Replace Feature Template | Part Navigator | Parameter Table | Command Finder |
| Inspect/View | `V` | Fit | Trimetric | Measure | Object Information | Hide | Show Only | Layer Settings | Command Finder |
| Selection/Object | `F` | Body Priority | Face Priority | Edge Priority | Feature Priority | Component Priority | Curve Priority | Datum Priority | Reset Filter |

---

# 13. Каталог команд для поиска в Customize

## 13.1. Modeling

| Команда EN | Альтернативный поиск |
|---|---|
| Sketch | `Create Sketch`, `Sketch` |
| Extrude | `Extrude` |
| Hole | `Hole` |
| Revolve | `Revolve` |
| Edge Blend | `Blend`, `Edge Blend` |
| Chamfer | `Chamfer` |
| Pattern Feature | `Pattern`, `Pattern Feature` |
| Mirror Feature | `Mirror`, `Mirror Feature` |
| Datum Plane | `Datum Plane` |
| Unite | `Boolean Unite`, `Unite` |
| Subtract | `Boolean Subtract`, `Subtract` |
| Intersect | `Boolean Intersect`, `Intersect` |

## 13.2. Edit/Synchronous

| Команда EN | Альтернативный поиск |
|---|---|
| Edit Parameters | `Edit Parameters` |
| Edit Sketch | `Edit Sketch` |
| Move Face | `Move Face` |
| Replace Face | `Replace Face` |
| Delete Face | `Delete Face` |
| Resize Hole | `Resize Hole` |
| Resize Pattern | `Resize Pattern` |
| Offset Region | `Offset Face`, `Offset Region` |
| Feature Playback | `Playback`, `Feature Playback` |

## 13.3. Sketch

| Команда EN | Альтернативный поиск |
|---|---|
| Line | `Line` |
| Rectangle | `Rectangle` |
| Circle | `Circle` |
| Arc | `Arc` |
| Trim | `Trim` |
| Extend | `Extend` |
| Offset Curve | `Offset`, `Offset Curve` |
| Rapid Dimension | `Dimension`, `Rapid Dimension` |
| Geometric Constraints | `Constraints`, `Geometric Constraints` |
| Sketch Checker | `Sketch Checker` |
| Finish Sketch | `Exit Sketch`, `Finish Sketch` |

## 13.4. Assembly

| Команда EN | Альтернативный поиск |
|---|---|
| Add Component | `Place Component`, `Add Component` |
| Move Component | `Move Component` |
| Assembly Constraints | `Constraints`, `Assembly Constraints` |
| Replace Component | `Replace Component` |
| Make Work Part | `Make Work Part` |
| Make Displayed Part | `Make Displayed Part` |
| Assembly Navigator | `Assembly Navigator` |
| Arrangements | `Arrangement`, `Arrangements` |
| Reference Set | `Reference Set` |
| Load Options | `Load Options` |

## 13.5. Drafting/PMI

| Команда EN | Альтернативный поиск |
|---|---|
| Rapid Dimension | `Dimension`, `Rapid Dimension` |
| Note | `Note`, `Annotation` |
| Feature Control Frame | `FCF`, `Feature Control Frame` |
| Surface Finish Symbol | `Surface Finish`, `Surface Finish Symbol` |
| Base View | `Base View` |
| Projected View | `Projected View` |
| Section View | `Section View` |
| Detail View | `Detail View` |
| Update Views | `Update View`, `Update Views` |
| Parts List | `Parts List`, `BOM` |
| Balloon | `Balloon`, `Auto Balloon` |

---

# 14. JSON-манифест профиля

Блок можно использовать как основу для конфигурационного файла утилиты NXKeys.

```json
{
  "profile": {
    "id": "nx2512-ergo-80-v3",
    "name": "NX 2512 Ergonomic 80",
    "target_version": "2512",
    "coverage_target_percent": 80,
    "estimated_coverage_percent": 88
  },
  "preserve": [
    "Ctrl+N",
    "Ctrl+O",
    "Ctrl+S",
    "Ctrl+Z",
    "Ctrl+Y",
    "Ctrl+E",
    "Ctrl+F",
    "Ctrl+1",
    "Ctrl+2",
    "Ctrl+I",
    "Ctrl+J",
    "Ctrl+L",
    "Ctrl+M",
    "Ctrl+Shift+D",
    "Ctrl+Q",
    "Home",
    "End",
    "F8",
    "Shift+F8",
    "W"
  ],
  "keyboard": [
    {"keys": "Alt+1", "command": "Sketch", "context": "Modeling"},
    {"keys": "Alt+2", "command": "Extrude", "context": "Modeling"},
    {"keys": "Alt+3", "command": "Hole", "context": "Modeling"},
    {"keys": "Alt+4", "command": "Edge Blend", "context": "Modeling"},
    {"keys": "Alt+5", "command": "Chamfer", "context": "Modeling"},

    {"keys": "Ctrl+3", "command": "Revolve", "context": "Modeling"},
    {"keys": "Ctrl+4", "command": "Pattern Feature", "context": "Modeling"},
    {"keys": "Ctrl+5", "command": "Mirror Feature", "context": "Modeling"},
    {"keys": "Ctrl+6", "command": "Datum Plane", "context": "Modeling"},
    {"keys": "Ctrl+7", "command": "Unite", "context": "Modeling"},

    {"keys": "F2", "command": "Command Finder", "context": "Global"},
    {"keys": "F4", "command": "Measure", "context": "Global"},

    {"keys": "L", "command": "Line", "context": "Sketch"},
    {"keys": "R", "command": "Rectangle", "context": "Sketch"},
    {"keys": "O", "command": "Circle", "context": "Sketch"},
    {"keys": "A", "command": "Arc", "context": "Sketch"},
    {"keys": "T", "command": "Trim", "context": "Sketch"},
    {"keys": "D", "command": "Rapid Dimension", "context": "Sketch"},
    {"keys": "C", "command": "Geometric Constraints", "context": "Sketch"},
    {"keys": "F", "command": "Sketch Fillet", "context": "Sketch"},
    {"keys": "M", "command": "Mirror Curve", "context": "Sketch"},
    {"keys": "P", "command": "Pattern Curve", "context": "Sketch"}
  ],
  "application_radials": {
    "Modeling": {
      "radial_1": {
        "N": "Sketch",
        "NE": "Extrude",
        "E": "Hole",
        "SE": "Revolve",
        "S": "Edge Blend",
        "SW": "Chamfer",
        "W": "Pattern Feature",
        "NW": "Mirror Feature"
      },
      "radial_2": {
        "N": "Edit Parameters",
        "NE": "Edit Sketch",
        "E": "Move Face",
        "SE": "Replace Face",
        "S": "Delete Face",
        "SW": "Resize Hole",
        "W": "Resize Pattern",
        "NW": "Feature Playback"
      },
      "radial_3": {
        "N": "Expressions",
        "NE": "Measure",
        "E": "Part Navigator",
        "SE": "Reuse Library",
        "S": "WAVE Geometry Linker",
        "SW": "Layer Settings",
        "W": "Object Display",
        "NW": "Command Finder"
      }
    },
    "Sketch": {
      "radial_1": {
        "N": "Line",
        "NE": "Rectangle",
        "E": "Circle",
        "SE": "Arc",
        "S": "Trim",
        "SW": "Extend",
        "W": "Offset Curve",
        "NW": "Studio Spline"
      },
      "radial_2": {
        "N": "Rapid Dimension",
        "NE": "Geometric Constraints",
        "E": "Move Curve",
        "SE": "Mirror Curve",
        "S": "Delete",
        "SW": "Convert to Reference",
        "W": "Pattern Curve",
        "NW": "Sketch Checker"
      },
      "radial_3": {
        "N": "Finish Sketch",
        "NE": "Normal to Sketch",
        "E": "Show Constraints",
        "SE": "Auto Constrain",
        "S": "Fix",
        "SW": "Relax Dimensions",
        "W": "Sketch Navigator",
        "NW": "Command Finder"
      }
    }
  }
}
```

---

# 15. Проверка конфликтов

Перед назначением программа или пользователь должны проверить:

| Сочетание | Возможный риск | Действие |
|---|---|---|
| `Alt+1…5` | корпоративная QAT/роль | проверить и переназначить только при отсутствии конфликта |
| `F2` | переименование в Navigator или корпоративная команда | проверить |
| `F4` | системная/корпоративная команда | проверить |
| `L/R/O/T/F/M/P` | глобальные команды другой роли | назначать только Sketch-командам |
| `Ctrl+3…7` | старый пользовательский профиль | заменить осознанно |
| `A/C/D/Z` | исторические Sketch-команды | сохранить ожидаемое поведение |

### Запрещённые комбинации профиля

```text
Ctrl+Alt+...     не использовать из-за AltGr
Alt+Shift+...    не использовать из-за переключения языка Windows
Ctrl+Shift+A     не менять: Save As
Ctrl+Shift+D     не менять: Drafting
Shift+F/G/B/E/C  не менять: приоритет выбора
```

---

# 16. Порядок внедрения

1. Создать отдельную роль `NX 2512 Ergonomic 80`.
2. Снять резервную копию текущего пользовательского профиля.
3. Проверить штатное ядро.
4. Назначить только `Alt+1…5`.
5. Протестировать на простой детали.
6. Назначить `Ctrl+3…7`.
7. Добавить локальные Sketch-клавиши.
8. Настроить Modeling Radial 1–3.
9. Настроить Sketch Radial 1–3.
10. Настроить Assembly Radial 1–3.
11. Настроить Drafting Radial 1–3.
12. Добавить Object Radial для Face, Edge, Feature, Body и Component.
13. Перезапустить NX.
14. Пройти контрольные сценарии.
15. Только после этого распространять роль.

---

# 17. Контрольные сценарии

## Сценарий A — простая параметрическая деталь

```text
Ctrl+N
Alt+1      Sketch
L/R/O      геометрия
D/C        размеры и ограничения
Ctrl+Q
Alt+2      Extrude
Alt+3      Hole
Alt+4/5    Blend/Chamfer
Ctrl+S
```

## Сценарий B — симметричная деталь

```text
Alt+1      Sketch
D          размеры
Ctrl+Q
Alt+2      Extrude
Ctrl+4     Pattern Feature
Ctrl+5     Mirror Feature
F4         Measure
Ctrl+S
```

## Сценарий C — сборка

```text
Ctrl+M или приложение Assemblies
Radial 1 N       Add Component
Radial 1 SE      Assembly Constraints
Radial 2 N       Make Work Part
Radial 2 E       Touch Align
Radial 3 N       Assembly Navigator
Ctrl+S
```

## Сценарий D — выпуск чертежа

```text
Ctrl+Shift+D
Drafting Radial 3 N/NE  Base/Projected View
Drafting Radial 1       размеры
Drafting Radial 2       обозначения
Drafting Radial 3 S     Update Views
Ctrl+S
```

---

# 18. Минимальная карточка

```text
PRIMARY
Alt+1  Sketch
Alt+2  Extrude
Alt+3  Hole
Alt+4  Edge Blend
Alt+5  Chamfer

SHAPE
Ctrl+3  Revolve
Ctrl+4  Pattern Feature
Ctrl+5  Mirror Feature
Ctrl+6  Datum Plane
Ctrl+7  Unite

GLOBAL
F2      Command Finder
F4      Measure
Ctrl+E  Expressions
Ctrl+F  Fit
Ctrl+Q  Finish Sketch

SKETCH
L Line       R Rectangle
O Circle     A Arc
T Trim       D Dimension
C Constraint F Fillet
M Mirror     P Pattern

RADIAL
Ctrl+Shift+MB1  Create
Ctrl+Shift+MB2  Edit
Ctrl+Shift+MB3  Control
Ctrl+MB3        View
```

---

# 19. Как расширять профиль

## Surface Modeling

Создать отдельную роль и заменить:

| Solid | Surface |
|---|---|
| Hole | Through Curves |
| Revolve | Swept |
| Pattern Feature | Trim Sheet |
| Mirror Feature | Sew |

## Sheet Metal

Application Radial 1:

```text
Tab
Flange
Contour Flange
Bend
Unbend
Rebend
Normal Cutout
Mirror Feature
```

## Feature Templates

Создать отдельную группу Ribbon, не перегружая глобальную клавиатуру.

## CAM/CAE

Создавать отдельные роли. Не добавлять CAM и CAE-команды в механический профиль Ergonomic 80.

---

# 20. Источники и основания

1. Siemens Designcenter, **NX Timesaving Clicks and Tricks — Part Two**:
   Application Radial вызываются через `Ctrl+Shift+MB1/MB2/MB3`; радиальные панели можно настраивать под конкретное приложение, уровень пользователя и корпоративную роль.

2. Siemens, **NX Shortcut Keys — View Full List and Create Custom Keys**:
   настройка сочетаний выполняется через Customize; `Ctrl+1` открывает Customize.

3. Siemens, **NX Hints and Tips — Rapid Sketches with Keyboard Shortcuts**:
   исторические команды `W`, `End`, `F8`, `Z`, `C`, `D`, `A`, `Ctrl+Q`.

4. Siemens, **Using the END key, HOME key and F8 keys**:
   применение стандартных клавиш ориентации вида.

Перед внедрением проверьте отображаемые имена команд в вашей локализации и конкретной роли NX 2512: состав команд зависит от лицензии, приложения и корпоративной настройки.
