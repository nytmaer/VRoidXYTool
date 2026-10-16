# Feature parity audit

Reviewed October 9, 2026 against preserved legacy source and the current IL2CPP implementation. This is a source audit, not a fresh native acceptance run. Existing test evidence remains in [compatibility results](COMPATIBILITY_RESULTS.md).

## GitHub state checked

- `main` at `7416cf15b809df0b6bd1bf3df9501e434108da0a` includes the merged migration [PR #1](https://github.com/nytmaer/VRoidXYTool/pull/1).
- Claude's [PR #2: Rewrite README for the IL2CPP port](https://github.com/nytmaer/VRoidXYTool/pull/2) is open, based on `main`, with commit `a956236e494d28cc54ec979c9e7c696f1290d6ef` on `docs/readme-il2cpp`. It changes only README.md and README_English.md. It makes the current fork the primary documentation and preserves upstream credit and links.
- The rewrite accurately describes the implemented workflows and experimental limits. Its camera/reference status rows say “Ported (edit mode)”; “Partial port (edit mode)” would distinguish the smaller slice from full legacy parity. Pose presets also merit the disabled-source distinction below. This audit leaves that branch and its README text unchanged.

## Comparison

| Feature | Legacy source behavior | Current IL2CPP behavior | Remaining gap |
| --- | --- | --- | --- |
| Live texture sync | Raster export/import and polling; configurable directory and interval | Multiple explicit links, stable PNG imports, native history, structural identity and session guards; configurable directory | Core workflow restored. Legacy manual Import now, UV-guide export, configurable polling interval, base-directory/per-model layout and open-folder action are absent |
| Texture naming | Human-readable layer filenames; duplicate names could be resolved by renaming the document layer | Session directories and hashed structural identities | Intentional replacement: collision avoidance needs no document rename. Human-readable export aliases would be a convenience, not a missing correctness fix |
| Camera direction/projection | Body/head front, back, left and right; perspective/orthographic | Same basic directions and projection switch in edit mode; session restoration added | Photo-booth coverage is outside current implementation; legacy use of the common avatar camera is not evidence of tested photo-booth parity |
| Custom camera presets | Ten perspective slots plus ten orthographic slots; save/recall/clear; position and Euler rotation persisted | Four shared slots, projection/position/target/size persisted; save overwrites a slot | More slots, clear action, separate projection banks if desired, roll/rotation fidelity and migration of legacy CameraPosPreset JSON |
| Orthographic controls | Size slider and quick values 0.15, 0.2, 0.3, 0.8 | Automatic body/head sizes and captured size recalled from slots | Explicit size adjustment and quick-value controls |
| Reference images | Multiple world-space image planes; per-image visibility/delete, XYZ position/rotation, scale/alpha using numeric inputs or sliders; PNG/JPG file selection | One PNG screen overlay; visibility, scale/alpha and 2D movement; clipboard path input | Multiple references, world-space placement/rotation, numeric controls, JPG support and file-picker convenience |
| Reference presets | Save/load JSON list of image paths and transforms | No guide persistence/import/export | Bounded schema, save/recall/import/export, missing-image recovery and legacy data migration |
| Ruler/grid | World-space box prefab, create/show/hide | Eight-division screen alignment grid | World-space ruler/grid geometry and measurement behavior; screen grid is a different reference aid |
| Anti-aliasing | Toggle plus 2x/4x/8x settings; changes camera MSAA/HDR and global quality | No plugin anti-aliasing control | Discover the current renderer's actual AA path; verify visual effect and restore settings on disable/unload rather than copying the old toggle blindly |
| Wireframe | Pre/post-render callbacks for NormalLayerCamera; GL.wireframe and temporary clear flags | Unported | Current render-pipeline hook or model-only overlay; exclude UI and guarantee state restoration |
| Pose presets | Reset/save/load/delete .posejson code and constructor patch are present | Unported | Crucially, startup construction and GUI registration are commented out in the preserved legacy snapshot. This is dormant source, not a verified enabled legacy feature |
| MMD/VMD | Photo-booth UnityVMDPlayer, VMD load, play/pause/stop, loop and progress display | Unported | IL2CPP component/callback port, current rig/bone mapping and animation cleanup. The legacy progress slider's return value is discarded; it does not establish working scrubbing |
| Video recording | Photo-booth MP4 capture using embedded NatSuite/NatCorder; configurable FPS/bitrate, G hotkey, even-resolution check and output-folder action | Unported | Compatible capture/encoder integration, frame delivery, finalization and interruption cleanup. Existing native library installation must be revisited separately; no audio-parity claim (recorder is initialized with zero audio sample rate/channels) |
| Common UI/settings | Localization/language choice, configurable Tab and backtick mini-window, draggable windows, config-file action, background setting | English native tabs and separate Companion; Tab fixed, startup visibility configurable, background updates maintained while bridge active and restored on unload | Localization, configurable shortcuts, compact window, reposition/resize and user-facing common settings |
| Companion | Absent | Linked rows, thumbnails/status/time, chosen/default editor, copy path, remembered preferences and stale-action guards | New functionality, not legacy parity; installer remains explicitly deferred |

## Proposed order

1. Finish the existing workspace slice: exercise clipboard reference loading and active-reference document switching; add camera clear/size controls and more slots; add multiple references and guide preset persistence. Acceptance should cover restart, malformed preset preservation, missing image recovery and no model-content changes.
2. Restore UV-guide export. It directly helps the already working external texture-editing workflow. Verify output dimensions/UV correspondence and that exporting never imports or alters a layer.
3. Add wireframe and anti-aliasing, after inspecting current render-camera/pipeline behavior. Verify only the intended viewport changes, camera recreation works, and disabling/unloading restores rendering state.
4. Extend camera/reference tools into the photo booth, then evaluate dormant pose-preset code against current pose APIs. Verify camera/pose restoration and save/reopen behavior independently.
5. Port experimental VMD playback and recording last. These require substantially more animation/rendering/dependency work than the reference and texture tools. Acceptance must include repeated start/stop, model/mode changes, unload and interrupted output finalization.
6. Restore common UI/localization conveniences as each feature settles. Installer work stays deferred until explicitly resumed.

This order is a recommendation based on continuity with the texture/reference workflow and the dependencies visible in source. It does not begin implementation of the missing features.

## Source references

- [Legacy initialization](../VRoidXYTool/XYTool.cs) and [UI registration/settings](../VRoidXYTool/XYTool.GUI.cs)
- [Texture tools](../VRoidXYTool/LinkTextureTool.cs)
- [Camera controls](../VRoidXYTool/CameraTool.cs) and [preset schema](../VRoidXYTool/DataModel/CameraData.cs)
- [Guide creation/presets](../VRoidXYTool/GuideTool.cs), [transforms](../VRoidXYTool/GuideObject.cs), [image filters](../VRoidXYTool/FileHelper.cs)
- [Pose source](../VRoidXYTool/PosePersetTool.cs), [VMD controls](../VRoidXYTool/MMDTool.cs), [recording](../VRoidXYTool/VideoTool.cs), [wireframe](../VRoidXYTool/WireframeTool.cs)
- [Current workspace](../src/VRoidXYTool.IL2CPP/WorkspaceTools.cs), [texture bridge](../src/VRoidXYTool.IL2CPP/VRoid214Bridge.cs), [camera schema](../src/VRoidXYTool.CompanionCore/WorkspacePresets.cs), [Companion](../src/VRoidXYTool.Companion/Program.cs)
