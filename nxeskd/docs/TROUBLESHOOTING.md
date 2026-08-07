# Troubleshooting

## Command does not appear in NX

1. Confirm `NX_ESKD_ROOT` points to the installed package.
2. Inspect user `UGII_CUSTOM_DIRECTORY_FILE`.
3. Confirm `startup/nx_eskd.men` exists under install root.
4. Fully restart NX.
5. Do not edit system `user.mtx` as a workaround.

## Inventory works, Generate does not

Generate has stricter requirements: open WorkPart, Drafting collections/builders, ownership preflight and required licenses. Read the execution report codes beginning with `NX_CAPABILITY_` or `NX_LICENSE_`.

## `Generate already managed`

The current profile/job scope already owns objects. Use Update. Do not change `jobId` merely to bypass the check; that creates a second ownership scope.

## `Update not managed`

No exact-owned object exists. Use Generate or explicitly migrate legacy ownership after reviewing collisions.

## Duplicate managed ID

Locate every object listed by inventory. Keep only the intended object or assign a new logical ID through a controlled migration. Automatic selection of the first duplicate is forbidden.

## Flat Pattern reference errors

Create and name:

```text
FLAT_PATTERN_STATIONARY_FACE
FLAT_PATTERN_X_EDGE
```

or set the configured attribute on the stationary face. Automatic largest-face/longest-edge selection requires ManualReview.

## PMI inheritance unresolved

1. Run API Inventory.
2. Confirm source model view and target drawing view IDs.
3. Confirm PMI license.
4. Compare actual method signatures with `nx2512-api-map.json`.
5. Do not add multiple speculative aliases that invoke side effects.

## Report cannot be saved

The host falls back to `%LOCALAPPDATA%\NxEskdGenerator\reports`. A `REPORT_PATH_INVALID` or `REPORT_SAVE_FAILED` diagnostic preserves the original reason.

## NXMessageBox is missing

Messages fall back to Listing Window and then `%LOCALAPPDATA%\NxEskdGenerator\logs\command-host-fallback.log`.

## Rollback failed

Treat as P0. Do not save. Capture report/syslog, close the working copy without saving and repeat only on a fresh fixture.

## Build says wrong NX maintenance release

Do not copy DLLs from another installation. Confirm the official version metadata of the station. Pass the actual official maintenance identifier only if Siemens encodes it differently from `6000`.

## Signed installation rejected

- verify the trusted thumbprint list;
- confirm certificate validity and timestamp;
- verify `release-metadata.p7s` and `NxEskd.cat` exist;
- do not bypass signature verification for production use.
