# Диагностика

## В окне видны только DLL/EXE/JAR/CLASS

Это нормально. Выберите собранную DLL из каталога `dist`.

## `dotnet` не найден

Установите .NET 8 SDK x64 или Visual Studio 2022 с компонентом `.NET desktop development`.

Проверка:

```powershell
dotnet --list-sdks
```

Должна присутствовать версия `8.x`.

## `NXOpen.dll не найден`

Передайте путь явно:

```powershell
.\build.ps1 -NxOpenDll "C:\Program Files\Siemens\NX2512\NXBIN\managed\NXOpen.dll"
```

## NX сообщает об ошибке подписи/доверия

Повторите сборку с подписью:

```powershell
.\build.ps1 -Sign
```

Для этого могут потребоваться `NXSigningResource.res`, `SignDotNet.exe` и NX Open .NET Author license.

## NX сообщает `Failed to load image` или `Error in external library`

Проверьте:

- DLL собрана против `NXOpen.dll` именно вашей NX 2512;
- выбрана x64 сборка;
- целевая платформа — .NET 8;
- рядом с DLL нет чужой копии `NXOpen.dll`;
- в `File → Help → Log File` / syslog есть подробное исключение;
- DLL пересобрана после обновления NX.

## Каталог долго формируется

Сканирование UI-файлов и `uf_*.h` может занять время, особенно при сетевых `UGII_SITE_DIR` или `UGII_GROUP_DIR`.
