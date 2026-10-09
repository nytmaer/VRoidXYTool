# VRoidXYTool — IL2CPP port for VRoid Studio 2.14

A port of [xiaoye97/VRoidXYTool](https://github.com/xiaoye97/VRoidXYTool) to current VRoid Studio.

The original plugin stopped working when VRoid Studio 1.26.1 switched from Mono to IL2CPP. This fork rebuilds its core workflow, live texture linking, for **VRoid Studio 2.14.0** on **BepInEx 6 (IL2CPP)**. It also adds a second-monitor Companion and edit-mode camera and reference tools.

> **Credit.** VRoidXYTool was created by xiaoye97 and released under the MIT license. The original source, license and attribution are kept in this repository. For the original plugin and its Chinese documentation, see the [upstream repository](https://github.com/xiaoye97/VRoidXYTool).

This plugin is free. It must not be sold in any form.

## Status

**Experimental.** It works on the tested setup below, but no release binaries are published yet. You build it yourself, and you should test on copies of the app and your models.

| Feature | Status |
| --- | --- |
| Live texture linking | Ported. Link raster layers, edit the PNGs in Krita or another editor, and saves sync into VRoid automatically |
| Camera presets | Partial port (edit mode). Body and head views, perspective/orthographic, four saved slots |
| Reference guides | Partial port (edit mode). One PNG overlay and alignment grid inside the model viewport |
| Second-monitor Companion | New. A separate window listing linked layers with live thumbnails and sync status |
| Pose presets, MMD/VMD player, video recording, anti-aliasing, wireframe | Not ported. Legacy source is kept under `VRoidXYTool/` |

Legacy pose-preset code is preserved, but its startup and UI registration were disabled in the original source snapshot. Camera and reference tools do not yet reproduce every legacy control or preset format.

### What changed from the original

- **New runtime.** It is rebuilt as a BepInEx 6 IL2CPP plugin (`src/VRoidXYTool.IL2CPP`) against VRoid 2.14's generated interop APIs. The legacy Mono plugin can't load on any VRoid version after 1.26.0.
- **Safer texture sync.** Links are tied to the document and the specific layer, so edits go only to the layer you linked, even after you select another layer or open a different model. Saves are applied only after the file stops changing and passes a PNG check. A truncated or half-written save is ignored until a valid one arrives. Imports go through VRoid's own undo/redo. Opening a different model clears all links.
- **Multiple layers.** You can link several layers at once and switch materials while editing.
- **Companion app.** A Windows window for a second monitor. It shows each linked layer, a thumbnail, sync status and last import time, and opens the PNG in your editor in one click.
- **Workspace tools.** Camera presets and reference guides change only the editing view; they never modify your model.

## Requirements

- Windows x64
- VRoid Studio **2.14.0**. On any other version, the plugin disables itself.
- [BepInEx 6.0.0-be.788 (Unity IL2CPP, win-x64)](https://builds.bepinex.dev/projects/bepinex_be/788/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.788%2B5b766a3.zip). Don't install it over an old BepInEx 5 setup.
- .NET SDK with .NET 6 targeting packs to build the plugin
- .NET 10 Windows Desktop runtime, for the Companion only

## Install and use

**Work on a copy of the VRoid app and a copy of your `.vroid` file.** The plugin edits documents through VRoid's internal APIs, and only the tested setup is verified.

1. Install BepInEx 6 into the copied app (see [bootstrap setup](docs/IL2CPP_BOOTSTRAP.md)). Launch it once to generate `BepInEx/interop`, then close it.
2. Build the plugin:

   ```powershell
   dotnet build src/VRoidXYTool.IL2CPP -c Release -p:BepInExRoot="C:/your-test-app/BepInEx" -p:InteropRoot="C:/your-test-app/BepInEx/interop"
   ```

3. Copy `VRoidXYTool.IL2CPP.dll`, `VRoidXYTool.SyncCore.dll` and `VRoidXYTool.CompanionCore.dll` from `src/VRoidXYTool.IL2CPP/bin/Release/net6.0/` to `BepInEx/plugins/VRoidXYTool.IL2CPP/`.
4. Open your model, enter texture editing, select a raster layer and click **Link / export selected**.
5. Click **Copy linked PNG path**, open the file in your editor, and save as PNG. The change appears in VRoid automatically.
6. Save the `.vroid` normally. Saving the PNG alone doesn't save your model.

**Tab** shows or hides the panel. Settings are in `BepInEx/config/io.github.nytmaer.vroidxytool.il2cpp.cfg`.

Full details:
- [Installation and usage](docs/IL2CPP_INSTALLATION.md)
- [Second-monitor Companion](docs/COMPANION.md)
- [Camera presets and reference guides](docs/WORKSPACE_TOOLS.md)

## Known limits

- Tested only on VRoid Studio 2.14.0 with Krita, on one Windows two-monitor setup. Photoshop saves and other monitor/DPI arrangements are not yet verified.
- PNGs are limited to 4096×4096 and 64 MiB.
- Camera and reference tools work in edit mode only, not the photo booth.
- No installer or prebuilt download yet.

Test evidence is recorded in [compatibility results](docs/COMPATIBILITY_RESULTS.md), and the migration design in the [architecture audit](docs/ARCHITECTURE_AUDIT.md).

## Legacy plugin (VRoid Studio 1.26.0 and earlier)

The original Mono plugin's source is kept in `VRoidXYTool/` for reference. To use the original plugin with its full feature set, install VRoid Studio 1.26.0 and follow the [upstream instructions](https://github.com/xiaoye97/VRoidXYTool).

## Feedback

Report bugs and suggestions in [GitHub Issues](https://github.com/nytmaer/VRoidXYTool/issues). Please don't post BepInEx or player logs publicly without removing account information first.

## License

MIT, copyright 2021 xiaoye97. See [LICENSE](LICENSE) for the original attribution. The IL2CPP port and Companion are released under the same license.
