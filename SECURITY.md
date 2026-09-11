# Security

WolfPatcher is designed as a local, offline patcher.

## Network behavior

The production design does not require Internet access to install or restore a localization and must not depend on downloads, uploads, telemetry, analytics, remote license checks, or cloud services.

The public first-party source tree will be reviewed against this requirement before this repository is presented as a complete source-to-binary verification reference.

## Windows Registry

Installation-discovery code may read Windows Registry locations used by supported storefronts in order to locate existing game installations.

WolfPatcher must not create, modify, or delete storefront Registry keys as part of normal discovery.

## Elevation

If the selected game directory is not writable by the current user, the GUI may request elevation for `WolfPatcher.Worker.exe` through the standard Windows UAC `runas` mechanism.

No service is installed and no persistent elevated process is created.

## File modification safeguards

WolfPatcher is designed to:

- identify supported installation states before modification;
- refuse unsupported, unknown, or modified baselines;
- create backup data before commit;
- use temporary/work files and transactional replacement;
- preserve recovery information across interrupted operations;
- validate resulting file hashes after installation or restoration.

For the HOTF 1.0.0 profile, a successful installation must reproduce all 13 expected final file hashes.

## Windows security features

WolfPatcher must not disable, bypass, or modify Microsoft Defender, SmartScreen, Firewall, or other Windows security settings.

A Microsoft SmartScreen reputation warning for a new or unsigned executable is not the same as a malware detection. Users should investigate specific antivirus detections rather than being instructed to disable security software.

## Reporting a security concern

Please contact **TheDogDen** through the project repository or Nexus Mods profile. Avoid posting sensitive exploit details publicly until they can be reviewed.
