# ADR-0001: Разделение каталога, bootstrap, generated и installed profile

- Status: Superseded
- Date: 2026-07-30
- Superseded by: [ADR-0003](0003-v8-runtime-profile-and-legacy-catalog-separation.md)

## Historical context

Это решение фиксировало состояние проекта на 30 июля 2026 года, когда NXKeys одновременно должен был:

- хранить полный каталог 1169 намерений K1–K5;
- предоставлять daily runtime scope K3–K5 из 885 намерений;
- сохранять вручную проверенные `BUTTON ID`, safety и deployment settings;
- адаптироваться к конкретной NX installation/role/license;
- сохранять compatibility filename существующих установок.

На тот момент один JSON действительно нельзя было использовать одновременно как stable intent catalog, bootstrap, machine-specific resolution output и installed artifact без потери traceability.

## Historical decision

Использовались четыре уровня.

### 1. Intent catalog

```text
config/full-command-map/
```

1169 K1–K5 intents.

### 2. Bootstrap profile

```text
config/nx2512-pro-hybrid.json
```

Legacy schema-6 source с deployment/known IDs/curated settings.

### 3. Generated main profile

```text
config/nx2512-pro-main.generated.json
```

K3/K4/K5 output с 885 unique intent references и resolution metadata.

### 4. Installed profile — historical behavior

```text
%LOCALAPPDATA%\NXKeys\managed\NX2512.6000\nx2512-pro-hybrid.json
```

Compatibility filename содержал generated main profile.

## Почему решение superseded

Current v8 runtime изменил источник истины:

```text
config/nx2512-v8-profile.json
profile schema 8
```

Installer и HotkeyStudio теперь используют v8 profile как default/current runtime, а K1–K5/K3–K5 слой остаётся catalog/compatibility pipeline.

Поэтому следующие утверждения этого ADR являются **историческими**, а не current contract:

- daily runtime = K3–K5 / 885 intents;
- default installed profile = `nx2512-pro-hybrid.json`;
- runtime определяется generated main profile.

Текущее решение: [ADR-0003](0003-v8-runtime-profile-and-legacy-catalog-separation.md).

## Что остаётся действующим из ADR-0001

Концептуальное разделение источников всё ещё полезно:

- intent catalog не является автоматически executable authority;
- exact target `BUTTON ID` требует evidence;
- generated resolution должен быть воспроизводим;
- ambiguous/unresolved mappings нельзя включать только ради coverage;
- machine-specific catalog evidence следует хранить отдельно от stable intent semantics.

## Historical consequences

Решение позволило безопасно развить полный каталог и compiler до появления самостоятельного v8 operation contract. Оно также объясняет наличие в репозитории legacy filenames, 885-intent reports и старых generated artifacts.

## Verification status

Исторические validators K3–K5 остаются в репозитории и могут использоваться для legacy/catalog pipeline. Current runtime verification определяется ADR-0003, schema-8 tests и current documentation validator.
