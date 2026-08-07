# Security policy

## Supported status

The repository is a release candidate until the exact NX 2512:6000 station protocol passes. Source changes alone do not establish safe CAD behavior.

## Reporting a vulnerability

Do not publish model-corruption, arbitrary file-write, installer, signing-key, or command-execution vulnerabilities in a public issue. Report them privately to the repository owner with:

- affected commit and package version;
- NX release and maintenance release;
- minimal reproduction using a non-production fixture;
- execution report and NX syslog;
- before/after managed-object inventory;
- impact on model, filesystem, installer, or signing chain.

Do not include Siemens proprietary assemblies or customer models.

## Trust boundaries

- NX Open objects are trusted only after capability checks and postcondition read-back.
- JSON profiles are untrusted input and must pass strict schema and semantic validation.
- Only objects with the exact ownership key `profileId + jobId + objectKind + logicalId` may be modified or removed.
- Manual objects and foreign scopes must never be adopted implicitly.
- External Configurator requests are versioned, time-limited, bound to the profile hash and target part.
- NXOpen DLLs are loaded from the installed Siemens NX and are never packaged or committed.
- SHA-256 manifests detect corruption but do not authenticate the publisher.

## Release signing

Production releases require:

1. station build using `scripts/build.ps1` with exact NX release/MR verification;
2. Authenticode signing of DLL, EXE and PowerShell files;
3. a signed Windows file catalog;
4. detached CMS signature for `release-metadata.json`;
5. offline verification of signer certificate, timestamp and hashes;
6. retention of `build-info.json`, station reports and release metadata.

Use:

```powershell
.\scripts\sign-release.ps1 `
  -PackageDirectory .\dist\NxEskd `
  -ZipPath .\dist\NxEskd-NX2512-v1.0.2-rc1.zip `
  -CertificateThumbprint <CODE_SIGNING_CERTIFICATE>
```

Signing keys must be held in a hardware-backed store or protected CI secret. They must not be present in repository files, environment examples, test logs, or release archives.

## Model-safety response

If rollback cannot be confirmed:

1. stop further commands in that NX session;
2. do not save the part;
3. capture NX syslog and execution report;
4. close the working copy without saving;
5. reproduce only on a fresh fixture;
6. classify the defect as P0.

## Dependency and supply-chain rules

- Pin GitHub Actions by trusted major versions and review updates.
- Do not add runtime NuGet dependencies without license and vulnerability review.
- Do not download executable tools during installation.
- Do not bypass manifest/signature validation for production installation.
- Treat `-SkipNxVersionCheck`, unsigned packages and missing station reports as diagnostic-only states.
