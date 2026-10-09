using VRoidXYTool.CompanionCore;

string root = Path.Combine(Path.GetTempPath(), "vroid-companion-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    string png = Path.Combine(root, "layer.png"); File.WriteAllBytes(png, new byte[] { 1 });
    string state = Path.Combine(root, "state.json");
    var now = DateTimeOffset.UtcNow;
    var snapshot = new BridgeSnapshot { PublishedUtc = now, Running = true, DocumentSession = "session1", ExportRoot = root,
        Layers = new() { new LinkedTextureSnapshot { Id = "session1/layer1", Name = "Layer", PngPath = png } } };
    SnapshotFile.Write(state, snapshot);
    var read = SnapshotFile.Read(state);
    Check(read.Layers.Single().Id == "session1/layer1", "state roundtrip retains document/layer identity");
    Check(SnapshotFile.GetPngToOpen(read, "session1/layer1", now) == png, "live registered PNG can open");
    Reject(() => SnapshotFile.GetPngToOpen(read, "session1/layer1", now.AddSeconds(7)), "stale heartbeat cannot open PNG");
    read.Running = false;
    Reject(() => SnapshotFile.GetPngToOpen(read, "session1/layer1", now), "normal shutdown cannot open PNG");
    read.Running = true;
    Reject(() => SnapshotFile.GetPngToOpen(read, "old-session/layer1", now), "old document identity cannot open PNG");
    read.Layers[0].PngPath = Path.Combine(root, "..", "escaped.png");
    Reject(() => SnapshotFile.GetPngToOpen(read, "session1/layer1", now), "path traversal rejected");
    read.Layers[0].PngPath = Path.Combine(root, "program.exe"); File.WriteAllText(read.Layers[0].PngPath, "fixture");
    Reject(() => SnapshotFile.GetPngToOpen(read, "session1/layer1", now), "non-PNG cannot be launched as a texture");
    read.Layers[0].PngPath = Path.Combine(root, "missing.png");
    Reject(() => SnapshotFile.GetPngToOpen(read, "session1/layer1", now), "missing PNG handled");
    read.SchemaVersion = 2; SnapshotFile.Write(state, read);
    Reject(() => SnapshotFile.Read(state), "unknown schema rejected");
    read.SchemaVersion = 1; read.Layers.Clear(); SnapshotFile.Write(state, read);
    Check(SnapshotFile.Read(state).Layers.Count == 0, "atomic replacement clears old document rows");
    File.WriteAllText(state, "{partial"); Reject(() => SnapshotFile.Read(state), "malformed state handled");
    File.WriteAllText(state, "{\"SchemaVersion\":1,\"Layers\":[null]}"); Reject(() => SnapshotFile.Read(state), "null layer entries rejected");
    File.WriteAllText(state, new string(' ', 1024 * 1024 + 1)); Reject(() => SnapshotFile.Read(state), "oversized state rejected");
    Console.WriteLine("PASS: all Companion contract regressions");
    string settings = Path.Combine(root, "preferences", "settings.json");
    Check(CompanionPreferences.Load(settings).BridgePath == null, "missing preferences use defaults");
    new CompanionPreferences { BridgePath = state, EditorPath = "C:/editor path/editor.exe" }.Save(settings);
    var preferences = CompanionPreferences.Load(settings);
    Check(preferences.BridgePath == state && preferences.EditorPath == "C:/editor path/editor.exe", "bridge and editor preferences survive restart");
    new CompanionPreferences { BridgePath = state, UseDefaultEditor = true }.Save(settings);
    Check(CompanionPreferences.Load(settings).UseDefaultEditor, "explicit default editor choice survives restart");
    File.WriteAllText(settings, "{partial"); Check(CompanionPreferences.Load(settings).EditorPath == null, "corrupt preferences recover to defaults");
    File.WriteAllText(settings, new string(' ', 16385)); Check(CompanionPreferences.Load(settings).BridgePath == null, "oversized preferences recover to defaults");
    string cameraFile = Path.Combine(root, "camera.json");
    var cameraPresets = new WorkspacePresets();
    cameraPresets.Cameras[0] = new CameraPreset(new Point3(0, 1, 4), new Point3(0, 1, 0), true, .9f);
    cameraPresets.Save(cameraFile);
    Check(WorkspacePresets.Load(cameraFile).Cameras[0] == cameraPresets.Cameras[0], "camera view and projection persist");
    cameraPresets.Cameras[0] = cameraPresets.Cameras[0]! with { Size = -1 };
    Reject(() => cameraPresets.Save(cameraFile), "invalid camera values cannot overwrite presets");
    Check(WorkspacePresets.Load(cameraFile).Cameras[0]!.Size == .9f, "failed save preserves previous valid view");
    File.WriteAllText(cameraFile, "{\"SchemaVersion\":99}"); Reject(() => WorkspacePresets.Load(cameraFile), "unknown camera schema rejected");
    File.WriteAllText(cameraFile, "{\"SchemaVersion\":1,\"Cameras\":[{\"Position\":{\"X\":0,\"Y\":1,\"Z\":4},\"Target\":{\"X\":0,\"Y\":1,\"Z\":0},\"Orthographic\":true,\"Size\":0.9},null,null,null]}");
    var migrated = WorkspacePresets.Load(cameraFile);
    Check(migrated.SchemaVersion == 2 && migrated.Orthographic[0]?.Size == .9f && migrated.Perspective[0] == null, "old shared camera slots migrate into the correct projection bank");
    migrated.Perspective[9] = new CameraPreset(new Point3(0, 1, 4), new Point3(0, 1, 0), false, .8f) { Rotation = new Point3(10, 180, 25) };
    migrated.Save(cameraFile);
    Check(WorkspacePresets.Load(cameraFile).Perspective[9]?.Rotation?.Z == 25, "tenth camera slot preserves roll independently of orthographic bank");
    string guideFile = Path.Combine(root, "guides.json");
    var guides = new GuidePresets { Images = new() { new GuideImagePreset { Path = Path.Combine(root, "missing.jpg"), WorldSpace = true, Position = new Point3(0, 1, -1), Rotation = new Point3(0, 90, 0) } } };
    guides.Save(guideFile);
    Check(GuidePresets.Load(guideFile).Images[0] == guides.Images[0], "missing reference retains its settings for later recovery");
    guides.Images[0] = guides.Images[0] with { Alpha = float.NaN };
    Reject(() => guides.Save(guideFile), "non-finite guide opacity rejected before overwrite");
    Check(GuidePresets.Load(guideFile).Images[0].Alpha == .35f, "failed guide save preserves previous preset");
    File.WriteAllText(guideFile, new string(' ', 131073));
    Reject(() => GuidePresets.Load(guideFile), "oversized guide preset rejected");
    byte[] jpegHeader = { 255,216,255,192,0,8,8,0,32,0,64,0,255,217 };
    Check(JpegGuard.IsBoundedJpeg(jpegHeader), "JPEG dimensions inspected before native allocation");
    jpegHeader[9] = 17;
    Check(!JpegGuard.IsBoundedJpeg(jpegHeader), "JPEG wider than 4096 rejected");
    Check(!JpegGuard.IsBoundedJpeg(jpegHeader.AsSpan(0,10)), "truncated JPEG header rejected");
    var emptyVmd = new byte[66];
    System.Text.Encoding.ASCII.GetBytes("Vocaloid Motion Data 0002").CopyTo(emptyVmd,0);
    VmdGuard.Validate(emptyVmd);
    Check(true,"empty VMD sections remain structurally valid");
    emptyVmd[50]=255;emptyVmd[51]=255;emptyVmd[52]=255;emptyVmd[53]=127;
    Reject(()=>VmdGuard.Validate(emptyVmd),"VMD claimed keyframe count cannot overrun the input");
    Reject(()=>VmdGuard.Validate(new byte[58]),"invalid VMD signature rejected");
}
finally { Directory.Delete(root, true); }

static void Check(bool condition, string name) { if (!condition) throw new Exception(name); Console.WriteLine("PASS: " + name); }
static void Reject(Action action, string name)
{
    bool rejected = false;
    try { action(); } catch (Exception error) when (error is InvalidOperationException or IOException or InvalidDataException or System.Text.Json.JsonException) { rejected = true; }
    Check(rejected, name);
}
