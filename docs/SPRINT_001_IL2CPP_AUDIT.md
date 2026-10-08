# Sprint 001: IL2CPP Migration Audit

Status: **Source audit and IL2CPP bootstrap implemented. Bootstrap startup and batch-mode normal shutdown verified on VRoid Studio 2.14.0; live texture integration and persistence remain unverified.**

Implementation details: [architecture audit](ARCHITECTURE_AUDIT.md), [bootstrap setup](IL2CPP_BOOTSTRAP.md), and [compatibility results](COMPATIBILITY_RESULTS.md).

## Confirmed from upstream README
- VRoid Studio 1.26.1 changed its scripting backend from Mono to IL2CPP, invalidating the original plugin integration.
- Upstream recommends VRoid Studio 1.26.0 for the legacy plugin.
- Linked Texture exports the currently selected texture and synchronizes changes after an external editor saves the file.
- Original features include camera presets, reference tools, pose presets, antialiasing, experimental VMD playback, video recording, and wireframe mode.
- Legacy toggle: Tab. Linked texture directory is configurable.
- Prior VRoid Studio 1.18 issue required setting `HideManagerGameObject = true` in BepInEx.cfg; do not assume this fixes IL2CPP.

## Engineering plan
1. Inventory solution/project files and dependency versions; locate linked-texture code, Harmony patches, and Unity/VRoid internal API calls.
2. Capture exact VRoid Studio, Unity, and BepInEx versions for the target installation.
3. Prototype a **minimal** BepInEx IL2CPP plugin that only logs successful startup.
4. Resolve managed-to-IL2CPP interop and changed Unity/VRoid internal types. Keep version-specific hooks isolated.
5. Restore selected-texture identification and export.
6. Implement safe file change detection (debounce, wait for file write completion, preserve texture import settings, avoid watcher loops).
7. Reapply changed pixels to the correct in-editor texture; test persistence through save/reopen.
8. Test repeated edits, layers, material switching, file locking, and graceful teardown.

## Acceptance test
- On a documented current VRoid Studio release, link a texture, edit it in an external image editor, save, and observe the update inside VRoid Studio without manual reimport.
- Repeat at least five saves; change active layers; close/reopen the project and confirm the expected result.
- Preserve upstream MIT license and attribution. Keep `main` unchanged.

## Research questions
- Which original plugin classes directly reference Mono-only Unity objects?
- Which runtime hooks are no longer valid in IL2CPP?
- Can the selected texture be updated without destabilizing VRoid's undo/history and save systems?
- Can the plugin safely exchange files or commands with a separate second-monitor companion?

This document is an audit checklist, not a claim that the plugin has been ported.
