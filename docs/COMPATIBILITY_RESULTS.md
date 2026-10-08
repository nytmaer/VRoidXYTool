# Sprint 001 compatibility results

Test date: October 7–8, 2026 (America/Indianapolis). Sprint is **incomplete**: external edits have not yet been applied to a VRoid document.

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
| Layer capture / export inside editor | Not implemented or tested |
| External PNG applies without manual import | Not implemented or tested |
| Repeated saves inside editor | Untested |
| Layer/material/document switching | Untested |
| Save/reopen persistence | Untested |
| Runtime plugin Unload | Implemented, not exercised |

Commands:

```powershell
dotnet build src/VRoidXYTool.IL2CPP -c Release --no-restore
dotnet run --project tests/VRoidXYTool.SyncCore.Tests -c Release -p:NuGetAudit=false
git diff --check
```

The file core harness passed baseline/export deduplication, quiet period, same-size/same-timestamp edits, partial-write settling, repeated saves, file locks/recovery, atomic replacement, rejected apply/retry, missing/empty/oversized files, reexport suppression and disposal. The first sandbox run could not perform overwrite-by-rename in the temporary directory; the authorized rerun outside the sandbox passed. NuGetAudit=false avoids an unavailable advisory lookup for this dependency-free harness; it is not a vulnerability scan result.

## Shortest path to a functional prototype

1. Add a VRoid 2.14 bridge project referencing the generated assemblies. Verify current-file context access and a usable layer discovery hook on a disposable model; constructor presence alone is insufficient.
2. Capture full document, texture and layer identity at export. Export via GetRasterLayerContentQuery and encoding utilities. Use identity-based filenames and retain display labels separately.
3. Connect SettledFileLink polling to the Unity component. Reject callbacks after document changes or invalid target layers. Decode PNG with Unity ImageConversion, convert pixel arrays as required, then execute LoadImageToEditableImageRasterLayerCommand through the correct current document context.
4. Test at least five saves, locked/atomic editor saves, layer/material switching, document replacement, undo/history behavior and normal application quit.
5. Save and reopen the disposable `.vroid`; confirm the imported content survived. Only then mark live synchronization and Sprint 001 complete.

No changes were pushed, and main was not modified. Development changes are committed locally on the migration branch.
