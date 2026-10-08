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
}
finally { Directory.Delete(root, true); }

static void Check(bool condition, string name) { if (!condition) throw new Exception(name); Console.WriteLine("PASS: " + name); }
static void Reject(Action action, string name)
{
    bool rejected = false;
    try { action(); } catch (Exception error) when (error is InvalidOperationException or IOException or InvalidDataException or System.Text.Json.JsonException) { rejected = true; }
    Check(rejected, name);
}
