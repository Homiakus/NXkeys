# JSON configuration reference

The JSON file is the only editable source of truth. Generated NX files should not be edited manually because the next apply will replace them.

## Root fields

| Field | Meaning |
|---|---|
| `schema_version` | `1` legacy or `2` modular profile |
| `profile` | Profile name, NX version and description |
| `scan` | Search roots and scanner limits |
| `deployment` | Overlay, backup, launcher and write policy |
| `keyboard` | Shortcut-to-command bindings |
| `radials` | Desired radial-menu sectors |
| `modules` | Module-aware command sets, module switching and radial layouts |
| `workflow_controls` | Common Accept/OK, Apply, Cancel and Back/Previous Step command references |
| `performance` | Cache, lazy scan and bridge watcher switches |
| `role_deployment` | Optional known-good `.mtx` copy operation |

## Schema v2 migration

NXKeys still reads schema v1 files. When HotkeyStudio saves a profile, it writes schema v2 and keeps legacy `keyboard[]` and `radials[]` for compatibility. The new `modules[]` layer becomes the preferred source for Leader HUD, module-specific command lists and modular radial plans.

## `keyboard[]`

```json
{
  "shortcut": "Ctrl+4",
  "command": {
    "id": "",
    "name": "Extrude",
    "aliases": []
  },
  "scope": "Modeling",
  "enabled": true,
  "notes": "NX Pro Hybrid bank"
}
```

Resolution order:

1. exact `command.id`;
2. exact normalized label or synonym;
3. alias match;
4. fuzzy token and edit-distance score;
5. refuse if the score is weak or two candidates are too close.

Use the explicit `id` in production configurations.

## `modules[]`

Each module defines the command set visible when the user is working in that NX application:

```json
{
  "id": "modeling",
  "label": "Modeling",
  "enabled": true,
  "nx_application_ids": ["UG_APP_MODELING"],
  "switch_command": {"id": "UG_APP_MODELING", "name": "Modeling"},
  "leader_prefix": "M",
  "selection_priorities": [],
  "command_sets": [
    {
      "id": "primary",
      "label": "Primary",
      "slot_semantics": {
        "N": "start/create/open primary object",
        "NE": "next main process step",
        "E": "add object/material/dependency",
        "SE": "transform or replace",
        "S": "finish/delete/secondary processing",
        "SW": "remove/reduce/relax",
        "W": "structure/link/pattern",
        "NW": "inspect/measure/service command"
      },
      "commands": [
        {
          "slot": "N",
          "command": {"id": "UG_CREATE_SKETCH", "name": "Sketch"},
          "requires_selection": false,
          "destructive": false,
          "confirm_before_execute": false,
          "fallback": ""
        }
      ]
    }
  ],
  "radials": []
}
```

Validation rules:

- enabled modules need a unique `id`;
- commands inside one `command_set` cannot repeat the same `slot`;
- each command needs either `command.id` or `command.name`;
- radial directions remain limited to `N NE E SE S SW W NW`.

The built-in v2 module defaults cover Modeling, Sketch, Assembly, Drafting, PMI, Surface, Sheet Metal, CAM/Manufacturing, CAE/Simulation, Routing, Mold/Tooling, Reuse/Templates, Inspect/View and Selection/Object. Specialized module commands are recommended starting points; availability depends on installed licenses and NX command catalogs.

## `workflow_controls`

`workflow_controls` standardizes the commands users expect while moving between dialogs and modules:

```json
{
  "workflow_controls": {
    "accept_ok": {"name": "OK", "aliases": ["Accept", "Finish"]},
    "apply": {"name": "Apply"},
    "cancel": {"name": "Cancel", "aliases": ["Deselect"]},
    "back_previous_step": {"name": "Back", "aliases": ["Previous Step"]},
    "confirm_dangerous": true
  }
}
```

HotkeyStudio uses this policy for unsaved configuration edits. Leader HUD uses it for dangerous commands: `Esc` cancels, `Backspace` goes back, and `Enter` confirms.

## `performance`

```json
{
  "performance": {
    "catalog_cache_enabled": true,
    "lazy_studio_scan": true,
    "bridge_watcher": true
  }
}
```

When cache is enabled, NXKeys stores parsed catalog data under `%LOCALAPPDATA%\NXKeys\cache\catalog-{nxVersion}-{rootsHash}.json`. The cache is invalidated by menu-file path, size and modification time. HotkeyStudio starts scans in the background when `lazy_studio_scan` is enabled. The Command Bridge uses a file watcher for pending requests and keeps a slower polling fallback.

## `deployment.mode`

### `managed-wrapper`

Creates a private custom directory and a `.cmd` launcher that sets `UGII_CUSTOM_DIRECTORY_FILE` for that NX process only. Recommended for first deployment.

### `existing-custom-dirs`

Adds the NXKeys custom directory to a discovered or configured custom-directory list. This affects every NX launch that consumes that list.

## Conflict policy

`clear_detected_conflicts` defaults to `false`.

When enabled, NXKeys emits an empty `ACCELERATOR` directive for conflicting `BUTTON` IDs found in scanned menu files. It cannot reliably inspect opaque role data, so this option does not guarantee that all user-role conflicts are removed.

## Radial menus

In schema v2, prefer `modules[].radials` for module-specific Application Radials and Object Radials. Legacy `radials` is still read and exported under a separate legacy section. Directions must be one of:

```text
N NE E SE S SW W NW
```

Automatic application requires `role_deployment.enabled: true` and an exported role template.
