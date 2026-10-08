# Live texture prototype — VRoid Studio 2.14.0

Version 0.2.0 implements a bridge prototype. Hook installation is verified; document editing and persistence still require acceptance testing. Use a disposable copy of a model.

## Installation in the isolated test copy

Build as described in `IL2CPP_BOOTSTRAP.md`. Copy **both** `VRoidXYTool.IL2CPP.dll` and `VRoidXYTool.SyncCore.dll` from `src/VRoidXYTool.IL2CPP/bin/Release/net6.0/` to `.local/VRoidStudio/BepInEx/plugins/VRoidXYTool.IL2CPP/`. Do not copy game or loader references into the plugins directory.

In `.local/VRoidStudio/BepInEx/config/io.github.nytmaer.vroidxytool.il2cpp.cfg`, set:

```ini
[Diagnostics]
QuitAfterSeconds = 0

[TextureSync]
Enabled = true
```

The bridge requires Application.version `2.14.0`. Other versions log a warning and leave texture integration disabled. Bootstrap logging remains available. The default PNG directory is `<test application>/LinkTextureIL2CPP`; `TextureSync.Directory` can override it.

Launch `.local/VRoidStudio/VRoidStudio.exe` interactively. The installed Steam copy remains unchanged. The test copy shares per-user preferences/login state with the installed application.

## Editing workflow

1. Copy an existing `.vroid` into `.local/test-models/` and open that copy in the isolated application.
2. Enter texture editing and select a raster layer. Press **Tab** to show the plugin panel.
3. Click **Link / export selected**. The plugin exports only that raster layer. It retains the full editable-image/layer path and document session.
4. Click **Copy linked PNG path** and open that path in Photoshop, Krita or another PNG editor. Exact paths are also logged in BepInEx/LogOutput.log.
5. Save the PNG. The plugin polls every 250 ms, waits for 500 ms of stable content, checks PNG completeness and bounds, decodes it, and executes the document's LoadImageToEditableImageRasterLayerCommand for the original linked layer.
6. Save the `.vroid` normally to preserve the imported edits. External PNG saves alone do not save the project file.

More than one raster layer can be linked. Display names are labels; filenames hash structural layer identity. Layer switching does not change existing link destinations. Opening/closing/replacing the model clears all links, and reopening a model requires linking again. **Unlink all** stops imports without deleting PNG files. Reexporting an already linked layer replaces its PNG with current document pixels and resets its baseline; preserve external edits before doing so.

The panel includes a transparent uGUI raycast blocker to keep pointer input off underlying editor controls while the panel is shown. Interactive input isolation still needs verification. Tab toggles the panel; it does not pause linked imports. Linking enables VRoid's background updates so saving from an external editor can synchronize while VRoid is unfocused.

## Limits and failure behavior

PNG files are limited to 64 MiB compressed and 4096 pixels on each axis before decode. The guard checks chunk bounds and IEND but does not replace the Unity image decoder or validate every PNG semantic rule. Missing, locked and incomplete files remain pending. Failed command/decode operations retry; warnings are limited to once per five seconds. Deleted layers fail the document query rather than being redirected to a selected replacement.

The adapter is version-specific and uses generated private VRoid APIs. It hooks `TextureEditor.ViewModel.OnSelectedLayerUpdated`, not a constructor, to capture the active editor. Capturing the hook and opening a model are separate from successfully invoking generic queries and editing commands. There is no claim of working end-to-end synchronization until the checks below pass.

## Acceptance checklist

Record the application version and BepInEx log for every run. Avoid posting account-bearing player logs publicly.

- Link/export selected layer; verify the PNG pixels match that layer, rather than the composited material.
- Edit/save five times; verify each result updates without manual reimport and without repeated unchanged imports.
- Test a locked save and an editor that replaces PNGs by rename.
- Save a truncated/non-PNG file; verify no document change, then save a valid PNG and verify recovery.
- Link two layers, switch selection/materials, and edit each file; verify the original destinations.
- Delete a linked layer; verify no other layer receives its updates.
- Open another model; edit the old PNG; verify no changes to the new model.
- Save/reopen the disposable model; verify imported pixels persisted and relinking is explicit.
- Quit normally; require shutdown logging and no lingering test process.
