# ADR-0001: Разделение каталога, bootstrap, generated и installed profile

- Status: Accepted
- Date: 2026-07-30

## Context

NXKeys должен одновременно:

- хранить полный каталог 1169 намерений K1–K5;
- предоставлять ежедневный runtime scope K3–K5 из 885 намерений;
- сохранять вручную проверенные `BUTTON ID`, safety и deployment settings;
- адаптироваться к конкретной установке NX, лицензии, роли и локализации;
- не включать команду без подтверждённого точного ID;
- сохранять compatibility filename существующих установок.

Один JSON-файл не может быть одновременно стабильным source catalog, переносимым bootstrap, machine-specific resolution output и installed artifact без потери трассировки.

## Decision

Используются четыре уровня.

### 1. Intent catalog

```text
config/full-command-map/
```

Содержит 1169 намерений, частоты, исходные разделы, имена и path hints. Не является напрямую исполняемым профилем.

### 2. Bootstrap profile

```text
config/nx2512-pro-hybrid.json
```

Содержит profile/deployment/scan settings, 12 basic shortcuts, 14 modules, известные IDs, curated aliases и safety defaults.

### 3. Generated main profile

```text
config/nx2512-pro-main.generated.json
```

Создаётся compiler для K3/K4/K5, содержит 885 уникальных intent references, resolution metadata и module rows. Machine-specific catalog может изменить долю `resolved`, `ambiguous` и `unresolved`.

### 4. Installed profile

```text
%LOCALAPPDATA%\NXKeys\managed\NX2512.6000\nx2512-pro-hybrid.json
```

Compatibility filename сохраняется, но содержимое при стандартной установке является generated main profile, а не source bootstrap.

## Consequences

### Положительные

- полный каталог не смешивается с daily runtime scope;
- known IDs и safety settings остаются reviewable;
- generated resolution можно пересоздать для каждой workstation;
- unresolved commands видимы для аудита, но disabled;
- installer может машинно проверять `selected_frequencies` и `selected_intents`;
- старые launcher paths остаются совместимыми.

### Издержки

- имя `nx2512-pro-hybrid.json` имеет два контекста: source bootstrap и installed compatibility file;
- generated artifacts могут устареть относительно source compiler;
- документация обязана явно различать уровни;
- изменения compiler требуют regeneration и review большого diff.

## Alternatives considered

### Один универсальный профиль

Отклонён: смешивает source и machine-specific resolution, усложняет audit и безопасное отключение неподтверждённых IDs.

### Отдельное runtime-имя без compatibility

Не выбрано: ломает существующие launcher и integration paths. Может быть рассмотрено в future breaking release с migration.

### Исполнять intent без точного BUTTON ID

Отклонено: имя или similarity score не являются достаточным доказательством безопасного dispatch.

## Verification

- installer проверяет K3/K4/K5 и 885 selected intents;
- main validator проверяет coverage и отсутствие K1/K2 leakage;
- enabled rows обязаны иметь точный ID;
- resolution report показывает status каждой команды;
- health-check проверяет installed package и compatibility filename.
