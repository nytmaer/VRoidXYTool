# Performance and memory review — October 10, 2026

Run `dotnet run --project tests/VRoidXYTool.Performance.Tests -c Release -p:NuGetAudit=false`.

The harness compares the previous read-all-bytes/hash approach with real unchanged-file polls after warmup. It fails if 40 polls allocate over 1 MiB or reach 10% of baseline. Timings are reported, not asserted.

| File size / 40 polls | Previous allocation | Current allocation | Previous elapsed | Current elapsed |
|---|---:|---:|---:|---:|
| 1 MiB | 41,957,480 B | 89,408 B | 26.8 ms | 24.8 ms |
| 8 MiB | 335,560,776 B | 89,408 B | 238.7 ms | 204.6 ms |

These are one local run, not VRoid process benchmarks. A returned pooled 64 KiB buffer hashes files; full PNG allocation occurs after changed content settles. Polling still reads each file at the configured interval, preserving same-size/same-timestamp changes. IO/CPU grow with texture size and link count. Locked-save, atomic-replacement, continued-write, rejected-import and identity regressions pass.

Native-path changes:

- Cache the avatar editor while valid; invalidate after destruction/inactivation and on disposal. Search at most once per frame when unavailable.
- Skip viewport discovery when no screen references/grid exist; draw guides only during GUI repaint.
- Resolve wireframe camera membership once per frame rather than per camera callback.
- Reuse fixed tab/sample arrays on GUI events.
- Skip recording GPU readback/frame allocation when the two-frame encoder queue is full; encoder faults remain observable.
- Discard display-only reference CPU pixels and enforce a combined 256 MiB RGBA reference budget before decoding.
- Cancel pose/motion picker callbacks when their photo booth is no longer active.
- Restore VMD facial weights and release managed animation references during disposal; restore animator state when initialization fails.

Recording still holds pending raw frames, the writer's previous/current frame, and capture data. High-resolution recording remains memory-intensive. MP4 integration verifies decoded scenes/duration, idempotent stop, failure and overwrite prevention. Unity destruction is deferred; the reference budget is accounting for textures, not an exact process-memory ceiling.

Remaining: native access is unavailable in the current computer-use inventory. Measure actual idle CPU, polling, references and recording; repeat document switching, guide replacement and VMD start/stop after deferred destruction. Check private-memory trends, cache invalidation, rendering restoration, timing and responsiveness. Core results do not establish those native properties.

CI runs the allocation harness alongside SyncCore/CompanionCore tests and the WPF build. Hosted CI cannot build the IL2CPP adapter without local generated VRoid interop. Require the GitHub check only after the updated workflow is published and passes.
