# NXKeys

NXKeys is a safety-first Siemens NX 2512 hotkey, Leader Key, radial-menu, and command-bridge toolkit.

It provides two operator surfaces:

- `nxkeys` Go CLI/TUI for scanning NX, resolving commands, planning deployment, applying MenuScript overlays, and restoring backups.
- `NX2512_HotkeyStudio` WinForms console for day-to-day control: hotkeys, Leader Key sequences, radial menus, command catalog search, deployment, health checks, bridge status, backups, and JSON profiles.

The default profile is `NX Pro Hybrid 2512.6000`.

## Safety Model

NXKeys does not edit Siemens installation files and does not rewrite opaque `user.mtx` role internals.

It uses supported, reversible artifacts:

- `.men` MenuScript overlays for keyboard accelerators and menu button actions.
- `.tbr` / `.rtb` toolbar and ribbon placement files for NXKeys entry points.
- JSON profiles as the source of truth.
- SHA-256 backups before apply.
- Optional copied `.mtx` role templates only when the operator explicitly provides an exported role.

Important MenuScript rule for NX 2512:

| File type | Expected version |
|---|---:|
| `.men` | `VERSION 139` |
| `.tbr` / `.rtb` | `VERSION 170` |

NXKeys now writes these versions by file type. This prevents the two common errors:

- `.men` rejects `VERSION 170`.
- toolbar/ribbon parser rejects `.tbr/.rtb` written as `VERSION 139`.

## Requirements

- Windows 10/11.
- Siemens NX / Designcenter NX 2512.
- Go 1.25+ for the Go CLI/TUI.
- .NET 8 SDK for HotkeyStudio and CommandBridge builds.
- NX should be closed for full reinstall/update because NX can lock `NX2512_CommandBridge.dll`.

## Quick Start

Build the Go CLI:

```powershell
.\scripts\build.ps1
```

Build HotkeyStudio:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\NX2512_HotkeyStudio\build.ps1
```

Build CommandBridge if needed:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\NX2512_CommandBridge\build.ps1
```

Install the managed NXKeys package:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\install-nx-ribbon-buttons.ps1
```

Launch NX through the generated wrapper:

```text
%LOCALAPPDATA%\NXKeys\managed\NX2512.6000\launch-nx2512-with-nxkeys.cmd
```

The wrapper starts the HotkeyStudio background Leader Key engine and launches NX with the managed custom directory.

## Managed Package Layout

Default install location:

```text
%LOCALAPPDATA%\NXKeys\managed\NX2512.6000\
├─ NX2512_HotkeyStudio.exe
├─ NX2512_HotkeyStudio.dll
├─ nx2512-pro-hybrid.json
├─ launch-nx2512-with-nxkeys.cmd
├─ custom_dirs.dat
├─ custom\
│  ├─ application\
│  │  ├─ NX2512_CommandBridge.dll
│  │  └─ nxkeys_command_bridge.men
│  └─ startup\
│     ├─ NX2512_CommandBridge.dll
│     ├─ nxkeys_generated.men
│     ├─ nxkeys_ribbon.rtb
│     ├─ nxkeys_toolbar.tbr
│     ├─ launch-hotkeystudio-daemon.cmd
│     └─ launch-hotkeystudio-gui.cmd
├─ resolution-report.md
├─ radial-menu-plan.md
└─ radial-menu-plan.json
```

Runtime state:

```text
%LOCALAPPDATA%\NXKeys\
├─ backups\
├─ bridge\
│  ├─ pending\
│  ├─ completed\
│  ├─ failed\
│  ├─ context.json
│  └─ status.json
└─ logs\
```

## HotkeyStudio Console

HotkeyStudio is the main operator UI. It is a compact adaptive console with these sections:

- `Обзор`: profile, health, NX process, bridge state, unresolved bindings.
- `Команды`: keyboard bindings plus command catalog search.
- `Leader Key`: background hook controls, HUD preview, sequence management.
- `Radials`: radial menu list and 8-direction editor.
- `NX / Bridge`: scanner results, generated files, pending/completed/failed queue state.
- `Deploy`: dry-run, deployment plan, apply.
- `Backups / Profile`: selected backup restore and JSON profile save.

Open it directly:

```powershell
& "$env:LOCALAPPDATA\NXKeys\managed\NX2512.6000\NX2512_HotkeyStudio.exe" --gui --config ".\nx2512-pro-hybrid.json"
```

Or from NX via the NXKeys ribbon/toolbar/menu entry.

## CLI Commands

The installed HotkeyStudio executable supports operational CLI commands:

```powershell
& "$env:LOCALAPPDATA\NXKeys\managed\NX2512.6000\NX2512_HotkeyStudio.exe" validate --config ".\nx2512-pro-hybrid.json"
& "$env:LOCALAPPDATA\NXKeys\managed\NX2512.6000\NX2512_HotkeyStudio.exe" scan --config ".\nx2512-pro-hybrid.json"
& "$env:LOCALAPPDATA\NXKeys\managed\NX2512.6000\NX2512_HotkeyStudio.exe" plan --config ".\nx2512-pro-hybrid.json"
& "$env:LOCALAPPDATA\NXKeys\managed\NX2512.6000\NX2512_HotkeyStudio.exe" apply --config ".\nx2512-pro-hybrid.json" --yes
& "$env:LOCALAPPDATA\NXKeys\managed\NX2512.6000\NX2512_HotkeyStudio.exe" health --config ".\nx2512-pro-hybrid.json"
& "$env:LOCALAPPDATA\NXKeys\managed\NX2512.6000\NX2512_HotkeyStudio.exe" bridge-status
& "$env:LOCALAPPDATA\NXKeys\managed\NX2512.6000\NX2512_HotkeyStudio.exe" backups --config ".\nx2512-pro-hybrid.json"
```

Restore latest backup:

```powershell
& "$env:LOCALAPPDATA\NXKeys\managed\NX2512.6000\NX2512_HotkeyStudio.exe" restore --config ".\nx2512-pro-hybrid.json"
```

Restore a specific backup manifest:

```powershell
& "$env:LOCALAPPDATA\NXKeys\managed\NX2512.6000\NX2512_HotkeyStudio.exe" restore --config ".\nx2512-pro-hybrid.json" --manifest "$env:LOCALAPPDATA\NXKeys\backups\YYYYMMDD_HHMMSS.mmm\manifest.json"
```

The Go CLI/TUI remains available from `dist\nxkeys.exe` or `dist\nxkeys-windows-amd64.exe` after `scripts\build.ps1`.

## Health Checks

Use `health` whenever NX shows a customization error or the bridge behaves strangely:

```powershell
& "$env:LOCALAPPDATA\NXKeys\managed\NX2512.6000\NX2512_HotkeyStudio.exe" health --config ".\nx2512-pro-hybrid.json"
```

It reports:

- managed package path;
- live NX processes;
- expected `.men` / `.tbr` / `.rtb` versions;
- invalid NXKeys MenuScript files;
- bridge loaded state;
- pending/completed/failed bridge queue counts;
- recent failed command executions;
- locked bridge DLL;
- missing managed files;
- dist-vs-installed hash mismatches for HotkeyStudio.

Healthy version output should include:

```text
MenuScript versions OK: yes
Invalid NXKeys VERSION files: 0
Managed package OK: yes
```

When NX is closed, `Bridge loaded: no` is normal. When NX is launched through the wrapper and the CommandBridge loads, it should become `yes`.

## Command Bridge

`NX2512_CommandBridge.dll` is loaded by NX from the managed custom directory. HotkeyStudio and the Leader HUD enqueue command requests under:

```text
%LOCALAPPDATA%\NXKeys\bridge\pending
```

The bridge moves results to:

```text
%LOCALAPPDATA%\NXKeys\bridge\completed
%LOCALAPPDATA%\NXKeys\bridge\failed
```

Failures can be legitimate NX context failures. Examples:

- button does not exist in this NX install;
- command exists but is unavailable in the current module;
- command is insensitive because no suitable object/dialog/context is active.

The bridge records these instead of guessing or force-running unavailable commands.

## Leader Key

The Leader Key engine uses module-aware sequential chords.

Default behavior:

- trigger key: usually `CapsLock`;
- `Tab` / `Shift+Tab`: cycle modules when NX context is unavailable;
- `Space`: search;
- `Backspace`: step back;
- `Esc`: cancel;
- `Enter`: confirm a pending dangerous command.

Commands marked as destructive or requiring confirmation are not executed immediately. The HUD asks before enqueueing the bridge request.

## Keyboard Bindings

Bindings live in JSON:

```json
{
  "shortcut": "Ctrl+S",
  "command": {
    "id": "UG_FILE_SAVE_PART",
    "name": "Save",
    "aliases": ["Save Part"]
  },
  "scope": "Global",
  "enabled": true
}
```

If `command.id` is empty, NXKeys searches discovered menu files by name and aliases. Weak or ambiguous matches are omitted from generated overlays and listed in `resolution-report.md`.

Current known profile status may include ambiguous bindings such as command names that match several NX buttons. Fix those by setting the exact `command.id`.

## Radial Menus

NXKeys keeps radial menu intent in JSON and exports:

```text
radial-menu-plan.md
radial-menu-plan.json
```

For unattended role deployment, configure radial menus in NX, export a known-good `.mtx` role, and set `role_deployment.source_mtx`. NXKeys backs up and copies that role; it does not rewrite role internals.

## Deployment Modes

Default mode:

```json
{
  "deployment": {
    "mode": "managed-wrapper"
  }
}
```

This writes everything under `%LOCALAPPDATA%\NXKeys\managed\...` and activates it only through the generated launch wrapper.

Existing `custom_dirs.dat` mode:

```json
{
  "deployment": {
    "mode": "existing-custom-dirs",
    "existing_custom_dirs_file": "D:\\NX\\custom_dirs.dat",
    "patch_existing_custom_dirs": true
  }
}
```

Use this only when you intentionally want NXKeys added to an existing NX customization file.

## Troubleshooting

MenuScript error in `.men` says `VERSION 170` is invalid:

- `.men` must be `VERSION 139`.
- Run `health` and reinstall after closing NX.

Toolbar/ribbon error in `.tbr` or `.rtb` says `VERSION 139` is unexpected:

- `.tbr/.rtb` must be `VERSION 170`.
- The toolbar/ribbon files should be thin placement files. Button labels/actions are defined in `.men`.

Installer cannot copy `NX2512_CommandBridge.dll`:

- NX is still running and has loaded the DLL.
- Close NX, then rerun `install-nx-ribbon-buttons.ps1`.

Bridge shows failed commands:

- Check `%LOCALAPPDATA%\NXKeys\bridge\failed`.
- Failures may indicate the command is unavailable in the current NX context, not that NXKeys itself is broken.

HotkeyStudio says managed package hash mismatch:

- Rebuild and rerun the installer, or copy the current `NX2512_HotkeyStudio.exe/.dll` into the managed root.

## Development

Recommended verification:

```powershell
go test ./...
dotnet build .\NX2512_HotkeyStudio\NX2512_HotkeyStudio.csproj -c Release --nologo
dotnet build .\NX2512_CommandBridge\NX2512_CommandBridge.csproj -c Release --nologo
& .\NX2512_HotkeyStudio\dist\NX2512_HotkeyStudio.exe validate --config .\nx2512-pro-hybrid.json
& .\NX2512_HotkeyStudio\dist\NX2512_HotkeyStudio.exe plan --config .\nx2512-pro-hybrid.json
& .\NX2512_HotkeyStudio\dist\NX2512_HotkeyStudio.exe health --config .\nx2512-pro-hybrid.json
```

Refresh distributable HotkeyStudio artifacts:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\NX2512_HotkeyStudio\build.ps1
```

Refresh managed installation:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\install-nx-ribbon-buttons.ps1
```

If NX is running, the installer may fail on the bridge DLL. Close NX and rerun for a full update.

## Project Layout

```text
cmd/nxkeys/                    Go CLI/TUI entry point
internal/config/               JSON config, defaults, validation
internal/discovery/            bounded NX install/profile scanner
internal/nxmenu/               MenuScript parser and Go overlay generator
internal/backup/               Go backup/restore engine
internal/engine/               Go planning/apply pipeline
internal/tui/                  Bubble Tea/Lip Gloss TUI
NX2512_HotkeyStudio/           WinForms console, Leader HUD, deployment services
NX2512_CommandBridge/          NXOpen in-process command bridge
NX2512_Catalog_Studio/         catalog extraction/inspection helper
config/                        source profile templates
internal/defaults/             embedded Go profile defaults
roles/                         exported .mtx role templates
docs/                          design and safety docs
scripts/                       build scripts
```

## What Should Be Committed

Commit:

- source code;
- `config/` templates;
- `internal/defaults/` templates;
- docs;
- build/install scripts;
- curated role templates in `roles/` if intentionally shared.

Do not commit:

- `bin/`, `obj/`, `dist/`;
- local root-level operator profiles;
- managed runtime trees;
- `%LOCALAPPDATA%\NXKeys` backups/bridge/logs copied into the repo;
- generated executables, DLLs, PDBs, runtimeconfig/deps files.

## License

MIT.
