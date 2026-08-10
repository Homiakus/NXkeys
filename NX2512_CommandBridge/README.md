# NX2512_CommandBridge

Command Bridge — .NET 8 x64 library, загружаемая внутрь Siemens NX 2512. Она является последней NXKeys boundary перед NX: публикует context, принимает authenticated IPC schema 4 requests, проверяет permissions/guards и выполняет command, module switch или selection action.

## Ответственность

- `status.json` / `context.json`;
- atomic `pending → processing` claim;
- protocol schema 4 validation;
- HMAC/session/source-process verification;
- nonce/sequence anti-replay;
- profile permission verification;
- expiry/context/application/selection checks;
- destructive confirmation enforcement;
- exact UI command invocation;
- module switching;
- Leader selection-filter actions;
- in-process Selection Intent hotkeys `0…4`;
- result publication;
- `interrupted_unknown` recovery.

Bridge **не** генерирует mnemonic paths и не разрешает «похожее имя» в command ID.

## Production build

```powershell
.\NX2512_CommandBridge\build.ps1 `
  -NxRoot "C:\Program Files\Siemens\NX2512" `
  -Clean
```

Либо:

```powershell
.\NX2512_CommandBridge\build.ps1 `
  -NxOpenDll "C:\Program Files\Siemens\NX2512\NXBIN\managed\NXOpen.dll" `
  -Clean
```

Output:

```text
NX2512_CommandBridge\dist\NX2512_CommandBridge.dll
```

## Contract build без NX

CI собирает minimal NXOpen stubs и затем Bridge против них. Это подтверждает используемую форму API, но не доказывает runtime sensitivity, license availability или semantics interactive commands.

## Размещение

Canonical managed path:

```text
custom\application\NX2512_CommandBridge.dll
```

Bridge не должен дублироваться в `custom\startup`.

Загруженная DLL удерживается процессом NX. Для update полностью закройте NX.

## IPC schema 4

Shared contract: [`NXKeys.Protocol/NxProtocol.cs`](../NXKeys.Protocol/NxProtocol.cs).

Полное описание: [`docs/api.md`](../docs/api.md).

Authenticated request содержит session/HMAC/anti-replay/profile-digest fields. Queue file сам по себе не является authority.

Managed launch session обязательна для штатного command dispatch.

## Context

Bridge публикует:

- application/module;
- selection count/state/types/fingerprint;
- Work/Display Part;
- modal state;
- active command;
- semantic revision;
- security status/session/profile digest;
- last request/result/message.

`selection_count = -1` означает unknown.

## Sheet Metal

Runtime использует canonical NX2512 names:

```text
UG_APP_SBSM
UG_SBSM_*
```

Legacy `UG_APP_SHEETMETAL` и `UG_SHEET_METAL_*` нормализуются для compatibility. Security permission keys применяют ту же canonicalization.

## Selection Intent `0…4`

`SelectionIntentHotkeys.cs` устанавливается через module initializer внутри NX process.

```text
0  Reset
1  Single
2  Connected / Chain
3  Tangent
4  Inferred Path / Region Boundary
```

Handler:

- работает только когда foreground принадлежит NX process;
- не обрабатывает Ctrl/Alt/Win combinations;
- не обрабатывает injected events;
- не должен забирать цифры в text-like controls;
- требует usable native collector control либо seed selection;
- защёлкивает physical key до key-up.

Native controls включают `UG_SEL_CHAINING`, inferred/path/boundary buttons и tangent selectors. Advanced `ScRuleFactory` expansion вызывается через reflection для совместимости с минимальными CI stubs.

Документация: [`docs/SELECTION_INTENT.md`](../docs/SELECTION_INTENT.md).

## Interactive command invocation

Обычный UI command проходит availability/sensitivity checks и затем `DialogTester.InvokeMenuButtonAction`.

Для некоторых интерактивных NX commands return `false` может потребовать live-NX интерпретации. Contract build не подтверждает, открылся ли фактически dialog/collector.

Поэтому при расхождении «UI открылся, result failed»:

1. не повторяйте command автоматически;
2. сохраните request/result/context/log;
3. проверьте фактический NX state;
4. воспроизведите на тестовой детали.

## Recovery

Request, оставшийся в `processing` после interruption, получает unknown result и не исполняется автоматически повторно. Это at-most-once recovery.

## Диагностика

```powershell
$root = "$env:LOCALAPPDATA\NXKeys\managed\NX2512.6000"
& "$root\NX2512_HotkeyStudio.exe" bridge-status `
  --config "$root\nx2512-v8-profile.json"
```

При `authentication_required` запустите NX и HotkeyStudio через managed launcher.

При OFFLINE проверьте custom dirs, DLL placement, status/context/log и наличие stale NX process.

## Изменение Bridge

Обязательно:

1. сохранить/обновить protocol contract;
2. синхронизировать security canonicalization и profile permissions;
3. обновить NXOpen stubs при использовании нового member;
4. выполнить strict/contract build;
5. запустить protocol/HotkeyStudio regression tests;
6. обновить API, safety, operations и troubleshooting docs;
7. выполнить live NX test для command dispatch;
8. отдельно проверить Selection Intent `0…4`, если менялся hook/rules code;
9. проверить recovery без duplicate execution.

## Safety rules

- unknown/unsigned/stale request rejected;
- exact permission required;
- destructive request без confirmation rejected;
- `interrupted_unknown` не retry-ится автоматически;
- similar command fallback запрещён;
- integration tests выполняются на копии детали.
