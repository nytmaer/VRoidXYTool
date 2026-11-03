# VRoidXYTool

Edit a VRoid texture in your image editor, save the PNG, and see the change in VRoid. The optional Companion shows linked layers on another monitor and provides texture snapshots, project notes, and image archives.

**Windows x64 · VRoid Studio 2.14.0 or 2.16.0 · Unity 6000.0.62f1**

## Start here

**There is no public installer release yet.** GitHub's **Code → Download ZIP** downloads source code; it does not install the tool. This project is experimental. The new simplified launcher is still awaiting complete first-run testing.

| What you have | Follow these directions |
| --- | --- |
| Only this GitHub repository | **Install from source**, below. |
| A separately provided `VRoidXYTool-Setup.exe` candidate | **Install using the Setup candidate**, below. Local packages are not attached to a public release. |
| A development `VRoidXYTool.exe` launcher | Read the [launcher guide](docs/LAUNCHER_GUIDE.md) and [development status](docs/LAUNCHER_STATUS.md). Full native setup/launch acceptance is pending. |

Use a copy of the VRoid application and a copy of your `.vroid` model for the first test. Application copies still share VRoid's normal per-user preferences and custom items.

## Install using the Setup candidate

Use this path only if you were given the local **0.3.1 Setup candidate**.

1. Save your work and close VRoid and the Companion.
2. Run **VRoidXYTool-Setup.exe**.
3. Select your existing **VRoidStudio.exe**. Use **Browse…** for custom drives or folders.
4. Leave **Install into a separate writable copy (recommended)** checked. Choose an empty installation folder and a separate writable artwork/data folder, or keep the defaults.
5. Click **Check setup**, then **Install / repair**. Internet is required when Setup downloads its verified BepInEx loader.
6. Click **Open installed VRoid**. Allow the first startup to generate integration files before launching another instance.
7. Follow **Activate texture linking** below. Use **Open Companion** if you want the second window.

The Setup candidate and its Companion include their .NET runtimes. No SDK or separate .NET installation is needed for this packaged route. VRoid, an image editor, and FFmpeg are not bundled.

For later sessions, run `VRoidStudio.exe` from your **chosen installation folder**. The original Steam shortcut may open the unmodified application instead. The Companion is at `VRoidXYTool/Companion/VRoidXYTool.Companion.exe` inside the installed copy.

The local candidate is unsigned. If Windows blocks it, retain the exact message for troubleshooting without disabling security settings. See the [full installation guide](docs/INSTALL_GUIDE.md) for upgrades, repair, troubleshooting, and uninstalling.

## Install from source

You need a supported VRoid installation, an image editor, and the **.NET 10 SDK**. Building the plugin also requires .NET 6 targeting/reference packs. Clone or download this repository, extract it if needed, and run the following commands from its root folder.

### 1. Prepare a clean application copy

Copy the VRoid application into a writable folder. Keep model backups separately.

Download the pinned [BepInEx 6.0.0-be.788 — Unity IL2CPP, Windows x64](https://builds.bepinex.dev/projects/bepinex_be/788/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.788%2B5b766a3.zip). Extract it into the copied application folder, beside `VRoidStudio.exe`.

Use a clean loader installation. Do not extract BepInEx 6 over BepInEx 5 or an unknown loader. See [bootstrap preparation](docs/IL2CPP_BOOTSTRAP.md) for the exact application files and pinned dependency details.

Start the copied `VRoidStudio.exe` once. First startup may need internet and several minutes while BepInEx generates its interop assemblies. Wait for startup to finish, then close VRoid.

### 2. Build and copy the plugin

Replace both example paths with the actual paths inside your copied application:

```powershell
dotnet build src/VRoidXYTool.IL2CPP -c Release -p:BepInExRoot="C:/YourVRoidCopy/BepInEx" -p:InteropRoot="C:/YourVRoidCopy/BepInEx/interop"
```

Create `BepInEx/plugins/VRoidXYTool.IL2CPP/` inside the copied application. Copy these three files from `src/VRoidXYTool.IL2CPP/bin/Release/net6.0/` into it:

- `VRoidXYTool.IL2CPP.dll`
- `VRoidXYTool.SyncCore.dll`
- `VRoidXYTool.CompanionCore.dll`

Launch the copied `VRoidStudio.exe` again. The plugin loads automatically through BepInEx. **Tab** shows or hides its workspace panel.

### 3. Build the optional Companion

```powershell
dotnet build src/VRoidXYTool.Companion -c Release
```

Run `VRoidXYTool.Companion.exe` from `src/VRoidXYTool.Companion/bin/Release/net10.0-windows/`. This framework-dependent build requires the **.NET 10 Windows Desktop runtime**.

Click **Choose bridge file** in the Companion. For a default manual installation, select:

```text
<your copied VRoid folder>/Companion/bridge-state.json
```

Launch VRoid with the plugin first so the file exists. If you customized `Companion.StatePath`, select that configured file instead. Setup-installed copies normally use `<chosen data folder>/Bridge/bridge-state.json`.

## Activate texture linking

**No license key or separate activation account is required.** Loading the plugin displays the workspace; linking a layer starts synchronization for that layer.

1. Open a **test copy** of your `.vroid` model in the copied VRoid application.
2. Choose the material, enter **Edit Texture**, and select a **raster layer**.
3. Show the workspace with **Tab**, open **Textures**, and click **Link / export selected**.
4. Click **Copy linked PNG path** or **Open export folder**. Open that exact PNG in Krita or your image editor.
5. Make a small test edit. Preserve dimensions and transparency, then save as **PNG**.
6. Confirm the edit appears in VRoid. If necessary, click **Import linked PNG now**. Native Undo/Redo should work for the imported edit.
7. **Save the `.vroid` model in VRoid.** Saving only the PNG does not save the model.

In the Companion, select the linked layer, use **Choose editor**, then **Open PNG in editor**. A second monitor is optional.

Opening or reopening a document clears its links. Select and link the layer again for each session. **Unlink all** stops syncing and keeps exported files. Reexporting can replace the linked PNG with current VRoid pixels, so preserve unsaved editor work first.

## If it does not work

| Problem | What to check |
| --- | --- |
| No workspace panel | Start the copied executable, press **Tab**, and inspect `<copied VRoid folder>/BepInEx/LogOutput.log`. Check that the three plugin DLLs are in the correct plugin folder. |
| Unsupported version | Use exactly **2.14.0 or 2.16.0** with Unity **6000.0.62f1**. Other application/engine combinations deliberately leave the editor bridge disabled. |
| Sync disabled or panel hidden | In `BepInEx/config/io.github.nytmaer.vroidxytool.il2cpp.cfg`, check `[TextureSync] Enabled = true` and `ShowControlsOnStartup = true`, then restart the copied application. |
| Cannot link a layer | Enter **Edit Texture** and select a raster layer. Hide the overlay with **Tab** when it covers native controls. |
| PNG saves do not appear | Save the exact linked PNG, let the write finish, and try **Import linked PNG now**. Relink after document changes. PNG limits: **4096×4096**, **64 MiB**. |
| Companion offline or empty | Confirm VRoid is running with the plugin, select the correct bridge file, and link a layer. |
| First startup is slow | Wait for interop generation and inspect the startup log before launching additional instances. |

For normal use, leave `[Diagnostics] ModelPath` blank and `QuitAfterSeconds = 0`. Review logs before sharing them; they can contain account information.

## Other tools and current limits

- [Texture snapshots and character projects](docs/CHARACTER_PROJECTS.md): compare/restore PNG versions, retain recovery copies, and keep independent images and notes.
- [Companion](docs/COMPANION.md): thumbnails, sync status, and external-editor access.
- [Camera presets and reference guides](docs/WORKSPACE_TOOLS.md).
- [MP4 recording](docs/RECORDING.md): requires your own FFmpeg executable with `libx264`.
- [VRoid 2.16 compatibility results](docs/VROID_216_COMPATIBILITY.md), [native acceptance](docs/WORKSPACE_NATIVE_ACCEPTANCE.md), and [remaining work](docs/LOCAL_COMPLETION_CHECKLIST.md).

The 2.16 native texture diagnostic and selected workspace smoke tests passed. Full 2.16 VMD/MP4 coverage, broader editor/DPI testing, and the new launcher's complete first-run workflow remain pending. No public stable release is claimed.

## Credits and license

This fork modernizes [xiaoye97/VRoidXYTool](https://github.com/xiaoye97/VRoidXYTool), whose legacy plugin stopped working after VRoid switched from Mono to IL2CPP. Original source and attribution remain in this repository. See [LICENSE](LICENSE) and the upstream project for its original documentation.

This plugin is free. It must not be sold in any form.
