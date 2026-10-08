# Camera presets and reference guides

The experimental VRoid 2.14 adapter adds **Camera presets** and **Reference guides** tabs to the native workspace panel. **Tab** hides/shows the panel. Camera and reference tools affect the editing view; they do not write model content or enter undo history.

Camera presets provide front/back/left/right body and head views, perspective/orthographic switching, restoration of the session's original view, and four save/recall slots. Custom slots persist in `BepInEx/config/VRoidXYToolWorkspace.json`; loading checks schema, finite coordinates and projection size. This file is separate from the legacy camera JSON. Tools are available only with a model in edit mode; photo-booth cameras are outside this slice.

Reference guides provide one PNG overlay and an eight-division alignment grid clipped to the active model viewport. Copy an absolute PNG path, use **Paste PNG path**, then **Load reference PNG**. The label displays the filename. The initial path is `Companion/reference.png` under the application directory. Controls adjust image scale, opacity and position; image/grid visibility can be toggled independently. Hiding the panel leaves the guides visible. **Clear references**, changing documents or unloading removes the reference texture and grid.

Reference PNGs must be complete, at most 4096×4096 and 64 MiB. An unsuccessful load preserves the existing image. The overlay does not intercept viewport clicks and remains a screen reference while the camera or model moves. It is not the legacy world-space reference plane/ruler prefab, and guide-preset import/export is not implemented in this slice. Legacy source and assets remain preserved.

See [compatibility results](COMPATIBILITY_RESULTS.md) for native acceptance evidence. Keep using the disposable application/model copies described in [installation](IL2CPP_INSTALLATION.md).
