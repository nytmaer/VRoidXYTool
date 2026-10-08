# Architecture audit — Sprint 001

Date: October 7, 2026. Source baseline: `0e58140`, branch `dev/il2cpp-migration`.
Status: source audit complete; migration runtime results are tracked separately in `COMPATIBILITY_RESULTS.md`.

## Solution and build

`VRoidXYTool.sln` contains the legacy plugin and `I18NTool`. The plugin is an old-style .NET Framework 4.7.2 library with explicit source/resource lists. `I18NTool` is a .NET 6 executable using NPOI 2.5.5 for localization tooling. Embedded resources include translations, IMGUI skins, a guide asset, and NatCorder/NatSuite recorder assemblies. MIT license and original xiaoye97 attribution remain unchanged.

There is no reproducible package-based plugin dependency lock. BepInEx, Harmony, Unity modules, Newtonsoft.Json, MToon, UnityTablet, VRoidSDK and the VRoid assemblies are direct references into a hard-coded Steam installation. BepInEx/Harmony versions cannot be established from the project alone. The discovered installation has the BepInEx 5.4.22.0 archive and old Mono loader files; this is local evidence, not a repository version pin. Debug output also targets that installation's plugins directory. Release output is local. Several `*-nstrip.dll` references imply access to internal members; the project does not document how those assemblies were prepared.

## Lifecycle and injection

`XYTool : BaseUnityPlugin` uses Mono Unity callbacks: Awake binds config and logs; Start finds `AvatarEditor`, installs Harmony patches and constructs the tools; Update handles hotkeys/background running and calls tool updates. IMGUI is implemented in `XYTool.GUI.cs` and `XYModLib/GUI`.

Active Harmony hooks:

| Source | Hook | Purpose |
| --- | --- | --- |
| XYTool.cs | StartScreen.ViewModel constructor(MainViewModel, BindableResources, GlobalBus), postfix | Clear texture links on return to start screen |
| LinkTextureTool.cs | RasterLayerViewModel constructor(BindableResources, Engine.Model, TextureEditor.ViewModel, TextureViewModel, EditableImageRasterLayerPath), postfix | Discover instantiated raster layers; replace entries with matching NodeId |
| PosePersetTool.cs | PosesViewModel.Enable, postfix | Pose capture; tool construction is commented out in XYTool.Start |

The linked-texture patch discovers all instantiated raster-layer view models, not a dedicated selected-layer API. It installs the patch before initializing its static list, which deserves attention when migrating. Harmony imports in other files do not themselves establish additional patches.

## Original texture workflow

1. `AvatarEditor._viewModel.CurrentFile.model` exposes the current document and engine context. Linking requires an existing saved `.vroid` file.
2. Raster layers retain a view-model object and `EditableImageRasterLayerPath`. Identity comparisons use `Path.NodeId`, without explicit document identity.
3. `GetRasterLayerContentQuery(layer.Path)` returns size/RGBA bytes; `ImageEncodingUtil.EncodeToPNG` exports them. PNG filenames use translated layer display names under `LinkTexture/<model name>` or a configurable common directory. Duplicate layer names block export; a rename command offers random names.
4. Update polls every 0.2 seconds by default (minimum 0.1). It checks file modification time, reads the file, decodes with `Texture2D.LoadImage`, obtains `Color[]`, and destroys the temporary texture.
5. `LoadImageToEditableImageRasterLayerCommand(layer.Path, bitmapSize, pixels)` executes through the document's `Context.ExecuteSyncCommand`. This is a document-editing operation, not a renderer-only texture replacement. Persistence and history semantics still need current-version testing.
6. `LastWriteTime` is set to wall-clock now after import. IOExceptions retry on subsequent polls. Invalid view models are removed; return to start screen clears links.

UV export uses `_parent.ReferringTexturePaths`, `actionHandler.IsGuideExportable`, and `CurrentFileVM.Engine.GetUVGuideTexturePNGBytes`. Layer renaming uses `ActionHandler.CreateModifyNameCommand`.

## Migration blockers and reliability risks

* IL2CPP stores application code in GameAssembly.dll and metadata rather than the original managed application assemblies. The legacy loader, BaseUnityPlugin lifecycle, Framework references and internal managed types cannot simply be reused.
* BepInEx 6 IL2CPP uses BasePlugin.Load and generated Il2CppInterop assemblies. Unity callbacks require registered component types when introduced. Managed collections, pixels, generic queries and command signatures must be checked against generated wrappers.
* Private view-model fields, constructor signatures and commands are version-specific. A successfully loading bootstrap does not validate any of these hooks. Constructors may not be usable detour targets; inspect generated methods before choosing a patch.
* Display-name filenames allow collisions across documents and unsafe filename characters. NodeId alone is insufficient to prevent assignment to a different open document. A bridge must retain document, texture and raster-layer identity and invalidate links when the document changes.
* Timestamp polling can miss preserved timestamps and import partly written files. No quiet period or content deduplication exists. Reexport can trigger an unnecessary import. Decode failures can flood the log. Repeated view-model construction resets import tracking.
* Shutdown does not explicitly unpatch or dispose resources in the legacy entry point. Recording dynamically loads Mono-era embedded libraries with AppDomain.Load and accesses Paths.ManagedPath; it is excluded from this sprint.

## Migration boundary

New code is under `src/`, leaving the legacy project available for reference. The bootstrap has its own plugin ID and initially performs logging only. Future bridge code owns current-version layer discovery, PNG decode/export, document validation and edit commands, all on Unity's main thread. File-save stabilization and deduplication belong in a runtime-independent core. Avoid raw material replacement because it cannot establish document persistence. Do not implement Companion UI or unrelated legacy features during this sprint.

Official references: [IL2CPP installation](https://docs.bepinex.dev/master/articles/user_guide/installation/unity_il2cpp.html), [plugin creation](https://docs.bepinex.dev/master/articles/dev_guide/plugin_tutorial/2_plugin_start.html), [BepInEx build artifacts](https://builds.bepinex.dev/projects/bepinex_be).
