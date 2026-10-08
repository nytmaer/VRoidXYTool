using System.Diagnostics;
using System.Buffers.Binary;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Data;
using System.Windows.Threading;
using Microsoft.Win32;
using VRoidXYTool.CompanionCore;

namespace VRoidXYTool.Companion;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        new Application().Run(new CompanionWindow(args.Length == 0 ? null : args[0]));
    }
}

internal sealed class CompanionWindow : Window
{
    private readonly TextBlock connection = Text("Choose your VRoid bridge", 24, FontWeights.SemiBold);
    private readonly TextBlock detail = Text("Connect to the local state file published by the VRoid plugin.", 14);
    private readonly TextBlock stateLocation = Text("No bridge selected", 12);
    private readonly TextBlock feedback = Text("", 14);
    private readonly TextBlock editorLabel = Text("", 12);
    private readonly ListView layers = new() { Background = Brush("#19212D"), Foreground = Brushes.White, BorderThickness = new Thickness(0), Margin = new Thickness(0, 20, 0, 20) };
    private readonly Button open = Button("Open PNG in editor");
    private readonly Button copy = Button("Copy PNG path");
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private string? statePath;
    private string? editorPath;
    private BridgeSnapshot? snapshot;
    private readonly string preferencesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VRoidXYTool", "Companion", "settings.json");
    private readonly Dictionary<string, (DateTime Modified, long Length, ImageSource Image)> thumbnails = new();
    private readonly TextBlock empty = Text("Your linked textures will appear here", 18, FontWeights.SemiBold);

    public CompanionWindow(string? initialStatePath)
    {
        Title = "VRoid Companion — Linked textures";
        // Leave room for a preview row below the connection summary and above the actions.
        Width = 1060; Height = 640; MinWidth = 780; MinHeight = 580;
        Background = Brush("#101620"); Foreground = Brushes.White; FontFamily = new FontFamily("Segoe UI");
        var root = new Grid { Margin = new Thickness(28) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var header = new DockPanel();
        var choose = Button("Choose bridge file"); DockPanel.SetDock(choose, Dock.Right); header.Children.Add(choose);
        var title = new StackPanel(); title.Children.Add(Text("VROID COMPANION", 12, FontWeights.Bold));
        title.Children.Add(Text("Linked textures", 30, FontWeights.SemiBold)); header.Children.Add(title); root.Children.Add(header);
        var summary = new StackPanel { Margin = new Thickness(0, 24, 0, 0) };
        stateLocation.TextTrimming = TextTrimming.CharacterEllipsis; stateLocation.TextWrapping = TextWrapping.NoWrap;
        stateLocation.Foreground = Brush("#A8B5C8");
        summary.Children.Add(connection); summary.Children.Add(detail); summary.Children.Add(stateLocation);
        Grid.SetRow(summary, 1); root.Children.Add(summary);
        var view = new GridView();
        var thumbnail = new FrameworkElementFactory(typeof(Image));
        thumbnail.SetValue(FrameworkElement.WidthProperty, 56d); thumbnail.SetValue(FrameworkElement.HeightProperty, 56d);
        thumbnail.SetValue(FrameworkElement.MarginProperty, new Thickness(4)); thumbnail.SetBinding(Image.SourceProperty, new Binding("Thumbnail"));
        view.Columns.Add(new GridViewColumn { Header = "Preview", Width = 76, CellTemplate = new DataTemplate { VisualTree = thumbnail } });
        view.Columns.Add(new GridViewColumn { Header = "Layer", Width = 190, DisplayMemberBinding = new Binding("Name") });
        view.Columns.Add(new GridViewColumn { Header = "Sync status", Width = 205, DisplayMemberBinding = new Binding("Status") });
        view.Columns.Add(new GridViewColumn { Header = "Last imported", Width = 120, DisplayMemberBinding = new Binding("LastImported") });
        view.Columns.Add(new GridViewColumn { Header = "PNG file", Width = 260, DisplayMemberBinding = new Binding("FileName") });
        layers.View = view;
        var textureArea = new Grid(); textureArea.Children.Add(layers);
        empty.HorizontalAlignment = HorizontalAlignment.Center; empty.VerticalAlignment = VerticalAlignment.Center;
        empty.IsHitTestVisible = false; empty.Foreground = Brush("#A8B5C8"); textureArea.Children.Add(empty);
        Grid.SetRow(textureArea, 2); root.Children.Add(textureArea);
        var rowStyle = new Style(typeof(ListViewItem));
        rowStyle.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
        rowStyle.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, new Binding("PngPath")));
        var selected = new Trigger { Property = ListViewItem.IsSelectedProperty, Value = true };
        selected.Setters.Add(new Setter(Control.ForegroundProperty, Brush("#101620")));
        rowStyle.Triggers.Add(selected);
        var hovered = new Trigger { Property = ListViewItem.IsMouseOverProperty, Value = true };
        hovered.Setters.Add(new Setter(Control.ForegroundProperty, Brush("#101620")));
        rowStyle.Triggers.Add(hovered); layers.ItemContainerStyle = rowStyle;
        var footer = new StackPanel();
        var actions = new WrapPanel(); actions.Children.Add(open); actions.Children.Add(copy);
        var chooseEditor = Button("Choose editor"); actions.Children.Add(chooseEditor);
        var defaultEditor = Button("Use default app"); actions.Children.Add(defaultEditor); footer.Children.Add(actions);
        footer.Children.Add(editorLabel); footer.Children.Add(feedback);
        footer.Children.Add(Text("Link layers in VRoid. Save PNG edits in your editor, then save the .vroid in VRoid.", 13));
        Grid.SetRow(footer, 3); root.Children.Add(footer); Content = root;
        var preferences = CompanionPreferences.Load(preferencesPath);
        statePath = preferences.BridgePath;
        editorPath = preferences.EditorPath;
        if (!preferences.UseDefaultEditor && string.IsNullOrWhiteSpace(editorPath))
            editorPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Krita (x64)", "bin", "krita.exe");
        if (preferences.UseDefaultEditor || !File.Exists(editorPath)) editorPath = null;
        UpdateEditorLabel();
        choose.Click += (_, _) => ChooseState();
        chooseEditor.Click += (_, _) => ChooseEditor();
        defaultEditor.Click += (_, _) => { editorPath = null; UpdateEditorLabel(); SavePreferences(); };
        open.Click += (_, _) => OpenSelected();
        copy.Click += (_, _) => RunAction(() => { Clipboard.SetText(SelectedPng()); feedback.Text = "PNG path copied."; });
        layers.SelectionChanged += (_, _) => UpdateActions();
        timer.Tick += (_, _) => RefreshState();
        Closed += (_, _) => timer.Stop();
        if (!string.IsNullOrWhiteSpace(initialStatePath)) statePath = Path.GetFullPath(initialStatePath);
        RefreshState(); timer.Start();
    }

    private void ChooseState()
    {
        var dialog = new OpenFileDialog { Title = "Choose VRoid bridge-state.json", Filter = "Bridge state (*.json)|*.json", CheckFileExists = true };
        if (dialog.ShowDialog(this) != true) return;
        statePath = dialog.FileName; snapshot = null; feedback.Text = ""; SavePreferences(); RefreshState();
    }

    private void ChooseEditor()
    {
        var dialog = new OpenFileDialog { Title = "Choose your image editor", Filter = "Image editor (*.exe)|*.exe", CheckFileExists = true };
        if (dialog.ShowDialog(this) != true) return;
        editorPath = dialog.FileName; UpdateEditorLabel(); SavePreferences();
    }

    private void SavePreferences()
    {
        try { new CompanionPreferences { BridgePath = statePath, EditorPath = editorPath, UseDefaultEditor = editorPath == null }.Save(preferencesPath); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { feedback.Text = "Settings could not be saved: " + error.Message; }
    }

    private ImageSource? Thumbnail(BridgeSnapshot current, LinkedTextureSnapshot layer)
    {
        try
        {
            string path = SnapshotFile.GetPngToOpen(current, layer.Id, DateTimeOffset.UtcNow);
            var info = new FileInfo(path);
            if (info.Length > 64 * 1024 * 1024) return null;
            if (thumbnails.TryGetValue(path, out var cached) && cached.Modified == info.LastWriteTimeUtc && cached.Length == info.Length) return cached.Image;
            // Decode eagerly from a shared stream so previews never hold an editor's PNG open.
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            Span<byte> header = stackalloc byte[24];
            if (stream.Read(header) != header.Length || !header[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return null;
            uint width = BinaryPrimitives.ReadUInt32BigEndian(header[16..20]), height = BinaryPrimitives.ReadUInt32BigEndian(header[20..24]);
            if (width == 0 || height == 0 || width > 4096 || height > 4096) return null;
            stream.Position = 0;
            var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
            if (width >= height) bitmap.DecodePixelWidth = 112; else bitmap.DecodePixelHeight = 112;
            bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze();
            thumbnails[path] = (info.LastWriteTimeUtc, info.Length, bitmap); return bitmap;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or NotSupportedException or FormatException) { return null; }
    }

    private void UpdateEditorLabel() => editorLabel.Text = editorPath == null ? "Editor: default PNG application (choose an editor to change it)" : "Editor: " + Path.GetFileNameWithoutExtension(editorPath);

    private void RefreshState()
    {
        stateLocation.Text = statePath ?? "No bridge selected";
        stateLocation.ToolTip = statePath;
        if (statePath == null) { UpdateActions(); return; }
        try
        {
            var current = SnapshotFile.Read(statePath);
            string? selectedId = (layers.SelectedItem as LayerRow)?.Id;
            snapshot = current;
            bool live = SnapshotFile.IsLive(current, DateTimeOffset.UtcNow);
            connection.Text = live ? $"Connected · {current.Layers.Count} linked {(current.Layers.Count == 1 ? "layer" : "layers")}" : "VRoid bridge offline";
            connection.Foreground = live ? Brush("#76DDD0") : Brush("#F1C078");
            detail.Text = !live ? "Start VRoid with the bridge plugin. Stale links cannot be opened." : current.Layers.Count == 0 ? "Open texture editing in VRoid and link a layer to begin." : "Select a linked layer to open its PNG. Save edits in your editor to sync with VRoid.";
            var rows = current.Layers.Select(x => new LayerRow(x.Id, x.Name, x.Status, x.LastImportedUtc?.ToLocalTime().ToString("HH:mm:ss") ?? "—", x.PngPath, live ? Thumbnail(current, x) : null)).ToList();
            var paths = rows.Select(x => x.PngPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (string path in thumbnails.Keys.Where(x => !paths.Contains(x)).ToArray()) thumbnails.Remove(path);
            // Keep row containers stable while only the heartbeat changes, preserving focus and selection.
            if (layers.ItemsSource is not List<LayerRow> previous || !previous.SequenceEqual(rows))
            {
                layers.ItemsSource = rows;
                layers.SelectedItem = rows.FirstOrDefault(x => x.Id == selectedId);
            }
            layers.Opacity = live ? 1 : 0.5;
            empty.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidDataException or ArgumentException or NotSupportedException)
        {
            snapshot = null; layers.ItemsSource = null;
            thumbnails.Clear(); empty.Visibility = Visibility.Visible;
            connection.Text = "Bridge unavailable"; connection.Foreground = Brush("#F1C078");
            detail.Text = error is FileNotFoundException ? "Start VRoid or choose its current bridge-state.json file." : error.Message;
        }
        UpdateActions();
    }

    private void UpdateActions() => open.IsEnabled = copy.IsEnabled = snapshot != null && layers.SelectedItem != null && SnapshotFile.IsLive(snapshot, DateTimeOffset.UtcNow);
    private string SelectedPng()
    {
        // Re-read immediately before opening: selection may belong to a closed/replaced document.
        var current = SnapshotFile.Read(statePath!);
        return SnapshotFile.GetPngToOpen(current, ((LayerRow)layers.SelectedItem).Id, DateTimeOffset.UtcNow);
    }

    private void OpenSelected() => RunAction(() =>
    {
        string png = SelectedPng();
        if (editorPath == null) Process.Start(new ProcessStartInfo(png) { UseShellExecute = true });
        else
        {
            var start = new ProcessStartInfo(editorPath) { UseShellExecute = false };
            start.ArgumentList.Add(png); Process.Start(start);
        }
        feedback.Text = "Opened " + ((LayerRow)layers.SelectedItem).Name + ". Save as PNG to sync with VRoid.";
    });

    private void RunAction(Action action)
    {
        try { action(); }
        catch (Exception error) { feedback.Text = error.Message; RefreshState(); }
    }
    private static SolidColorBrush Brush(string value) => new((Color)ColorConverter.ConvertFromString(value));
    private static TextBlock Text(string value, double size, FontWeight? weight = null) => new() { Text = value, FontSize = size, FontWeight = weight ?? FontWeights.Normal, Foreground = Brushes.White, Margin = new Thickness(0, 4, 0, 4), TextWrapping = TextWrapping.Wrap };
    private static Button Button(string value) => new() { Content = value, Padding = new Thickness(18, 10, 18, 10), Margin = new Thickness(0, 0, 12, 8), FontSize = 14 };
    private sealed record LayerRow(string Id, string Name, string Status, string LastImported, string PngPath, ImageSource? Thumbnail)
    {
        public string FileName => Path.GetFileName(PngPath);
    }
}
