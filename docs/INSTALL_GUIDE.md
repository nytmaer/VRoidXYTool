# VRoidXYTool install guide

Local package **0.3.1**, October 30, 2026 · Windows x64 · VRoid Studio **2.14.0 or 2.16.0** with Unity **6000.0.62f1**.

Use **VRoidXYTool-Setup.exe** from the `output-2.16` package folder. The installer and Companion include their .NET runtimes; you do not need a developer SDK or a separate .NET installation. You do need your own supported VRoid Studio installation. Setup does not include VRoid itself.

## 1. Before installing

1. Save your work and close VRoid Studio and the Companion.
2. Back up your `.vroid` models. Make a separate model copy for your first test.
3. Connect to the internet. Setup downloads its checksum-verified BepInEx loader, and the first VRoid launch may need additional downloads.
4. Leave enough disk space for a second VRoid installation, its generated files, and your exported textures.

This is an unsigned local candidate. Package hashes are provided in `SHA256SUMS.txt`. If Windows blocks it, keep the exact message for troubleshooting; do not disable Windows security settings.

## 2. Choose your folders

Run **VRoidXYTool-Setup.exe**. Fill in the following fields:

| Setup field | Choose |
| --- | --- |
| **Your VRoidStudio.exe** | The executable in your existing VRoid installation. Use **Browse…** if automatic detection finds the wrong folder. |
| **Install into a separate writable copy (recommended)** | Leave checked. This keeps the original application intact. |
| **Installation folder** | An empty writable folder for the copied application. Default: `%LOCALAPPDATA%\VRoidXYTool\Studio`. |
| **Textures, bridge state and recordings** | A separate writable data folder. Default: your Windows Documents folder, then `VRoidXYTool\Data`. |

For Steam installations, **Steam → Library → VRoid Studio → Manage → Browse local files** can help you locate `VRoidStudio.exe`. Custom drives, spaces and Unicode folder names are supported. Documents may be redirected to OneDrive; use the actual path shown by Setup.

Keep the application and data folders separate. Give separate installations different data folders.

Click **Check setup** to verify the source application. Then review the destination paths before installing. Destination validation also happens during installation.

If Setup reports an unsupported build or incompatible loader, resolve that message first. Do not edit application version/build files or install BepInEx 6 over BepInEx 5.

## 3. Install and launch

1. Click **Install / repair**.
2. Wait for **Installation complete**. Setup copies application files, installs the verified loader and tool files, and creates configuration for your data folder. Personal models and old plugins are excluded from the application copy.
3. Click **Open installed VRoid**.
4. Allow the first launch to finish generating interop files. It can take several minutes. Do not repeatedly launch additional instances while it is working.
5. Confirm VRoid opens with the VRoidXYTool workspace panel. **Tab** shows or hides the panel.

The panel includes **Textures**, **Cameras**, **Guides**, **Photo booth**, **Rendering**, and **Recording**.

The copied application still uses VRoid's normal per-user preferences and custom-item storage. It is a separate application folder, not a separate Windows user profile.

## 4. Verify one texture edit

Use your test model copy for this check.

1. Open the model in the copied VRoid application.
2. Choose a material, enter **Edit Texture**, and select a raster layer.
3. In the tool's **Textures** tab, click **Link / export selected**.
4. Use **Copy linked PNG path** or **Open export folder** to locate the exported PNG.
5. Open that PNG in Krita or your preferred image editor. Make a small test mark, preserve transparency and canvas size, and save it as PNG.
6. Return to VRoid. The linked layer should update once the save finishes. You can also use **Import linked PNG now**.
7. Test VRoid's Undo and Redo, then save the `.vroid` model normally.
8. Close and reopen the test model. Confirm the edit remains.

**Saving the PNG does not save the model.** Save the `.vroid` in VRoid to preserve imported changes.

Document changes clear live links. After reopening a model, select and link its layer again. **Unlink all** stops synchronization and leaves exported PNGs on disk. Linking/exporting can overwrite the linked PNG with current VRoid pixels, so preserve any unsaved editor work first.

## 5. Connect the Companion

1. In Setup, click **Open Companion** after installation succeeds. Setup supplies the configured bridge path.
2. Select a linked layer. Its thumbnail and sync status should appear.
3. Click **Choose editor** and select an installed image-editor executable, or use **Use default app** for your PNG file association.
4. Click **Open PNG in editor**. Save another small edit and confirm it appears in VRoid.

You can place the Companion on a second monitor, or use it on the same monitor.

If you launch the Companion directly and it is disconnected, click **Choose bridge file** and select `<data folder>\Bridge\bridge-state.json`. Launch VRoid with the plugin active first; the bridge file is created and refreshed by the running plugin.

Optional features:

- **Texture snapshots:** save a named PNG version, compare it with the current PNG, or restore it. Restore backs up the current saved PNG first. It does not include unsaved image-editor changes, and you still need to save the model in VRoid. Your editor may need to reload the restored file.
- **Character project:** create a project associated with your model, keep notes and independent image archives, and reopen it later. Check the model open in VRoid before confirming its session for archiving. Project archives do not automatically relink or restore layers in a reopened model.

## 6. Launch it next time

Create shortcuts to these executables in your chosen installation folder:

| Application | Executable |
| --- | --- |
| Copied VRoid with the plugin | `<installation folder>\VRoidStudio.exe` |
| Companion | `<installation folder>\VRoidXYTool\Companion\VRoidXYTool.Companion.exe` |

Start the copied VRoid, open your model, and explicitly link the layers you want to edit. Then open the Companion if needed. The original Steam shortcut may launch the original application instead.

Setup's launch buttons become enabled after a successful installation or repair in that Setup session. Direct executable shortcuts are more convenient for daily use.

An image editor and FFmpeg are not bundled. Recording is optional and requires an existing FFmpeg executable selected in **Recording**.

## 7. Upgrade or repair

1. Save your model and external artwork. Close VRoid and the Companion.
2. Run the new Setup and choose the same installation folder and data folder as before.
3. For a Steam update, select the newly updated original `VRoidStudio.exe` as the source. Keep the separate-copy option checked to refresh the application copy.
4. Click **Check setup**, then **Install / repair**.
5. Launch the copied application and repeat the small texture test on a model copy.

Repair preserves existing configuration. Changing the data-folder field does not automatically redirect an older configuration. Existing settings are in `<installation folder>\BepInEx\config\io.github.nytmaer.vroidxytool.il2cpp.cfg`.

A separate application copy does not automatically follow Steam updates. Future unsupported VRoid/Unity versions are deliberately refused or leave the editor bridge disabled until compatibility is verified.

## 8. Troubleshooting

| Symptom | Check |
| --- | --- |
| Unsupported build | Use the supported 2.14.0 or 2.16.0 build. Keep the exact detected version and error text. |
| Incompatible loader | Keep the separate-copy option checked and choose an empty writable destination. Do not overlay an unknown loader. |
| Download fails | Check connectivity and retry. Setup verifies the pinned loader before installing application files. |
| First launch seems slow | Allow interop generation to complete. Inspect `<installation folder>\BepInEx\LogOutput.log` for progress/errors before launching again. |
| No workspace panel | Press **Tab**. Confirm you started the copied executable, then inspect `BepInEx\LogOutput.log` for loader or unsupported-version messages. |
| Companion is offline or empty | Confirm the copied VRoid is running, the bridge path points to this installation's data folder, and a raster layer has been linked. |
| Editor saves do not appear | Save the exact linked PNG, let the save finish, and try **Import linked PNG now**. Supported PNG limits are 4096×4096 and 64 MiB. Relink after document changes. |
| Restored PNG looks unchanged in the editor | The editor may still show its previous buffer. Preserve unsaved work separately, then reload the restored PNG. |
| Access denied or a protected destination | Choose a writable application-copy folder rather than installing into a protected Program Files directory. |

When reporting an issue, include the VRoid version, installation/data paths, the step that failed, and exact error text. Review logs before sharing them; they can contain account information.

## 9. Uninstall

1. Save your work and close VRoid and the Companion.
2. Open Setup and set **Installation folder** to the copied application you want to remove the tool from.
3. Click **Uninstall** and review the destination before confirming.

Uninstall removes unchanged Setup-owned files or restores applicable backups. Models, exported artwork, edited configuration, the VRoid application copy and generated files remain. A loader needed by another plugin is preserved. Keep `.VRoidXYTool.setup` backups until you no longer need recovery.

## Verification status

Native 2.16 texture imports, Undo/Redo, save/reopen and selected workspace smoke tests passed. The rebuilt 417-file package passed 44 installer engine checks and embedded-payload verification. A complete fresh-install GUI walkthrough, clean-machine testing, code signing, full 2.16 VMD/MP4 scenarios and resource benchmarking remain pending. See `TEST-RESULTS.md` and `VROID_216_COMPATIBILITY.md` beside the installer.
