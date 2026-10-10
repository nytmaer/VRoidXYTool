# Workspace native acceptance — October 9, 2026

Work is published for review on `codex/complete-feature-parity` following the user's October 9, 2026 authorization. Tests use the isolated VRoid Studio 2.14.0 installation and a disposable model copy under `.local/test-models/`.

## Pose and VMD

Verified through the native application:

- Enable pose editing switches into manual posing and enables the pose controls.
- Save produces a `.vroidpose` file; refreshing after an application restart lists persisted poses.
- Applying a saved pose completes through VRoid's native pose API. The original reset smoke test used an unchanged pose and did not establish that custom-pose reset works.
- The VMD picker opens; selecting `.vroid` instead of `.vmd` is rejected before parsing.
- A generated, three-keyframe head-turn VMD animates the current model over 120 frames.
- Loop playback wraps; pause holds its frame; seeking to 50% applies frame 60 and holds the visibly turned head.
- Stop restores the pose captured before playback.
- The final cleanup build clears the loaded motion when leaving photo booth, restores the editing viewport and produces no cleanup exception.
- Pose editing controls are disabled while a motion is loaded.

The native extension-filter struct loses its array field through the generated generic bindings. The picker therefore opens without an extension filter and validates the selected extension before dispatching. Parsing and size checks still apply.

The exit test exposed destruction of native IK targets before plugin cleanup. Cleanup now checks whether those Unity objects still exist and clears managed motion references before restoration.

Remaining pose/VMD acceptance: legacy `.posejson` migration, recoverable deletion, native import/export dialogs, broader full-body/IK/morph motions, document switching, and replay after a non-looping end. The latest build passes with zero warnings/errors; CompanionCore regression tests pass.

## October 10 follow-up

A disposable legacy head-roll fixture visibly applied and saved as a native pose without modifying its source. Resetting that distinct pose exposed a bug: the native ResetPose call left the custom roll in place. The local fix now clears the loaded custom pose, selects preset zero, plays that preset and resets pose/hand controls. Reset and saved-pose reload still need live verification.

Legacy conversion now merges imported members into the current native definition, preserving omitted fields and rejecting unknown members before applying controls. Core tests pass; the changed merge path needs native regression.

Title dragging, folding/unfolding and resetting panel position passed their earlier native check. The clipped Compact label is now Fold/Open. Restart persistence and scale/hotkey checks remain pending.

Current computer-use inventory exposes no native apps. New reset, rotation, rendering and performance changes are build/core-tested, not newly accepted in VRoid. No remaining native checkbox is marked complete from these results.

The VMD review also corrected pre-first-keyframe morph interpolation to use floating-point division, restores original facial blend weights on stop, releases managed animation references, preserves the supplied starting frame, and restores animator state if initialization fails. All require native regression with body/IK/morph fixtures.

## Rendering

In the editing viewport, enabling wireframe visibly renders model triangles. Applying the 4× anti-aliasing request completes. Restore rendering disables wireframe and returns the textured model view. Requested sample count is not proof of the GPU's effective sample count.

Remaining rendering acceptance: all sample choices, photo-booth rendering, effective sample diagnostics, and restoration on document switch/shutdown.

## Recording

See [RECORDING.md](RECORDING.md) for the accepted MP4 integration and native tests.

## Approval gate

This report does not mark overall parity complete. See [LOCAL_COMPLETION_CHECKLIST.md](LOCAL_COMPLETION_CHECKLIST.md). The user approved merging to main on October 10, 2026 with the pending checks disclosed. Release binaries require separate approval.
