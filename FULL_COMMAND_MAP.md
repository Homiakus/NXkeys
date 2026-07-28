# Главный профиль K3–K5 и полный каталог NX 2512

Исходный каталог NXKeys содержит **1169 функций** Siemens NX 2512 в **32 разделах**. Рабочий профиль по умолчанию намеренно ограничен приоритетными уровнями **K3–K5**:

| Уровень | Количество |
|---|---:|
| `K5` | 69 |
| `K4` | 371 |
| `K3` | 445 |
| **Главный профиль** | **885** |
| `K2` | 280 |
| `K1` | 4 |
| **Полный источник** | **1169** |

## Политика профилей

- **Главный профиль:** K3–K5, 885 команд-намерений. Используется при обычной сборке, установке, поиске и работе HUD.
- **K1–K2:** остаются в версионируемом источнике, но не входят в основной runtime.
- **Полный экспорт K1–K5:** доступен только явной опцией `--all-frequencies` для аудита и специальных рабочих мест.

## Файлы

```text
config/full-command-map/
  nx2512-full-command-map.json.gz.b64.part1
  nx2512-full-command-map.json.gz.b64.part2
  nx2512-full-command-map.json.gz.b64.part3

config/nx2512-pro-hybrid.json             bootstrap-профиль
config/nx2512-pro-main.generated.json     главный K3–K5
config/nx2512-pro-all.generated.json      необязательный K1–K5

docs/generated/main-profile-resolution.md
docs/generated/all-frequency-resolution.md
```

Bootstrap-профиль содержит проверенные IDs, базовые сочетания, структуру 14 модулей и safety-policy. Компилятор использует его как доверенный справочник, затем удаляет команды вне выбранного частотного scope.

## Что хранится для каждой функции

- `intent_id`;
- исходный индекс раздела `0–31`;
- `runtime_module`;
- `frequency` (`K1–K5`);
- исходный модуль и группа;
- английское и русское название;
- канонический путь длиной 2–5 клавиш.

Составные названия сохраняются без повреждения: `DXF/DWG`, `Zoom In/Out`, `Select Similar Faces/Edges`, `Arc/Circle`.

## Компиляция главного профиля

Рекомендуемый способ:

```powershell
.\install-main-profile.ps1 `
  -CatalogDir "D:\NX2512_Catalog_Output" `
  -CompileOnly
```

Ручной вызов:

```powershell
node .\scripts\compile-main-command-map.mjs `
  --profile .\config\nx2512-pro-hybrid.json `
  --intents .\config\full-command-map `
  --catalog-dir "D:\NX2512_Catalog_Output" `
  --out .\config\nx2512-pro-main.generated.json `
  --report .\docs\generated\main-profile-resolution.md
```

По умолчанию компилятор выбирает `K3,K4,K5`. Явная форма:

```powershell
node .\scripts\compile-main-command-map.mjs --frequencies K3,K4,K5
```

## Полный совместимый экспорт

```powershell
node .\scripts\compile-main-command-map.mjs `
  --all-frequencies `
  --out .\config\nx2512-pro-all.generated.json `
  --report .\docs\generated\all-frequency-resolution.md
```

Либо:

```powershell
.\install-full-command-profile.ps1 `
  -CatalogDir "D:\NX2512_Catalog_Output" `
  -CompileOnly
```

## Разрешение BUTTON ID

Файл `06_ui_commands_buttons.csv` формируется `NX2512_Catalog_Studio` на целевой установке NX. Компилятор объединяет:

1. IDs и названия из каталога установки;
2. проверенные команды bootstrap-профиля;
3. runtime probe;
4. ручные мнемонические правила `MnemonicPathGenerator.cs`.

Статусы:

- `existing` — точная команда уже была в bootstrap-профиле;
- `resolved` — найдено надёжное соответствие в каталоге;
- `ambiguous` — несколько близких кандидатов;
- `unresolved` — надёжного ID нет.

`ambiguous` и `unresolved` сохраняются с путём и поисковыми данными, но имеют `enabled: false`.

## Метаданные главного профиля

```json
{
  "full_command_catalog": {
    "schema_version": 2,
    "source_intents": 1169,
    "selected_intents": 885,
    "selected_frequencies": ["K3", "K4", "K5"],
    "frequency_counts": {
      "K1": 4,
      "K2": 280,
      "K3": 445,
      "K4": 371,
      "K5": 69
    }
  }
}
```

## Политика путей

- пути prefix-free внутри каждого активного модуля;
- длина 2–5 алфавитно-цифровых токенов;
- структура `действие → объект → команда → вариант`;
- проверенные пути по `BUTTON ID` имеют приоритет;
- короткий alias сохраняется только без конфликтов;
- глобальные функции по умолчанию дублируются в активные модули;
- `--no-global-duplication` оставляет их в специализированном scope.

## Проверка

```powershell
node .\scripts\validate-main-command-map.mjs
```

Валидатор проверяет:

- 1169 исходных функций и 32 раздела;
- точные количества: K1=4, K2=280, K3=445, K4=371, K5=69;
- ровно 885 уникальных intent в главном K3–K5 профиле;
- отсутствие K1–K2 в главном профиле;
- сохранение всех выбранных `catalog_refs`;
- prefix-free пути и aliases;
- отсутствие включённых команд без точного ID;
- возможность отдельной компиляции всех K1–K5;
- актуальность README и эксплуатационной документации.
