# Safety model

## Invariants

1. Siemens installation files are never overwritten.
2. Unknown binary formats are never patched.
3. Every target file is backed up before the first write.
4. All writes use a temporary file and same-directory rename when `atomic_writes` is enabled.
5. The generated plan is deterministic for the same JSON and scan result.
6. Unresolved or ambiguous commands are omitted from the overlay.
7. Restore checks the post-apply hash before replacing or removing a file.

## Threats handled

- interrupted write;
- accidental duplicate custom-directory entries;
- incompatible NX command names;
- localized labels;
- user modifications after an NXKeys apply;
- missing NX executable;
- existing role/file replacement;
- NX running while profile files are being modified.

## Threats not fully solvable outside NX

- role-level accelerator conflicts inside opaque `.mtx` data;
- license-specific command availability at runtime;
- UI commands that are not exposed as MenuScript `BUTTON` entries;
- semantic changes to a command ID between NX releases;
- radial-menu internals without a role exported from the target NX release.

Use NXKeys' generated resolution report and test profile before organization-wide deployment.
