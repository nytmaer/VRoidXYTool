# IL2CPP bootstrap

Version 0.2.0 adds an experimental VRoid 2.14.0 live-texture bridge. The document diagnostic verifies live file imports and save/reopen persistence. See `LIVE_TEXTURE_PROTOTYPE.md` for usage and `COMPATIBILITY_RESULTS.md` for remaining interactive acceptance checks.

## Target and dependency pin

* Windows x64; locally installed VRoid Studio 2.14.0, identified from Player.log.
* Unity 6000.0.62f1 (f99f05b3e950), confirmed by UnityPlayer.dll and Player.log.
* GameAssembly.dll and VRoidStudio_Data/il2cpp_data/Metadata/global-metadata.dat are present.
* BepInEx Unity IL2CPP Windows x64: `6.0.0-be.788+5b766a3` (September 1, 2026).
* [Official download](https://builds.bepinex.dev/projects/bepinex_be/788/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.788%2B5b766a3.zip).
* Downloaded ZIP SHA256: `F4CC496BD098A0DF4164B81E3737297707F13A47C2478DBA2F60EEFAB784817A`. This records the inspected artifact; no independently published checksum was found.
* Build: .NET SDK with .NET 6 targeting packs; plugin targets net6.0 to match the bundled runtime. The inspected host has SDKs 9.0.318 and 10.0.401. Runtime is provided by the BepInEx archive's `dotnet` directory.

## Prepare and build

Use an isolated copy of the VRoid executable, GameAssembly.dll, UnityPlayer.dll, baselib.dll, UnityCrashHandler64.exe, License.txt and VRoidStudio_Data for initial loader tests. Copy the application data; do not move the installed application. Keep project files backed up before testing document edits.

Extract the pinned archive into the test application's root. Do not overlay it on BepInEx 5: use a clean loader directory and the new winhttp.dll, doorstop_config.ini and dotnet directory together. Run the test application once without the plugin to generate `BepInEx/interop`. Initial startup may download Unity base libraries and generate interop assemblies, so allow network access and additional startup time. Wait for chainloader startup before launching another instance. Test copies share VRoid's normal per-user settings and login state; copying the executable does not isolate those settings.

```powershell
dotnet build src/VRoidXYTool.IL2CPP -c Release -p:BepInExRoot="C:/path/to/test/VRoidStudio/BepInEx" -p:InteropRoot="C:/path/to/test/VRoidStudio/BepInEx/interop"
```

The default dependency paths are `.local/bepinex-788/BepInEx` and `.local/VRoidStudio/BepInEx/interop`. Game/loader DLL references are not copied into plugin output. Generated Il2Cppmscorlib is aliased; nullable annotations are disabled in the plugin source and a CLR NullableAttribute shim supports delegate metadata because generated stripped attributes interfere with compilation. Install `VRoidXYTool.IL2CPP.dll` and `VRoidXYTool.SyncCore.dll` from `src/VRoidXYTool.IL2CPP/bin/Release/net6.0/` into `BepInEx/plugins/VRoidXYTool.IL2CPP/` inside the test application.

Launch VRoidStudio.exe normally and inspect `BepInEx/LogOutput.log`. Require both BepInEx chainloader completion and `Bootstrap initialized` from this plugin. The bootstrap registers a Unity lifecycle component via BasePlugin.AddComponent; OnApplicationQuit disposes the bridge and logs shutdown. Explicit plugin Unload destroys the component and is idempotent. Cleanup runs through Unity lifecycle callbacks rather than ProcessExit, so no native Unity calls are made from a CLR exit thread. The texture bridge installs a selection-event postfix and begins document edits only after an explicit layer link and changed external PNG.

For an unattended normal-shutdown test, set `[Diagnostics] QuitAfterSeconds = 10` in `BepInEx/config/io.github.nytmaer.vroidxytool.il2cpp.cfg`. This enables background updates and invokes Unity Application.Quit after ten seconds. Default is 0 (disabled). Require `Bootstrap shutdown completed` and a clean process exit; an abrupt process kill does not verify shutdown. Reset the setting to 0 before document tests.

If startup fails, retain the full log and generated-artifact state. Do not infer compatibility from a successful build. Keep the original installation's BepInEx 5 and legacy plugin disabled in the test environment. Generated interop DLLs, application binaries and personal project data are excluded from Git via `.local/`.

## Next bridge validation

Inspect the generated VRoid assemblies for RasterLayerViewModel, current document/model access, raster-layer path identity, GetRasterLayerContentQuery and LoadImageToEditableImageRasterLayerCommand. Confirm signatures before implementing hooks. Exercise a disposable model and require import, repeated saves, layer/material switching, document switching and save/reopen persistence before calling the prototype functional.

## File synchronization core

`src/VRoidXYTool.SyncCore` implements explicit file links keyed by document/texture/layer identity. Poll it on the Unity thread with monotonic elapsed time. It uses exclusive bounded reads, stable content hashes over a quiet period, retry after failed imports, and export acknowledgements. No FileSystemWatcher is required, so rename-based saves and missed watcher events are covered by polling. This reads/hashes the file per poll: tune intervals and profile before scaling to many large textures.

The bridge callback verifies document session and layer identity, resolves the stored layer through a document query, preflights PNG bounds/completeness, decodes it and executes the document edit command. The core deliberately does not claim that stable bytes imply a complete valid PNG. Links are disposed on document change/shutdown. Actual export, repeated imports and persistence have passed the opt-in Song document diagnostic.

```powershell
dotnet run --project tests/VRoidXYTool.SyncCore.Tests -c Release -p:NuGetAudit=false
```

The regression harness requires .NET 10 and uses synthetic byte fixtures to test filesystem behavior. It does not test image decoding or application persistence.
