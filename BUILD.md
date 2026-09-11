# Building WolfPatcher

This repository is the public source home of WolfPatcher.

## Requirements

- Windows 10 or Windows 11, x64
- .NET SDK 8.0.424

The required SDK version is pinned by `global.json`.

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

## Source publication status

The authoritative first-party source tree corresponding to the audited HOTF 1.0.0 runtime package is being prepared for publication in this repository.

Until that source import is complete, this repository must not be represented as a complete reproducible build of `WolfPatcher_HOTF_PTBR_1.0.0.zip`.

When the source tree is published, build commands and source-to-binary verification instructions will be finalized here.

## Runtime package data

Compiled executables alone are not a complete localization package.

At runtime WolfPatcher uses separate game-specific profile data, supported baseline definitions, patch manifests/integration configuration, localization patch payloads, and required third-party delta tools.

Those runtime package files are distributed separately from the first-party public source repository.

Original game files are not required to compile the first-party source and must not be committed to this repository.
