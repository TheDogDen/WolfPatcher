# Security policy and design

WolfPatcher is a local/offline Windows patcher. This document describes the intended behavior of the first-party source published in this repository.

## Network behavior

The first-party WolfPatcher source does not implement a network client for patching, downloading, uploading, telemetry, analytics, account access, or remote license checks.

The runtime package is designed to work completely offline.

The self-contained .NET runtime includes framework assemblies whose names contain `System.Net`; their presence in a packaged .NET application does not by itself indicate that WolfPatcher uses them for network activity.

## Installation discovery

WolfPatcher may read local filesystem metadata and standard Windows registry locations in order to locate game installations for supported storefronts.

Storefront information is discovery evidence only. Compatibility is determined by the hashes of the actual game files.

## Privilege elevation

The normal frontend is not designed to run permanently elevated.

When the selected game directory requires administrator write access, WolfPatcher may request normal Windows UAC elevation for `WolfPatcher.Worker.exe`. Elevation is limited to the requested installation or restore operation.

## Filesystem changes

Before modifying supported game files, WolfPatcher performs read-only state/compatibility checks. Unknown or changed baselines are refused before the patching operation proceeds.

A supported installation is backed up before replacement. Patched outputs are validated against expected hashes, and the operation is designed to support recovery/rollback and later restoration of the original files.

## Persistence and Windows security settings

WolfPatcher does not intentionally:

- install a Windows service;
- install startup persistence;
- create a scheduled task for persistence;
- disable or bypass Microsoft Defender;
- disable or bypass SmartScreen;
- change Windows Firewall configuration;
- weaken Windows execution policy or other host security controls.

SmartScreen or antivirus reputation warnings for a new or unsigned executable must remain under the user's control. Users should verify the published SHA-256 values and public source rather than disabling security protections.

## Runtime third-party tools

The HOTF runtime package uses xdelta3/VCDIFF and XZ/liblzma tooling. Third-party executable binaries are not committed to this public first-party source repository. Their versions and release hashes are documented separately.

## Source verification

The HOTF 1.0.0 first-party GUI, Core and Worker artifacts were reproduced byte-for-byte from the archived source published here. See `BUILD.md`, `SOURCE_PUBLICATION_STATUS.md`, and `RELEASE_HASHES.md`.

## Reporting a security issue

Please report a suspected security problem privately to the project author before publishing exploit details. Include the WolfPatcher version, affected file/hash, Windows version, reproduction steps, and any relevant logs.
