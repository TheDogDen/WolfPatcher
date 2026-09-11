# Third-Party Notices

This public repository is intended to contain first-party WolfPatcher source code and documentation only. It does not vendor third-party executable binaries.

## xdelta3 / VCDIFF

The HOTF 1.0.0 runtime package uses the external `xdelta3` command-line tool to apply VCDIFF deltas.

Verified runtime tool version:

`xdelta3 3.2.0`

Upstream project:

https://github.com/jmacd/xdelta

The upstream xdelta project states that the 3.2.x series is licensed under the Apache License 2.0.

The `xdelta3` binary is not stored in this public source repository.

## XZ Utils

The HOTF 1.0.0 runtime package also contains the external `xz` command-line tool for the UnityFS/LZMA processing path used by the patcher.

Verified runtime tool version:

`XZ Utils 5.8.1`

Upstream project:

https://tukaani.org/xz/

No XZ Utils binary or source code is stored in this public source repository.

Licensing of XZ Utils depends on the specific version/component. Consult the upstream licensing files for the redistributed runtime version.

## Microsoft .NET

WolfPatcher targets .NET 8 and Windows Forms. The public source repository does not vendor the .NET runtime binaries.

Microsoft .NET licensing and notices apply to runtime files distributed as part of a self-contained Windows build.
