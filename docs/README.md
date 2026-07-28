# Документация NXKeys

NXKeys — контекстный клавиатурный слой для Siemens NX / Designcenter NX 2512. **Главный профиль проекта покрывает все команды уровней K3–K5** из иерархического каталога: **885 команд-намерений** в 32 разделах.

| Частота | Команд | В главном профиле |
|---|---:|---|
| K5 | 69 | да |
| K4 | 371 | да |
| K3 | 445 | да |
| K2 | 280 | нет |
| K1 | 4 | нет |

K1–K2 остаются в исходном каталоге и могут быть включены только явным режимом полного экспорта. Они не входят в стандартную установку и не раздувают ежедневное дерево команд.

## Что является главным профилем

- `config/nx2512-pro-hybrid.json` — bootstrap: 14 модулей, проверенные `BUTTON ID`, короткие aliases, guards и параметры deployment.
- `config/full-command-map/` — полный версионируемый источник из 1169 намерений K1–K5.
- `config/nx2512-pro-main.generated.json` — **главный сгенерированный профиль K3–K5** для конкретной установки NX.
- `docs/generated/main-profile-resolution.md` — отчёт разрешения намерений в реальные `BUTTON ID`.

При установке главный профиль копируется в managed-пакет под совместимым runtime-именем `nx2512-pro-hybrid.json`. Это техническое имя не меняет область покрытия: установленный профиль остаётся K3–K5.

## Рекомендуемый порядок чтения

1. [Корневой README](../README.md) — назначение и быстрый старт.
2. [Главная карта K3–K5](../FULL_COMMAND_MAP.md) — охват, компиляция и статусы разрешения.
3. [Установка](INSTALLATION.md) — сборка каталога, компиляция и deployment.
4. [Конфигурация](CONFIGURATION.md) — bootstrap, generated profile и metadata.
5. [Мнемонический язык](MNEMONIC_COMMAND_LANGUAGE.md) — грамматика путей.
6. [Архитектура](ARCHITECTURE.md) — компоненты и поток данных.
7. [Конечные автоматы](STATE_MACHINE_ARCHITECTURE.md) — DFA/HFSM и guards.
8. [Модель безопасности](SAFETY_MODEL.md) — запрет выдуманных IDs и подтверждения.
9. [Диагностика](TROUBLESHOOTING.md) — отчёты, логи и типовые ошибки.
10. [Интерактивная карта](command-tree.html) — просмотр команд и модулей.

Исторические аудиты находятся в `docs/audit/`. Они фиксируют состояние проекта на дату анализа и не заменяют текущую эксплуатационную документацию.

## Быстрый старт

После экспорта `06_ui_commands_buttons.csv` из Catalog Studio:

```powershell
.\install-main-profile.ps1 `
  -CatalogDir "D:\NX2512_Catalog_Output" `
  -NxRoot "C:\Program Files\Siemens\NX2512" `
  -Clean
```

Обычный установщик также компилирует главный профиль K3–K5 по умолчанию:

```powershell
.\install-nx-ribbon-buttons.ps1 `
  -CatalogDir "D:\NX2512_Catalog_Output" `
  -NxRoot "C:\Program Files\Siemens\NX2512" `
  -Clean
```

## Источники истины

- состав K1–K5: `config/full-command-map/`;
- область главного профиля: `selected_frequencies = [K3, K4, K5]`;
- мнемонические пути: каталог намерений и `MnemonicPathGenerator.cs`;
- реальные IDs: `06_ui_commands_buttons.csv`, bootstrap и runtime probe;
- безопасность: `config/nx2512-state-machines.json` и Command Bridge;
- runtime schema: v5; source/generated profile: schema 4; IPC: schema 3.

## Проверка

```powershell
node .\scripts\validate-main-command-map.mjs
node .\scripts\validate-command-tree.mjs
```

Первый валидатор подтверждает 885 намерений K3–K5, отсутствие утечки K1–K2, prefix-free пути, корректный optional export 1169 команд и актуальность документации. Второй проверяет bootstrap, базовые shortcuts, модули, selection routing и runtime migration.
