# Установка

## 1. Сборка

```powershell
.\scripts\build.ps1 -NxRoot "C:\Program Files\Siemens\NX2512"
```

## 2. Установка пользователя

```powershell
.\dist\NxEskd\scripts\install.ps1
```

## 3. Проверка переменных

```powershell
[Environment]::GetEnvironmentVariable('NX_ESKD_ROOT','User')
[Environment]::GetEnvironmentVariable('UGII_CUSTOM_DIRECTORY_FILE','User')
```

## 4. Перезапуск NX

NX считывает custom directories и startup MenuScript при запуске.

## 5. Восстановление

Установщик сохраняет резервные копии в:

```text
%LOCALAPPDATA%\NxEskdGenerator\backups
```

Удаление:

```powershell
.\scripts\uninstall.ps1 -KeepProfiles
```
