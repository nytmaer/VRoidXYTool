# MP4 recording

Recording is available in the workspace's **Recording** tab while VRoid's photo booth is open. It captures the photo-booth image without the workspace controls or VRoid's editing UI. Output is silent H.264 MP4, matching the original tool's video-only recording scope.

## Encoder

Set `Recording.FFmpegPath` in the BepInEx configuration to an existing FFmpeg executable, or copy its absolute path and click **Paste FFmpeg executable path**. FFmpeg must include the `libx264` encoder. The plugin does not download or install dependencies. [FFmpeg's download page](https://ffmpeg.org/download.html) links to Windows build providers. FFmpeg is a separate program with its own license; it is not bundled with this project.

## Controls

- Choose a photo-booth picture size with even width and height, between 2 and 4096 pixels per side.
- Set FPS and video bitrate in configuration, or copy `FPS,bitrateKbps` (for example `30,20000`) and use the corresponding paste button. FPS supports 1–240; bitrate supports 256–100000 kbps.
- Use **Start recording** / **Stop recording**, or the configured hotkey (default **G**). Tab can hide the workspace during recording.
- **Open recording folder** opens the configured output directory. Each clip gets a unique name; existing clips are never overwritten.

The requested FPS describes the output timeline. If rendering or encoding cannot keep up, the last frame repeats to preserve elapsed time. It does not guarantee that many distinct captures per second. The queue is bounded to two pending frames and encoding runs off Unity's thread.

## Finalization and cleanup

Leaving photo booth, changing documents, or closing VRoid normally stops and finalizes the active clip. Stop returns control while encoding finishes. The completed `.mp4` name appears only after FFmpeg exits successfully. If capture resolution changes, capture stops and already accepted frames are finalized.

Failed output remains under a `.partial.mp4` name if an encoder created data. It is not presented as a completed recording. A hard process crash or power loss can leave a partial file that cannot be recovered; automatic crash recovery is not implemented.

## Verification

Run the integration test with a local FFmpeg build and its adjacent `ffprobe.exe`:

```powershell
dotnet run --project tests/VRoidXYTool.Recording.Tests -c Release -- C:\Tools\ffmpeg\bin\ffmpeg.exe
```

The test decodes a two-scene MP4, checks dimensions, duration, both scenes, repeated stop, late-frame rejection, overwrite protection and encoder failure. It writes disposable fixtures in the temporary directory.

Local native acceptance on October 9, 2026 used VRoid Studio 2.14.0 and FFmpeg 9.0.2: a 688×600, 10 FPS clip finalized through the G hotkey, decoded through all 100 frames, and displayed an upright model without editor UI. Further recordings finalized automatically when leaving photo booth (132 frames, 13.2 seconds) and closing VRoid normally (163 frames, 16.3 seconds). Both MP4s decoded completely without errors. Temporary automatic test-model opening was disabled afterward. Full-project acceptance remains pending; see `LOCAL_COMPLETION_CHECKLIST.md`.
