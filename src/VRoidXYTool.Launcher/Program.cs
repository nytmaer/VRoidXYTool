using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using VRoidXYTool.LauncherCore;
using VRoidXYTool.Setup;

namespace VRoidXYTool.Launcher;

internal static class Program
{
    internal static Stream Payload() => Assembly.GetExecutingAssembly().GetManifestResourceStream("VRoidXYTool.Payload") ?? throw new IOException("The workspace package is missing.");
    [STAThread] public static void Main(string[] args)
    {
        try
        {
            if (args.Contains("--verify-payload"))
            {
                using var stream = Payload(); using var zip = new ZipArchive(stream);
                using var reader = new StreamReader(zip.GetEntry("package-hashes.json")!.Open());
                var hashes = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.ReadToEnd())!;
                var files = zip.Entries.Where(e => !e.FullName.EndsWith('/') && e.FullName != "package-hashes.json").ToArray();
                if (files.Length != hashes.Count) throw new InvalidDataException("Package manifest does not match.");
                foreach (var file in files) { using var input = file.Open(); if (Convert.ToHexString(SHA256.HashData(input)) != hashes[file.FullName]) throw new InvalidDataException("Package integrity failure."); }
                return;
            }
            string? Value(string flag) { int i = Array.IndexOf(args, flag); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
            string home = Value("--home") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VRoidXYTool");
            string mutexName = "Local\\VRoidXYTool-Launcher-" + Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(home).ToUpperInvariant())))[..24];
            using var mutex = new Mutex(true, mutexName, out bool created);
            if (!created) { MessageBox.Show("VRoidXYTool is already open for this workspace.", "VRoidXYTool"); return; }
            new Application().Run(new LauncherWindow(home, Value("--source"), Value("--loader"), !args.Contains("--no-shortcut")));
        }
        catch (Exception e) { MessageBox.Show(e.Message + "\n\nYour workspace and artwork have been preserved.", "VRoidXYTool could not open"); Environment.ExitCode = 1; }
    }
}

public sealed class LauncherWindow : Window
{
    private WorkspaceManager manager;
    private readonly string payloadId;
    private readonly string? loader;
    private readonly bool createShortcut;
    private readonly TextBlock heading = Text("Welcome to your workspace", 24, FontWeights.SemiBold);
    private readonly TextBlock status = Text("Looking for VRoid Studio…", 15);
    private readonly TextBlock sourceLabel = Text("", 14);
    private readonly TextBlock progress = Text("", 14);
    private readonly TextBox source = new() { IsReadOnly = true, Padding = new Thickness(8) };
    private readonly TextBox log = new() { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Height = 130 };
    private readonly CheckBox companion = new() { Content = "Open the Companion with VRoid", IsChecked = true, Margin = new Thickness(0, 14, 0, 12) };
    private readonly Button primary = Button("Set up workspace"), repair = Button("Repair"), rollback = Button("Use previous workspace"), cancel = Button("Cancel");
    private readonly StackPanel advanced = new();
    private readonly Expander details = new() { Header = "Advanced settings and diagnostics", Margin = new Thickness(0, 20, 0, 0) };
    private bool busy;
    private CancellationTokenSource? cancellation;
    private string? sourceError;

    public LauncherWindow(string home, string? explicitSource, string? cachedLoader, bool createShortcut)
    {
        manager = new(home); loader = cachedLoader; this.createShortcut = createShortcut;
        using (var stream = Program.Payload()) payloadId = WorkspaceManager.PayloadHash(stream);
        Title = "VRoidXYTool"; Width = 720; Height = 650; MinWidth = 560; MinHeight = 460;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontFamily = new FontFamily("Segoe UI"); FontSize = 14;
        Background = Brush("#F3F5F9"); Foreground = Brush("#182435");
        var panel = new StackPanel { Margin = new Thickness(32) };
        panel.Children.Add(Text("VROIDXYTOOL", 12, FontWeights.Bold));
        heading.Margin = new Thickness(0, 14, 0, 10); panel.Children.Add(heading);
        panel.Children.Add(status); sourceLabel.Margin = new Thickness(0, 12, 0, 0); panel.Children.Add(sourceLabel);
        panel.Children.Add(companion);
        primary.Background = Brush("#2359D1"); primary.Foreground = Brushes.White; primary.FontWeight = FontWeights.SemiBold; primary.FontSize = 17;
        primary.Padding = new Thickness(24, 13, 24, 13); primary.HorizontalAlignment = HorizontalAlignment.Left;
        panel.Children.Add(primary); progress.Margin = new Thickness(0, 12, 0, 0); panel.Children.Add(progress);
        cancel.Visibility = Visibility.Collapsed; panel.Children.Add(cancel);
        panel.Children.Add(Text("Your models stay yours. Setup uses a separate copy of VRoid and keeps artwork outside the application.", 13));
        details.Content = advanced; panel.Children.Add(details);
        advanced.Children.Add(Text("Source application", 13, FontWeights.SemiBold)); advanced.Children.Add(source);
        var browse = Button("Choose VRoid…"); advanced.Children.Add(browse);
        var storage = Button("Choose storage location…"); advanced.Children.Add(Text("Storage: " + manager.Home, 12)); advanced.Children.Add(storage);
        var actions = new WrapPanel(); actions.Children.Add(repair); actions.Children.Add(rollback);
        Button folder = Button("Open workspace folder"), data = Button("Open artwork folder"), logs = Button("Open startup log");
        actions.Children.Add(folder); actions.Children.Add(data); actions.Children.Add(logs); advanced.Children.Add(actions);
        advanced.Children.Add(Text("Details stay on this computer. Review logs before sharing them; they may contain account information.", 12));
        advanced.Children.Add(log);
        panel.Children.Add(Text("Local candidate · Windows x64 · VRoid 2.14.0 / 2.16.0 · unsigned", 12));
        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        companion.IsChecked = manager.State.OpenCompanion;
        primary.Click += async (_, _) => await Run(async () =>
        {
            bool installed = manager.Current != null;
            if (NeedsSetup() || UpdateAvailable()) { await Prepare(); if (installed) await Launch(); }
            else await Launch();
        });
        repair.Click += async (_, _) => await Run(Prepare);
        rollback.Click += async (_, _) => await Run(() => { manager.RollBack(); Refresh(); progress.Text = "Previous workspace selected. Click Launch workspace."; return Task.CompletedTask; });
        cancel.Click += (_, _) => cancellation?.Cancel();
        browse.Click += (_, _) => Browse();
        storage.Click += (_, _) => ChangeStorage();
        folder.Click += (_, _) => OpenFolder(manager.Current?.Root ?? manager.Home);
        data.Click += (_, _) => OpenFolder(manager.Current?.DataDirectory ?? Path.Combine(manager.Home, "Data"));
        logs.Click += (_, _) => OpenLog();
        Closing += (_, e) => { if (busy) { e.Cancel = true; progress.Text = "Use Cancel, then wait for the current operation to finish."; } };
        Loaded += async (_, _) => await Run(async () =>
        {
            var found = await Task.Run(() => explicitSource != null ? new[] { InstallDiscovery.Inspect(explicitSource) } : InstallDiscovery.Find(manager.State.Source));
            var best = found.FirstOrDefault(i => i.Error == null) ?? found.FirstOrDefault();
            if (best != null) SelectSource(best);
            Refresh();
        });
    }

    private bool NeedsSetup() => manager.Current == null || manager.Health(manager.Current) != null;
    private bool UpdateAvailable() => manager.Current != null && sourceError == null && File.Exists(source.Text)
        && (WorkspaceManager.BuildId(source.Text) != manager.Current.BuildId || manager.Current.PayloadId != payloadId);
    private void Refresh()
    {
        var current = manager.Current;
        string? error = current == null ? null : manager.Health(current);
        heading.Text = current == null ? "Set up your VRoid workspace" : "Your VRoid workspace";
        status.Text = current == null ? "One setup. Then launch VRoid and the Companion together." : error ?? $"Ready · VRoid Studio {WorkspaceManager.VersionLabel(current.BuildId)}";
        sourceLabel.Text = string.IsNullOrWhiteSpace(source.Text) ? "VRoid was not found. Choose its application in Advanced settings." : sourceError == null ? "Compatible VRoid found · " + InstallDiscovery.Inspect(source.Text).Version : "Source update is unsupported. Your existing workspace is preserved.";
        bool newBuild = false;
        if (current != null && sourceError == null && File.Exists(source.Text)) newBuild = WorkspaceManager.BuildId(source.Text) != current.BuildId;
        bool toolUpdate = current != null && current.PayloadId != payloadId;
        primary.Content = current == null ? "Set up workspace" : error != null ? "Repair workspace" : newBuild || toolUpdate && sourceError == null ? "Update and launch" : "Launch workspace";
        primary.IsEnabled = !busy && (current != null && error == null || sourceError == null && File.Exists(source.Text));
        repair.Content = newBuild ? "Prepare updated VRoid" : toolUpdate ? "Update workspace tools" : "Repair";
        repair.IsEnabled = !busy && sourceError == null && File.Exists(source.Text);
        rollback.IsEnabled = !busy && manager.State.Previous != null;
        if (newBuild) status.Text += "\nA supported VRoid update is ready. Your previous workspace will remain available.";
        else if (toolUpdate) status.Text += "\nThis launcher includes newer workspace tools.";
        if (current != null && error == null && sourceError != null) status.Text += "\nThe source version is unsupported; you can keep launching this compatible workspace.";
    }
    private void SelectSource(Installation selected)
    {
        source.Text = selected.Executable; sourceError = selected.Error;
        if (selected.Error != null) Note(selected.Error);
    }
    private void Browse()
    {
        if (busy) return;
        var dialog = new OpenFileDialog { Title = "Choose your existing VRoid Studio", Filter = "VRoid Studio|VRoidStudio.exe" };
        if (dialog.ShowDialog(this) == true) { SelectSource(InstallDiscovery.Inspect(dialog.FileName)); Refresh(); }
    }
    private void ChangeStorage()
    {
        if (busy) return;
        var dialog = new OpenFolderDialog { Title = "Choose VRoidXYTool storage (existing workspaces stay in their current location)" };
        if (dialog.ShowDialog(this) != true) return;
        try { manager = new(dialog.FolderName); companion.IsChecked = manager.State.OpenCompanion; Refresh(); progress.Text = "Storage selected: " + manager.Home + ". Existing files were not moved."; }
        catch (Exception e) { progress.Text = e.Message; }
    }
    private async Task Prepare()
    {
        if (sourceError != null || !File.Exists(source.Text)) throw new InvalidOperationException("Choose a compatible VRoid installation first.");
        manager.State.OpenCompanion = companion.IsChecked == true;
        await manager.Prepare(source.Text, Program.Payload, payloadId, loader, new Progress<string>(Note), cancellation!.Token);
        // Persist a stable launcher entry point only after installation succeeds.
        try { if (createShortcut) InstallEntryPoint(); }
        catch (Exception e) { Note("Workspace is ready. Shortcut creation was unavailable: " + e.Message); }
        progress.Text = "Workspace ready. Click Launch workspace.";
    }

    private async Task Launch()
    {
        var workspace = manager.Current ?? throw new InvalidOperationException("Set up your workspace first.");
        string? error = manager.Health(workspace); if (error != null) throw new InvalidDataException(error);
        manager.State.OpenCompanion = companion.IsChecked == true; manager.Save();
        string exe = Path.Combine(workspace.Root, "VRoidStudio.exe"), bridge = manager.ReadBridgePath(workspace);
        DateTimeOffset started = DateTimeOffset.UtcNow;
        bool alreadyRunning = false;
        foreach (var process in Process.GetProcessesByName("VRoidStudio"))
            using (process)
            {
                string? path;
                try { path = process.MainModule?.FileName; } catch { throw new InvalidOperationException("Close the running VRoid instance before launching this workspace."); }
                if (!string.Equals(path, exe, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Another VRoid installation is open. Save your work and close it before launching this workspace.");
                alreadyRunning = true;
            }
        if (!alreadyRunning) Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = workspace.Root, UseShellExecute = true });
        else started = started.AddSeconds(-6);
        var clock = Stopwatch.StartNew(); bool ready = false;
        while (clock.Elapsed < TimeSpan.FromMinutes(8))
        {
            cancellation!.Token.ThrowIfCancellationRequested();
            if (WorkspaceManager.IsLiveBridge(bridge, started, WorkspaceManager.VersionLabel(workspace.BuildId))) { ready = true; break; }
            progress.Text = clock.Elapsed.TotalSeconds < 20 ? "Starting VRoid…" : "Preparing VRoid for first use. This can take several minutes. Please keep this window open.";
            if (clock.Elapsed.TotalSeconds > 20 && Process.GetProcessesByName("VRoidStudio").Length == 0) throw new InvalidOperationException("VRoid closed before the workspace connected. Open the startup log in Advanced settings.");
            await Task.Delay(1000, cancellation.Token);
        }
        if (!ready) throw new TimeoutException("VRoid has not connected yet. It may still be generating files. Open the startup log before trying again; the application was left running.");
        if (companion.IsChecked == true)
        {
            string companionExe = Path.Combine(workspace.Root, "VRoidXYTool", "Companion", "VRoidXYTool.Companion.exe");
            bool companionRunning = false;
            foreach (var p in Process.GetProcessesByName("VRoidXYTool.Companion")) using (p)
                try { companionRunning |= string.Equals(p.MainModule?.FileName, companionExe, StringComparison.OrdinalIgnoreCase); } catch { }
            if (!companionRunning)
            {
                var start = new ProcessStartInfo(companionExe) { WorkingDirectory = Path.GetDirectoryName(companionExe), UseShellExecute = false };
                start.ArgumentList.Add(bridge); Process.Start(start);
            }
        }
        progress.Text = "Workspace connected. Open a model and link a texture to begin.";
    }
    private void InstallEntryPoint()
    {
        // A normal developer apphost depends on adjacent DLLs; only persist the published single-file executable.
        if (!string.IsNullOrEmpty(Assembly.GetExecutingAssembly().Location)) { Note("Developer build: launch from the build folder. Published builds create the Start menu entry."); return; }
        string current = Environment.ProcessPath ?? throw new IOException("Launcher path unavailable.");
        string id = Installer.Hash(current)[..16];
        string stable = Installer.SafePath(manager.Home, "Launcher/" + id + "/VRoidXYTool.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(stable)!);
        if (!File.Exists(stable)) File.Copy(current, stable);
        // Use the standard per-user Start menu, without elevation or a machine-wide installer.
        string programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
        dynamic shortcut = shell.CreateShortcut(Path.Combine(programs, "VRoidXYTool.lnk"));
        shortcut.TargetPath = stable; shortcut.Arguments = "--home \"" + manager.Home + "\""; shortcut.WorkingDirectory = manager.Home; shortcut.Description = "Launch your VRoidXYTool workspace"; shortcut.Save();
    }
    private async Task Run(Func<Task> operation)
    {
        if (busy) return; busy = true; cancellation = new();
        primary.IsEnabled = repair.IsEnabled = rollback.IsEnabled = details.IsEnabled = companion.IsEnabled = false;
        cancel.Visibility = Visibility.Visible;
        try { await operation(); }
        catch (OperationCanceledException) { progress.Text = "Cancelled. Previous files are preserved; a running VRoid application is left open."; }
        catch (Exception e) { progress.Text = e.Message; Note(e.ToString()); details.IsExpanded = true; }
        finally { busy = false; cancellation.Dispose(); cancellation = null; details.IsEnabled = companion.IsEnabled = true; cancel.Visibility = Visibility.Collapsed; Refresh(); }
    }
    private void Note(string message) { progress.Text = message; if (log.Text.Length > 16000) log.Text = log.Text[^8000..]; log.AppendText(message + Environment.NewLine); log.ScrollToEnd(); }
    private void OpenFolder(string path) { try { Directory.CreateDirectory(path); Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); } catch (Exception e) { progress.Text = e.Message; } }
    private void OpenLog() { string? path = manager.Current == null ? null : Path.Combine(manager.Current.Root, "BepInEx", "LogOutput.log"); if (path == null || !File.Exists(path)) { progress.Text = "No startup log yet. Launch the workspace first."; return; } try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); } catch (Exception e) { progress.Text = e.Message; } }
    private static SolidColorBrush Brush(string value) => new((Color)ColorConverter.ConvertFromString(value));
    private static TextBlock Text(string text, int size, FontWeight? weight = null) => new() { Text = text, FontSize = size, FontWeight = weight ?? FontWeights.Normal, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 6) };
    private static Button Button(string text) => new() { Content = text, Padding = new Thickness(13, 8, 13, 8), Margin = new Thickness(0, 5, 8, 5) };
}
