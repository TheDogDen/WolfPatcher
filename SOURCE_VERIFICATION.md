# Source-to-binary verification — HOTF 1.0.0

This document records the verification of the archived WolfPatcher first-party source against the public `WolfPatcher_HOTF_PTBR_1.0.0.zip` runtime package.

## Verification environment

- Windows x64
- .NET SDK `8.0.424`
- historical build path: `D:\WolfPatcher\WOLFPATCHER_CORE_QA_CSHARP`
- configuration: `Release`
- runtime: `win-x64`
- publish mode: self-contained

The historical build path matters for exact byte reproduction because compiler/debug metadata records source/build paths.

## Public release package

`WolfPatcher_HOTF_PTBR_1.0.0.zip`

- Size: `104,198,215` bytes
- SHA-256: `619b2d0b6ebe817611736d2e948035e857ed13f4caf13773b1f296f49f5b0ab9`

## GUI verification

Command:

```powershell
dotnet publish .\src\WolfPatcher.Gui\WolfPatcher.Gui.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -o D:\WolfPatcher_VERIFY_1.0.0\gui
```

Results:

| Artifact | Result | SHA-256 |
|---|---|---|
| `WolfPatcher.Gui.exe` | PASS | `1c54b9b519d42b6b5d8cd3e60ffd9adf5a5a7cff8ebe3e0e8929d6480507aac0` |
| `WolfPatcher.Gui.dll` | PASS | `e6709581378db9504c2377f1d6838f946428e278e156aac96628b19dcc4ddba3` |

Both files reproduce the public package byte-for-byte.

## Core / Worker verification

Binary comparison showed that the public HOTF 1.0.0 package retained the Core/Worker artifacts from the `1.0.0-rc2` build. Rebuilding the archived source with that version property reproduces the release artifacts exactly.

Command:

```powershell
dotnet publish .\src\WolfPatcher.Worker\WolfPatcher.Worker.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:Version=1.0.0-rc2 `
  -o D:\WolfPatcher_VERIFY_RC2
```

Results:

| Artifact | Result | SHA-256 |
|---|---|---|
| `WolfPatcher.Core.dll` | PASS | `0d86983210cb34236f8432a8dccfb16abfcfdccdbf5cd4e15c48d5b384f7fc51` |
| `WolfPatcher.Worker.dll` | PASS | `9df440f398c53745acba74bbd5a86e3b1a9401703460ecc4f4d3ceefceb811b1` |
| `WolfPatcher.Worker.exe` | PASS | `c184e4ae3536c99f43b79cef97809e31a44903c302ea8e962b728b17f9293c12` |

All three files reproduce the public package byte-for-byte.

## Conclusion

The archived first-party source published in this repository reproduces all five principal first-party artifacts used by the HOTF 1.0.0 runtime package byte-for-byte when the release's actual component versioning is respected.

This verification does not depend on decompiled source. The published source comes from the archived WolfPatcher development workspace.
