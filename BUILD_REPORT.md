# NXKeys 0.1.0 — build and validation report

Date: 2026-07-21

## Delivered scope

- Go application with Bubble Tea v2 and Lip Gloss v2 TUI.
- Windows-oriented NX 2512 installation/profile scanner.
- MenuScript command catalog parser for `BUTTON`, `LABEL`, `SYNONYMS`, `MESSAGE`, and `ACCELERATOR`.
- Safe command resolution by exact ID, label, aliases, and guarded fuzzy matching.
- JSON-only configuration source.
- 47 keyboard bindings from the supplied NX Pro Hybrid specification.
- 3 Application Radial layouts and 4 Object Radial layouts.
- Dry-run and deterministic change plan.
- Atomic writes.
- SHA-256 backup manifest and guarded restore.
- Managed `UGII_CUSTOM_DIRECTORY_FILE` launcher.
- Optional deployment of a known-good exported NX `.mtx` role.

## Validation performed

The following checks passed in the build sandbox:

- `gofmt` on all Go sources;
- JSON syntax validation for both default configuration copies;
- equality check between distributable and embedded JSON defaults;
- unit tests for config, menu parsing/resolution, backup, and engine packages;
- full package compile/type check using local Bubble Tea/Lip Gloss interface stubs;
- `go vet` across all packages using those interfaces;
- end-to-end simulation against a fake NX 2512 menu tree:
  - scan;
  - command resolution;
  - plan;
  - dry-run;
  - apply;
  - generated `ACCELERATOR` verification;
  - backup listing;
  - restore.

## Toolchain note

The project pins:

- `charm.land/bubbletea/v2 v2.0.8`;
- `charm.land/lipgloss/v2 v2.0.5`.

Both require Go 1.25. The sandbox did not have online module download or a Go 1.25 toolchain, so a release Windows executable and `go.sum` were not generated here. On a connected Windows workstation, `scripts/build.ps1` runs `go mod download`, the real dependency build, all tests, and produces `dist/nxkeys-windows-amd64.exe`.

## Safety boundary

NXKeys deliberately does not decode or modify the internal structure of `user.mtx` or arbitrary `.mtx` files. Keyboard assignments are deployed as a generated MenuScript overlay. Radial menus are represented in JSON and can be deployed automatically only by copying a role exported and tested in NX 2512.
