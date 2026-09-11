# Building and verifying WolfPatcher

These instructions build the first-party WolfPatcher executables from the public source.

## Requirements

- Windows 10 or Windows 11, x64
- .NET SDK `8.0.424`

The SDK version is pinned by `global.json`.

Verify it with:

```powershell
dotnet --version
```

Expected:

```text
8.0.424
```

## Clone

```powershell
git clone https://github.com/TheDogDen/WolfPatcher.git
cd WolfPatcher
```

## Normal build

Build the GUI:

```powershell
dotnet restore .\src\WolfPatcher.Gui\WolfPatcher.Gui.csproj
dotnet build .\src\WolfPatcher.Gui\WolfPatcher.Gui.csproj -c Release
```

Build the worker:

```powershell
dotnet restore .\src\WolfPatcher.Worker\WolfPatcher.Worker.csproj
dotnet build .\src\WolfPatcher.Worker\WolfPatcher.Worker.csproj -c Release
```

## Self-contained Windows publish

```powershell
dotnet publish .\src\WolfPatcher.Gui\WolfPatcher.Gui.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -o .\artifacts\gui

dotnet publish .\src\WolfPatcher.Worker\WolfPatcher.Worker.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -o .\artifacts\worker
```

## Reproducing the HOTF 1.0.0 first-party binaries

The public HOTF 1.0.0 runtime package contains GUI artifacts built in the `1.0.0` version state and Core/Worker artifacts retained from the immediately preceding `1.0.0-rc2` build.

For the byte-for-byte verification performed for publication, the archived source was placed at the historical build path recorded by the assemblies:

```text
D:\WolfPatcher\WOLFPATCHER_CORE_QA_CSHARP
```

Using the same path is recommended when independently checking the exact hashes because compiler/debug metadata records source/build paths.

### GUI 1.0.0

From the historical path:

```powershell
dotnet publish .\src\WolfPatcher.Gui\WolfPatcher.Gui.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -o D:\WolfPatcher_VERIFY_1.0.0\gui
```

Expected first-party hashes:

```text
WolfPatcher.Gui.exe  1c54b9b519d42b6b5d8cd3e60ffd9adf5a5a7cff8ebe3e0e8929d6480507aac0
WolfPatcher.Gui.dll  e6709581378db9504c2377f1d6838f946428e278e156aac96628b19dcc4ddba3
```

### Core / Worker 1.0.0-rc2

Clean `bin`/`obj` for Core and Worker, then publish:

```powershell
dotnet publish .\src\WolfPatcher.Worker\WolfPatcher.Worker.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:Version=1.0.0-rc2 `
  -o D:\WolfPatcher_VERIFY_RC2
```

Expected first-party hashes:

```text
WolfPatcher.Core.dll    0d86983210cb34236f8432a8dccfb16abfcfdccdbf5cd4e15c48d5b384f7fc51
WolfPatcher.Worker.dll  9df440f398c53745acba74bbd5a86e3b1a9401703460ecc4f4d3ceefceb811b1
WolfPatcher.Worker.exe  c184e4ae3536c99f43b79cef97809e31a44903c302ea8e962b728b17f9293c12
```

The publication verification reproduced all five artifacts byte-for-byte. See `SOURCE_PUBLICATION_STATUS.md` and `RELEASE_HASHES.md`.

## Runtime package data

The compiled executables alone are not a complete localization package.

At runtime WolfPatcher uses separate game-specific data such as profiles, supported baseline definitions, patch manifests, localization delta payloads, and third-party delta/compression tools.

Those runtime payloads and third-party executable binaries are intentionally not included in this source repository. No original Werewolf: The Apocalypse game files are required to compile the first-party source.
