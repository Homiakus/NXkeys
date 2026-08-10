# NXKeys Mnemonic Command Language — реализованный v8 runtime

Этот документ описывает **фактически реализованный** мнемонический слой текущей ветки `main`.

Большая целевая v8.3 Left-Hand First спецификация сохранена отдельно как historical/target design:

[historical/MNEMONIC_COMMAND_LANGUAGE_v8.3_LEFT_HAND_TARGET_SPEC.md](historical/MNEMONIC_COMMAND_LANGUAGE_v8.3_LEFT_HAND_TARGET_SPEC.md)

Она полезна как дизайн-направление, но не должна использоваться как доказательство уже работающей команды.

Канонический общий контракт: [RUNTIME_V8.md](RUNTIME_V8.md).

## 1. Current contracts

```text
profile schema 8
IPC schema 4
sequence policy v8
default profile config/nx2512-v8-profile.json
```

Пользователь работает с активным контекстом NX. Internal module prefix добавляется runtime автоматически.

## 2. Общая модель

```text
keyboard input
→ active NX application/module
→ v8 operation path / alias
→ Leader DFA + state guards
→ signed IPC request
→ Bridge permission/context verification
→ exact NX adapter/command
```

Leader не должен исполнять произвольный command ID напрямую.

## 3. CapsLock

`CapsLock` открывает Leader. Physical key-down защёлкивается до real key-up, поэтому keyboard autorepeat не должен превращать одно удержание в несколько Leader activations.

```text
CapsLock → mnemonic path
```

Внутренний module prefix не вводится пользователем.

## 4. Path lengths

В v8 нет универсального минимального размера «2 tokens».

Допустимы:

- 1 token — частая команда в узком контексте;
- 2+ tokens — смысловая группа/уточнение/вариант;
- aliases — дополнительные prefix-free routes.

Current Sketch является основным примером one-token layer.

## 5. Sketch

В active Sketch:

```text
L                Line
R                Rectangle
C                Circle
A                Arc
T                Trim
E                Extend
O                Offset
F                Fillet
H                Chamfer
M                Mirror
V                Move
Y                Pattern
N                Navigator
Z                Checker
```

Составные families:

```text
D → …            Dimensions
K → …            Constraints
C → V → …        Construction variants
J → …            Projection / derived geometry
U → …            Utilities
```

Примеры:

```text
CapsLock → L
CapsLock → K → C
CapsLock → D → Q
CapsLock → C → V → L → 2
```

Полная таблица: [SKETCH_INTENT_LANGUAGE.md](SKETCH_INTENT_LANGUAGE.md).

## 6. Modeling `M = Manage`

Current runtime поддерживает смысловой root Manage в Modeling.

```text
M → L → S    Layer Settings
```

Diagnostic/internal DFA может показывать:

```text
M → M → L → S
```

Первый `M` — hidden Modeling module prefix. Пользователь вводит только второй `M → L → S`.

## 7. `secondary_aliases`

V8 operation может иметь:

```json
{
  "paths": {
    "leader": ["W", "L", "S"],
    "secondary_aliases": ["M->L->S"]
  }
}
```

Alias реально разворачивается в runtime operation path до построения DFA.

Все primary/secondary paths внутри module должны оставаться prefix-free.

## 8. `workspace_key`

`workspace_key` означает клавишу **внутри явного workspace state**.

Current runtime не превращает operation с одним только `workspace_key` в root terminal, пока workspace-state boundary не реализован как отдельное состояние.

Это защищает от конфликтов:

```text
M            terminal workspace-local command
M → L → S    Manage subtree
```

Current expected behavior — не создавать первый root terminal автоматически.

## 9. Module switching

`G → …` зарезервирован для application/module switching в current sequence policy.

Основные направления:

```text
G → M    Modeling
G → S    Sketch
G → A    Assembly
G → D    Drafting
G → P    PMI
G → U    Surface
G → H    Sheet Metal
G → C    Manufacturing
G → N    Simulation
G → R    Routing
G → O    Mold
G → L    Reuse
G → V    Inspect/View
```

Switch завершается только после fresh Bridge context, подтверждающего новую application/module.

Sheet Metal canonical target: `UG_APP_SBSM`.

## 10. Selection type filters через Leader

```text
S → B    Body
S → F    Face
S → E    Edge
S → T    Feature
S → C    Component
S → U    Curve
S → D    Datum
S → R    Reset
S → A    Select All
S → N    Deselect All
```

Этот слой отвечает на вопрос **какой тип объекта** выбирать.

## 11. Selection Intent `0…4`

Selection Intent — отдельный in-process Bridge mechanism, **не Leader path**.

```text
0    Reset
1    Single
2    Connected / Chain
3    Tangent
4    Inferred Path / Region Boundary
```

Он отвечает на вопрос **как распространить выбор** от seed geometry.

Цифры перехватываются только при NX foreground и подходящем active collector/seed, с guards против text/numeric input.

Подробности: [SELECTION_INTENT.md](SELECTION_INTENT.md).

## 12. Direct keys

Profile model поддерживает `paths.direct`, но direct key обязан иметь более строгие context/input guards, чем Leader path.

Не следует считать target-spec direct-key таблицу автоматически реализованной. Реальное поведение определяется current v8 profile + runtime code.

## 13. BUTTON ID policy

Command adapter считается пригодным для production только при подтверждённом точном mapping.

Запрещено:

- подставлять похожий ID по имени;
- использовать Modeling command вместо Sketch/CAM command без доказательства;
- считать API candidate эквивалентом UI command только по similarity;
- автоматически разрешать `tbd_adapter`.

Фактическая availability/sensitivity проверяется в target NX.

## 14. Sheet Metal

Новые/current operations должны использовать NX2512 canonical namespace:

```text
UG_APP_SBSM
UG_SBSM_*
```

Runtime/security поддерживают старые `UG_APP_SHEETMETAL` / `UG_SHEET_METAL_*` только как compatibility mapping.

## 15. Prefix-free invariant

Для каждого runtime module:

- два terminals не могут иметь одинаковый normalized path;
- terminal не может быть префиксом другого terminal;
- alias конфликтует по тем же правилам, что primary path;
- workspace-local key не должен загрязнять root path space;
- hidden module prefixes позволяют одинаковые user paths в разных applications.

## 16. Context priority

Практический порядок admission:

```text
1. system/text-input safety
2. current NX application/module
3. active Leader state
4. resolved v8 path/alias
5. context/selection guards
6. confirmation policy
7. authenticated IPC permission
8. NX availability/sensitivity
```

Selection Intent имеет отдельный узкий in-process guard path.

## 17. Почему target v8.3 spec вынесена отдельно

Target Left-Hand First specification включает:

- будущие direct-key layouts;
- persistent workspaces;
- расширенное role/collector inference;
- целевую ergonomic vocabulary;
- команды/adapters со статусами design intent.

Часть этих идей уже отражена в v8 profile, но не вся спецификация является исполняемым контрактом. Разделение предотвращает ситуацию, когда проектная идея воспринимается как уже проверенная feature.

## 18. Как менять mnemonic language

При изменении current paths:

1. изменить v8 profile/runtime mapping;
2. проверить aliases/prefix-free DFA;
3. добавить regression test;
4. обновить `CHEATSHEET.md` и domain doc;
5. запустить documentation/profile validators;
6. при command ID change проверить target NX catalog;
7. при interactive behavior выполнить live NX test.

Минимум:

```powershell
node .\scripts\validate-documentation.mjs
node .\scripts\validate-command-tree.mjs
node .\scripts\validate-main-command-map.mjs

dotnet run --project .\NX2512_HotkeyStudio.Tests\NX2512_HotkeyStudio.Tests.csproj -c Release
```

## 19. Приоритет источников

1. current code/tests;
2. `RUNTIME_V8.md` и current v8 profile;
3. этот implemented-runtime reference;
4. generated artifacts того же commit;
5. target/historical specs.

Не переносите путь из historical target spec в пользовательскую шпаргалку, пока он не подтверждён current profile/runtime и tests.
