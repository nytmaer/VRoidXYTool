# Local completion checklist

The user authorized publishing this work to the repository on October 9, 2026. Publish the feature branch for review; merging to main and releasing binaries still require final approval.

Incremental native results and remaining checks are recorded in [WORKSPACE_NATIVE_ACCEPTANCE.md](WORKSPACE_NATIVE_ACCEPTANCE.md).

- [ ] Camera: ten slots per projection, clear, size adjustment, rotation fidelity, legacy migration, photo booth
- [ ] Guides: multiple images, screen/world placement, ruler/grid, PNG/JPG, preset persistence/import/export, missing-file recovery
- [ ] Textures: UV export, manual import, folder action and configurable polling/layout
- [ ] Rendering: wireframe and anti-aliasing with restoration
- [ ] Photo booth: pose save/load/reset/delete and legacy migration
- [ ] VMD: load, play/pause/stop/loop, current rig, cleanup
- [x] Recording: capture, FPS/bitrate, hotkey stop, photo-booth exit and normal shutdown finalization; encoder failure preserves partial output (hard-crash recovery is not implemented)
- [ ] Common UI: configuration, shortcuts, compact/repositionable panel, localization
- [ ] Core tests, native acceptance, regression and final review package

Installer work remains deferred by the user's earlier instruction. Feature parity audit is in the local `codex/feature-parity-audit` branch, commit fa2dbe3; current implementation branch is `codex/complete-feature-parity`.
