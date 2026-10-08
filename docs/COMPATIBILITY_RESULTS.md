# Sprint 001 compatibility results

Test dates: October 7–8, 2026 (America/Indianapolis). **Core synchronization now passes document integration tests**: five external PNG saves update a raster layer and persist after save/reopen. Interactive selected-layer controls and material/layer switching still require acceptance checks. The earlier bootstrap/prototype findings below are historical; see the Song diagnostic section for current results.

## Environment and execution

Source checkout: fork `nytmaer/VRoidXYTool`, `dev/il2cpp-migration`, baseline `0e58140`. Installed VRoid Studio: `C:/Program Files (x86)/Steam/steamapps/common/VRoid Studio`. Test copy: `.local/VRoidStudio`, excluded from Git. Original application binaries and loader were not modified.

Player.log reports VRoid Studio **2.14.0**; UnityPlayer and logs report **6000.0.62f1**. Metadata header is 31; Cpp2IL identifies the actual layout as **31.1**. BepInEx **6.0.0-be.788+5b766a3**, bundled CLR **6.0.7**, Windows x64. Generated interop folder contains 185 files.

First startup generated interop successfully but took substantial time. Additional launches were mistakenly started before generation completed. These test processes were subsequently stopped; they do not count as normal-shutdown tests. Hidden interactive windows did not execute the quit diagnostic within the observed interval. A final isolated test used `-batchmode -nographics -logFile bootstrap-batch-player.log` and `Diagnostics.QuitAfterSeconds=10`; it initialized the plugin, logged shutdown, and exited with code **0**. Batch-mode rendering/editor behavior is not an interactive acceptance test. Test config was reset to 0 afterward.

Test copies share per-user VRoid settings/login state. VRoid startup wrote normal preference/account metadata and produced login/analytics messages; no model was opened or edited. Raw player logs contain account information and are excluded from Git.

Selected loader evidence from the final test:

```text
BepInEx 6.0.0-be.788 - VRoidStudio
Running under Unity 6000.0.62f1
Runtime information: .NET 6.0.7
Chainloader initialized
Registered mono type VRoidXYTool.IL2CPP.BootstrapLifecycle in il2cpp domain
Bootstrap initialized; CLR 6.0.7; process 316352.
Texture integration is not enabled. This bootstrap does not modify projects.
Chainloader startup complete
Bootstrap shutdown completed.
Exit code: 0
```

Loader warning: `Class::Init signatures have been exhausted, using a substitute!`. Initial interop generation reported 1442 unrestored methods and 1665 failed method-body unstrips. Startup succeeded despite these messages; they remain relevant when evaluating specific editor calls. There is no evidence yet that all required command bodies and generics work at runtime.

## Current API inspection

Inspected generated wrappers with Mono.Cecil without executing game methods:

* `VRoid.Studio.CurrentFileModel.engine` and `.path` are present.
* `VRoidStudio.GUI.AvatarEditor.AvatarEditor._viewModel` is present.
* `RasterLayerViewModel` retains its original five-argument constructor (BindableResources, Engine.Model, TextureEditor.ViewModel, TextureViewModel, EditableImageRasterLayerPath).
* RasterLayerViewModel exposes `_engine`, `_parent`, `Path`, `ReferringTexturePaths`, `TranslatedDisplayName`, and `IsValid`.
* `GetRasterLayerContentQuery(EditableImageRasterLayerPath)` is present in VRoidCore.dll.
* `LoadImageToEditableImageRasterLayerCommand(EditableImageRasterLayerPath, BitmapSize, Il2CppStructArray<UnityEngine.Color>)` is present in VRoidCore.dll. The pixel parameter is an IL2CPP array rather than managed Color[].

These are confirmed wrapper signatures, not verified invocations or usable Harmony detours.

## Validation matrix

| Check | Result |
| --- | --- |
| Release bootstrap compile | Pass, zero warnings/errors |
| Bootstrap initializes in actual VRoid executable | Pass |
| Registered Unity lifecycle component | Pass |
| Normal shutdown hook and exit | Pass in batch mode; interactive quit untested |
| File core tests | Pass |
| Layer export from loaded document | Pass through document query; selected-layer UI capture untested |
| External PNG applies without manual import | Pass, five saves by a separate PowerShell process |
| Repeated saves inside editor | Pass, exact corner marker verified after each save |
| Layer/material/document switching | Untested |
| Save/reopen persistence | Pass, complete raster-byte hash unchanged after reopen |
| Runtime plugin Unload | Implemented, not exercised |

Commands:

```powershell
dotnet build src/VRoidXYTool.IL2CPP -c Release --no-restore
dotnet run --project tests/VRoidXYTool.SyncCore.Tests -c Release -p:NuGetAudit=false
git diff --check
```

The file core harness passed baseline/export deduplication, quiet period, same-size/same-timestamp edits, partial-write settling, repeated saves, file locks/recovery, atomic replacement, rejected apply/retry, missing/empty/oversized files, reexport suppression and disposal. The first sandbox run could not perform overwrite-by-rename in the temporary directory; the authorized rerun outside the sandbox passed. NuGetAudit=false avoids an unavailable advisory lookup for this dependency-free harness; it is not a vulnerability scan result.

## Shortest path to a functional prototype

1. The version-specific bridge now exists in `VRoid214Bridge.cs`. Verify selected-layer capture and context access on a disposable model.
2. Exercise its export path: document-session identity, editable-image/NodeId paths, GetRasterLayerContentQuery, PNG encoding and identity-based filenames are implemented.
3. Exercise connected SettledFileLink polling and document commands. Document switches dispose links; PNG import uses generated pixel arrays. These paths compile but have not run with a loaded model.
4. Test at least five saves, locked/atomic editor saves, layer/material switching, document replacement, undo/history behavior and normal application quit.
5. Save and reopen the disposable `.vroid`; confirm the imported content survived. Only then mark live synchronization and Sprint 001 complete.

No changes were pushed, and main was not modified. Development changes are committed locally on the migration branch.

## Version 0.2.0 follow-up

Added the version-gated bridge, selected-layer controls, document-session link invalidation, stable PNG import through editing commands and a uGUI input blocker. Version gate checks `Application.version == 2.14.0`. The postfix targets `TextureEditor.ViewModel.OnSelectedLayerUpdated`, avoiding constructor detours. Linking explicitly enables background updates and restores the original setting when links are cleared.

Final tested build (PID 320808) loaded `VRoidXYTool Live Texture Prototype 0.2.0`, logged `VRoid 2.14 texture selection hook installed`, completed chainloader startup and logged normal shutdown with exit code 0. This verifies hook installation and blocker construction in batch mode, not invocation of the hook with a selected layer, interactive input blocking or PNG import. The existing Class::Init fallback warning remains.

The expanded file-core harness passed a real 1x1 PNG fixture, rejection of every truncated prefix, excessive dimensions, trailing bytes and non-PNG data, plus all previous file synchronization regressions. Final release build has zero warnings/errors. PNG semantic decoding still belongs to Unity's decoder. See `LIVE_TEXTURE_PROTOTYPE.md` for the interactive acceptance checklist.

## Song document diagnostic — October 8, 2026

The user supplied a Song `.vroid` source. Copied it to `.local/test-models/Song-test.vroid` (5,942,443 bytes at baseline). Source SHA256 before and after testing: `E932FB79966C243F3373CA6E0615151DA4E1817FE920D674369A60CC673C2CC1`. The original was never opened or saved by VRoid during this test. Only the local copy was changed and saved; no model or PNG files are committed.

The diagnostic is opt-in through `Diagnostics.ModelPath` and rejects paths outside `.local/test-models`. It calls MainActionHandler.Open on the copy, enumerates editable-image/raster-layer queries, then invokes the same bridge ExportPath and SettledFileLink/Import methods as the interactive controls. It requires a second layer for its isolation assertions. `scripts/Test-DocumentBridge.ps1 -Visible` writes candidate PNGs from a separate process into the exported file; no manual reimport action is used. Fixtures modify only a corner marker pixel. Exact marker colors are checked after each import, not merely a changed hash.

Verified on VRoid Studio 2.14.0 / Unity 6000.0.62f1:

* Loaded the disposable Song project and exported a real raster layer through the document query.
* Five successive external PNG saves imported automatically. Each imported marker matched the fixture; another raster layer's complete byte hash stayed unchanged.
* SaveSync saved only the disposable model. Open reloaded it, and the imported raster-byte hash matched the saved content.
* A sixth external save to the previous PNG did not alter the reopened document. Document replacement had cleared the old link.
* Unity OnApplicationQuit removed the hook and links, logged shutdown and exited with code 0.
* Release build and file-core regression harness passed. The original source hash remained unchanged.

Result text:

```text
PASS: export, five externally saved PNG imports with marker verification,
save/reopen pixel persistence, old-link invalidation, unlinked-layer isolation.
```

Runtime issues found and fixed: IL2CPP callback wrappers did not reliably expose the initially assigned managed instance callback state; lifecycle owner/diagnostic state is now shared explicitly through static fields and callback logging confirms Update execution. Editor discovery includes inactive objects, which are needed before entering the editor screen. The native core-library reference is available through global and il2cpp aliases; the local CLR NullableAttribute shim prevents stripped-attribute compilation errors.

Earlier batch tests verified startup and OnApplicationQuit only. Player logs reveal that VRoid's DeepLinkReceiver automatically quits batch mode, so those runs did **not** validate the QuitAfterSeconds timer or a loaded document. Hidden-window document launches did not progress reliably; visible tests did. Final document tests use visible normal-mode windows and finish through the diagnostic's normal Application.Quit. The Class::Init fallback warning remains, but the tested generic queries and import command execute successfully.

Remaining limits: the diagnostic bypasses the panel's selected-layer capture; Tab controls, pointer blocking, active UI layer/material switching, undo behavior and Photoshop/Krita-specific save behavior have not been exercised. File locks/rename/truncation are covered by the core harness rather than application-level editor saves. Existing links are intentionally not restored after reopening; imported project content persists. Full user-facing acceptance remains pending these checks.
