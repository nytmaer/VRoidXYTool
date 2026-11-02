using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using VRoidXYTool.Setup;

namespace VRoidXYTool.LauncherCore;

public sealed record Workspace(string Root, string Source, string BuildId, string PayloadId, string DataDirectory);
public sealed class LauncherState
{
    public int Schema { get; set; } = 1;
    public string Source { get; set; } = "";
    public string? Current { get; set; }
    public string? Previous { get; set; }
    public bool OpenCompanion { get; set; } = true;
    public List<Workspace> Workspaces { get; set; } = new();
}

public sealed class WorkspaceManager
{
    public string Home { get; }
    public LauncherState State { get; private set; }
    public string StatePath => Path.Combine(Home, "launcher.json");
    public Workspace? Current => State.Workspaces.SingleOrDefault(w => Same(w.Root, State.Current));
    public WorkspaceManager(string home)
    {
        string fullHome = Path.GetFullPath(home);
        if (fullHome == Path.GetPathRoot(fullHome)) throw new InvalidDataException("Choose a storage folder, not the root of a drive.");
        Home = fullHome.TrimEnd(Path.DirectorySeparatorChar);
        // Also reject reparse-point storage before following launcher state.
        Installer.SafePath(Home, "launcher.json");
        State = File.Exists(StatePath) ? ReadState() : new();
    }

    private LauncherState ReadState()
    {
        if (new FileInfo(StatePath).Length > 128 * 1024) throw new InvalidDataException("Launcher settings are too large.");
        var state = JsonSerializer.Deserialize<LauncherState>(File.ReadAllText(StatePath)) ?? throw new InvalidDataException("Launcher settings are empty.");
        if (state.Schema != 1 || state.Workspaces == null || state.Workspaces.Count > 32) throw new InvalidDataException("Unsupported launcher settings. Your files have been preserved.");
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var workspace in state.Workspaces)
        {
            if (workspace == null || !roots.Add(workspace.Root) || !Contained(workspace.Root) || !IsBuildId(workspace.BuildId)) throw new InvalidDataException("Invalid managed workspace record.");
        }
        if (state.Current != null && !roots.Contains(state.Current) || state.Previous != null && !roots.Contains(state.Previous)) throw new InvalidDataException("Unknown selected workspace.");
        return state;
    }

    public void Save()
    {
        Directory.CreateDirectory(Home);
        string temporary = StatePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, JsonSerializer.Serialize(State, new JsonSerializerOptions { WriteIndented = true })); File.Move(temporary, StatePath, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public static string BuildId(string executable)
    {
        var layout = Installer.Inspect(executable);
        return File.ReadLines(Path.Combine(layout.Root, "VRoidStudio_Data", "boot.config"))
            .Select(l => l.Trim()).Single(l => l.StartsWith("build-guid=", StringComparison.OrdinalIgnoreCase))[11..].ToLowerInvariant();
    }
    public static string VersionLabel(string buildId) => buildId switch
    {
        "49044bb6ab464dbc934e49a426e6a25b" => "2.14.0",
        "a34c8843981a4aada647acf24a55dece" => "2.16.0",
        _ => "unknown"
    };
    public static string PayloadHash(Stream payload)
    {
        string hash = Convert.ToHexString(SHA256.HashData(payload));
        if (payload.CanSeek) payload.Position = 0;
        return hash;
    }

    public string? Health(Workspace workspace)
    {
        try
        {
            if (!Contained(workspace.Root) || BuildId(Path.Combine(workspace.Root, "VRoidStudio.exe")) != workspace.BuildId) return "The managed VRoid copy needs repair.";
            var manifestPath = Installer.SafePath(workspace.Root, ".VRoidXYTool.setup/installed.json");
            if (!File.Exists(manifestPath) || new FileInfo(manifestPath).Length > 2 * 1024 * 1024) return "Installation record missing or invalid.";
            var manifest = Installer.ReadManifest(workspace.Root);
            if (manifest?.Schema != 1 || manifest.Product != "VRoidXYTool-local-candidate") return "Installation record is not recognized.";
            foreach (string required in new[] { "BepInEx/plugins/VRoidXYTool.IL2CPP/VRoidXYTool.IL2CPP.dll", "BepInEx/plugins/VRoidXYTool.IL2CPP/VRoidXYTool.SyncCore.dll", "BepInEx/plugins/VRoidXYTool.IL2CPP/VRoidXYTool.CompanionCore.dll", "VRoidXYTool/Companion/VRoidXYTool.Companion.exe" })
            {
                var record = manifest.Files.SingleOrDefault(f => f.Path == required);
                string file = Installer.SafePath(workspace.Root, required);
                if (record == null || !File.Exists(file) || Installer.Hash(file) != record.Hash) return "A tool file is missing or changed. Choose Repair.";
            }
            if (Installer.NeedsLoader(new(Path.Combine(workspace.Root, "VRoidStudio.exe"), workspace.Root, workspace.DataDirectory, false))) return "The loader needs repair.";
            ReadBridgePath(workspace); // Validate configuration before enabling launch.
            return null;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or InvalidOperationException or ArgumentException or JsonException) { return e.Message; }
    }

    public async Task<Workspace> Prepare(string source, Func<Stream> payload, string payloadId, string? cachedLoader, IProgress<string>? progress, CancellationToken cancellation)
    {
        source = Path.GetFullPath(source);
        string id = BuildId(source);
        string destination = Installer.SafePath(Home, "Workspaces/" + id);
        string data = Path.Combine(Home, "Data");
        Installer.EnsureClosed(destination);
        var old = Current;
        if (old != null) Installer.EnsureClosed(old.Root);
        bool owned = File.Exists(Installer.SafePath(destination, ".VRoidXYTool.setup/installed.json"));
        if (Directory.Exists(destination) && Directory.EnumerateFileSystemEntries(destination).Any() && !owned)
        {
            // A failed first copy can leave empty directories. Preserve them and use a fresh attempt.
            destination = Installer.SafePath(Home, "Workspaces/" + id + "-" + Guid.NewGuid().ToString("N"));
        }
        var request = new InstallRequest(source, destination, data, true);
        string? loader = cachedLoader;
        if (Installer.NeedsLoader(request))
        {
            string cache = Installer.SafePath(Home, "Cache/bepinex-788.zip");
            if (loader == null && File.Exists(cache))
            {
                try { Installer.VerifyLoader(cache); loader = cache; }
                catch (InvalidDataException) { progress?.Report("Cached loader is damaged; downloading a verified replacement."); }
            }
            if (loader == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(cache)!);
                string temporary = cache + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try { await Installer.DownloadLoader(temporary, progress, cancellation); File.Move(temporary, cache, true); loader = cache; }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
            Installer.VerifyLoader(loader);
        }
        await Task.Run(() => { using var stream = payload(); Installer.Install(request, stream, loader, progress, cancellation); }, cancellation);
        if (old != null && !Same(old.Root, destination))
        {
            Installer.EnsureClosed(old.Root);
            MigratePreferences(old.Root, destination);
        }
        var workspace = new Workspace(destination, source, id, payloadId, data);
        string? error = Health(workspace);
        if (error != null) throw new InvalidDataException(error);
        // Commit only after a completed install. Failure leaves the previous workspace selected.
        var previousState = JsonSerializer.Serialize(State);
        State.Workspaces.RemoveAll(w => Same(w.Root, destination));
        State.Workspaces.Add(workspace);
        if (old != null && !Same(old.Root, destination)) State.Previous = old.Root;
        State.Current = destination; State.Source = source;
        try { Save(); }
        catch { State = JsonSerializer.Deserialize<LauncherState>(previousState)!; throw; }
        return workspace;
    }

    public void RollBack()
    {
        var previous = State.Workspaces.SingleOrDefault(w => Same(w.Root, State.Previous)) ?? throw new InvalidOperationException("No previous workspace is available.");
        string? error = Health(previous); if (error != null) throw new InvalidDataException(error);
        if (Current != null) Installer.EnsureClosed(Current.Root);
        Installer.EnsureClosed(previous.Root);
        (State.Current, State.Previous) = (State.Previous, State.Current);
        Save();
    }

    public string ReadBridgePath(Workspace workspace)
    {
        string config = Installer.SafePath(workspace.Root, "BepInEx/config/io.github.nytmaer.vroidxytool.il2cpp.cfg");
        string section = "";
        foreach (string raw in File.ReadLines(config))
        {
            string line = raw.Trim();
            if (line.StartsWith('[')) section = line;
            if (section == "[Companion]" && line.StartsWith("StatePath =", StringComparison.Ordinal))
            {
                string value = line[(line.IndexOf('=') + 1)..].Trim();
                if (!Path.IsPathFullyQualified(value)) throw new InvalidDataException("Bridge path must be absolute. Repair the workspace configuration.");
                return value;
            }
        }
        throw new InvalidDataException("Bridge configuration is missing. Choose Repair.");
    }

    public static bool IsLiveBridge(string path, DateTimeOffset started, string version)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length > 1024 * 1024) return false;
            using var json = JsonDocument.Parse(stream);
            var root = json.RootElement;
            var published = root.GetProperty("PublishedUtc").GetDateTimeOffset();
            return root.GetProperty("SchemaVersion").GetInt32() == 1 && root.GetProperty("Running").GetBoolean()
                && root.GetProperty("ApplicationVersion").GetString() == version && published >= started
                && DateTimeOffset.UtcNow - published < TimeSpan.FromSeconds(6) && published <= DateTimeOffset.UtcNow.AddSeconds(2);
        }
        catch (Exception e) when (e is IOException or JsonException or KeyNotFoundException or InvalidOperationException or FormatException) { return false; }
    }

    private static void MigratePreferences(string source, string destination)
    {
        // No generated interop, loader files, diagnostics or model files are carried forward.
        foreach (string relative in new[] { "BepInEx/config/VRoidXYToolWorkspace.json" }) Copy(relative);
        string configRelative = "BepInEx/config/io.github.nytmaer.vroidxytool.il2cpp.cfg";
        string oldConfig = Installer.SafePath(source, configRelative);
        if (File.Exists(oldConfig))
        {
            if (new FileInfo(oldConfig).Length > 256 * 1024) throw new InvalidDataException("Previous configuration is too large.");
            string section = "";
            var lines = new List<string>();
            foreach (string raw in File.ReadLines(oldConfig))
            {
                string line = raw.Trim();
                if (line.StartsWith('[')) section = line;
                if (section != "[Diagnostics]") lines.Add(raw);
            }
            lines.AddRange(new[] { "[Diagnostics]", "ModelPath =", "OpenOnly = false", "QuitAfterSeconds = 0" });
            File.WriteAllLines(Installer.SafePath(destination, configRelative), lines);
        }
        string poses = Installer.SafePath(source, "Pose");
        if (Directory.Exists(poses)) foreach (string file in Directory.EnumerateFiles(poses, "*", SearchOption.AllDirectories))
            Copy(Path.GetRelativePath(source, file).Replace('\\', '/'));
        void Copy(string relative)
        {
            string input = Installer.SafePath(source, relative), output = Installer.SafePath(destination, relative);
            if (!File.Exists(input) || File.Exists(output)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(output)!); File.Copy(input, output);
        }
    }
    private bool Contained(string root) => Path.GetFullPath(root).StartsWith(Home + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    private static bool IsBuildId(string value) => value?.Length == 32 && value.All(Uri.IsHexDigit);
    private static bool Same(string? a, string? b) => a != null && b != null && string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
}
