# Установка и обновление NXKeys

Стандартная установка NXKeys строит и устанавливает **главный профиль K3–K5 из 885 команд-намерений**.

## Требования

- Windows 10/11 x64;
- Siemens NX / Designcenter NX 2512;
- .NET 8 SDK x64;
- Node.js 20+;
- `NXOpen.dll` и `NXOpenUI.dll` целевой установки;
- экспорт Catalog Studio с `06_ui_commands_buttons.csv` для максимального числа исполняемых команд;
- права записи в `%LOCALAPPDATA%\NXKeys`.

## 1. Получение каталога NX

Запустите `NX2512_Catalog_Studio` на целевой рабочей станции. В каталоге результата должен существовать:

```text
06_ui_commands_buttons.csv
```

Каталог нужен, потому что роль, лицензии, локализация и корпоративные расширения меняют доступные `BUTTON ID`.

## 2. Рекомендуемая установка главного профиля

Закройте NX и выполните:

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

Скрипт:

1. проверяет исходный каталог 1169 намерений;
2. выбирает ровно K3–K5 — 885 намерений;
3. разрешает их в реальные IDs;
4. создаёт `config/nx2512-pro-main.generated.json`;
5. пишет `docs/generated/main-profile-resolution.md`;
6. собирает .NET-компоненты и Command Bridge;
7. устанавливает generated profile транзакционно;
8. создаёт backup, manifest и ярлыки;
9. выполняет health-check.

## 3. Единый установщик

`install-nxkeys.ps1` — единственный поддерживаемый PowerShell-вход для компиляции и установки профиля:

```powershell
.\install-nxkeys.ps1 `
  -CatalogDir "D:\NX2512_Catalog_Output" `
  -NxRoot "C:\Program Files\Siemens\NX2512" `
  -Clean
```

`-ConfigPath` нужен только для явной установки заранее подготовленного K3–K5 профиля. Скрипт отклоняет generated profile с другим frequency scope.

## 4. Только компиляция

```powershell
.\install-nxkeys.ps1 `
  -CatalogDir "D:\NX2512_Catalog_Output" `
  -CompileOnly
```

Проверьте:

```text
config/nx2512-pro-main.generated.json
docs/generated/main-profile-resolution.md
```

В отчёте должны быть указаны selected frequencies K3/K4/K5, 885 selected intents и количества `existing`, `resolved`, `ambiguous`, `unresolved`.

## 5. Установка без каталога

Допускается диагностическая компиляция без `-CatalogDir`. Компилятор использует bootstrap и runtime probe, но часть команд останется `unresolved` и будет отключена. Для production рекомендуется повторить установку с актуальным `06_ui_commands_buttons.csv`.

## 6. Запуск

```text
%LOCALAPPDATA%\NXKeys\managed\NX2512.6000\launch-nx2512-with-nxkeys.cmd
```

Installed profile хранится под runtime-именем:

```text
%LOCALAPPDATA%\NXKeys\managed\NX2512.6000\nx2512-pro-hybrid.json
```

Это compatibility filename; стандартный installer помещает туда содержимое main K3–K5 profile.

## 7. Проверка

```powershell
$root = "$env:LOCALAPPDATA\NXKeys\managed\NX2512.6000"
& "$root\NX2512_HotkeyStudio.exe" validate --config "$root\nx2512-pro-hybrid.json"
& "$root\NX2512_HotkeyStudio.exe" health --config "$root\nx2512-pro-hybrid.json"
& "$root\NX2512_HotkeyStudio.exe" bridge-status --config "$root\nx2512-pro-hybrid.json"
```

## 9. Обновление и восстановление

Повторите установку с `-Clean`. Deployment создаёт новый backup и удаляет только ранее управляемые устаревшие файлы.

```powershell
& "$root\NX2512_HotkeyStudio.exe" backups --config "$root\nx2512-pro-hybrid.json"
& "$root\NX2512_HotkeyStudio.exe" restore `
  --config "$root\nx2512-pro-hybrid.json" `
  --manifest "C:\...\manifest.json"
```

Не обновляйте Bridge DLL при работающем NX: загруженная библиотека будет заблокирована процессом.
