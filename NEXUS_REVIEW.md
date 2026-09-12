# Nexus Mods support review — WolfPatcher HOTF PT-BR 1.0.0

This document is intended to make independent review of the WolfPatcher package easier for Nexus Mods support and security reviewers.

## Package identity

Official runtime archive:

`WolfPatcher_HOTF_PTBR_1.0.0.zip`

- Size: `104,198,215` bytes
- SHA-256: `619b2d0b6ebe817611736d2e948035e857ed13f4caf13773b1f296f49f5b0ab9`

Main first-party executables:

- `WolfPatcher.Gui.exe`
  - SHA-256: `1c54b9b519d42b6b5d8cd3e60ffd9adf5a5a7cff8ebe3e0e8929d6480507aac0`
- `worker/WolfPatcher.Worker.exe`
  - SHA-256: `c184e4ae3536c99f43b79cef97809e31a44903c302ea8e962b728b17f9293c12`

See [RELEASE_HASHES.md](RELEASE_HASHES.md) for all documented runtime hashes.

## Source-to-binary verification

The first-party source in this repository was recovered from the archived WolfPatcher development workspace used for the HOTF 1.0.0 release.

All five principal first-party executable/assembly artifacts were reproduced byte-for-byte from this source on Windows x64 using .NET SDK `8.0.424` and the historical build path recorded by the assemblies.

Verified artifacts:

- `WolfPatcher.Gui.exe` — byte-for-byte PASS
- `WolfPatcher.Gui.dll` — byte-for-byte PASS
- `WolfPatcher.Core.dll` — byte-for-byte PASS
- `WolfPatcher.Worker.dll` — byte-for-byte PASS
- `WolfPatcher.Worker.exe` — byte-for-byte PASS

The GUI artifacts correspond to the `1.0.0` version state. The release package retained Core/Worker artifacts from the immediately preceding `1.0.0-rc2` build; rebuilding with `-p:Version=1.0.0-rc2` reproduces those files exactly.

Full commands and hashes are documented in:

- [BUILD.md](BUILD.md)
- [SOURCE_VERIFICATION.md](SOURCE_VERIFICATION.md)
- [SOURCE_PUBLICATION_STATUS.md](SOURCE_PUBLICATION_STATUS.md)

## Runtime behavior relevant to security review

WolfPatcher is a local/offline Windows patcher.

The published first-party source does not implement:

- downloads or an HTTP/network client for patching;
- uploads;
- telemetry or analytics;
- account access;
- remote license checks;
- Windows services;
- startup persistence;
- scheduled-task persistence;
- Microsoft Defender disabling/bypass;
- SmartScreen disabling/bypass;
- Windows Firewall changes;
- execution-policy weakening.

The application may read standard local Windows registry locations and filesystem metadata to locate Steam, GOG or Epic installations. Store detection only helps locate the game; compatibility is determined by SHA-256 hashes of the actual game files.

When the selected game directory requires administrator write access, the GUI starts `WolfPatcher.Worker.exe` using the normal Windows UAC `runas` mechanism. This elevation is used only for the requested install/restore operation.

Before modifying supported game files, WolfPatcher performs compatibility checks. Unknown or modified baselines are refused. A supported installation is backed up before replacement, generated outputs are checked against expected final hashes, and restoration uses the local backup.

See [SECURITY.md](SECURITY.md) for the security design summary.

## Why antivirus or reputation systems may inspect the package closely

The package is an unsigned/new Windows patching utility. Its legitimate function includes modifying files inside a game installation, creating backups, launching a dedicated worker with UAC when write permissions require it, and invoking bundled delta/compression command-line tools.

These characteristics can be relevant to heuristic or reputation-based scanning, but they are also directly inspectable in the public source and documented here. This repository does not ask users to disable antivirus software, Defender, SmartScreen, Firewall or other Windows security controls.

## Bundled third-party runtime tools

The runtime package includes third-party tooling required for deterministic patch application:

- xdelta3 `3.2.0`
- XZ Utils / liblzma `5.8.1`

Verified hashes for the bundled third-party binaries are listed in [RELEASE_HASHES.md](RELEASE_HASHES.md), and license information is documented in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

Third-party executable binaries are intentionally not committed to this first-party source repository.

## Repository scope

This repository is intended to provide the complete first-party application source needed to inspect and build the WolfPatcher executables, together with build/security documentation and representative tests.

It intentionally does not redistribute:

- original commercial game files;
- full translated game assets;
- private baseline copies;
- localization delta payloads from the runtime package;
- third-party executable binaries.

Those exclusions are distribution/licensing boundaries and are not required to inspect or rebuild the first-party WolfPatcher executable code.

## Nexus Mods page

The existing HOTF PT-BR localization page is:

https://www.nexusmods.com/werewolftheapocalypseheartoftheforest/mods/2

The project author can provide the blocked Nexus upload, scan reference, or any additional diagnostic information requested by Nexus Mods support.
