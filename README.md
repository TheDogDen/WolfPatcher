# WolfPatcher

WolfPatcher is an offline Windows patcher used to install and restore Brazilian Portuguese (PT-BR) localizations for **Werewolf: The Apocalypse** games.

The first supported profile is **Werewolf: The Apocalypse — Heart of the Forest (HOTF)**.

This repository is the public source home of the first-party WolfPatcher application. It exists for transparency, security inspection, maintenance, and build verification.

## Current public package

Profile: `HOTF`

Localization version: `1.0.0`

Official runtime archive:

`WolfPatcher_HOTF_PTBR_1.0.0.zip`

SHA-256:

`619b2d0b6ebe817611736d2e948035e857ed13f4caf13773b1f296f49f5b0ab9`

Main GUI executable SHA-256:

`1c54b9b519d42b6b5d8cd3e60ffd9adf5a5a7cff8ebe3e0e8929d6480507aac0`

Elevated worker executable SHA-256:

`c184e4ae3536c99f43b79cef97809e31a44903c302ea8e962b728b17f9293c12`

See [RELEASE_HASHES.md](RELEASE_HASHES.md) for additional verified hashes.

## Architecture

WolfPatcher separates the generic patching engine from game-specific profiles.

The generic engine is responsible for installation discovery, compatibility checks, SHA-256 validation, backup, transactional patching, rollback/recovery, restoration, logs, and the user interface.

Game-specific information belongs to profiles. This includes executable names, `*_Data` directories, store identifiers, target files, supported baselines, patch sets, and expected final hashes.

Current profile:

- `HOTF` — Werewolf: The Apocalypse — Heart of the Forest

Planned future profile:

- `PURGATORY` — Werewolf: The Apocalypse — Purgatory

A future profile is not considered supported until its own baselines, patches, and final hashes have been independently validated.

## Security and privacy design

WolfPatcher is designed to operate locally and offline.

The production design does not require downloads, telemetry, analytics, account access, or remote license checks. It does not install services or persistence mechanisms and does not modify Microsoft Defender, SmartScreen, Firewall, or other Windows security settings.

When the selected game directory requires administrator access, the frontend may request elevation for `WolfPatcher.Worker.exe` through the normal Windows UAC mechanism.

See [SECURITY.md](SECURITY.md) for details.

## Compatibility model

The store is used only to help locate an installation.

Compatibility is determined by the real hashes of the game files. Steam, GOG, Epic Games Store, or manually selected installations are accepted only when they match a supported baseline. Unknown or modified builds are refused before game files are changed.

## Repository scope

This repository is intended to contain first-party source code and public documentation. It is not a distribution point for original game files.

It must not contain:

- original Werewolf: The Apocalypse game assets;
- full translated game assets from the commercial game;
- private baseline copies;
- localization delta payloads intended only for the runtime package;
- third-party executable binaries.

The end-user runtime package is distributed separately.

## Source publication status

The repository documentation is being established from the audited HOTF 1.0.0 runtime release.

The authoritative first-party source tree corresponding to that release must be present before this repository is presented as a complete source-to-binary verification reference. Until then, do not describe the repository as a reproducible build of the HOTF 1.0.0 binaries.

## Copyright / source usage

The first-party WolfPatcher source is made public for inspection and build verification. It is not automatically placed under an open-source license.

See [LICENSE.md](LICENSE.md).

Werewolf: The Apocalypse, Heart of the Forest, Purgatory, World of Darkness, and all original game content, names, trademarks, characters, settings, and assets remain the property of their respective rights holders. This project is unofficial and is not affiliated with or endorsed by the game developers, publishers, Paradox Interactive, or World of Darkness.

## Author

**TheDogDen**
