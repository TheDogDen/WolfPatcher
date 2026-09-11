# WolfPatcher source publication status

Status: **verified first-party source published**

The first-party source tree in this repository was recovered from the archived WolfPatcher development workspace used for the HOTF 1.0.0 release and was validated against the distributed binaries.

## Binary reproduction verification

Environment:

- Windows x64
- .NET SDK `8.0.424`
- historical build path reproduced as `D:\WolfPatcher\WOLFPATCHER_CORE_QA_CSHARP`
- `Release`
- `win-x64`
- self-contained publish for GUI and Worker

The archived source reproduces the HOTF 1.0.0 release binaries byte-for-byte when the release's original component versioning is respected.

### GUI

Published with the source/default `1.0.0` version state:

- `WolfPatcher.Gui.exe` — PASS
  - SHA-256: `1c54b9b519d42b6b5d8cd3e60ffd9adf5a5a7cff8ebe3e0e8929d6480507aac0`
- `WolfPatcher.Gui.dll` — PASS
  - SHA-256: `e6709581378db9504c2377f1d6838f946428e278e156aac96628b19dcc4ddba3`

### Core / Worker

The public HOTF 1.0.0 package retained the Core/Worker artifacts from the `1.0.0-rc2` build. Rebuilding the same archived source with `-p:Version=1.0.0-rc2` reproduces them byte-for-byte:

- `WolfPatcher.Core.dll` — PASS
  - SHA-256: `0d86983210cb34236f8432a8dccfb16abfcfdccdbf5cd4e15c48d5b384f7fc51`
- `WolfPatcher.Worker.dll` — PASS
  - SHA-256: `9df440f398c53745acba74bbd5a86e3b1a9401703460ecc4f4d3ceefceb811b1`
- `WolfPatcher.Worker.exe` — PASS
  - SHA-256: `c184e4ae3536c99f43b79cef97809e31a44903c302ea8e962b728b17f9293c12`

This gives byte-for-byte reproduction of all five first-party executable/assembly artifacts used by the public HOTF 1.0.0 runtime package.

## Repository scope

The repository publishes first-party WolfPatcher source and tests. Runtime localization payloads, original game assets, private baseline copies, translated commercial game assets, and third-party executable binaries are intentionally excluded.

Some CLI/test harness defaults refer to the HOTF baseline because HOTF was the first validated profile. The patching engine itself remains profile-driven.
