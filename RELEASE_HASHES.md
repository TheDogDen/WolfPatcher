# WolfPatcher HOTF PT-BR 1.0.0 — verified hashes

The values below were verified from the final public runtime package.

## Runtime archive

`WolfPatcher_HOTF_PTBR_1.0.0.zip`

- Size: `104,198,215` bytes
- SHA-256: `619b2d0b6ebe817611736d2e948035e857ed13f4caf13773b1f296f49f5b0ab9`

## First-party executable / assembly hashes

- `WolfPatcher.Gui.exe`
  - Size: `151,552` bytes
  - SHA-256: `1c54b9b519d42b6b5d8cd3e60ffd9adf5a5a7cff8ebe3e0e8929d6480507aac0`
- `WolfPatcher.Gui.dll`
  - Size: `38,400` bytes
  - SHA-256: `e6709581378db9504c2377f1d6838f946428e278e156aac96628b19dcc4ddba3`
- `WolfPatcher.Core.dll`
  - Size: `122,880` bytes
  - SHA-256: `0d86983210cb34236f8432a8dccfb16abfcfdccdbf5cd4e15c48d5b384f7fc51`
- `worker/WolfPatcher.Worker.exe`
  - Size: `151,552` bytes
  - SHA-256: `c184e4ae3536c99f43b79cef97809e31a44903c302ea8e962b728b17f9293c12`
- `worker/WolfPatcher.Worker.dll`
  - Size: `15,872` bytes
  - SHA-256: `9df440f398c53745acba74bbd5a86e3b1a9401703460ecc4f4d3ceefceb811b1`
- `worker/WolfPatcher.Core.dll`
  - Size: `122,880` bytes
  - SHA-256: `0d86983210cb34236f8432a8dccfb16abfcfdccdbf5cd4e15c48d5b384f7fc51`

These first-party hashes were independently reproduced byte-for-byte from the archived source during source publication verification. The GUI matches the `1.0.0` source/default version state; Core/Worker match when rebuilt with `-p:Version=1.0.0-rc2`, reflecting the actual component composition of the public HOTF 1.0.0 package.

## Bundled third-party runtime tools

- `tools/xdelta3/xdelta3.exe`
  - Size: `336,896` bytes
  - SHA-256: `53d90226615f217d3380c39892833311b4e24acd863e1ca01f14b5e772e2e6d0`
- `tools/xz/xz.exe`
  - Size: `266,766` bytes
  - SHA-256: `000c3e35f1192bd905c4db5b2517259396058ab4638e260db26e943cc245a626`
- `tools/xz/liblzma.dll`
  - Size: `183,822` bytes
  - SHA-256: `a6f022c8e4cd78e026c8b0da9f793dca1d3360840635f85ea631a31aed1081e7`

## Canonical HOTF localization targets

The installer validates these final localized files after installation:

| File | SHA-256 |
|---|---|
| `resources.assets` | `4e7f6edb1b1989082a23430aa3167031dc49e51b8c89c8eca3c5d29bd08c646f` |
| `level1` | `cd154bf1abf8915da2b3dbd60f7f5c96851772e266206296b3caa5314eef8c62` |
| `level2` | `f7e2d4f79d185afea565eb3ae94eb931e332a1c4c891d5157054ea4f3188af7c` |
| `sharedassets0.assets` | `dfe6e9dc2d6110c6efc9c95291560ab3c71c2177d9709f7b32067e9c2a100a81` |
| `sharedassets2.assets` | `fb0e2b861ffbf33cef385019708fde3178405b908fb6dc5438d4fc792757e55e` |
| `Managed/Assembly-CSharp.dll` | `5556d2bd800bc947938ad5330452c762806d856c0557d0866b6464e1c12f115f` |
| `StreamingAssets/Bundles/scenes_wod01_0-prologue.hd` | `a07f9e0e0790e50aaba96cdecc0df410775a3a658909afef0f12983741e37edc` |
| `StreamingAssets/Bundles/scenes_wod01_1-day1.hd` | `c07b8eb0600044969d6be60b8255b6851fcea5776884f92293ca73b9ce7c8c9f` |
| `StreamingAssets/Bundles/scenes_wod01_2-day2.hd` | `3732ae565cfb9a65764fd92743e0acf243b0571f8bf91cc34975afdf2d8dd64a` |
| `StreamingAssets/Bundles/scenes_wod01_3-night2.hd` | `5df86e8a31df934fbd77ebcad80bb322bb0f0f393340321cccaf9862fe787b6e` |
| `StreamingAssets/Bundles/scenes_wod01_4-day3.hd` | `8b7e073caaf7449f5b34d83ad87bd76c02e58bf953af11557e4ae4cbf05132bf` |
| `StreamingAssets/Bundles/scenes_wod01_5-day4.hd` | `2757acb599bbbca43a346b40220be3d9bf137ddd0a4a29637e6d3eba9c2255a6` |
| `StreamingAssets/Bundles/scenes_wod01_6-day5.hd` | `5f1542bb01382a243aef016cbef3781f8b82d6f2c1426f3357d393deaa8a0fe0` |

The source repository does not contain those game files.
