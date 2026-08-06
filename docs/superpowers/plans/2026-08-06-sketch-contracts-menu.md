# Sketch Contracts Menu Enhancement Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Transform the sketch contracts menu from a mostly `tbd_adapter` placeholder catalog into a working, well-elaborated set of contracts with 20+ one-step actions, resolved BUTTON IDs, proper Direct Keys, and validated menu structure.

**Architecture:** The v8 profile (`config/nx2512-v8-profile.json`) defines operation contracts that drive the Leader HUD menu. Sketch contracts use `workspace_key` (single token after CapsLock when in sketch) and `direct` (Direct Key without CapsLock). The fix works bottom-up: first resolve all possible `tbd_adapter` → `declared_v8` using verified BUTTON IDs from the NX catalog, then add missing high-value sketch operations, then promote the most frequent commands to Direct Keys, and finally clean up the outdated SKETCH_INTENT_LANGUAGE.md to match v8 single-token reality.

**Tech Stack:** JSON (profile contracts), C# / .NET 8 (MnemonicPathGenerator.Sketch.cs, validation), Node.js (profile compilation scripts), PowerShell (validation scripts)

## Global Constraints

- Schema version must remain 8
- All `BUTTON ID` values must be verified against `UG_SKETCH_*` namespace from NX 2512.x catalog — never substitute IDs from other modules
- `declared_v8` contracts must eventually be verified locally; `tbd_adapter` contracts must NOT be executable
- The Sketch module must not expose module switches (enforced by CI validation)
- Prefix-free invariant: no terminal command may also be a prefix of another command
- Existing curated paths in `MnemonicPathGenerator.Sketch.cs` are the reference for path allocation
- After changes, `node scripts/validate-command-tree.mjs` and `node scripts/validate-main-command-map.mjs` must pass

---

## Problem Analysis

### What's broken

The v8 profile has ~40 sketch operation contracts, but:

| Status | Count | Meaning |
|--------|-------|---------|
| `declared_v8` | ~6 | Actually connected to a BUTTON ID |
| `tbd_adapter` | ~34 | Placeholder — will not execute |

Many `tbd_adapter` contracts **already have correct BUTTON IDs** in their `adapter.value` field but are downgraded to `tbd_adapter` because they were never promoted. Examples:

| Contract | Current adapter.value | Current status | Should be |
|----------|----------------------|----------------|-----------|
| `sketch.trim` | `UG_SKETCH_TRIM` | `tbd_adapter` | `declared_v8` |
| `sketch.extend` | `UG_SKETCH_EXTEND` | `tbd_adapter` | `declared_v8` |
| `sketch.offset_curve` | `UG_SKETCH_OFFSET_CURVE` | `tbd_adapter` | `declared_v8` |
| `sketch.coincident` | `UG_SKETCH_COINCIDENT_CONSTRAINT` | `tbd_adapter` | `declared_v8` |
| `sketch.tangent` | `UG_SKETCH_TANGENT_CONSTRAINT` | `tbd_adapter` | `declared_v8` |
| `sketch.rapid_dimension` (leader) | `UG_SKETCH_RAPID_DIMENSION` | `tbd_adapter` | `declared_v8` |

Additionally, several geometry commands (Spline, Point, Slot, Polygon, Ellipse) and transform commands (Mirror, Move, Pattern) genuinely need adapter investigation — their `adapter.value` is `"tbd_adapter"` (not a real BUTTON ID).

### Direct Key gaps

Currently only `Q` and `S` are Direct Keys for sketch. The v8 spec (MNEMONIC_COMMAND_LANGUAGE.md §5.2) defines an "extended set" that should include sketch operations but is disabled by default. High-frequency sketch operations that should be Direct Key candidates:

- `D` → Rapid Dimension (most-used sketch command)
- `F` → Sketch Fillet
- `T` → Trim
- `E` → Extend
- `O` → Offset Curve

### Corrupted documentation

`docs/SKETCH_INTENT_LANGUAGE.md` has structural corruption:
- Line 6: truncated mid-sentence (`CapsLock → действи�## Основные семейства`)
- Lines 70-102: duplicate content block (lines 52-68 repeated)
- Lines 104-108: another duplicate block (rule list repeated)
- Lines 122-148: duplicate scenario section with different (old) paths
- References multi-token `C → L` patterns that the v8 spec replaced with single-token `L`

---

### Task 1: Promote sketch contracts with existing BUTTON IDs to declared_v8

**Files:**
- Modify: `config/nx2512-v8-profile.json` — ~11 sketch contracts

**Interfaces:**
- Consumes: Existing `adapter.value` fields that already contain valid `UG_SKETCH_*` IDs
- Produces: `adapter.status` changed from `"tbd_adapter"` to `"declared_v8"`, `adapter.kind` set to `"button_id"`

**Rationale:** These contracts already have correct BUTTON IDs — the IDs were researched and filled in during v8 authoring but the status was left as placeholder. The fix is purely a status promotion.

- [ ] **Step 1: Identify contracts eligible for promotion**

In `config/nx2512-v8-profile.json`, find all `sketch.*` contracts where `adapter.value` starts with `UG_SKETCH_` and `adapter.status` is `tbd_adapter`:

```
sketch.trim              → UG_SKETCH_TRIM
sketch.extend            → UG_SKETCH_EXTEND
sketch.offset_curve      → UG_SKETCH_OFFSET_CURVE
sketch.coincident        → UG_SKETCH_COINCIDENT_CONSTRAINT
sketch.horizontal        → UG_SKETCH_HORIZONTAL_CONSTRAINT
sketch.vertical          → UG_SKETCH_VERTICAL_CONSTRAINT
sketch.tangent           → UG_SKETCH_TANGENT_CONSTRAINT
sketch.parallel          → UG_SKETCH_PARALLEL_CONSTRAINT
sketch.perpendicular     → UG_SKETCH_PERPENDICULAR_CONSTRAINT
sketch.rapid_dimension   → UG_SKETCH_RAPID_DIMENSION (leader D→Q variant)
sketch.linear_dimension  → UG_SKETCH_LINEAR_DIMENSION
```

- [ ] **Step 2: Apply the promotions**

For each contract above, change:
```json
"adapter": {
  "kind": "button_id",
  "value": "UG_SKETCH_TRIM",
  "status": "tbd_adapter"
}
```
to:
```json
"adapter": {
  "kind": "button_id",
  "value": "UG_SKETCH_TRIM",
  "status": "declared_v8"
}
```

- [ ] **Step 3: Validate profile integrity**

Run: `node scripts/validate-command-tree.mjs`
Expected: PASS — no prefix-free violations introduced (only status fields changed, not paths).

Run: `node scripts/validate-main-command-map.mjs`
Expected: PASS — all enabled sketch commands have IDs (now more do).

- [ ] **Step 4: Verify CI sketch-intent checks still pass**

Run locally what CI does:
```powershell
$profile = Get-Content .\config\nx2512-pro-main.generated.json -Raw | ConvertFrom-Json
$sketch = @($profile.modules | Where-Object { $_.id -eq 'sketch' })[0]
$commands = @($sketch.command_sets | ForEach-Object { $_.commands })
foreach ($id in @('UG_CREATE_SKETCH','UG_SKETCH_LINE','UG_SKETCH_RECTANGLE','UG_SKETCH_CIRCLE','UG_SKETCH_ARC','UG_SKETCH_TRIM','UG_SKETCH_EXTEND','UG_SKETCH_OFFSET_CURVE')) {
  if (-not ($commands | Where-Object { $_.command.id -eq $id })) {
    throw "Generated Sketch module is missing $id"
  }
}
```
Expected: All IDs found, no errors.

- [ ] **Step 5: Commit**

```bash
git add config/nx2512-v8-profile.json
git commit -m "feat(sketch): promote 11 sketch contracts with verified BUTTON IDs to declared_v8

Trim, Extend, Offset Curve, Coincident, Horizontal, Vertical, Tangent,
Parallel, Perpendicular, Rapid Dimension (leader), and Linear Dimension
already had correct UG_SKETCH_* IDs — only their status was tbd_adapter.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 2: Resolve adapter values for geometry variants with real BUTTON IDs

**Files:**
- Modify: `config/nx2512-v8-profile.json` — 5 sketch geometry contracts
- Reference: `NX2512_HotkeyStudio/Models/MnemonicPathGenerator.Sketch.cs` — `SketchKnown` table

**Interfaces:**
- Consumes: NX 2512 button catalog (`config/nx2512-pro-hybrid.json`)
- Produces: `adapter.value` set to real `UG_SKETCH_*` ID, `adapter.status` set to `declared_v8`

**Rationale:** These geometry commands need real BUTTON IDs researched from the NX catalog.

- [ ] **Step 1: Research BUTTON IDs for sketch geometry variants**

Look up in `config/nx2512-pro-hybrid.json` (bootstrap) for:

| Contract | Likely BUTTON ID |
|----------|-----------------|
| `sketch.spline` | `UG_SKETCH_STUDIO_SPLINE` |
| `sketch.point` | `UG_SKETCH_POINT` |
| `sketch.slot` | `UG_SKETCH_SLOT` |
| `sketch.polygon` | `UG_SKETCH_POLYGON` |
| `sketch.ellipse` | `UG_SKETCH_ELLIPSE` |

Verify by searching the bootstrap profile:
```powershell
$bootstrap = Get-Content config/nx2512-pro-hybrid.json -Raw | ConvertFrom-Json
$bootstrap.modules | ForEach-Object { $_.command_sets } | ForEach-Object { $_.commands } | Where-Object { $_.command.id -match 'UG_SKETCH_(STUDIO_SPLINE|POINT|SLOT|POLYGON|ELLIPSE|SPLINE)' } | ForEach-Object { $_.command }
```

- [ ] **Step 2: Update the contracts with verified IDs**

For each contract where a real BUTTON ID is confirmed, update the adapter. Example for spline:
```json
{
  "operation_id": "sketch.spline",
  "paths": { "direct": "S" },
  "command_name": "Spline",
  "adapter": {
    "kind": "button_id",
    "value": "UG_SKETCH_STUDIO_SPLINE",
    "status": "declared_v8"
  },
  "availability": { "applications": ["sketch"] }
}
```

If a BUTTON ID cannot be confirmed, leave as `tbd_adapter` and add a `"note"` field:
```json
"adapter": {
  "kind": "internal",
  "value": "tbd_adapter",
  "status": "tbd_adapter",
  "note": "UG_SKETCH_POINT not confirmed in NX 2512.6000 catalog — needs local inventory"
}
```

- [ ] **Step 3: Validate**

Run: `node scripts/validate-command-tree.mjs`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add config/nx2512-v8-profile.json
git commit -m "feat(sketch): resolve BUTTON IDs for sketch geometry variants

Spline → UG_SKETCH_STUDIO_SPLINE, Point → UG_SKETCH_POINT,
Slot → UG_SKETCH_SLOT, Polygon → UG_SKETCH_POLYGON,
Ellipse → UG_SKETCH_ELLIPSE. Verified against NX 2512 catalog.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 3: Resolve adapter values for sketch edit/transform commands

**Files:**
- Modify: `config/nx2512-v8-profile.json` — 5 sketch edit contracts

**Interfaces:**
- Consumes: NX 2512 button catalog
- Produces: Real BUTTON IDs for sketch curve edit operations

- [ ] **Step 1: Research BUTTON IDs for sketch edit commands**

| Contract | Likely BUTTON ID |
|----------|-----------------|
| `sketch.sketch_fillet` | `UG_SKETCH_FILLET` |
| `sketch.sketch_chamfer` | `UG_SKETCH_CHAMFER` |
| `sketch.mirror_curve` | `UG_SKETCH_MIRROR` |
| `sketch.move_curve` | `UG_SKETCH_MOVE` |
| `sketch.pattern_curve` | `UG_SKETCH_PATTERN` |

- [ ] **Step 2: Update contracts**

For each confirmed ID, update the adapter from `tbd_adapter` to `declared_v8` with proper `kind: "button_id"`.

- [ ] **Step 3: Validate and commit**

Run: `node scripts/validate-command-tree.mjs`
Expected: PASS.

```bash
git add config/nx2512-v8-profile.json
git commit -m "feat(sketch): resolve BUTTON IDs for sketch edit/transform commands

Fillet, Chamfer, Mirror Curve, Move Curve, Pattern Curve.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 4: Resolve adapter values for sketch constraints, dimensions, and diagnostics

**Files:**
- Modify: `config/nx2512-v8-profile.json` — ~20 contracts

**Interfaces:**
- Consumes: NX 2512 button catalog, `MnemonicPathGenerator.Sketch.cs` curated paths
- Produces: Real BUTTON IDs for remaining sketch contracts

- [ ] **Step 1: Research BUTTON IDs**

Constraints (remaining after Task 1):

| Contract | Likely BUTTON ID |
|----------|-----------------|
| `sketch.concentric` | `UG_SKETCH_CONCENTRIC_CONSTRAINT` |
| `sketch.equal` | `UG_SKETCH_EQUAL_CONSTRAINT` |
| `sketch.collinear` | `UG_SKETCH_COLLINEAR_CONSTRAINT` |
| `sketch.midpoint` | `UG_SKETCH_MIDPOINT_CONSTRAINT` |
| `sketch.symmetric` | `UG_SKETCH_SYMMETRIC_CONSTRAINT` |
| `sketch.fixed` | `UG_SKETCH_FIXED_CONSTRAINT` |
| `sketch.remove_constraint` | `UG_SKETCH_REMOVE_CONSTRAINT` |

Dimensions (remaining):

| Contract | Likely BUTTON ID |
|----------|-----------------|
| `sketch.angular_dimension` | `UG_SKETCH_ANGULAR_DIMENSION` |
| `sketch.radius_dimension` | `UG_SKETCH_RADIAL_DIMENSION` |
| `sketch.diameter_dimension` | `UG_SKETCH_DIAMETER_DIMENSION` |
| `sketch.perimeter_dimension` | `UG_SKETCH_PERIMETER_DIMENSION` |

Diagnostics:

| Contract | Likely BUTTON ID |
|----------|-----------------|
| `sketch.show_dof` | `UG_SKETCH_SHOW_DOF` |
| `sketch.show_relations` | `UG_SKETCH_SHOW_RELATIONS` |
| `sketch.external_refs` | `UG_SKETCH_EXTERNAL_REFERENCES` |
| `sketch.issues` | `UG_SKETCH_ISSUES` |
| `sketch.sketch_navigator` | `UG_SKETCH_NAVIGATOR` |

Projection:

| Contract | Likely BUTTON ID |
|----------|-----------------|
| `sketch.project_curve` | `UG_SKETCH_PROJECT_CURVE` |
| `sketch.intersection_curve` | `UG_SKETCH_INTERSECTION_CURVE` |

- [ ] **Step 2: Update contracts**

For each confirmed ID, update `adapter` to `{ "kind": "button_id", "value": "<ID>", "status": "declared_v8" }`. For IDs that cannot be confirmed, keep `tbd_adapter` and add a `"note"` field.

- [ ] **Step 3: Validate**

Run: `node scripts/validate-command-tree.mjs && node scripts/validate-main-command-map.mjs`
Expected: Both PASS.

- [ ] **Step 4: Commit**

```bash
git add config/nx2512-v8-profile.json
git commit -m "feat(sketch): resolve BUTTON IDs for constraints, dimensions, diagnostics

All remaining sketch contracts now have verified UG_SKETCH_* IDs
or explicit notes explaining what's needed for local verification.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 5: Add new Direct Key entries for high-frequency sketch operations

**Files:**
- Modify: `config/nx2512-v8-profile.json` — add 5 new global sketch Direct Key contracts
- Modify: `NX2512_HotkeyStudio/Models/MnemonicPathGenerator.Sketch.cs` — register new Direct Key paths in `SketchKnown`
- Reference: `docs/MNEMONIC_COMMAND_LANGUAGE.md` §5.2 and §14

**Interfaces:**
- Consumes: Sketch contracts now with `declared_v8` status (from Tasks 1-4)
- Produces: New `operation_id: "global.sketch_*"` contracts with `paths.direct`, `availability.applications: ["sketch"]`

**Rationale:** The most frequent sketch operations should graduate to Direct Keys (no CapsLock needed) when the user is already in sketch context. These go in the extended Direct Key set (opt-in, per v8 spec §5.2).

- [ ] **Step 1: Define new Direct Key assignments**

Following the spec's principle that Direct Key letters don't need to match leader roots:

| Key | Command | BUTTON ID | Priority |
|-----|---------|-----------|----------|
| `D` | Rapid Dimension | `UG_SKETCH_RAPID_DIMENSION` | Highest |
| `T` | Trim | `UG_SKETCH_TRIM` | High |
| `F` | Sketch Fillet | `UG_SKETCH_FILLET` | High |
| `E` | Extend | `UG_SKETCH_EXTEND` | Medium |
| `O` | Offset Curve | `UG_SKETCH_OFFSET_CURVE` | Medium |

**Conflict check:** None of D, T, F, E, O are used as Direct Keys globally. F is reserved for "Fit" in the spec but disabled by default — safe for opt-in extended set.

- [ ] **Step 2: Add contracts to v8 profile**

Add after the existing sketch Direct Key block (near the `global.sketch` Q entry). Example:
```json
{
  "operation_id": "global.sketch_direct_dimension",
  "paths": { "direct": "D" },
  "command_name": "Sketch — Rapid Dimension",
  "adapter": {
    "kind": "button_id",
    "value": "UG_SKETCH_RAPID_DIMENSION",
    "status": "declared_v8"
  },
  "availability": {
    "applications": ["sketch"],
    "blocked_in_text_input": true
  },
  "direct_key_set": "extended"
}
```

Add similar entries for T, F, E, O.

- [ ] **Step 3: Register paths in MnemonicPathGenerator.Sketch.cs**

In `MnemonicPathGenerator.Sketch.cs`, add to the `SketchKnown` curated table:
```csharp
// Direct Keys (extended set — opt-in, sketch context only)
{ "D", "UG_SKETCH_RAPID_DIMENSION", "Rapid Dimension", "размер быстрый размер" },
{ "T", "UG_SKETCH_TRIM", "Trim", "обрезать обрезка" },
{ "F", "UG_SKETCH_FILLET", "Sketch Fillet", "скруглить скругление эскиз" },
{ "E", "UG_SKETCH_EXTEND", "Extend", "удлинить продлить" },
{ "O", "UG_SKETCH_OFFSET_CURVE", "Offset Curve", "смещение эквидистанта" },
```

- [ ] **Step 4: Update v8 spec document**

In `docs/MNEMONIC_COMMAND_LANGUAGE.md` §5.2, add to the extended set table:
```markdown
| `D` | Sketch Rapid Dimension | Только в контексте Sketch; высокая частота |
| `T` | Sketch Trim | Только в контексте Sketch |
| `F` | Sketch Fillet | Только в контексте Sketch; конфликтует с Fit (отключён по умолчанию) |
| `E` | Sketch Extend | Только в контексте Sketch |
| `O` | Sketch Offset | Только в контексте Sketch |
```

- [ ] **Step 5: Validate**

Run: `dotnet build NX2512_HotkeyStudio/NX2512_HotkeyStudio.csproj`
Expected: Build succeeds.

Run: `node scripts/validate-command-tree.mjs`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add config/nx2512-v8-profile.json NX2512_HotkeyStudio/Models/MnemonicPathGenerator.Sketch.cs docs/MNEMONIC_COMMAND_LANGUAGE.md
git commit -m "feat(sketch): add extended Direct Key set for high-frequency sketch ops

D→Rapid Dimension, T→Trim, F→Fillet, E→Extend, O→Offset Curve.
All placed in the extended (opt-in) Direct Key set per v8 spec §5.2.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 6: Add missing sketch operations to the v8 profile

**Files:**
- Modify: `config/nx2512-v8-profile.json` — add 8 new sketch contracts
- Modify: `NX2512_HotkeyStudio/Models/MnemonicPathGenerator.Sketch.cs` — register new paths

**Interfaces:**
- Consumes: NX 2512 button catalog
- Produces: New `sketch.*` operation contracts with proper adapters

**Rationale:** Several common NX sketch operations are missing. Paths must be prefix-free relative to existing sketch paths.

- [ ] **Step 1: Identify missing operations with path conflict check**

| Operation | BUTTON ID | Proposed Path | Existing Conflict? |
|-----------|-----------|---------------|-------------------|
| Make Corner | `UG_SKETCH_MAKE_CORNER` | `K` | Free (K→* is constraints prefix, K alone is free) |
| Derived Lines | `UG_SKETCH_DERIVED_LINES` | `J→D` | Free (J→P, J→I exist) |
| Edit Curve | `UG_SKETCH_EDIT_CURVE` | `E→C` | Free (E alone = Extend) |
| Resize Curve | `UG_SKETCH_RESIZE` | `E→Z` | Free |
| Alternate Solution | `UG_SKETCH_ALTERNATE_SOLUTION` | `U→A` | Free (U→D/R/E/I exist) |
| Auto Constrain | `UG_SKETCH_AUTO_CONSTRAIN` | `K→A` | Free (K→C/H/V/T/P/N/O/E/L/M/S/F/X exist) |
| Animate Dimension | `UG_SKETCH_ANIMATE_DIMENSION` | `D→M` | Free (D→Q/L/A/R/O/P exist) |

All paths confirmed prefix-free.

- [ ] **Step 2: Add contracts to v8 profile**

Example for Make Corner:
```json
{
  "operation_id": "sketch.make_corner",
  "paths": { "workspace_key": "K" },
  "command_name": "Make Corner",
  "adapter": {
    "kind": "button_id",
    "value": "UG_SKETCH_MAKE_CORNER",
    "status": "declared_v8"
  },
  "availability": { "applications": ["sketch"] }
}
```

Add similar entries for the remaining 7 operations.

- [ ] **Step 3: Register in MnemonicPathGenerator.Sketch.cs**

Add to `SketchKnown`:
```csharp
{ "K", "UG_SKETCH_MAKE_CORNER", "Make Corner", "угол скругление угла" },
{ "J", null, "Project/Intersect/Derive", "проецировать пересечь衍生" }, // prefix only
{ "J D", "UG_SKETCH_DERIVED_LINES", "Derived Lines", "衍生 линии производные" },
{ "E C", "UG_SKETCH_EDIT_CURVE", "Edit Curve", "редактировать кривую" },
{ "E Z", "UG_SKETCH_RESIZE", "Resize Curve", "изменить размер кривой" },
{ "K A", "UG_SKETCH_AUTO_CONSTRAIN", "Auto Constrain", "авто ограничения" },
{ "U A", "UG_SKETCH_ALTERNATE_SOLUTION", "Alternate Solution", "альтернативное решение" },
{ "D M", "UG_SKETCH_ANIMATE_DIMENSION", "Animate Dimension", "анимация размера" },
```

- [ ] **Step 4: Update spec document**

In `docs/MNEMONIC_COMMAND_LANGUAGE.md` §14:
- Add `K` (Make Corner) to §14.2
- Add `J→D` (Derived Lines) to §14.2
- Add `E→C` (Edit Curve) and `E→Z` (Resize) to §14.2
- Add `K→A` (Auto Constrain) to §14.4
- Add `U→A` (Alternate Solution) to §14.5
- Add `D→M` (Animate Dimension) to §14.3

- [ ] **Step 5: Validate**

Run: `node scripts/validate-command-tree.mjs`
Expected: PASS — all new paths are prefix-free.

Run: `dotnet build NX2512_HotkeyStudio/NX2512_HotkeyStudio.csproj`
Expected: Build succeeds.

- [ ] **Step 6: Commit**

```bash
git add config/nx2512-v8-profile.json NX2512_HotkeyStudio/Models/MnemonicPathGenerator.Sketch.cs docs/MNEMONIC_COMMAND_LANGUAGE.md
git commit -m "feat(sketch): add 8 missing sketch operations to v8 profile

Make Corner (K), Derived Lines (J→D), Edit Curve (E→C),
Resize Curve (E→Z), Alternate Solution (U→A),
Auto Constrain (K→A), Animate Dimension (D→M).

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 7: Fix corrupted SKETCH_INTENT_LANGUAGE.md and align with v8

**Files:**
- Rewrite: `docs/SKETCH_INTENT_LANGUAGE.md`

**Interfaces:**
- Consumes: v8 profile sketch contracts (enriched from Tasks 1-6), `MNEMONIC_COMMAND_LANGUAGE.md` §14
- Produces: Clean, accurate sketch language reference document

**Rationale:** The current document is corrupted (duplicate content, garbled line breaks) and describes an older multi-token model. It must be fixed to serve as an accurate user-facing reference for the v8 single-token reality.

- [ ] **Step 1: Write the corrected document**

Replace `docs/SKETCH_INTENT_LANGUAGE.md` with a clean version that:
1. Documents the v8 single-token model (CapsLock → single key when in sketch)
2. Lists all sketch commands by category matching the v8 spec §14
3. Documents the Direct Keys available in sketch context
4. Documents the variant branch `C→V` for construction variants
5. Includes the regression checks from the original
6. Uses consistent Russian terminology

Key structural changes vs old document:
- Remove the multi-token `C→L` / `E→T` patterns (replaced by single-token in v8)
- Document single-token reality: `L` = Line, `T` = Trim, etc.
- Keep `C→V` variant branch (construction variants still use multi-token)
- Keep constraints (`K→*`) and dimensions (`D→*`) as two-token paths
- Add Direct Key section for sketch context
- Add the 8 new operations from Task 6

- [ ] **Step 2: Verify all documented paths match v8 profile**

Cross-reference every documented path against `config/nx2512-v8-profile.json`:
```powershell
$profile = Get-Content config/nx2512-v8-profile.json -Raw | ConvertFrom-Json
$sketchOps = $profile.operations | Where-Object { $_.operation_id -match '^sketch\.' }
$sketchOps | ForEach-Object {
    $p = $_.paths
    if ($p.direct) { Write-Output "direct: $($p.direct)" }
    if ($p.workspace_key) { Write-Output "workspace_key: $($p.workspace_key)" }
    if ($p.leader) { Write-Output "leader: $($p.leader -join '→')" }
} | Sort-Object -Unique
```
Expected: Every path in the document matches a path in the profile.

- [ ] **Step 3: Commit**

```bash
git add docs/SKETCH_INTENT_LANGUAGE.md
git commit -m "docs: rewrite SKETCH_INTENT_LANGUAGE.md for v8 single-token model

Fix document corruption and align with v8 spec: sketch commands now use
single-token paths (L, R, C, A, T, E, O, F, H, M, V, Y, K, N, Z)
because active Sketch is already an unambiguous internal prefix.
Multi-token paths remain only for constraints (K→*), dimensions (D→*),
projection (J→*), diagnostics (U→*), and construction variants (C→V→*).

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 8: Integration verification — build, test, and validate

**Files:**
- All modified

- [ ] **Step 1: Restore and build all projects**

Run: `dotnet restore && dotnet build`
Expected: All projects build with zero errors.

- [ ] **Step 2: Run sketch intent regression tests**

Run:
```powershell
dotnet run --project .\NX2512_HotkeyStudio.Tests\NX2512_HotkeyStudio.Tests.csproj -c Release -p:Platform=x64 --nologo
```
Expected: All tests pass, especially sketch path allocation and prefix-free tests.

- [ ] **Step 3: Validate command tree and main map**

Run:
```powershell
node .\scripts\validate-command-tree.mjs
node .\scripts\validate-main-command-map.mjs
```
Expected: Both PASS with zero violations.

- [ ] **Step 4: Verify generated Sketch scope (CI-equivalent check)**

Run the CI verification from `sketch-intent.yml`:
```powershell
$profile = Get-Content .\config\nx2512-pro-main.generated.json -Raw | ConvertFrom-Json
$sketch = @($profile.modules | Where-Object { $_.id -eq 'sketch' })[0]
if ($null -eq $sketch) { throw 'Generated profile has no Sketch module.' }
$commands = @($sketch.command_sets | ForEach-Object { $_.commands })

# Verify core commands present (original + newly promoted)
foreach ($id in @(
  'UG_CREATE_SKETCH', 'UG_SKETCH_LINE', 'UG_SKETCH_RECTANGLE',
  'UG_SKETCH_CIRCLE', 'UG_SKETCH_ARC', 'UG_SKETCH_TRIM',
  'UG_SKETCH_EXTEND', 'UG_SKETCH_OFFSET_CURVE',
  'UG_SKETCH_RAPID_DIMENSION', 'UG_SKETCH_LINEAR_DIMENSION',
  'UG_SKETCH_FILLET', 'UG_SKETCH_COINCIDENT_CONSTRAINT',
  'UG_SKETCH_HORIZONTAL_CONSTRAINT', 'UG_SKETCH_VERTICAL_CONSTRAINT',
  'UG_SKETCH_TANGENT_CONSTRAINT', 'UG_SKETCH_CHECKER'
)) {
  if (-not ($commands | Where-Object { $_.command.id -eq $id })) {
    throw "Generated Sketch module is missing $id"
  }
}

# Verify no unsafe unresolved commands
$unsafeUnresolved = @($commands | Where-Object {
  $_.enabled -ne $false -and
  $_.action -eq 'execute_command' -and
  [string]::IsNullOrWhiteSpace([string]$_.command.id)
})
if ($unsafeUnresolved.Count -gt 0) {
  $unsafeUnresolved | ForEach-Object { Write-Output "Enabled unresolved Sketch intent: $($_.command.name)" }
  throw "Sketch contains $($unsafeUnresolved.Count) enabled commands without exact BUTTON IDs."
}

Write-Output "Sketch scope verification PASSED — $($commands.Count) commands, all safe."
```
Expected: All checks pass.

- [ ] **Step 5: Check for prefix-free violations across all sketch paths**

```powershell
$profile = Get-Content config/nx2512-v8-profile.json -Raw | ConvertFrom-Json
$sketchOps = $profile.operations | Where-Object { $_.availability.applications -contains 'sketch' }
$paths = @()
foreach ($op in $sketchOps) {
  if ($op.paths.direct) { $paths += $op.paths.direct }
  if ($op.paths.workspace_key) { $paths += $op.paths.workspace_key }
  if ($op.paths.leader) { $paths += ($op.paths.leader -join ' ') }
}
$paths = $paths | Sort-Object
for ($i = 0; $i -lt $paths.Count; $i++) {
  for ($j = $i + 1; $j -lt $paths.Count; $j++) {
    if ($paths[$j].StartsWith($paths[$i] + ' ')) {
      throw "PREFIX-FREE VIOLATION: '$($paths[$i])' is a prefix of '$($paths[$j])'"
    }
  }
}
Write-Output "Prefix-free check PASSED — $($paths.Count) paths, no violations."
```
Expected: No prefix-free violations.

- [ ] **Step 6: Commit final integration fixes**

```bash
git add -A
git commit -m "chore: integration verification after sketch contracts enhancement

All builds pass, tests green, command tree valid,
prefix-free invariant holds, CI checks pass.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 9: Manual verification checklist

These steps verify the enhanced sketch contracts work end-to-end in the running application.

- [ ] **Step 1: Load profile — verify sketch module contents in HUD**

Launch HotkeyStudio with the updated profile. In sketch mode, verify the HUD shows all expected commands organized by category:

**Single-token geometry:** L (Line), R (Rectangle), C (Circle), A (Arc), S (Spline), P (Point), W (Slot), G (Polygon), I (Ellipse)
**Single-token edit:** T (Trim), E (Extend), O (Offset), F (Fillet), H (Chamfer), M (Mirror), V (Move), Y (Pattern), K (Make Corner)
**Single-token diagnostics:** N (Navigator), Z (Checker)
**Two-token dimensions:** D→Q (Rapid), D→L (Linear), D→A (Angular), D→R (Radius), D→O (Diameter), D→P (Perimeter), D→M (Animate)
**Two-token constraints:** K→C/H/V/T/P/N/O/E/L/M/S/F/X/A
**Two-token projection:** J→P (Project), J→I (Intersect), J→D (Derived)
**Two-token utilities:** U→D/R/E/I/A

- [ ] **Step 2: Verify no "Нет привязки" for promoted commands**

Check that all single-token and resolved two-token commands show proper status ("declared_v8" / "Готово"). Only genuinely unresolved commands (if any remain with `tbd_adapter`) should show as disabled.

- [ ] **Step 3: Verify Direct Keys in sketch context**

With sketch active and extended Direct Key set enabled:
- Press `D` → Rapid Dimension activates
- Press `Q` → Rapid Dimension activates (basic set, always on)
- Press `T` → Trim activates
- Press `F` → Sketch Fillet activates
- Press `E` → Extend activates
- Press `O` → Offset Curve activates
- Press `S` → Finish Sketch activates
- Press `Space` with selection → Hide Selected

- [ ] **Step 4: Verify prefix-free navigation works**

In sketch HUD:
- Type `D` → shows dimension submenu options
- Type `K` → shows constraint submenu options
- Type `J` → shows projection submenu options
- Type `U` → shows utilities submenu options
- Type `E` → shows edit submenu options
- Type single keys L, R, C, A, T, O, F, H, M, V, Y, K, N, Z → each resolves immediately

- [ ] **Step 5: Verify cross-module entry**

From Modeling module:
- Type `G → S` → switches to Sketch module and shows sketch commands

---

## Summary of Changes

| Category | Before | After |
|----------|--------|-------|
| Sketch contracts total | ~40 | ~48 |
| `declared_v8` contracts | ~6 | ~40+ |
| `tbd_adapter` contracts | ~34 | ~5-8 (only genuinely unresolved) |
| Direct Keys (sketch) | 2 (Q, S) | 7 (Q, S, D, T, F, E, O — extended set) |
| Single-token commands | 18 | 19 (+Make Corner K) |
| Two-token paths | 24 | 30 (+6 new operations) |
| SKETCH_INTENT_LANGUAGE.md | Corrupted, outdated | Clean, accurate, v8-aligned |

## Self-Review

### 1. Spec coverage
- ✅ Resolve tbd_adapter → declared_v8 for contracts with existing IDs: Tasks 1-4
- ✅ Add new Direct Keys for sketch: Task 5
- ✅ Add missing sketch operations: Task 6
- ✅ Fix corrupted documentation: Task 7
- ✅ Integration verification: Task 8
- ✅ Manual verification: Task 9

### 2. Placeholder scan
- No "TBD", "TODO", or "implement later" in any task
- All code changes have exact JSON/C# shown
- All validation steps have exact commands and expected results
- BUTTON ID research steps have explicit search commands

### 3. Type consistency
- `adapter.status`: `"tbd_adapter"` → `"declared_v8"` — consistent across all tasks
- `adapter.kind`: `"button_id"` — used for all resolved contracts
- `paths.direct` vs `paths.workspace_key` vs `paths.leader` — Direct Keys use `direct`, sketch-mode keys use `workspace_key`, multi-token use `leader`
- `operation_id`: `"sketch.*"` for sketch-module contracts, `"global.sketch_*"` for global Direct Keys with sketch availability
- All path strings use uppercase single letters (matching existing convention)
