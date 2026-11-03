# VRoid Studio 2.16 compatibility

October 30, 2026. Local compatibility update for Windows x64. Core document workflows pass on VRoid 2.16.0 / Unity 6000.0.62f1. This is an experimental local candidate, not a published release or certification of every feature.

Official release notes: https://vroid.pixiv.help/hc/en-us/articles/63720616567449--v2-16-0-Save-hair-shapes-Performance-Monitor-Oct-29-2026

## Changes

- Runtime accepts exactly VRoid 2.14.0 or 2.16.0 with Unity 6000.0.62f1; unknown versions, previews and engine changes stay disabled.
- Selection-hook logging reports the actual runtime version. The internal VRoid214Bridge name remains for source continuity; compilation against fresh 2.16 interop required no editor API signature changes.
- Local installer recognizes exact build GUIDs for both versions and rejects partial, commented or conflicting profile declarations. The new 2.16 GUID is a34c8843981a4aada647acf24a55dece; the 2.14 GUID remains 49044bb6ab464dbc934e49a426e6a25b.
- Documentation and local package support profiles are updated. Existing unpublished snapshot/dashboard work is preserved and included in the rebuilt Companion.

## Validation

A clean application copy was made from Steam build 26267394 under .local/VRoidStudio-2.16, excluding legacy plugins/loaders. Pinned BepInEx 6.0.0-be.788 generated 182 interop DLLs. Unity reports 6000.0.62f1 (f99f05b3e950). No Steam application files or original user models were modified.

- PASS: fresh loader generation, chainloader startup, 2.16 selection hooks, Update/OnGUI callbacks and normal shutdown.
- PASS: plugin compilation against fresh 2.16 interop and the previous 2.14 interop, zero errors. Final compilation uses 2.16 references.
- PASS: exact runtime-profile regression tests, filesystem synchronization regressions, and CompanionCore project/snapshot tests.
- PASS: actual native document diagnostic on .local/test-models/Song-216-20261030.vroid: repeated external PNG imports, simultaneous links, invalid PNG rejection and valid recovery, native Undo/Redo pixel roundtrip, save/reopen pixel persistence, old-document link invalidation, unlinked-layer isolation, and deleted-layer destination protection. Result: PASS: six imports, simultaneous links, invalid PNG recovery, undo/redo, persistence, document invalidation, deleted-layer isolation.
- PASS: bridge publishes ApplicationVersion 2.16.0 and offline/empty-layer state after shutdown.
- PASS: interactive head-camera movement and original-view restore, screen-grid show/hide, wireframe enable and rendering restore, photo-booth entry, enable pose editing, pose reset and clean return to editor.
- PASS: native Performance Monitor enabled during workspace smoke tests, visible together with the plugin panel and grid. Restored its original Off preference afterward. This is coexistence testing, not a CPU/GPU/memory benchmark.
- PASS: 44 local installer engine checks, including the 2.16 profile, malformed profiles, official loader download/hash, installation/repair/uninstall/rollback, path safety and user-data preservation.

The native test deliberately logs exceptions for rejected invalid PNG/deleted-layer inputs; these are expected negative tests. The subsequent interactive workspace session logged no Error/Exception matches. BepInEx still emits the previously known Class::Init substitute warning.

## Remaining limits

Full VMD motion and MP4 recording workflows were not repeated on 2.16. Saved hair shapes and the Alpha Lock eraser correction are native VRoid features; their detailed behavior was not certified by this compatibility pass. Project relocation/error-path UI tests, clean-machine installation, signing and resource benchmarking remain separate checks. Existing 2.14 native acceptance remains historical; this run additionally checked compilation against its interop.

Local workspace logs: .local/compatibility-216-20261030/. Test app and disposable model artifacts remain local. No commit, push, PR update or release occurred.
