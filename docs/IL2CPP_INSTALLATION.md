# Experimental IL2CPP live texture installation

Supported test target: Windows x64, VRoid Studio 2.14.0 (Unity 6000.0.62f1), BepInEx 6.0.0-be.788+5b766a3. Other VRoid versions disable the adapter. This branch ports live texture linking only; original camera, guide, pose and other tools remain legacy code.

1. Make a separate copy of the installed VRoid application and a disposable copy of your `.vroid`. Follow `IL2CPP_BOOTSTRAP.md` to install the pinned BepInEx IL2CPP runtime in that application copy. Do not combine the old BepInEx 5 installation with the new loader. The copied app still uses per-user settings and custom-item storage.
2. Launch the copied app once to generate its `BepInEx/interop` assemblies, then close it. Build with a .NET SDK supporting net6.0:

   ```powershell
   dotnet build src/VRoidXYTool.IL2CPP -c Release -p:BepInExRoot="C:/your-test-app/BepInEx" -p:InteropRoot="C:/your-test-app/BepInEx/interop"
   ```

3. Copy `VRoidXYTool.IL2CPP.dll`, `VRoidXYTool.SyncCore.dll`, and `VRoidXYTool.CompanionCore.dll` from `src/VRoidXYTool.IL2CPP/bin/Release/net6.0/` to `BepInEx/plugins/VRoidXYTool.IL2CPP/` in the copied app. Do not redistribute VRoid binaries or generated interop assemblies.
4. Launch normally and open your disposable `.vroid`. Enter texture editing, select a raster layer, and click **Link / export selected**. The panel appears by default; **Tab** hides/shows it. Hide it to reach covered native controls.
5. Click **Copy linked PNG path**, open that file in Krita or another PNG editor, and save edits as PNG with transparency. Stable valid saves import automatically into the original linked layer, including while another material is selected. Multiple layers may be linked. Saving the PNG does not save the `.vroid`; save the model normally.
6. Opening/reopening a document clears links. Relink explicitly. **Unlink all** stops imports and leaves the PNG files intact. Reexport overwrites a linked PNG with current document pixels; preserve unsaved external edits first.

Configuration: `BepInEx/config/io.github.nytmaer.vroidxytool.il2cpp.cfg`. `TextureSync.Enabled` disables/enables the adapter; `Directory` controls PNG export storage; `ShowControlsOnStartup` controls initial panel visibility. Leave `Diagnostics.ModelPath` blank and `QuitAfterSeconds=0` for normal use.

The optional [second-monitor Companion](COMPANION.md) displays linked layers and opens their PNGs in an external editor. `Companion.StatePath` selects the local JSON snapshot (default `Companion/bridge-state.json` under the test application). The bridge keeps VRoid updating in the background while enabled so its heartbeat and texture imports continue when the Companion or editor has focus; unloading restores the original background setting. Installer work is deferred.

PNG bounds are 4096x4096 and 64 MiB compressed. Locked, missing or incomplete files wait/retry. Imports participate in native undo/redo; unchanged external content is acknowledged once, so undo is not immediately overwritten. A fresh external save can create a new import. Deleted destinations are resolved by stored identity and never redirected to the currently selected layer. Undoing a layer deletion can make its existing link valid again; unlink first if that is unwanted.

Krita saves, native two-layer linking and material switching, representative panel pointer blocking, six diagnostic imports, invalid-file recovery, native undo/redo, save/reopen persistence and normal shutdown are exercised. Photoshop-specific saves and broad screen-size/cross-version compatibility are not certified. See `COMPATIBILITY_RESULTS.md` for exact evidence and remaining limits. No release binaries are published with this draft.
