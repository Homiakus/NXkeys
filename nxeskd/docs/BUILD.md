# Build guide

## Core-only build

Does not require Siemens NX:

```powershell
dotnet restore .\tests\NxEskd.Core.Tests\NxEskd.Core.Tests.csproj
dotnet test .\tests\NxEskd.Core.Tests\NxEskd.Core.Tests.csproj -c Release
dotnet run --project .\tests\NxEskd.SmokeTests\NxEskd.SmokeTests.csproj -c Release -- .\config\active-profile.example.json
```

## NX runtime build

Requires the exact local NX 2512:6000 installation:

```powershell
.\scripts\build.ps1 `
  -NxRoot "C:\Program Files\Siemens\NX2512" `
  -ExpectedNxRelease 2512 `
  -ExpectedNxMaintenance 6000
```

The build is x64. `NXOpen.dll`, `NXOpen.UF.dll`, `NXOpenUI.dll` and `NXOpen.Utilities.dll` use `Private=false` and must not appear in output packages.

## Version evidence

`build-info.json` records:

- expected release and maintenance;
- .NET SDK;
- platform target;
- NX managed directory;
- file/product/assembly versions;
- public key token;
- SHA-256 and size of NXOpen and NXOpen.UF.

A build performed with `-SkipNxVersionCheck` is diagnostic-only.

## Output

```text
dist\NxEskd\
dist\NxEskd-NX2512-v<version>.zip
dist\NxEskd-NX2512-v<version>.zip.sha256
```

The package contains no Siemens assemblies. Production distribution additionally requires `scripts/sign-release.ps1` and verification with `scripts/install-production.ps1`.

## Common failures

| Failure | Meaning | Action |
|---|---|---|
| `NxOpenManagedDir is required` | NX project built outside station script | Use `scripts/build.ps1` |
| release/MR not found in metadata | Wrong installation or version encoding | Confirm NX installation; pass the official actual maintenance value |
| ProductVersion differs between NX DLLs | Mixed assemblies | Repair NX installation; never copy DLLs manually |
| public key token differs | Untrusted/mixed assembly pair | Stop build and verify source |
| closed NX assemblies in package | Packaging defect | Remove them; NX loads station copies |
| smoke/xUnit failure | Core contract regression | Fix before runtime build |
