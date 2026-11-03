using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using VRoidXYTool.CompanionCore;

namespace VRoidXYTool.Companion;

internal sealed class CharacterProjectWindow : Window
{
    private readonly Func<string?> bridgeLocation;
    private readonly Action<string> remember;
    private readonly TextBox projectName = new() { MaxLength = 100 };
    private readonly TextBox notes = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MaxLength = 65536, Height = 150, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly TextBlock model = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly ListBox assets = new() { DisplayMemberPath = "Label", Height = 160 };
    private readonly ComboBox linkedLayers = new() { DisplayMemberPath = "Name", MinWidth = 200 };
    private readonly StackPanel body = new() { Margin = new Thickness(20) };
    private CharacterProject? project;
    private string? manifest, boundSession, boundBridge;
    private bool busy, dirty;

    public CharacterProjectWindow(string? initialPath, Func<string?> bridgePath, Action<string> saveProjectPath)
    {
        bridgeLocation = bridgePath; remember = saveProjectPath;
        Title = "Character project"; Width = 900; Height = 820; MinWidth = 650; MinHeight = 550;
        body.Children.Add(new TextBlock { Text = "Keep a model reference, image assets and notes together. The dashboard never edits your .vroid file.", TextWrapping = TextWrapping.Wrap });
        var projects = new WrapPanel();
        AddButton(projects, "New project", Create); AddButton(projects, "Open project", Open);
        body.Children.Add(projects);
        Label("Project name", projectName); Label("Associated model", model);
        var settings = new WrapPanel();
        AddButton(settings, "Locate / change model", ChangeModel); AddButton(settings, "Save notes and name", Save);
        body.Children.Add(settings); Label("Notes", notes); Label("Stored images", assets);
        var files = new WrapPanel();
        AddButton(files, "Add reference images", AddImages); AddButton(files, "Open selected image", OpenImage);
        body.Children.Add(files);
        Label("Archive a linked texture from VRoid", linkedLayers);
        var textureActions = new WrapPanel();
        AddButton(textureActions, "Confirm current model session", BindSession);
        AddButton(textureActions, "Archive selected linked PNG", Archive);
        body.Children.Add(textureActions);
        body.Children.Add(new TextBlock { Text = "Session confirmation expires when VRoid changes documents. Archives are independent copies; they do not restore or relink layers automatically.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) });
        body.Children.Add(status);
        Content = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        projectName.TextChanged += (_, _) => dirty = true;
        notes.TextChanged += (_, _) => dirty = true;
        Closing += (_, e) => { if (busy || !CanDiscard()) e.Cancel = true; };
        Loaded += async (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(initialPath)) await Run(() => Load(initialPath));
            else status.Text = "Create or open a project to begin. Projects also work while VRoid is offline.";
        };
    }

    private void Label(string text, UIElement control)
    {
        body.Children.Add(new TextBlock { Text = text, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 12, 0, 6) });
        body.Children.Add(control);
    }
    private void AddButton(Panel parent, string text, Func<Task> action)
    {
        var button = new Button { Content = text, Margin = new Thickness(0, 8, 8, 0), Padding = new Thickness(10, 6, 10, 6) };
        button.Click += async (_, _) => await Run(action); parent.Children.Add(button);
    }
    private async Task Run(Func<Task> action)
    {
        if (busy) return;
        busy = true; body.IsEnabled = false;
        try { await action(); }
        catch (Exception error) { status.Text = error.Message; }
        finally { busy = false; body.IsEnabled = true; }
    }
    private bool CanDiscard() => !dirty || MessageBox.Show(this, "Discard unsaved project name or notes?", "Unsaved project", MessageBoxButton.YesNo) == MessageBoxResult.Yes;
    private void RequireProject()
    {
        if (project == null || manifest == null) throw new InvalidOperationException("Create or open a project first.");
    }
    private async Task Create()
    {
        if (!CanDiscard()) return;
        var modelPicker = new OpenFileDialog { Title = "Choose the model this project belongs to", Filter = "VRoid model (*.vroid)|*.vroid" };
        if (modelPicker.ShowDialog(this) != true) return;
        var folderPicker = new OpenFolderDialog { Title = "Choose a project folder" };
        if (folderPicker.ShowDialog(this) != true) return;
        string path = Path.Combine(folderPicker.FolderName, "character-project.json");
        var created = await Task.Run(() => CharacterProject.Create(path, modelPicker.FileName, Path.GetFileNameWithoutExtension(modelPicker.FileName)));
        SetProject(path, created);
        status.Text = "Project created. Your model remains in its original location.";
    }
    private async Task Open()
    {
        if (!CanDiscard()) return;
        var picker = new OpenFileDialog { Title = "Open character project", Filter = "Character project (*.json)|*.json" };
        if (picker.ShowDialog(this) == true) await Load(picker.FileName);
    }
    private async Task Load(string path)
    {
        var loaded = await Task.Run(() => CharacterProject.Load(path)); SetProject(path, loaded);
        status.Text = "Project loaded. Confirm the current VRoid model session before archiving linked textures.";
    }
    private void SetProject(string path, CharacterProject value)
    {
        manifest = Path.GetFullPath(path); project = value;
        projectName.Text = value.Name; notes.Text = value.Notes; dirty = false;
        boundSession = boundBridge = null; linkedLayers.ItemsSource = null;
        Refresh(); remember(manifest);
    }
    private void Refresh()
    {
        RequireProject();
        string modelPath = project!.ResolveModel(manifest!);
        model.Text = (File.Exists(modelPath) ? "" : "Missing model — locate it: ") + modelPath;
        assets.ItemsSource = project.Assets.Select(x => new AssetRow(x.Id, x.Kind + " · " + x.Name +
            (File.Exists(project.ResolveAsset(manifest!, x.Id)) ? "" : " · missing"))).ToArray();
    }
    private async Task Save()
    {
        RequireProject();
        string newName = projectName.Text.Trim(), newNotes = notes.Text;
        var updated = await Task.Run(() => CharacterProject.Load(manifest!)); updated.Name = newName; updated.Notes = newNotes;
        await Task.Run(() => updated.Save(manifest!)); project = updated; dirty = false;
        Refresh(); status.Text = "Name and notes saved.";
    }
    private async Task ChangeModel()
    {
        RequireProject();
        var picker = new OpenFileDialog { Title = "Locate the project's .vroid model", Filter = "VRoid model (*.vroid)|*.vroid" };
        if (picker.ShowDialog(this) != true) return;
        await Task.Run(() => project!.AssociateModel(manifest!, picker.FileName));
        boundSession = boundBridge = null; linkedLayers.ItemsSource = null; Refresh();
        status.Text = "Model association updated. Stored images were preserved; confirm the VRoid session again.";
    }
    private async Task AddImages()
    {
        RequireProject();
        var picker = new OpenFileDialog { Title = "Copy reference images into this project", Filter = "Images|*.png;*.jpg;*.jpeg", Multiselect = true };
        if (picker.ShowDialog(this) != true) return;
        int added = 0;
        try
        {
            foreach (string path in picker.FileNames) { await Task.Run(() => project!.AddAsset(manifest!, path)); added++; }
            status.Text = $"Copied {added} reference images into the project.";
        }
        catch (Exception error) { status.Text = $"Added {added} images. Remaining import stopped: {error.Message}"; }
        finally { Refresh(); }
    }
    private Task OpenImage()
    {
        RequireProject();
        string id = (assets.SelectedItem as AssetRow)?.Id ?? throw new InvalidOperationException("Select a stored image first.");
        string path = project!.ResolveAsset(manifest!, id);
        if (!File.Exists(path)) throw new FileNotFoundException("Stored image is missing. Restore it in the project's Assets folder.", path);
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); return Task.CompletedTask;
    }
    private Task BindSession()
    {
        RequireProject();
        string path = bridgeLocation() ?? throw new InvalidOperationException("Choose the live VRoid bridge in the Companion first.");
        if (!File.Exists(project!.ResolveModel(manifest!))) throw new InvalidOperationException("Locate the project's missing model before confirming a session.");
        var bridge = SnapshotFile.Read(path);
        if (!SnapshotFile.IsLive(bridge, DateTimeOffset.UtcNow) || string.IsNullOrWhiteSpace(bridge.DocumentSession))
            throw new InvalidOperationException("Open the model in VRoid and link a texture first.");
        if (MessageBox.Show(this, "Is the model currently open in VRoid the model associated with this project?\n\n" + project!.ResolveModel(manifest!) + "\n\nThe bridge does not report the model file path. Confirm only after checking VRoid.", "Confirm model session", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return Task.CompletedTask;
        boundSession = bridge.DocumentSession; boundBridge = path; linkedLayers.ItemsSource = bridge.Layers;
        status.Text = "Current session associated for archiving. Select a linked layer."; return Task.CompletedTask;
    }
    private async Task Archive()
    {
        RequireProject();
        if (boundSession == null || boundBridge == null || boundBridge != bridgeLocation()) throw new InvalidOperationException("Confirm the current model session first.");
        string id = (linkedLayers.SelectedItem as LinkedTextureSnapshot)?.Id ?? throw new InvalidOperationException("Select a linked layer first.");
        string path = boundBridge, session = boundSession;
        await Task.Run(() => project!.ArchiveLinkedTexture(manifest!, path, session, id));
        Refresh(); status.Text = "Linked PNG archived as an independent project image. Your live link was unchanged.";
    }
    private sealed record AssetRow(string Id, string Label);
}
