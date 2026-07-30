# NX2512_CommandBridge

## Назначение

Command Bridge — .NET 8 x64 library, загружаемая внутрь Siemens NX 2512. Она публикует runtime context, захватывает requests файловой очереди, повторно проверяет условия выполнения и вызывает точную NX UI-команду либо специальное selection/module action.

## Граница ответственности

Bridge отвечает за:

- heartbeat/status и `context.json`;
- atomic claim `pending → processing`;
- protocol validation;
- expiry и duplicate protection;
- expected context/application/selection checks;
- modal-state checks;
- destructive confirmation;
- selection filter actions;
- application/module switching;
- точный command invocation;
- result и recovery `interrupted_unknown`.

Bridge не генерирует mnemonic paths и не разрешает похожее имя в command ID.

## Требования

- Windows x64;
- .NET 8 build tools;
- NXOpen DLL целевой NX 2512;
- `NXOpen.dll` и `NXOpenUI.dll` в одном managed directory;
- при наличии используются `NXOpen.Utilities.dll` и `NXOpen.UF.dll`;
- signing resource — необязателен на уровне project file, но может требоваться политикой организации.

## Production build

Из корня репозитория:

```powershell
.\NX2512_CommandBridge\build.ps1 `
  -NxRoot "C:\Program Files\Siemens\NX2512" `
  -Clean
```

С точным DLL:

```powershell
.\NX2512_CommandBridge\build.ps1 `
  -NxOpenDll "C:\Program Files\Siemens\NX2512\NXBIN\managed\NXOpen.dll" `
  -Clean
```

Output:

```text
NX2512_CommandBridge\dist\NX2512_CommandBridge.dll
```

Скрипт выводит SHA-256 DLL. По умолчанию путь должен подтверждать expected NX version `2512`. `-AllowVersionMismatch` допустим только после ручной проверки.

## Contract build без NX

CI собирает Bridge против stubs:

```powershell
New-Item -ItemType Directory -Force .\artifacts\nxopen-contract | Out-Null

dotnet build .\NX2512_CommandBridge.Tests\NXOpenUI\NXOpenUI.csproj `
  -c Release -o .\artifacts\nxopen-contract --nologo

$nxOpenDir = (Resolve-Path .\artifacts\nxopen-contract).Path

dotnet build .\NX2512_CommandBridge\NX2512_CommandBridge.csproj `
  -c Release -p:Platform=x64 -p:NXOpenDir="$nxOpenDir" --nologo
```

Contract build подтверждает только используемую форму API. Он не доказывает загрузку, лицензирование, sensitivity или runtime semantics конкретной NX build.

## Размещение

Managed package устанавливает Bridge только в:

```text
custom\application\NX2512_CommandBridge.dll
```

Размещение в `custom\startup` запрещено deployment invariants.

Загруженная DLL блокируется процессом NX. Для обновления закройте NX полностью.

## IPC

Root:

```text
%LOCALAPPDATA%\NXKeys\bridge
```

Очередь:

```text
pending → processing → completed | failed
```

Shared contract: [`NXKeys.Protocol/NxProtocol.cs`](../NXKeys.Protocol/NxProtocol.cs). Полное описание: [`docs/api.md`](../docs/api.md).

## Context

Bridge публикует application/module, selection, Work/Display Part, modal state, active command, confidence, semantic revision и last result.

`selection_count = -1` означает неизвестное состояние.

## Recovery

При запуске Bridge проверяет requests, оставшиеся в `processing`. Они переводятся в failed с неопределённым результатом вместо автоматического повтора. Operator обязан проверить состояние детали перед новым действием.

## Изменение Bridge

Обязательно:

1. сохранить shared protocol compatibility или повысить schema;
2. обновить contract stubs, если используется новый NXOpen member;
3. выполнить contract build;
4. обновить API/operations docs;
5. проверить загрузку DLL в target NX;
6. проверить context freshness/revision;
7. проверить обычную, selection и module-switch команды;
8. проверить reject при modal/stale/wrong selection;
9. проверить recovery без duplicate execution.

## Диагностика

```powershell
$root = "$env:LOCALAPPDATA\NXKeys\managed\NX2512.6000"
& "$root\NX2512_HotkeyStudio.exe" bridge-status `
  --config "$root\nx2512-pro-hybrid.json"
```

При OFFLINE в запущенной NX проверьте managed launcher, custom directory, DLL placement, status/context/log и отсутствие старого процесса NX.

## Безопасность

- request file не считается доверенным;
- command ID повторно проверяется в runtime context;
- destructive request без confirmation отклоняется;
- unknown execution result не retry-ится;
- не добавляйте fallback, запускающий похожую команду при отсутствии exact ID;
- integration tests выполняйте на копии детали.
