# Second-monitor Companion: linked textures

The Companion is a separate Windows WPF window. It lists the bridge's linked raster layers, live PNG thumbnails, sync status, last import time, and PNG filenames. Hover a row for its full path. Select a row to open its PNG in Krita or a chosen editor, or copy its path. Linking and unlinking remain in the VRoid panel.

Build with .NET 10 SDK, then run from the repository root:

```powershell
dotnet build src/VRoidXYTool.Companion -c Release
dotnet run --project src/VRoidXYTool.Companion -c Release -- "C:/your-test-app/Companion/bridge-state.json"
```

Alternatively launch `src/VRoidXYTool.Companion/bin/Release/net10.0-windows/VRoidXYTool.Companion.exe` and use **Choose bridge file**. The framework-dependent executable requires the .NET 10 Windows Desktop runtime and its adjacent build output. No installer is included.

1. Launch the copied VRoid app with all three plugin DLLs from [installation](IL2CPP_INSTALLATION.md).
2. Connect the Companion to that app's `Companion/bridge-state.json`, or the configured `Companion.StatePath`.
3. Open the disposable model, select a raster layer in texture editing, and use **Link / export selected**. The row appears automatically within about one second.
4. Move the Companion window to a second monitor. Select a row and click **Open PNG in editor**. Krita is detected in its standard Program Files location; **Choose editor** selects another executable. Without a selected editor, Windows opens the PNG using its file association.
5. Save PNG edits in the editor to trigger the existing live import. Save the `.vroid` separately in VRoid.

Bridge and editor choices are saved to `%LOCALAPPDATA%/VRoidXYTool/Companion/settings.json` when selected. **Use default app** switches back to the Windows PNG association and remembers that choice. A command-line bridge path overrides the remembered bridge for that launch. Missing or corrupt settings recover to defaults; missing editor executables fall back to the Windows association.

Thumbnails are decoded to at most 112 pixels on their longer side and cached until file time or size changes. Decoding releases its shared file stream immediately, so previews do not lock PNGs against editor saves. Missing, incomplete or invalid PNGs omit the preview and retry on later refreshes; link actions retain the existing live-state validation.

The window reads atomic schema-1 JSON snapshots; no server or native object pointers are exposed. It rereads the snapshot immediately before each open/copy action and accepts only a current registered PNG inside the bridge export directory. A stopped bridge, a heartbeat older than six seconds, missing files, or an old document identity disables or rejects the action. Opening another model clears rows; old PNGs remain on disk.

Document editing controls, monitor placement automation, and packaging are still deferred. Actual multi-monitor placement and varied DPI remain unverified. It remains part of the draft migration. Validation and limitations are recorded in [compatibility results](COMPATIBILITY_RESULTS.md).
