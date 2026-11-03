# Managed launcher development status

November 2, 2026. This is a development checkpoint, not a release-ready installer.

## Implemented

- Source discovery and an explicit compatible-build allowlist.
- A remembered managed workspace and separate artwork data folder.
- Separate application generations for supported VRoid updates, retained previous-workspace selection, and preference/pose migration.
- Workspace health checks, repair, and rejection of unsupported source updates.
- Startup waits for a fresh matching bridge heartbeat before connecting the Companion.
- A simpler WPF entry point with one main setup/launch action, Advanced settings, cancellation and diagnostic access.
- A build script for a self-contained launcher; it excludes VRoid binaries, generated interop, personal models and logs from its payload.

The existing installer engine and its regression harness now live in `src/VRoidXYTool.SetupCore` and `tests/VRoidXYTool.Setup.Tests` rather than being available only under ignored local packaging files.

## Validation and limits

The launcher and core/test projects compile. CompanionCore, filesystem synchronization and exact runtime-profile regressions pass. All 21 launcher file integration checks pass: setup/restart, bridge configuration, separate application generations, preference/pose migration, source/artwork preservation, repair, reversible rollback, unsupported-source refusal, cancellation, heartbeat validation and managed-path containment. These tests use a real packaged payload and pinned loader with lightweight application fixtures; they never execute those fixture applications. CI compiles the launcher with a deliberately nonshipping fixture payload because VRoid binaries/interop are not distributed in the repository.

Still required before a release: finish publishing the new single-file candidate; exercise setup, first native launch, Companion connection, remembered launch, repair, cancellation, fallback and Start menu entry end to end. Legacy-install adoption/removal, clean-machine testing and signing also need review. Remote launcher self-updates are not configured. The prior 0.3.1 Setup artifacts and 2.16 native compatibility results apply to the earlier installer/runtime, not full acceptance of this launcher.

## Build

Prepare an isolated supported VRoid app and generate its BepInEx interop as described in `IL2CPP_BOOTSTRAP.md`. Then run:

```powershell
./scripts/Build-Launcher.ps1 -RuntimeRoot 'C:/path/to/isolated/VRoid'
```

The build also requires the pinned BepInEx core dependency under `.local/bepinex-788/BepInEx`, or an appropriate `BepInExRoot` MSBuild override when building the plugin directly. Outputs remain under `.local/Launcher` and are not committed.

No published release or automatic deployment is part of this checkpoint. Merge to main remains subject to the owner's approval.
