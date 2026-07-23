# Установка и обновление NXKeys

Документ описывает рекомендуемую установку NXKeys для Siemens NX 2512 на Windows x64.

## 1. Требования

- Windows 10/11 x64;
- Siemens NX / Designcenter NX 2512;
- PowerShell 5.1 или PowerShell 7;
- .NET 8 SDK;
- Go 1.25+ для Go CLI/TUI;
- права записи в `%LOCALAPPDATA%`;
- доступ к каталогу NXOpen целевой установки для сборки Command Bridge.

Перед полным обновлением закройте все процессы NX. Загруженная `NX2512_CommandBridge.dll` может быть заблокирована процессом NX.

## 2. Получение исходного кода

```powershell
git clone https://github.com/Homiakus/NXkeys.git
Set-Location .\NXkeys
```

Для обновления существующей копии:

```powershell
git pull --ff-only origin main
```

## 3. Сборка компонентов

### HotkeyStudio

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\NX2512_HotkeyStudio\build.ps1
```

### NX Command Bridge

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\NX2512_CommandBridge\build.ps1
```

Сценарий должен найти NXOpen-сборки целевой версии NX. При ошибке задайте корректный путь в соответствии с сообщением сценария сборки.

### Go CLI/TUI

```powershell
.\scripts\build.ps1
```

### Adaptive Control Center

```powershell
dotnet publish .\NX2512_ControlCenter\NX2512_ControlCenter.csproj `
  -c Release `
  -r win-x64 `
  --self-contained false `
  -o .\dist\control-center
```

Control Center требует установленный .NET 8 Desktop Runtime, потому что публикуется с `--self-contained false`.

## 4. Рекомендуемая установка: managed-wrapper

Запустите:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\install-nx-ribbon-buttons.ps1
```

Установщик создаёт управляемое дерево:

```text
%LOCALAPPDATA%\NXKeys\managed\NX2512.6000\
```

И формирует отдельный запуск NX:

```text
%LOCALAPPDATA%\NXKeys\managed\NX2512.6000\launch-nx2512-with-nxkeys.cmd
```

Этот режим рекомендуется первым, потому что он не меняет глобальную установку NX и активирует кастомизацию только для процесса, запущенного через обёртку.

## 5. Первый запуск

1. Закройте NX.
2. Запустите `launch-nx2512-with-nxkeys.cmd`.
3. Дождитесь открытия NX.
4. Проверьте появление элементов NXKeys в меню/ленте.
5. Откройте HotkeyStudio.
6. Выполните `health` и проверьте версии MenuScript.

Команда проверки:

```powershell
$studio = "$env:LOCALAPPDATA\NXKeys\managed\NX2512.6000\NX2512_HotkeyStudio.exe"
& $studio health --config .\config\nx2512-pro-hybrid.json
```

## 6. Установка Control Center

Текущий установщик управляемого пакета автоматически не копирует Control Center. Запускайте его из `dist\control-center` либо вручную разместите опубликованные файлы рядом с `NX2512_HotkeyStudio.exe`.

```powershell
.\dist\control-center\NX2512_ControlCenter.exe `
  --config .\config\nx2512-pro-hybrid.json
```

Чтобы кнопки `Запустить Leader` и `Открыть Studio` работали, `NX2512_HotkeyStudio.exe` должен находиться рядом с Control Center либо на ожидаемом соседнем уровне.

## 7. Подключение API-каталога

Сформируйте каталог через `NX2512_Catalog_Studio`, затем укажите его одним из способов.

Параметр запуска:

```powershell
.\dist\control-center\NX2512_ControlCenter.exe `
  --config .\config\nx2512-pro-hybrid.json `
  --catalog "D:\NX2512_Full_Function_API_Catalog_YYYYMMDD_HHMMSS"
```

Переменная окружения:

```powershell
$env:NXKEYS_CATALOG_DIR = "D:\NX2512_Full_Function_API_Catalog_YYYYMMDD_HHMMSS"
```

Автоматический поиск выполняется также в `%LOCALAPPDATA%\NXKeys\catalog`.

## 8. Альтернативный режим existing-custom-dirs

В JSON-профиле:

```json
{
  "deployment": {
    "mode": "existing-custom-dirs",
    "existing_custom_dirs_file": "D:\\NX\\custom_dirs.dat",
    "patch_existing_custom_dirs": true
  }
}
```

Этот режим изменяет существующий список пользовательских каталогов и влияет на каждый запуск NX, который использует данный файл. Перед применением обязательно выполните `plan` и проверьте резервную копию.

## 9. Обновление

Рекомендуемый порядок:

```powershell
git pull --ff-only origin main
powershell -NoProfile -ExecutionPolicy Bypass -File .\NX2512_HotkeyStudio\build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\NX2512_CommandBridge\build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\install-nx-ribbon-buttons.ps1
```

Затем отдельно обновите Control Center:

```powershell
dotnet publish .\NX2512_ControlCenter\NX2512_ControlCenter.csproj `
  -c Release -r win-x64 --self-contained false `
  -o .\dist\control-center
```

## 10. Версии MenuScript

NX 2512 ожидает:

| Тип | Версия |
|---|---:|
| `.men` | `VERSION 139` |
| `.tbr` и `.rtb` | `VERSION 170` |

Не копируйте версию из одного типа файла в другой.

## 11. Удаление управляемой установки

1. Закройте NX и HotkeyStudio.
2. Сохраните нужные пользовательские JSON-профили.
3. Удалите каталог `%LOCALAPPDATA%\NXKeys\managed\NX2512.6000`.
4. При необходимости удалите runtime-данные из `%LOCALAPPDATA%\NXKeys`.

Не удаляйте `backups`, пока не убедитесь, что восстановление больше не требуется.

Если использовался `existing-custom-dirs`, восстановите исходный `custom_dirs.dat` из резервной копии либо вручную удалите только строку NXKeys.

## 12. Проверка после установки

```powershell
go test ./...
go vet ./...
dotnet build .\NX2512_HotkeyStudio\NX2512_HotkeyStudio.csproj -c Release -p:Platform=x64
dotnet build .\NX2512_ControlCenter\NX2512_ControlCenter.csproj -c Release -p:Platform=x64
```

Для ошибок установки используйте [TROUBLESHOOTING.md](TROUBLESHOOTING.md).