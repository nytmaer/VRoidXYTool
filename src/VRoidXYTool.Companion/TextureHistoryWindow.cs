using System.IO;
using System.Buffers.Binary;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using VRoidXYTool.CompanionCore;

namespace VRoidXYTool.Companion;

internal sealed class TextureHistoryWindow : Window
{
    private readonly string bridgePath, session, layerId;
    private readonly ListBox entries = new() { DisplayMemberPath = "Name", MinHeight = 100 };
    private readonly TextBox name = new() { Text = "Texture version", MaxLength = 100, Margin = new Thickness(0, 8, 0, 8) };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
    private readonly StackPanel actions = new() { Orientation = Orientation.Horizontal };
    private readonly Image previous = new() { MaxHeight = 320 }, current = new() { MaxHeight = 320 };
    private bool busy;

    public TextureHistoryWindow(string path, string documentSession, string id)
    {
        bridgePath = path; session = documentSession; layerId = id;
        Title = "Texture snapshots — current linked session";
        Width = 850; Height = 700; MinWidth = 600; MinHeight = 500;
        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock { Text = "Save a named PNG version. Restore first backs up the current PNG. Save your .vroid separately.", TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(name);
        AddButton("Save snapshot", async () =>
        {
            string label = name.Text;
            await Task.Run(() => TextureSnapshots.Capture(ReadCurrent(), layerId, label));
            await Reload(); status.Text = "Snapshot saved. History stays with this document session and layer.";
        });
        AddButton("Compare selected", async () =>
        {
            string selected = Selected();
            var paths = await Task.Run(() =>
            {
                var bridge = ReadCurrent();
                return (TextureSnapshots.GetImage(bridge, layerId, selected), SnapshotFile.GetPngToOpen(bridge, layerId, DateTimeOffset.UtcNow));
            });
            previous.Source = Preview(paths.Item1); current.Source = Preview(paths.Item2);
            status.Text = "Selected snapshot (left) and current PNG (right). Previews are scaled to 512 pixels.";
        });
        AddButton("Restore selected", async () =>
        {
            string selected = Selected();
            if (MessageBox.Show(this, "Replace the linked PNG with this snapshot? The current PNG will be saved as a new snapshot first. Unsaved changes in your image editor are not included.", "Restore texture", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            await Task.Run(() => TextureSnapshots.Restore(bridgePath, session, layerId, selected));
            await Reload(); previous.Source = current.Source = null;
            status.Text = "PNG restored. Allow VRoid to sync, then save the .vroid. Your editor may still show its earlier buffer.";
        });
        panel.Children.Add(actions); panel.Children.Add(entries);
        var previews = new Grid { Margin = new Thickness(0, 12, 0, 0) };
        previews.ColumnDefinitions.Add(new ColumnDefinition()); previews.ColumnDefinitions.Add(new ColumnDefinition());
        previews.Children.Add(previous); Grid.SetColumn(current, 1); previews.Children.Add(current);
        panel.Children.Add(previews); panel.Children.Add(status);
        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Loaded += async (_, _) => await Run(Reload);
        Closing += (_, e) => { if (busy) e.Cancel = true; };
        Closed += (_, _) => previous.Source = current.Source = null;
    }

    private BridgeSnapshot ReadCurrent()
    {
        var bridge = SnapshotFile.Read(bridgePath);
        if (bridge.DocumentSession != session) throw new InvalidOperationException("The document changed. Close history and select the new layer.");
        return bridge;
    }
    private string Selected() => (entries.SelectedItem as TextureSnapshot)?.Id ?? throw new InvalidOperationException("Select a snapshot first.");
    private async Task Reload()
    {
        entries.ItemsSource = await Task.Run(() => TextureSnapshots.List(ReadCurrent(), layerId));
        if (entries.Items.Count == 0) status.Text = "No snapshots for this linked session. Save one before editing.";
    }
    private void AddButton(string label, Func<Task> action)
    {
        var button = new Button { Content = label, Margin = new Thickness(0, 0, 8, 8), Padding = new Thickness(10) };
        button.Click += async (_, _) => await Run(action); actions.Children.Add(button);
    }
    private async Task Run(Func<Task> action)
    {
        if (busy) return;
        busy = true; actions.IsEnabled = entries.IsEnabled = name.IsEnabled = false;
        try { await action(); }
        catch (Exception error) { status.Text = error.Message; }
        finally { busy = false; actions.IsEnabled = entries.IsEnabled = name.IsEnabled = true; }
    }
    private static BitmapImage Preview(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > TextureSnapshots.MaximumBytes) throw new InvalidDataException("PNG exceeds 64 MiB.");
        Span<byte> header = stackalloc byte[24];
        if (stream.Read(header) != 24) throw new InvalidDataException("PNG is incomplete.");
        uint width = BinaryPrimitives.ReadUInt32BigEndian(header[16..20]), height = BinaryPrimitives.ReadUInt32BigEndian(header[20..24]);
        if (width is 0 or > 4096 || height is 0 or > 4096) throw new InvalidDataException("PNG dimensions exceed the preview limit.");
        stream.Position = 0;
        var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
        if (width >= height) bitmap.DecodePixelWidth = (int)Math.Min(width, 512u);
        else bitmap.DecodePixelHeight = (int)Math.Min(height, 512u);
        bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze();
        return bitmap;
    }
}
