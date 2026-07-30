# NXKeys — состояние сборки и поставки

Дата актуализации: 30 июля 2026 года.

## Целевая платформа

- Windows x64;
- Siemens NX / Designcenter NX 2512;
- .NET 8;
- Node.js 20+ для profile toolchain;
- profile schema 6;
- IPC schema 3;
- source sequence policy 7.

## Архитектурный статус

Проект использует C#/.NET-контур для runtime и UI. Node.js применяется только для компиляции, валидации и аудита command profiles. Go source и `go.mod` запрещены CI.

## Компоненты

### HotkeyStudio

Подтверждено кодом:

- WinForms desktop/tray;
- CLI;
- profile migration/validation;
- adaptive Leader/HUD;
- scanner и resolver;
- IPC client;
- deployment, backups, restore и health;
- managed NX launcher.

### Command Bridge

Подтверждено кодом:

- .NET 8 x64 NXOpen library;
- file queue;
- context/status publishing;
- exact command invocation;
- selection actions;
- module switching;
- expiry/context/confirmation checks;
- interrupted request recovery.

### Control Center

Подтверждено кодом и project structure:

- profile/coverage view;
- Bridge diagnostics;
- context and module state;
- command search;
- API Explorer integration с Catalog Studio outputs.

### Catalog Studio

Подтверждено кодом/build script:

- NXOpen metadata inventory;
- UI commands и BUTTON IDs;
- UFUN inventory;
- UI→API candidates;
- configurable CSV/Markdown export.

## Profile toolchain

- source catalog: 1169 intents K1–K5;
- main scope: 885 K3–K5;
- bootstrap: `config/nx2512-pro-hybrid.json`;
- main output: `config/nx2512-pro-main.generated.json`;
- resolution report: `docs/generated/main-profile-resolution.md`;
- source selection support v7: десять paths `SB…SN`;
- module switches: `G*`.

### Известное состояние generated artifacts

Source sequence policy уже v7, но checked-in sequence audit/generated metadata могут отражать v6 до regeneration. Это открытый consistency task. Generated files не считаются источником истины до пересборки текущим compiler.

## Deployment

`DeploymentEngine` и installer реализуют:

1. validation;
2. main profile compilation;
3. component build;
4. clean staging;
5. SHA-256 staging verification;
6. backup manifest;
7. atomic managed deployment;
8. deletion только managed obsolete files;
9. package manifest;
10. post-install health-check;
11. rollback при failure;
12. launcher/shortcut integration.

Bridge устанавливается только в:

```text
custom/application/NX2512_CommandBridge.dll
```

Launcher передаёт `UGII_CUSTOM_DIRECTORY_FILE` и не должен переопределять глобальные `PATH`/`UGII_USER_DIR`.

## Сборка компонентов

### Без proprietary NXOpen

Поддержаны:

```powershell
dotnet build .\NX2512_HotkeyStudio\NX2512_HotkeyStudio.csproj -c Release -p:Platform=x64
dotnet build .\NX2512_ControlCenter\NX2512_ControlCenter.csproj -c Release -p:Platform=x64
dotnet run --project .\NXKeys.StateMachines.Tests\NXKeys.StateMachines.Tests.csproj -c Release
```

Command Bridge компилируется в CI против NXOpen contract stubs.

### С target NXOpen

Production Bridge и Catalog Studio требуют DLL целевой установки:

```powershell
.\NX2512_CommandBridge\build.ps1 -NxRoot "C:\Program Files\Siemens\NX2512" -Clean
.\NX2512_Catalog_Studio\build.ps1 -NxRoot "C:\Program Files\Siemens\NX2512" -Clean
```

Contract build не заменяет production build/runtime test.

## CI

`.github/workflows/ci.yml` подтверждённо выполняет:

- запрет Go source;
- JSON validation;
- adaptive profile/documentation validation;
- DFA/HFSM tests;
- HotkeyStudio build/publish;
- C# CLI validation canonical profile;
- Control Center build/publish;
- NXOpen contract stubs build;
- Command Bridge compile против stubs;
- protocol/state/deployment source invariants;
- публикацию Windows artifacts/logs.

Дополнительные workflows проверяют main K3–K5 profile и mnemonic command language.

## Известные несоответствия

1. `docs/audit/command-sequence-audit.*` требует regeneration policy v7.
2. Некоторые runtime error strings HotkeyStudio всё ещё говорят `schema v4`, хотя current schema 6.
3. `path_locked/path_source` добавлены в модель, но полный user override UI/persistence workflow не подтверждён.
4. Actual module cycle из exported `DEFAULT_MODULE_CYCLE` требует проверки runtime wiring.
5. Runtime availability всех 885 intents не может быть доказана CI.

## Обязательная проверка на NX workstation

- production Bridge build против фактических assemblies;
- DLL loading и единственная активная copy;
- launcher/custom dirs;
- context freshness/revision;
- application/module mapping;
- `SB…SN` selection behavior;
- `G*` module switching;
- critical BUTTON IDs;
- destructive confirmation;
- interruption recovery;
- role/license interaction;
- package update и rollback;
- signing policy, если применима.

## Границы доказательности

**Подтверждено CI:** source contracts, generation invariants, C# compilation, state/protocol tests и Bridge API contract stubs.

**Требует уточнения:** фактический эффект UI-command, лицензия, sensitivity, corporate role и production data safety в target NX.

## Документация

Каноническое оглавление: [`docs/README.md`](docs/README.md).  
Developer workflow: [`DEVELOPMENT.md`](DEVELOPMENT.md).  
Текущий аудит: [`docs/DOCUMENTATION_AUDIT.md`](docs/DOCUMENTATION_AUDIT.md).
