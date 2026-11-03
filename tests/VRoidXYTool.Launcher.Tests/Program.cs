using System.IO.Compression;
using System.Text.Json;
using VRoidXYTool.LauncherCore;
using VRoidXYTool.Setup;

if (args.Length != 3) throw new ArgumentException("Pass a real VRoidStudio.exe, pinned loader ZIP and payload ZIP. Fixture applications are never launched.");
string root = Path.Combine(Path.GetTempPath(), "VRoidXYTool-Launcher-Tests", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
int checks = 0;
// Retain artifacts if a check fails; cleanup only the exact unique fixture root after success.
string source214 = Fixture("Source 214", "49044bb6ab464dbc934e49a426e6a25b");
string source216 = Fixture("Source 216 空白", "a34c8843981a4aada647acf24a55dece");
string sourceHash = Installer.Hash(source216), payload = Path.GetFullPath(args[2]), loader = Path.GetFullPath(args[1]);
string home = Path.Combine(root, "Managed workspace 空白");
var manager = new WorkspaceManager(home);
string payloadId; using (var stream = File.OpenRead(payload)) payloadId = WorkspaceManager.PayloadHash(stream);
var first = await manager.Prepare(source214, () => File.OpenRead(payload), payloadId, loader, null, CancellationToken.None);
Check(manager.Health(first) == null, "first setup produces a healthy managed workspace");
Check(new WorkspaceManager(home).Current == first, "workspace survives launcher restart");
Check(manager.ReadBridgePath(first) == Path.Combine(home, "Data", "Bridge", "bridge-state.json"), "Companion bridge connects without manual selection");
File.WriteAllText(Path.Combine(first.DataDirectory, "artwork.png"), "precious artwork");
string config = Path.Combine(first.Root, "BepInEx", "config", "io.github.nytmaer.vroidxytool.il2cpp.cfg");
File.WriteAllText(config, File.ReadAllText(config).Replace("PollMilliseconds = 250", "PollMilliseconds = 750").Replace("ModelPath =", "ModelPath = never-open-original.vroid"));
File.WriteAllText(Path.Combine(first.Root, "BepInEx", "config", "VRoidXYToolWorkspace.json"), "saved camera settings");
Directory.CreateDirectory(Path.Combine(first.Root, "Pose")); File.WriteAllText(Path.Combine(first.Root, "Pose", "saved.pose"), "saved pose");
var second = await manager.Prepare(source216, () => File.OpenRead(payload), payloadId, loader, null, CancellationToken.None);
Check(second.Root != first.Root && manager.State.Previous == first.Root, "VRoid update creates a separate generation and retains fallback");
Check(File.Exists(Path.Combine(first.Root, "VRoidStudio.exe")), "previous application remains present");
Check(Installer.Hash(source216) == sourceHash, "source application is unchanged");
Check(File.ReadAllText(Path.Combine(second.DataDirectory, "artwork.png")) == "precious artwork", "artwork survives VRoid generation switch");
string newConfig = File.ReadAllText(Path.Combine(second.Root, "BepInEx", "config", "io.github.nytmaer.vroidxytool.il2cpp.cfg"));
Check(newConfig.Contains("PollMilliseconds = 750") && !newConfig.Contains("never-open-original"), "preferences migrate while diagnostic model opens are disabled");
Check(File.ReadAllText(Path.Combine(second.Root, "Pose", "saved.pose")) == "saved pose", "saved poses migrate to new generation");
Check(File.ReadAllText(Path.Combine(second.Root, "BepInEx", "config", "VRoidXYToolWorkspace.json")) == "saved camera settings", "camera/reference settings migrate");
string damaged = Path.Combine(second.Root, "BepInEx", "plugins", "VRoidXYTool.IL2CPP", "VRoidXYTool.IL2CPP.dll");
File.WriteAllText(damaged, "damaged file");
Check(manager.Health(second) != null, "changed tool file disables launch and requests repair");
second = await manager.Prepare(source216, () => File.OpenRead(payload), payloadId, loader, null, CancellationToken.None);
Check(manager.Health(second) == null && File.ReadAllText(Path.Combine(second.DataDirectory, "artwork.png")) == "precious artwork", "repair recovers tool files and preserves artwork");
manager.RollBack(); Check(manager.Current?.Root == first.Root, "previous workspace can be selected after update");
manager.RollBack(); Check(manager.Current?.Root == second.Root, "rollback is reversible");
string unsupported = Fixture("Unknown update", "unknown");
try { await manager.Prepare(unsupported, () => File.OpenRead(payload), payloadId, loader, null, CancellationToken.None); throw new Exception("Unknown source accepted"); }
catch (InvalidDataException) { Check(manager.Current?.Root == second.Root && manager.Health(second) == null, "unsupported source update preserves working workspace"); }
using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
try { await manager.Prepare(source216, () => File.OpenRead(payload), payloadId, loader, null, cancelled.Token); throw new Exception("Cancelled setup accepted"); }
catch (OperationCanceledException) { Check(new WorkspaceManager(home).Current?.Root == second.Root, "cancelled update leaves selected workspace intact"); }
string bridge = manager.ReadBridgePath(second); Directory.CreateDirectory(Path.GetDirectoryName(bridge)!);
DateTimeOffset now = DateTimeOffset.UtcNow;
WriteBridge(now.AddMinutes(-1), true, "2.16.0"); Check(!WorkspaceManager.IsLiveBridge(bridge, now, "2.16.0"), "stale bridge cannot finish startup");
WriteBridge(now, true, "2.14.0"); Check(!WorkspaceManager.IsLiveBridge(bridge, now, "2.16.0"), "wrong runtime bridge cannot finish startup");
WriteBridge(now, false, "2.16.0"); Check(!WorkspaceManager.IsLiveBridge(bridge, now, "2.16.0"), "offline bridge cannot finish startup");
WriteBridge(now, true, "2.16.0"); Check(WorkspaceManager.IsLiveBridge(bridge, now, "2.16.0"), "fresh matching runtime bridge completes startup");
string invalidHome = Path.Combine(root, "Invalid state"); Directory.CreateDirectory(invalidHome);
File.WriteAllText(Path.Combine(invalidHome, "launcher.json"), JsonSerializer.Serialize(new LauncherState { Current = root, Workspaces = new() { new(root, source216, second.BuildId, payloadId, root) } }));
try { _ = new WorkspaceManager(invalidHome); throw new Exception("Escaping state accepted"); }
catch (InvalidDataException) { Check(true, "launcher state cannot select a workspace outside managed storage"); }
Console.WriteLine($"PASS: {checks} launcher integration checks; actual packaged payload and loader used, fixture native apps not launched.");
string allowed = Path.Combine(Path.GetTempPath(), "VRoidXYTool-Launcher-Tests") + Path.DirectorySeparatorChar;
if (!Path.GetFullPath(root).StartsWith(allowed, StringComparison.OrdinalIgnoreCase) || Path.GetFileName(root).Length != 32) throw new Exception("Unsafe cleanup root");
Directory.Delete(root, true);

string Fixture(string name, string guid)
{
    string directory = Path.Combine(root, name); Directory.CreateDirectory(directory);
    string exe = Path.Combine(directory, "VRoidStudio.exe"); File.Copy(Path.GetFullPath(args[0]), exe);
    foreach (string file in new[] { "GameAssembly.dll", "UnityPlayer.dll" }) File.WriteAllText(Path.Combine(directory, file), "fixture");
    Directory.CreateDirectory(Path.Combine(directory, "VRoidStudio_Data", "il2cpp_data", "Metadata"));
    File.WriteAllText(Path.Combine(directory, "VRoidStudio_Data", "il2cpp_data", "Metadata", "global-metadata.dat"), "fixture");
    File.WriteAllText(Path.Combine(directory, "VRoidStudio_Data", "app.info"), "pixiv\nVRoid Studio");
    File.WriteAllText(Path.Combine(directory, "VRoidStudio_Data", "boot.config"), "build-guid=" + guid); return exe;
}
void WriteBridge(DateTimeOffset time, bool running, string version) => File.WriteAllText(bridge, JsonSerializer.Serialize(new { SchemaVersion = 1, PublishedUtc = time, Running = running, ApplicationVersion = version }));
void Check(bool value, string description) { if (!value) throw new Exception(description); checks++; Console.WriteLine("PASS: " + description); }
