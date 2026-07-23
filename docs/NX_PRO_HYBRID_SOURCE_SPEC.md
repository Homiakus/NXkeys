# Siemens NX 2412 — горячие клавиши, радиальные меню и каталог команд

> Практическая конфигурация для моделирования, эскизов, сборок, синхронного редактирования, Feature Templates и Reuse Library в NX 2412 под Windows.
>
> **Принцип документа:** отдельно показаны штатные сочетания, рекомендуемые пользовательские клавиши, три Application Radial, объектно-зависимые радиальные меню и английские названия команд для поиска в `Customize`. Доступность команд зависит от приложения, роли, лицензии, локализации и корпоративной конфигурации NX.

---

## 1. Главная идея раскладки

Оптимальная схема для NX должна:

1. сохранять стандартные сочетания Windows и NX;
2. не зависеть от русской, английской или немецкой раскладки клавиатуры;
3. не перегружать одиночные буквенные клавиши конфликтующими командами;
4. держать самые частые операции под левой рукой;
5. разделять обычное моделирование и работу с переиспользуемыми объектами;
6. позволять перенести настройки через пользовательскую роль NX.

Поэтому рекомендуется **гибридная схема**:

- штатные сочетания NX не изменяются;
- `Ctrl+3…0` — основные операции моделирования;
- `Ctrl+Shift+3…0` — шаблоны, библиотеки и параметризация;
- мышь отвечает за навигацию и подтверждение действий;
- редко используемые команды вызываются через **Command Finder**, контекстное меню или собственную вкладку ленты.

### 1.1. Принцип NXKeys v2: активный модуль меняет меню

В NXKeys v2 меню не является одной общей панелью на весь NX. Активное приложение NX выбирает командный набор: Modeling, Sketch, Assembly, Drafting, PMI, Surface, Sheet Metal, CAM/Manufacturing, CAE/Simulation, Routing, Mold/Tooling, Reuse/Templates, Inspect/View или Selection/Object.

Состав команд меняется, но семантика слотов остаётся общей:

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

Общий слой выбора геометрии доступен поверх любого модуля: Body, Face, Edge, Feature, Component, Curve, Point, Datum, Sheet Body и Reset Filter. Опасные операции помечаются как `destructive` или `confirm_before_execute`; Leader HUD требует `Enter`, а `Esc` отменяет выбор. Переключение модулей выполняется по контексту NX Command Bridge или вручную через `Tab`/`Shift+Tab`.

Почему используются цифры, а не `Ctrl+Alt+буква`:

- `Ctrl+Alt` на европейских клавиатурах может интерпретироваться как `AltGr`;
- буквенные сочетания меняются по смыслу при переключении языка;
- цифровой ряд одинаково расположен на большинстве полноразмерных и компактных клавиатур;
- цифровые сочетания легче объединить в логические «банки» команд.

---

## 2. Обозначения

| Метка | Значение |
|---|---|
| **Штатная** | Обычно назначена NX по умолчанию; проверьте в своей роли |
| **Рекомендуемая** | Пользовательское назначение, которое нужно добавить вручную |
| **Контекстная** | Работает только в определённом приложении, диалоге или при выбранном объекте |
| **Проверить** | Название или доступность команды может отличаться в вашей поставке NX |

---

## 3. Штатное ядро NX — не переназначать

### 3.1. Файлы и редактирование

| Сочетание | Команда | Статус | Комментарий |
|---|---|---|---|
| `Ctrl+N` | New | Штатная | Новый файл по шаблону |
| `Ctrl+O` | Open | Штатная | Открыть файл |
| `Ctrl+S` | Save | Штатная | Сохранить рабочую деталь |
| `Ctrl+Shift+A` | Save As | Штатная | Перед изменением проверьте назначение в своей роли |
| `Ctrl+Z` | Undo | Штатная | Отмена операции |
| `Ctrl+Y` | Redo | Штатная | Повтор отменённой операции |
| `Ctrl+X` | Cut | Штатная | Вырезать |
| `Ctrl+C` | Copy | Штатная | Копировать |
| `Ctrl+V` | Paste | Штатная | Вставить |
| `Ctrl+D` | Delete | Штатная | Удалить выбранное; клавиша `Delete` также часто работает в навигаторах |
| `Ctrl+A` | Select All | Штатная | Использовать осторожно в больших моделях и сборках |
| `Esc` | Cancel / Deselect | Контекстная | Отмена команды, выход из шага или очистка выбора |

### 3.2. Вид и ориентация

| Сочетание | Команда | Статус | Комментарий |
|---|---|---|---|
| `Ctrl+F` | Fit View | Штатная | Вписать видимую геометрию в графическое окно |
| `Home` | Trimetric View | Штатная | Базовый объёмный вид |
| `End` | Isometric View | Штатная | Изометрический вид |
| `F8` | Closest Orthographic View | Штатная | Ближайший ортогональный вид |
| `Shift+F8` | Normal to Sketch | Контекстная | Нормаль к плоскости активного эскиза |
| `Ctrl+F8` | Reset Orientation | Штатная/версионная | Проверить назначение в NX 2412 |
| `W` | WCS Display | Штатная | Показать или скрыть рабочую систему координат; **это не Wireframe** |
| `F5` | Refresh | Штатная/контекстная | Обновить отображение |
| `F11` | Maximize Resource Bar Tab | Штатная/версионная | Удобно для Part Navigator и Reuse Library |

### 3.3. Навигация мышью

| Действие | Результат |
|---|---|
| Средняя кнопка мыши + перемещение | Вращение вида |
| `Shift` + средняя кнопка + перемещение | Панорамирование |
| Колесо мыши | Масштабирование |
| Средняя кнопка мыши — короткое нажатие | Переход к следующему шагу, Apply или OK во многих диалогах NX |
| Двойное нажатие средней кнопки | В некоторых конфигурациях — Fit View; основной надёжный вариант: `Ctrl+F` |

> Средняя кнопка мыши в NX — часть основной системы управления. Не стоит заменять её клавиатурными командами без необходимости.

### 3.4. Системные команды NX

| Сочетание | Команда | Статус | Комментарий |
|---|---|---|---|
| `Ctrl+E` | Expressions | Штатная | Выражения и параметры детали |
| `Ctrl+1` | Customize | Штатная | Настройка ленты, команд и клавиатуры |
| `Ctrl+2` | User Interface Preferences | Штатная | Настройки интерфейса |
| `Ctrl+I` | Object Information | Штатная | Информация о выбранном объекте |
| `Ctrl+J` | Object Display | Штатная | Цвет, прозрачность и отображение объекта |
| `Ctrl+L` | Layer Settings | Штатная | Управление слоями |
| `Ctrl+M` | Modeling Application | Штатная | Переход в Modeling |
| `Ctrl+Shift+D` | Drafting Application | Штатная | Переход в Drafting |
| `F1` | Context Help | Штатная | Справка по активной команде |
| `F3` | Current Dialog | Штатная/версионная | Возврат фокуса к активному диалогу |

---

## 4. Эскиз — штатные и контекстные команды

В исходной шпаргалке клавиши `L`, `R`, `C`, `E`, `I` были представлены как универсальные штатные команды Sketch. Это ненадёжно: назначения зависят от версии, роли и пользовательских настроек.

Проверенное историческое ядро NX Sketch:

| Сочетание | Команда | Статус |
|---|---|---|
| `Z` | Create Sketch / Profile | Штатная/контекстная |
| `A` | Arc | Контекстная |
| `C` | Geometric Constraints | Контекстная |
| `D` | Rapid Dimension / Dimensions | Контекстная |
| `Ctrl+Q` | Finish Sketch | Штатная/контекстная |
| `Shift+F8` | Normal to Sketch | Штатная/контекстная |
| `X` | Extrude | Штатная/контекстная |

### Рекомендация для Sketch

Не назначайте отдельные одиночные клавиши для каждой геометрической примитивы, пока не определите реальные частоты использования. Для большинства пользователей достаточно:

- `Z` — войти в Sketch/Profile;
- `A` — дуга;
- `C` — ограничения;
- `D` — размеры;
- `Ctrl+Q` — завершить эскиз;
- контекстная панель NX — линия, окружность, прямоугольник, обрезка и смещение.

Так уменьшается число конфликтов между Sketch, Modeling, Drafting и Assembly.

---

## 5. Рекомендуемый профиль «NX Pro Hybrid»

### 5.1. Банк моделирования: `Ctrl+3…0`

| Сочетание | Рекомендуемая команда | Логика |
|---|---|---|
| `Ctrl+3` | Sketch | Начало большинства параметрических построений |
| `Ctrl+4` | Extrude | Основная объёмная операция после Sketch |
| `Ctrl+5` | Hole | Самая частая специализированная фича |
| `Ctrl+6` | Edge Blend | Скругления |
| `Ctrl+7` | Chamfer | Фаски рядом со скруглениями |
| `Ctrl+8` | Pattern Feature | Линейный или общий паттерн фич |
| `Ctrl+9` | Mirror Feature | Зеркальное копирование фич |
| `Ctrl+0` | Boolean / Unite | Базовая булева операция; при необходимости назначить на Unite |

### Почему такой порядок

`3 → 4 → 5` образует основной поток:

```text
Sketch → Extrude → Hole
```

`6 → 7` — обработка кромок:

```text
Edge Blend → Chamfer
```

`8 → 9` — размножение геометрии:

```text
Pattern → Mirror
```

`0` завершает ряд операцией над телами.

### Альтернатива для поверхностного моделирования

Если пользователь чаще работает с поверхностями, замените:

| Базовая команда | Поверхностная замена |
|---|---|
| `Ctrl+8` Pattern Feature | Through Curves / Studio Surface |
| `Ctrl+9` Mirror Feature | Trim Sheet |
| `Ctrl+0` Unite | Sew |

Не смешивайте два профиля в одной роли: создайте отдельные роли **NX Pro — Solid** и **NX Pro — Surface**.

---

## 6. Банк Feature Templates и Reuse Library: `Ctrl+Shift+3…0`

| Сочетание | Рекомендуемая команда | Примечание |
|---|---|---|
| `Ctrl+Shift+3` | Reuse Library | Открыть или активировать вкладку библиотеки ресурсов |
| `Ctrl+Shift+4` | Create / Author Feature Template | Создание шаблона из выбранных фич |
| `Ctrl+Shift+5` | Edit Feature Template | Если команда доступна в Customize Keyboard |
| `Ctrl+Shift+6` | Replace Feature Template | Команда возвращена как специализированная возможность в NX 2412 |
| `Ctrl+Shift+7` | WAVE Geometry Linker | Подготовка устойчивых внешних ссылок и мастер-геометрии |
| `Ctrl+Shift+8` | Parameter Table | Создание или редактирование таблицы конфигураций |
| `Ctrl+Shift+9` | Part Navigator | Быстрый возврат к дереву фич |
| `Ctrl+Shift+0` | Command Finder | Поиск редкой команды без запоминания меню |

### Ограничение

Некоторые операции Feature Template доступны только:

- при выбранной подходящей фиче;
- через контекстное меню Part Navigator;
- внутри диалога Template Author;
- в конкретном приложении или лицензируемом модуле.

Если команда не отображается в **Customize Keyboard**, не пытайтесь имитировать её сомнительным макросом. Вместо этого:

1. добавьте команду в собственную группу ленты;
2. добавьте её в Quick Access Toolbar;
3. используйте контекстное меню объекта;
4. примените NX Journal только для стабильного и проверяемого сценария.

---


## 7. Радиальные меню NX: правильная архитектура

NX предоставляет три **Application Radial Toolbar**, которые вызываются непосредственно в графическом окне:

| Вызов | Системное имя области настройки | Рекомендуемая роль |
|---|---|---|
| `Ctrl+Shift+MB1` | `Application Radial 1` | Создание геометрии |
| `Ctrl+Shift+MB2` | `Application Radial 2` | Редактирование и Synchronous Modeling |
| `Ctrl+Shift+MB3` | `Application Radial 3` | Параметры, проверка и повторное использование |

Обозначения мыши:

- `MB1` — левая кнопка;
- `MB2` — средняя кнопка/нажатие колеса;
- `MB3` — правая кнопка.

Отдельно существует **View Shortcut Menu**, вызываемое через `Ctrl+MB3`. Его не следует перегружать командами моделирования: это меню полезно сохранить для видов, Fit и фильтров выбора.

### 7.1. Почему три специализированных круга лучше одного

1. Движение мыши в одном направлении всегда означает похожее действие.
2. Команды создания не смешиваются с опасными командами удаления и замены.
3. После запоминания направления команду можно вызывать жестом почти без визуального поиска.
4. Роль NX можно развернуть на всю команду конструкторов с одинаковой моторной логикой.
5. Редкие и контекстные команды остаются в Object Radial, а не занимают глобальные сектора.

### 7.2. Постоянная пространственная семантика

Во всех радиальных меню рекомендуется закрепить одинаковый смысл направлений:

| Сектор | Семантика |
|---|---|
| **Север — N** | начать, создать, открыть главный объект |
| **Северо-восток — NE** | следующая основная операция процесса |
| **Восток — E** | добавить материал, объект или зависимость |
| **Юго-восток — SE** | преобразовать или заменить |
| **Юг — S** | завершить, удалить или выполнить вторичную обработку |
| **Юго-запад — SW** | уменьшить, убрать, отменить локальный элемент |
| **Запад — W** | размножить, связать или перейти к структуре |
| **Северо-запад — NW** | проверить, измерить или открыть служебную команду |

> Число видимых ячеек и форма панели могут зависеть от версии и роли. Если ваша панель показывает меньше позиций, сохраняйте сначала четыре главных направления `N/E/S/W`, затем добавляйте диагонали.

---

## 8. Рекомендуемое наполнение Application Radial

### 8.1. Application Radial 1 — создание геометрии

**Вызов:** `Ctrl+Shift+MB1`

| Сектор | Команда в интерфейсе NX | Поиск в Customize | Логика |
|---|---|---|---|
| **N** | Sketch | `Sketch`, `Create Sketch` | Начало параметрического построения |
| **NE** | Extrude | `Extrude` | Основная операция после эскиза |
| **E** | Hole | `Hole` | Добавление технологического отверстия |
| **SE** | Revolve | `Revolve` | Альтернативное создание тела вращением |
| **S** | Edge Blend | `Edge Blend`, `Blend` | Завершающая обработка кромок |
| **SW** | Chamfer | `Chamfer` | Вторая базовая обработка кромок |
| **W** | Pattern Feature | `Pattern Feature`, `Pattern` | Размножение созданной фичи |
| **NW** | Mirror Feature | `Mirror Feature`, `Mirror` | Симметричное размножение |

**Почему Hole находится на востоке:** направление вправо интерпретируется как «добавить следующий конструктивный элемент». Скругление и фаска помещены вниз, поскольку обычно выполняются ближе к завершению моделирования.

#### Вариант Surface

Для роли `NX Pro — Surface` замените четыре команды:

| Сектор | Solid | Surface |
|---|---|---|
| **E** | Hole | Through Curves |
| **SE** | Revolve | Swept |
| **W** | Pattern Feature | Trim Sheet |
| **NW** | Mirror Feature | Sew |

Названия для поиска: `Through Curves`, `Swept`, `Trim Sheet`, `Sew`.

### 8.2. Application Radial 2 — редактирование и Synchronous Modeling

**Вызов:** `Ctrl+Shift+MB2`

| Сектор | Команда в интерфейсе NX | Поиск в Customize | Когда использовать |
|---|---|---|---|
| **N** | Edit Parameters | `Edit Parameters` | Параметрическое изменение выбранной фичи |
| **NE** | Edit Sketch | `Edit Sketch` | Возврат к управляющему эскизу |
| **E** | Move Face | `Move Face` | Быстрое перемещение или поворот граней |
| **SE** | Replace Face | `Replace Face` | Замена граней целевой геометрией |
| **S** | Delete Face | `Delete Face` | Удаление локальной геометрии с восстановлением тела |
| **SW** | Resize Hole | `Resize Hole` | Изменение распознанных отверстий |
| **W** | Resize Pattern | `Resize Pattern` | Изменение шага и количества распознанного паттерна |
| **NW** | Radiate Face | `Radiate Face` | Радиальное смещение вращательной геометрии |

Команды `Move Face`, `Resize Hole`, `Replace Face`, `Delete Face`, `Resize Pattern` и `Radiate Face` относятся к текущему набору Synchronous Modeling. Если отдельная команда отсутствует, проверьте активное приложение Modeling и лицензию.

#### Более консервативный вариант для чисто параметрической работы

Если Synchronous Modeling используется редко:

| Сектор | Замена |
|---|---|
| **SE** | Edit with Rollback |
| **S** | Suppress Feature |
| **SW** | Unsuppress Feature |
| **W** | Reorder Feature |
| **NW** | Feature Playback |

Поиск: `Edit with Rollback`, `Suppress Feature`, `Unsuppress Feature`, `Reorder Feature`, `Feature Playback`.

### 8.3. Application Radial 3 — параметры и повторное использование

**Вызов:** `Ctrl+Shift+MB3`

| Сектор | Команда в интерфейсе NX | Поиск в Customize | Назначение |
|---|---|---|---|
| **N** | Expressions | `Expressions` | Главный центр параметризации |
| **NE** | WAVE Geometry Linker | `WAVE Geometry Linker`, `WAVE` | Ассоциативные междетальные ссылки |
| **E** | Reuse Library | `Reuse Library` | Переход к библиотеке объектов |
| **SE** | Create Feature Template | `Create Feature Template`, `Feature Template` | Создание переиспользуемого шаблона |
| **S** | Replace Feature Template | `Replace Feature Template` | Обновление экземпляра шаблона |
| **SW** | Part Navigator | `Part Navigator` | Возврат к дереву модели |
| **W** | Measure | `Measure` | Проверка размеров и геометрии |
| **NW** | Command Finder | `Command Finder` | Поиск редких команд |

`Reuse Library`, `Part Navigator` и некоторые команды Feature Template могут отображаться как элементы Resource Bar или быть контекстными. Если они не назначаются напрямую, сохраните тот же сектор, но добавьте туда ближайшую доступную команду открытия соответствующей вкладки либо корпоративный Journal.

### 8.4. Модульные наборы для всех NX областей

Эта таблица соответствует стартовым defaults в `modules[]`. Команды CAM, CAE, Routing, Mold, PMI и Sheet Metal зависят от лицензии, установленного приложения и корпоративной роли. Если `BUTTON ID` не найден, NXKeys оставляет команду нерешённой и показывает её в отчёте.

| Модуль | Префикс | `N` | `NE` | `E` | `SE` | `S` | `SW` | `W` | `NW` |
|---|---|---|---|---|---|---|---|---|---|
| Modeling | `M` | Sketch | Extrude | Hole | Revolve | Edge Blend | Chamfer | Pattern Feature | Mirror Feature |
| Sketch | `S` | Line | Rectangle | Circle | Arc | Trim | Extend | Offset Curve | Sketch Checker |
| Assembly | `A` | Add Component | New Component | Move Component | Constraints | Replace Component | Remove Component | Pattern Component | Navigator |
| Drafting | `D` | Base View | Projected View | Section View | Detail View | Update Views | View Style | Parts List | Rapid Dimension |
| PMI | `P` | Rapid Dimension | Datum Symbol | Feature Control Frame | Surface Finish | Note | Edit PMI | Model View | Validate PMI |
| Surface | `U` | Through Curves | Swept | Studio Surface | Trim Sheet | Sew | Untrim | Extract Geometry | Face Curvature |
| Sheet Metal | `H` | Base Tab | Flange | Contour Flange | Bend | Unbend | Rebend | Flat Pattern | Preferences |
| CAM/Manufacturing | `C` | Create Operation | Create Tool | Generate Tool Path | Verify Tool Path | Postprocess | Delete Operation | Operation Navigator | Information |
| CAE/Simulation | `X` | Create Solution | Create Load | Create Constraint | Mesh | Solve | Delete Simulation Object | Simulation Navigator | Results |
| Routing | `G` | Create Route | Place Part | Add Stock | Edit Route | Delete Route Object | Remove Part | Routing Navigator | Validate Route |
| Mold/Tooling | `O` | Initialize Project | Parting | Mold Base | Gate | Cooling | Ejector | Mold Library | Validate Mold Design |
| Reuse/Templates | `R` | Expressions | WAVE Linker | Reuse Library | Create Feature Template | Replace Template | Part Navigator | Parameter Table | Command Finder |
| Inspect/View | `V` | Fit | Trimetric | Measure | Object Information | Hide | Show Only | Layer Settings | Command Finder |
| Selection/Object | `F` | Body Priority | Face Priority | Edge Priority | Feature Priority | Component Priority | Curve Priority | Datum Priority | Reset Filter |

### 8.5. Быстрая карточка радиальных меню

```text
Ctrl+Shift+MB1 — CREATE
N  Sketch             NE Extrude
E  Hole               SE Revolve
S  Edge Blend         SW Chamfer
W  Pattern Feature    NW Mirror Feature

Ctrl+Shift+MB2 — EDIT
N  Edit Parameters    NE Edit Sketch
E  Move Face          SE Replace Face
S  Delete Face        SW Resize Hole
W  Resize Pattern     NW Radiate Face

Ctrl+Shift+MB3 — REUSE / CONTROL
N  Expressions        NE WAVE Geometry Linker
E  Reuse Library      SE Create Feature Template
S  Replace Template   SW Part Navigator
W  Measure            NW Command Finder
```

---

## 9. Object Radial — контекстные меню выбранного объекта

Application Radial работает от активного приложения. **Object Radial** должен зависеть от того, что выбрано: грань, кромка, фича или компонент. Это позволяет не помещать опасные и редко применяемые команды в глобальные круги.

### 9.1. Выбрана грань — Face Object Radial

| Сектор | Команда | Поиск в Customize |
|---|---|---|
| **N** | Move Face | `Move Face` |
| **NE** | Offset Region | `Offset Region`, `Offset Face` |
| **E** | Replace Face | `Replace Face` |
| **SE** | Delete Face | `Delete Face` |
| **S** | Object Display | `Object Display`, `Edit Object Display` |
| **SW** | Hide | `Hide` |
| **W** | Measure | `Measure` |
| **NW** | Select Similar Faces | `Select Similar Faces`, `Select Similar` |

### 9.2. Выбрана кромка — Edge Object Radial

| Сектор | Команда | Поиск в Customize |
|---|---|---|
| **N** | Edge Blend | `Edge Blend` |
| **NE** | Chamfer | `Chamfer` |
| **E** | Measure | `Measure` |
| **SE** | Delete | `Delete` |
| **S** | Object Display | `Object Display` |
| **SW** | Hide | `Hide` |
| **W** | Select Similar Edges | `Select Similar Edges`, `Select Similar` |
| **NW** | Object Information | `Object Information`, `Information` |

### 9.3. Выбрана фича — Feature Object Radial

| Сектор | Команда | Поиск в Customize |
|---|---|---|
| **N** | Edit Parameters | `Edit Parameters` |
| **NE** | Edit with Rollback | `Edit with Rollback` |
| **E** | Show Parents | `Show Parents`, `Parents` |
| **SE** | Show Children | `Show Children`, `Children` |
| **S** | Suppress Feature | `Suppress Feature`, `Suppress` |
| **SW** | Unsuppress Feature | `Unsuppress Feature`, `Unsuppress` |
| **W** | Rename | `Rename` |
| **NW** | Object Information | `Object Information` |

> `Show Parents` и `Show Children` могут быть представлены только в контекстном меню Part Navigator. Если их нет в списке Shortcuts, оставьте их в штатном контекстном меню.

### 9.4. Выбран компонент сборки — Component Object Radial

| Сектор | Команда | Поиск в Customize |
|---|---|---|
| **N** | Make Work Part | `Make Work Part` |
| **NE** | Make Displayed Part | `Make Displayed Part` |
| **E** | Move Component | `Move Component` |
| **SE** | Assembly Constraints | `Assembly Constraints`, `Constraints` |
| **S** | Replace Component | `Replace Component` |
| **SW** | Hide | `Hide` |
| **W** | Show Only | `Show Only` |
| **NW** | Properties | `Properties`, `Component Properties` |

`Replace Component` оставлен в нижнем секторе, чтобы снизить риск случайного вызова. Не размещайте Delete Component на главном направлении.

### 9.5. Как настроить Object Radial

1. Откройте `Ctrl+1 → Customize`.
2. Перейдите на вкладку `Shortcuts`.
3. Выберите нужный тип shortcut/radial.
4. Не закрывая Customize, выберите в графическом окне объект нужного типа.
5. Настройте появившийся объектно-зависимый radial или shortcut toolbar перетаскиванием команд.
6. Повторите отдельно для Face, Edge, Feature и Component.
7. Сохраните роль и перезапустите NX для контрольной проверки.

---

## 10. Каталог команд для быстрой настройки конфигурации

### 10.1. Как читать каталог

- **Основное имя EN** — первое имя, которое нужно вводить в поиске `Customize → Commands` или `Customize → Keyboard`.
- **Альтернативный поиск** — слова для случая другой локализации или немного изменённого имени команды.
- **Контекст** — приложение или тип выбранного объекта.
- **Назначение** — рекомендуемая клавиша или сектор radial из этого документа.

NX обычно показывает пользователю отображаемое имя команды, а не гарантированно стабильный внутренний идентификатор. Поэтому для переносимого корпоративного профиля лучше сохранять и распространять пользовательскую роль NX, а не редактировать конфигурационные файлы вручную.

### 10.2. Файлы, интерфейс и навигация

| Основное имя EN | Альтернативный поиск | Контекст | Назначение |
|---|---|---|---|
| New | `New File`, `New` | Global | `Ctrl+N` |
| Open | `Open File`, `Open` | Global | `Ctrl+O` |
| Save | `Save` | Global | `Ctrl+S` |
| Save As | `Save As` | Global | `Ctrl+Shift+A`, проверить |
| Undo | `Undo` | Global | `Ctrl+Z` |
| Redo | `Redo` | Global | `Ctrl+Y` |
| Delete | `Delete` | Selection | `Ctrl+D`/`Delete` |
| Select All | `Select All` | Global | `Ctrl+A` |
| Customize | `Customize` | Global | `Ctrl+1` |
| User Interface Preferences | `Interface Preferences`, `Preferences` | Global | `Ctrl+2` |
| Command Finder | `Command Finder`, `Find Command` | Global | Radial 3 — NW |
| Part Navigator | `Part Navigator`, `Navigator` | Modeling | Radial 3 — SW |
| Reuse Library | `Reuse Library`, `Resource Bar` | Modeling | Radial 3 — E |
| Modeling | `Modeling Application`, `Modeling` | Global | `Ctrl+M` |
| Drafting | `Drafting Application`, `Drafting` | Global | `Ctrl+Shift+D` |

### 10.3. Виды и отображение

| Основное имя EN | Альтернативный поиск | Контекст | Назначение |
|---|---|---|---|
| Fit | `Fit View`, `Fit` | Graphics | `Ctrl+F` |
| Trimetric | `Trimetric View`, `Trimetric` | Graphics | `Home` |
| Isometric | `Isometric View`, `Isometric` | Graphics | `End` |
| Closest Orthographic | `Closest Orthographic View`, `Orthographic` | Graphics | `F8` |
| Normal to Sketch | `Normal to Sketch`, `Normal View` | Sketch | `Shift+F8` |
| Reset Orientation | `Reset Orientation` | Graphics | `Ctrl+F8`, проверить |
| WCS Display | `WCS Display`, `Display WCS` | Graphics | `W` |
| Refresh | `Refresh` | Global | `F5` |
| Object Display | `Object Display`, `Edit Object Display` | Selection | `Ctrl+J`, Object Radial |
| Object Information | `Object Information`, `Information` | Selection | `Ctrl+I`, Object Radial |
| Layer Settings | `Layer Settings`, `Layers` | Global | `Ctrl+L` |
| Hide | `Hide`, `Blank` | Selection | Object Radial |
| Show Only | `Show Only`, `Show Exclusively` | Selection | Component Radial — W |

### 10.4. Sketch и параметризация

| Основное имя EN | Альтернативный поиск | Контекст | Назначение |
|---|---|---|---|
| Sketch | `Create Sketch`, `Sketch` | Modeling | `Ctrl+3`, Radial 1 — N |
| Profile | `Profile`, `Create Profile` | Sketch | штатная `Z`, проверить |
| Arc | `Arc` | Sketch | штатная `A`, проверить |
| Geometric Constraints | `Constraints`, `Geometric Constraints` | Sketch | штатная `C`, проверить |
| Rapid Dimension | `Rapid Dimension`, `Dimension` | Sketch | штатная `D`, проверить |
| Finish Sketch | `Finish Sketch`, `Exit Sketch` | Sketch | `Ctrl+Q` |
| Edit Sketch | `Edit Sketch` | Feature/Sketch | Radial 2 — NE |
| Expressions | `Expressions` | Modeling | `Ctrl+E`, Radial 3 — N |
| Edit Parameters | `Edit Parameters` | Feature | Radial 2 — N |
| Edit with Rollback | `Edit with Rollback`, `Rollback` | Feature | параметрический вариант Radial 2 |

### 10.5. Основное твердотельное моделирование

| Основное имя EN | Альтернативный поиск | Контекст | Назначение |
|---|---|---|---|
| Extrude | `Extrude` | Modeling | `Ctrl+4`, Radial 1 — NE |
| Hole | `Hole` | Modeling | `Ctrl+5`, Radial 1 — E |
| Revolve | `Revolve` | Modeling | Radial 1 — SE |
| Edge Blend | `Edge Blend`, `Blend` | Modeling | `Ctrl+6`, Radial 1 — S |
| Chamfer | `Chamfer` | Modeling | `Ctrl+7`, Radial 1 — SW |
| Pattern Feature | `Pattern Feature`, `Pattern` | Modeling | `Ctrl+8`, Radial 1 — W |
| Mirror Feature | `Mirror Feature`, `Mirror` | Modeling | `Ctrl+9`, Radial 1 — NW |
| Unite | `Unite`, `Boolean Unite` | Modeling | `Ctrl+0` |
| Subtract | `Subtract`, `Boolean Subtract` | Modeling | дополнительная команда |
| Intersect | `Intersect`, `Boolean Intersect` | Modeling | дополнительная команда |
| Datum Plane | `Datum Plane` | Modeling | кандидат для QAT/доп. radial |

### 10.6. Поверхности

| Основное имя EN | Альтернативный поиск | Контекст | Назначение Surface |
|---|---|---|---|
| Through Curves | `Through Curves` | Modeling | Radial 1 — E |
| Swept | `Swept`, `Sweep` | Modeling | Radial 1 — SE |
| Trim Sheet | `Trim Sheet`, `Trim` | Modeling | Radial 1 — W |
| Sew | `Sew` | Modeling | Radial 1 — NW |

### 10.7. Synchronous Modeling

| Основное имя EN | Альтернативный поиск | Контекст | Назначение |
|---|---|---|---|
| Move Face | `Move Face` | Face/Modeling | Radial 2 — E |
| Replace Face | `Replace Face` | Face/Modeling | Radial 2 — SE |
| Delete Face | `Delete Face` | Face/Modeling | Radial 2 — S |
| Resize Hole | `Resize Hole` | Face/Modeling | Radial 2 — SW |
| Resize Pattern | `Resize Pattern` | Face/Modeling | Radial 2 — W |
| Radiate Face | `Radiate Face` | Face/Modeling | Radial 2 — NW |
| Offset Region | `Offset Region`, `Offset Face` | Face/Modeling | Face Object Radial — NE |
| Resize Blend | `Resize Blend` | Face/Modeling | альтернативная команда |
| Resize Chamfer | `Resize Chamfer` | Face/Modeling | альтернативная команда |

### 10.8. Feature Templates, Reuse и WAVE

| Основное имя EN | Альтернативный поиск | Контекст | Назначение |
|---|---|---|---|
| Create Feature Template | `Feature Template`, `Author Feature Template`, `Template Author` | Modeling | `Ctrl+Shift+4`, Radial 3 — SE |
| Edit Feature Template | `Edit Feature Template`, `Feature Template` | Context | `Ctrl+Shift+5`, проверить |
| Replace Feature Template | `Replace Feature Template` | Context | `Ctrl+Shift+6`, Radial 3 — S |
| WAVE Geometry Linker | `WAVE Geometry Linker`, `WAVE` | Modeling/Assembly | `Ctrl+Shift+7`, Radial 3 — NE |
| Parameter Table | `Parameter Table`, `Table from Expressions` | Template Author | `Ctrl+Shift+8`, если доступно |
| Validate Template | `Validate Template` | Template Author | внутри диалога, не глобально |
| Define Feature Reference | `Define Feature Reference` | Template Author | внутри диалога |
| Configure User Interface | `Configure User Interface` | Template Author | внутри диалога |

### 10.9. Сборки

| Основное имя EN | Альтернативный поиск | Контекст | Назначение |
|---|---|---|---|
| Add Component | `Add Component`, `Place Component` | Assembly | отдельная клавиша/QAT |
| Move Component | `Move Component` | Assembly | Component Radial — E |
| Assembly Constraints | `Assembly Constraints`, `Constraints` | Assembly | Component Radial — SE |
| Replace Component | `Replace Component` | Assembly | Component Radial — S |
| Make Work Part | `Make Work Part` | Component | Component Radial — N |
| Make Displayed Part | `Make Displayed Part` | Component | Component Radial — NE |
| Component Properties | `Component Properties`, `Properties` | Component | Component Radial — NW |

### 10.10. Структура, выбор и диагностика

| Основное имя EN | Альтернативный поиск | Контекст | Назначение |
|---|---|---|---|
| Measure | `Measure` | Modeling | Radial 3 — W |
| Select Similar Faces | `Select Similar Faces`, `Select Similar` | Face | Face Radial — NW |
| Select Similar Edges | `Select Similar Edges`, `Select Similar` | Edge | Edge Radial — W |
| Suppress Feature | `Suppress Feature`, `Suppress` | Feature | Feature Radial — S |
| Unsuppress Feature | `Unsuppress Feature`, `Unsuppress` | Feature | Feature Radial — SW |
| Rename | `Rename` | Navigator | Feature Radial — W |
| Show Parents | `Show Parents`, `Parents` | Feature | Feature Radial — E, если доступно |
| Show Children | `Show Children`, `Children` | Feature | Feature Radial — SE, если доступно |
| Feature Playback | `Feature Playback`, `Playback` | Modeling | параметрический вариант Radial 2 — NW |

### 10.11. Скопируемый конфигурационный манифест

Этот блок можно использовать как чек-лист при ручной настройке роли:

```yaml
profile: NX_Pro_Hybrid_2412_v2

keyboard:
  Ctrl+3: Sketch
  Ctrl+4: Extrude
  Ctrl+5: Hole
  Ctrl+6: Edge Blend
  Ctrl+7: Chamfer
  Ctrl+8: Pattern Feature
  Ctrl+9: Mirror Feature
  Ctrl+0: Unite
  Ctrl+Shift+3: Reuse Library
  Ctrl+Shift+4: Create Feature Template
  Ctrl+Shift+5: Edit Feature Template
  Ctrl+Shift+6: Replace Feature Template
  Ctrl+Shift+7: WAVE Geometry Linker
  Ctrl+Shift+8: Parameter Table
  Ctrl+Shift+9: Part Navigator
  Ctrl+Shift+0: Command Finder

application_radial_1:
  N: Sketch
  NE: Extrude
  E: Hole
  SE: Revolve
  S: Edge Blend
  SW: Chamfer
  W: Pattern Feature
  NW: Mirror Feature

application_radial_2:
  N: Edit Parameters
  NE: Edit Sketch
  E: Move Face
  SE: Replace Face
  S: Delete Face
  SW: Resize Hole
  W: Resize Pattern
  NW: Radiate Face

application_radial_3:
  N: Expressions
  NE: WAVE Geometry Linker
  E: Reuse Library
  SE: Create Feature Template
  S: Replace Feature Template
  SW: Part Navigator
  W: Measure
  NW: Command Finder
```

> YAML выше — документированный манифест для человека, а не готовый импортируемый файл NX. NX следует настраивать через Customize и сохранять в пользовательскую роль.

---

## 11. Команды, которые не стоит назначать глобально

| Команда | Почему не нужна отдельная глобальная клавиша |
|---|---|
| Line, Rectangle, Circle | Доступны в контекстной панели Sketch; одиночные буквы быстро конфликтуют |
| OK / Apply / Next | Средняя кнопка мыши уже решает эту задачу во многих диалогах |
| Validate Template | Обычно элемент конкретного диалога Template Author |
| Insert из Reuse Library | Требует выбора объекта библиотеки и точки/грани размещения |
| Rename | Зависит от выбранного элемента Navigator |
| Replace Component | Опасная контекстная операция; лучше вызывать осознанно |
| Delete All / Show All | Слишком широкие действия для лёгкодоступной клавиши |
| Suppress / Unsuppress | Риск случайно изменить структуру модели или сборки |

---

## 12. Приоритеты выбора — сохранить штатными

В NX очень полезны горячие клавиши фильтра приоритета выбора. Они снижают количество ошибочных кликов в сложной модели.

| Сочетание | Приоритет |
|---|---|
| `Shift+F` | Feature |
| `Shift+G` | Face |
| `Shift+B` | Body |
| `Shift+E` | Edge |
| `Shift+C` | Component |

> Перед использованием проверьте назначения в `Ctrl+1 → Keyboard`, поскольку корпоративная роль может их изменить.

Не занимайте эти сочетания командами моделирования.

---

## 13. Быстрые рабочие последовательности

### 13.1. Создание типовой параметрической фичи

```text
Ctrl+3        создать Sketch
D             расставить размеры
Ctrl+Q        завершить Sketch
Ctrl+4        Extrude
Ctrl+E        переименовать и связать выражения
Ctrl+S        сохранить
```

### 13.2. Создание Feature Template

```text
1. Построить и проверить исходную геометрию.
2. Ctrl+E — дать выражениям устойчивые имена.
3. Выбрать главные фичи в Part Navigator.
4. Ctrl+Shift+4 — Create / Author Feature Template.
5. Включить зависимые фичи только после проверки графа зависимостей.
6. Определить входные ссылки, параметры и UI шаблона.
7. Добавить Parameter Table при наличии дискретных типоразмеров.
8. Выполнить Validate внутри Template Author.
9. Сохранить шаблон в управляемую библиотеку.
10. Проверить вставку в чистой тестовой детали.
```

### 13.3. Вставка объекта из Reuse Library

```text
Ctrl+Shift+3  открыть Reuse Library
F11           развернуть вкладку Resource Bar при необходимости
Выбрать       объект или Feature Template
Drag & Drop   разместить на геометрии
MMB           последовательно подтвердить шаги
Ctrl+S        сохранить
```

### 13.4. Обновление экземпляра шаблона в NX 2412

```text
1. Сохранить резервную копию детали.
2. Выбрать экземпляр Feature Template в Part Navigator.
3. Ctrl+Shift+6 — Replace Feature Template
   или вызвать команду через контекстное меню.
4. Выбрать новую версию шаблона.
5. Пересопоставить входные ссылки и параметры.
6. Проверить предупреждения обновления.
7. Выполнить визуальную и параметрическую проверку.
8. Сохранить только после успешной регенерации.
```

---

## 14. Как собрать конфигурацию в NX 2412

### 14.1. Назначение клавиатурных сочетаний

1. Активируйте приложение, для которого настраивается команда: `Modeling`, `Assemblies`, `Drafting` и т. д.
2. Нажмите `Ctrl+1` и откройте **Customize**.
3. Перейдите к настройке **Keyboard**.
4. Найдите команду по столбцу **Основное имя EN** из раздела 10.
5. Если команда не находится, используйте слова из столбца **Альтернативный поиск**.
6. Выберите найденную команду и проверьте её описание и категорию.
7. Введите сочетание и проверьте список существующих назначений.
8. При конфликте сначала определите, используется ли старое назначение в другом приложении.
9. Нажмите **Assign** и испытайте команду на тестовой детали.
10. Не закрывайте Customize, пока не проверите весь логический банк сочетаний.

### 14.2. Настройка Application Radial 1/2/3

1. Откройте `Ctrl+1 → Customize`.
2. Перейдите на вкладку **Shortcuts**.
3. Выберите по очереди:
   - `Application Radial 1`;
   - `Application Radial 2`;
   - `Application Radial 3`.
4. Перетаскивайте команды из списка **Commands** в нужные сектора radial toolbar.
5. Настройте круги по таблицам раздела 8.
6. Проверьте вызовы в графическом окне:
   - `Ctrl+Shift+MB1`;
   - `Ctrl+Shift+MB2`;
   - `Ctrl+Shift+MB3`.
7. Запомните направление каждой команды, затем проверьте жестовый вызов без визуального поиска.

### 14.3. Настройка View Shortcut Menu

1. В `Customize → Shortcuts` выберите тип shortcut, относящийся к виду.
2. Сохраните команды `Fit`, стандартные ориентации и фильтры выбора.
3. Проверяйте меню через `Ctrl+MB3` в графическом окне.
4. Не дублируйте здесь основной набор Create/Edit: View Shortcut должен оставаться быстрым и предсказуемым.

### 14.4. Настройка Object Radial

1. Оставьте окно Customize открытым.
2. Выберите объект нужного класса: Face, Edge, Feature или Component.
3. Перейдите к соответствующему object-specific shortcut/radial.
4. Разместите команды по таблицам раздела 9.
5. Повторите настройку для каждого типа объекта отдельно.
6. Проверьте, что опасные действия `Delete`, `Replace Component` и `Suppress` не находятся в самом лёгком для случайного жеста направлении.

### 14.5. Проверка имени команды

При одинаковых или похожих названиях используйте следующий порядок проверки:

```text
1. Основное имя EN из каталога
2. Альтернативный поисковый токен
3. Категория приложения
4. Описание команды в Customize
5. Пробный запуск на тестовой детали
```

Не ориентируйтесь только на перевод команды. Например, поиск по `Blend` может показать несколько разных типов скругления; для рекомендуемого профиля нужна именно команда `Edge Blend`.

### 14.6. Сохранение результата

После настройки клавиш и radial toolbar:

1. сохраните интерфейс как новую пользовательскую роль;
2. назовите роль с указанием версии, например `NX Pro Hybrid 2412 v2`;
3. перезапустите NX и повторно проверьте все три radial toolbar;
4. протестируйте роль в Modeling и Assembly;
5. сохраните резервную копию роли отдельно от рабочего профиля.

Названия могут отличаться в зависимости от локализации, лицензии и состава корпоративной роли. Каталог в разделе 10 специально содержит основной вариант и альтернативные слова поиска.

---

## 15. Сохранение и перенос профиля

Горячие клавиши и элементы интерфейса NX связаны с пользовательской ролью. После настройки:

1. создайте роль `NX Pro Hybrid 2412`;
2. сохраните текущую конфигурацию интерфейса в эту роль;
3. экспортируйте или скопируйте файл роли согласно правилам вашей установки;
4. храните резервную копию отдельно от локального профиля NX;
5. перед переносом в следующую версию NX протестируйте роль на копии профиля.

Рекомендуемые роли:

```text
NX Pro Hybrid 2412 — Solid
NX Pro Hybrid 2412 — Surface
NX Pro Hybrid 2412 — Assembly
NX Pro Hybrid 2412 — Feature Templates
```

Не пытайтесь сделать одну перегруженную роль для всех приложений.

---

## 16. Проверка конфликтов перед внедрением

Перед назначением каждой клавиши заполните таблицу:

| Сочетание | Новая команда | Было назначено | Контекст | Решение |
|---|---|---|---|---|
| `Ctrl+3` | Sketch | — | Modeling | Назначить |
| `Ctrl+4` | Extrude | — | Modeling | Назначить |
| `Ctrl+5` | Hole | — | Modeling | Назначить |
| `Ctrl+6` | Edge Blend | — | Modeling | Назначить |
| `Ctrl+7` | Chamfer | — | Modeling | Назначить |
| `Ctrl+8` | Pattern Feature | — | Modeling | Назначить |
| `Ctrl+9` | Mirror Feature | — | Modeling | Назначить |
| `Ctrl+0` | Unite | — | Modeling | Назначить |
| `Ctrl+Shift+3` | Reuse Library | — | Global/Modeling | Проверить |
| `Ctrl+Shift+4` | Create Feature Template | — | Modeling | Проверить |
| `Ctrl+Shift+5` | Edit Feature Template | — | Context | Проверить |
| `Ctrl+Shift+6` | Replace Feature Template | — | Context | Проверить |
| `Ctrl+Shift+7` | WAVE Geometry Linker | — | Modeling/Assembly | Проверить |
| `Ctrl+Shift+8` | Parameter Table | — | Modeling | Проверить |
| `Ctrl+Shift+9` | Part Navigator | — | Global | Проверить |
| `Ctrl+Shift+0` | Command Finder | — | Global | Проверить |

---

## 17. Что исправлено относительно исходной шпаргалки

### Исправлено

- `Ctrl+F` указан как штатный **Fit View** вместо неподтверждённой клавиши `V`.
- `W` корректно описана как показ/скрытие **WCS**, а не Wireframe.
- `Home`, `End`, `F8` и `Shift+F8` разделены по назначению.
- Команды Sketch отделены от пользовательских назначений.
- Удалены ненадёжные утверждения о глобальных `L`, `R`, `E`, `I`, `G`, `P`, `N`, `S`.
- `Alt+O`, `Alt+C`, `Alt+A` не используются как универсальные команды: буквенные мнемоники зависят от языка и конкретного диалога.
- Учтена роль средней кнопки мыши для перехода по шагам и подтверждения.
- Feature Template и Reuse Library отделены от обычного моделирования.
- Добавлена стратегия сохранения раскладки в роли NX.
- Убраны нерелевантные и потенциально неверные примеры NX Open Python: они не относятся к задаче горячих клавиш и требуют отдельной проверки API конкретной версии.

### Уточнено

- Наличие встроенных библиотек зависит от установленного контента, лицензии, Customer Defaults и конфигурации Teamcenter.
- `Replace Feature Template` рассматривается как контекстная команда NX 2412, а не как гарантированно глобальная клавиша.
- Пути меню могут отличаться между классическим меню, Ribbon UI, локализациями и корпоративными ролями.

---

## 18. Минимальная карточка для печати

### Штатное ядро

```text
Ctrl+N        New
Ctrl+O        Open
Ctrl+S        Save
Ctrl+Z / Y    Undo / Redo
Ctrl+E        Expressions
Ctrl+F        Fit View
Ctrl+1        Customize
Ctrl+Q        Finish Sketch
Home          Trimetric
End           Isometric
F8            Closest Orthographic
Shift+F8      Normal to Sketch
W             WCS On/Off
MMB           Next / Apply / OK
```

### NX Pro Hybrid

```text
Ctrl+3        Sketch
Ctrl+4        Extrude
Ctrl+5        Hole
Ctrl+6        Edge Blend
Ctrl+7        Chamfer
Ctrl+8        Pattern Feature
Ctrl+9        Mirror Feature
Ctrl+0        Unite

Ctrl+Shift+3  Reuse Library
Ctrl+Shift+4  Create Feature Template
Ctrl+Shift+5  Edit Feature Template
Ctrl+Shift+6  Replace Feature Template
Ctrl+Shift+7  WAVE Geometry Linker
Ctrl+Shift+8  Parameter Table
Ctrl+Shift+9  Part Navigator
Ctrl+Shift+0  Command Finder
```

---

## 19. Финальный чек-лист

- [ ] Открыта отдельная пользовательская роль NX.
- [ ] Снята резервная копия текущей роли.
- [ ] Проверены все штатные сочетания через `Ctrl+1 → Keyboard`.
- [ ] Назначен только один банк пользовательских команд.
- [ ] Настроены `Application Radial 1`, `Application Radial 2` и `Application Radial 3`.
- [ ] Проверены Object Radial для Face, Edge, Feature и Component.
- [ ] Направления команд одинаковы во всех ролях NX.
- [ ] Нет конфликтов с `Ctrl+1`, `Ctrl+2`, `Ctrl+E`, `Ctrl+F` и `Ctrl+Shift+D`.
- [ ] Сохранены приоритеты выбора `Shift+F/G/B/E/C`.
- [ ] Все сочетания проверены в Modeling.
- [ ] Контекстные сочетания проверены на реальных Feature Templates.
- [ ] Роль протестирована после перезапуска NX.
- [ ] Копия роли сохранена вне локального профиля NX.

---

## Источники проверки

0. Siemens Designcenter — **NX Timesaving Clicks and Tricks, Part Two**: вызов `Application Radial 1/2/3` через `Ctrl+Shift+MB1/2/3`, настройка radial toolbar и распространение через роли.
   https://blogs.sw.siemens.com/designcenter/nx-timesaving-clicks-and-tricks-two/


1. Siemens Community — **NX Shortcut Keys: View Full List and Create Custom Keys**
   https://community.sw.siemens.com/s/article/nx-shortcut-keys-view-full-list-and-create-custom-keys

2. Siemens Software Blog — **NX Hints and Tips: Rapid Sketches with Keyboard Shortcuts**
   https://blogs.sw.siemens.com/news/nx-hints-and-tips-rapid-sketches-with-keyboard-shortcuts/

3. Siemens Software Blog — **Using the END key, HOME key and F8 keys**
   https://blogs.sw.siemens.com/news/nx-hints-and-tips-using-the-end-key-home-key-and-f8-keys/

4. Siemens Designcenter — **NX Timesaving Clicks and Tricks, Part Two**
   https://blogs.sw.siemens.com/designcenter/nx-timesaving-clicks-and-tricks-two/

5. Siemens Community — обсуждения сохранения сочетаний в пользовательской роли NX.

6. Материалы по NX 2412 — **Replace Feature Template** как специализированная команда для экземпляров Feature Template.

> Дата актуализации документа: 21 июля 2026 года. Версия документа: 2.0. Перед корпоративным внедрением проверьте английские имена команд, доступность shortcut-назначений, активную роль и лицензию именно в вашей сборке NX 2412.
