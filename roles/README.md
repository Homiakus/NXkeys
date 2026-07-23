# Exported NX roles

Place a known-good NX 2512 `.mtx` role here only after configuring and testing radial menus inside NX.

Then point `role_deployment.source_mtx` in JSON to that file. NXKeys will back up the destination and copy the role as an opaque artifact; it will not edit its internals.
