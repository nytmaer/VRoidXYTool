#nullable disable
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using VRoid.Studio;
using VRoid.Studio.Util;
using VRoid.Studio.TextureEditor.Layer.ViewModel;
using VRoidCore.Common.SpecificTypes;
using VRoidCore.Draw.TiledBitmap;
using VRoidCore.Editing;
using VRoidCore.Editing.Query;
using VRoidCore.Editing.History.Command;
using VRoidXYTool.SyncCore;
using VRoidXYTool.CompanionCore;
using VRoidStudio.GUI.AvatarEditor;
using EditorVM = VRoid.Studio.TextureEditor.ViewModel;
using CoreModel = VRoidCore.Common.SpecificTypes.Model;

/// <summary>Version-specific adapter. Do not treat generated signatures as cross-version API.</summary>
namespace VRoidXYTool.IL2CPP;

internal sealed class VRoid214Bridge : IDisposable
{
    private static VRoid214Bridge active;
    private readonly ManualLogSource log;
    private readonly Harmony harmony = new(Plugin.Id + ".texture214");
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly string directory;
    private readonly string statePath;
    private readonly Dictionary<LayerIdentity, LinkedLayer> links = new();
    private CurrentFileModel document;
    private EditorVM editor;
    private string session;
    private TimeSpan nextPoll;
    private TimeSpan nextError;
    private TimeSpan nextSnapshot;
    private bool show;
    private bool disposed;
    private GameObject blockerCanvas;
    private GameObject blocker;
    private string exportedPath;
    private bool ownsBackgroundSetting;
    private bool originalBackgroundSetting;
    private GUIStyle panelStyle;
    private GUIStyle textStyle;
    private GUIStyle buttonStyle;
    private string status = "Open texture editing, select a raster layer, then link it.";

    public VRoid214Bridge(ManualLogSource log, string directory, bool showOnStartup = true, string statePath = null)
    {
        this.log = log;
        this.directory = Path.GetFullPath(directory);
        this.statePath = statePath;
        show = showOnStartup;
        active = this;
        try
        {
            // Native selection uses the setter; the update event alone misses normal UI selection.
            harmony.Patch(AccessTools.Method(typeof(EditorVM), "OnSelectedLayerUpdated"),
                postfix: new HarmonyMethod(typeof(VRoid214Bridge), nameof(SelectionUpdated)));
            harmony.Patch(AccessTools.PropertySetter(typeof(EditorVM), "SelectedLayer"),
                postfix: new HarmonyMethod(typeof(VRoid214Bridge), nameof(SelectionUpdated)));
            CreateInputBlocker();
            // Keep the Companion heartbeat current even before the first layer is linked.
            if (statePath != null)
            {
                originalBackgroundSetting = Application.runInBackground;
                ownsBackgroundSetting = true;
                Application.runInBackground = true;
            }
            log.LogInfo("VRoid 2.14 texture selection hook installed. Tab opens linked texture controls.");
            if (statePath != null) log.LogInfo("Companion bridge state: " + statePath);
        }
        catch
        {
            active = null;
            harmony.UnpatchSelf();
            if (blockerCanvas != null) UnityEngine.Object.Destroy(blockerCanvas);
            throw;
        }
    }

    private static void SelectionUpdated(EditorVM __instance)
    {
        if (active == null || active.disposed) return;
        active.editor = __instance;
    }

    public void Update()
    {
        if (disposed) return;
        try
        {
            if (BepInEx.UnityInput.Current.GetKeyDown(KeyCode.Tab))
            {
                show = !show;
                log.LogInfo("Texture controls visible: " + show);
            }
            if (blocker != null) blocker.SetActive(show);
            RefreshDocument();
            if (clock.Elapsed < nextPoll) return;
            nextPoll = clock.Elapsed + TimeSpan.FromMilliseconds(250);
            foreach (var link in links.Values.ToArray())
                link.File.Poll(clock.Elapsed, (_, bytes) => Import(link, bytes));
            if (clock.Elapsed >= nextSnapshot)
            {
                nextSnapshot = clock.Elapsed + TimeSpan.FromSeconds(1);
                PublishSnapshot(true);
            }
        }
        catch (Exception error) { Report(error); }
    }

    private void RefreshDocument()
    {
        var avatar = FindAvatar();
        var current = avatar == null ? null : avatar._viewModel?.CurrentFile?.model;
        if (current?.Pointer == document?.Pointer) return;
        ClearLinks();
        document = current;
        editor = null;
        session = current == null ? null : Guid.NewGuid().ToString("N");
        status = current == null ? "Open a model to link textures." : "Select a raster layer in texture editing.";
        log.LogInfo("Document changed; previous texture links invalidated.");
    }

    internal static AvatarEditor FindAvatar()
    {
        foreach (var candidate in UnityEngine.Object.FindObjectsOfType<AvatarEditor>(true))
            if (candidate != null && candidate._viewModel != null) return candidate;
        return null;
    }

    private bool HasCurrentLayer(out RasterLayerViewModel layer)
    {
        layer = editor?.SelectedLayer;
        return document != null && !document.engine.Disposed && layer != null && layer.IsValid &&
            layer._engine.Pointer == document.engine.Pointer;
    }

    public void OnGUI()
    {
        if (!show || disposed) return;
        // Explicit styles avoid changing VRoid's shared GUI skin and remain readable at high DPI.
        panelStyle ??= CopyStyle(GUI.skin.box, 20);
        textStyle ??= CopyStyle(GUI.skin.label, 18);
        buttonStyle ??= CopyStyle(GUI.skin.button, 18);
        var previousColor = GUI.color;
        GUI.color = new UnityEngine.Color(0.08f, 0.08f, 0.08f, 0.97f);
        GUI.DrawTexture(new Rect(20, 20, 780, 260), Texture2D.whiteTexture);
        GUI.color = previousColor;
        GUI.Box(new Rect(20, 20, 780, 260), "VRoidXYTool — Live Texture Sync (Tab to hide)", panelStyle);
        GUI.Label(new Rect(35, 55, 745, 55), status, textStyle);
        try
        {
            bool available = HasCurrentLayer(out var layer);
            GUI.Label(new Rect(35, 112, 745, 32), available ? "Selected: " + layer.TranslatedDisplayName : "Select a raster layer in texture editing.", textStyle);
            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && available;
            bool export = GUI.Button(new Rect(35, 150, 250, 42), "Link / export selected", buttonStyle);
            GUI.enabled = previousEnabled;
            if (export) Export(layer);
            if (GUI.Button(new Rect(300, 150, 180, 42), "Unlink all", buttonStyle))
            {
                ClearLinks();
                status = "Texture links cleared.";
            }
            GUI.Label(new Rect(500, 156, 270, 32), $"Linked layers: {links.Count}", textStyle);
            GUI.enabled = previousEnabled && exportedPath != null;
            bool copy = GUI.Button(new Rect(35, 210, 250, 42), "Copy linked PNG path", buttonStyle);
            GUI.enabled = previousEnabled;
            if (copy)
                GUIUtility.systemCopyBuffer = exportedPath;
            GUI.Label(new Rect(300, 210, 475, 52), "Save your .vroid to preserve imported edits.", textStyle);
        }
        catch (Exception error) { Report(error); }
    }

    private LayerIdentity Identity(EditableImageRasterLayerPath path) => new(session,
        path.EditableImagePath.TransferableId + "/" + path.EditableImagePath.ImageId, path.NodeId);

    private static GUIStyle CopyStyle(GUIStyle source, int fontSize)
    {
        // Unity strips the managed copy constructor; use its generated native copy operation.
        var style = new GUIStyle();
        GUIStyle.Internal_Destroy(style.m_Ptr);
        style.m_Ptr = GUIStyle.Internal_Copy(style, source);
        style.fontSize = fontSize;
        style.wordWrap = true;
        return style;
    }

    private void Export(RasterLayerViewModel layer)
    {
        RefreshDocument();
        if (!HasCurrentLayer(out var current) || current.Pointer != layer.Pointer) return;
        ExportPath(layer.Path, layer.TranslatedDisplayName);
    }

    internal string ExportPath(EditableImageRasterLayerPath path, string label)
    {
        var context = document.engine.Context;
        if (!context.IsQueryExecutable || !context.IsCommandExecutable) return null;
        var identity = Identity(path);
        var result = context.ExecuteSyncQuery<GetRasterLayerContentQuery.Result>(
            new GetRasterLayerContentQuery(path).Cast<ISyncQuery<GetRasterLayerContentQuery.Result>>());
        byte[] bytes = ImageEncodingUtil.EncodeToPNG(result.size, result.rgbaBytes).ToArray();
        string folder = Path.Combine(directory, session);
        Directory.CreateDirectory(folder);
        // Hash complete structural identity, never translated display names.
        string name = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity.Texture + "\0" + identity.Layer)));
        string filePath = Path.Combine(folder, name + ".png");
        File.WriteAllBytes(filePath, bytes);
        exportedPath = filePath;
        if (links.Remove(identity, out var previous)) previous.File.Dispose();
        links.Add(identity, new LinkedLayer(path, label,
            new SettledFileLink(identity, filePath, bytes, TimeSpan.FromMilliseconds(500))));
        if (!ownsBackgroundSetting)
        {
            originalBackgroundSetting = Application.runInBackground;
            ownsBackgroundSetting = true;
        }
        Application.runInBackground = true;
        status = "Linked " + label + ". PNG path is in BepInEx/LogOutput.log.";
        log.LogInfo($"Linked raster layer {identity.Texture}/{identity.Layer}: {filePath}");
        return filePath;
    }

    private bool Import(LinkedLayer link, byte[] bytes)
    {
        if (document == null || document.engine.Disposed || link.File.Identity != Identity(link.Path)) return false;
        var context = document.engine.Context;
        if (!context.IsCommandExecutable || !context.IsQueryExecutable) return false;
        Texture2D decoded = null;
        try
        {
            // Resolve the stored layer through this document before touching it. Deleted layers fail here.
            context.ExecuteSyncQuery<GetRasterLayerContentQuery.Result>(
                new GetRasterLayerContentQuery(link.Path).Cast<ISyncQuery<GetRasterLayerContentQuery.Result>>());
            if (!PngGuard.IsCompleteBoundedPng(bytes)) throw new InvalidDataException("PNG incomplete or exceeds 4096x4096 pixel limit.");
            decoded = new Texture2D(1, 1, TextureFormat.RGBA32, false, false);
            if (!ImageConversion.LoadImage(decoded, bytes, false)) throw new InvalidDataException("PNG decode failed.");
            var command = new LoadImageToEditableImageRasterLayerCommand(link.Path,
                new BitmapSize(decoded.width, decoded.height), decoded.GetPixels());
            context.ExecuteSyncCommand(command.Cast<ISyncCommand<Context.HistoryManagerContext, CoreModel>>());
            status = $"Updated linked layer at {DateTime.Now:T}.";
            log.LogInfo($"Imported linked raster layer {link.File.Identity.Texture}/{link.File.Identity.Layer}.");
            link.LastImportedUtc = DateTimeOffset.UtcNow;
            return true;
        }
        catch (Exception error) { Report(error); return false; }
        finally { if (decoded != null) UnityEngine.Object.Destroy(decoded); }
    }

    private void Report(Exception error)
    {
        status = "Texture operation deferred: " + error.Message;
        if (clock.Elapsed < nextError) return;
        nextError = clock.Elapsed + TimeSpan.FromSeconds(5);
        log.LogWarning(error);
    }

    internal void ClearLinks()
    {
        foreach (var link in links.Values) link.File.Dispose();
        links.Clear();
        exportedPath = null;
        if (ownsBackgroundSetting && (statePath == null || disposed))
        {
            Application.runInBackground = originalBackgroundSetting;
            ownsBackgroundSetting = false;
        }
    }

    private void PublishSnapshot(bool running)
    {
        if (statePath == null) return;
        try
        {
            SnapshotFile.Write(statePath, new BridgeSnapshot
            {
                PublishedUtc = DateTimeOffset.UtcNow, Running = running,
                ApplicationVersion = Application.version, DocumentSession = running ? session : null,
                ExportRoot = directory, Message = running ? status : "VRoid bridge stopped.",
                Layers = running ? links.Select(pair => new LinkedTextureSnapshot
                {
                    Id = pair.Key.Document + "/" + pair.Key.Texture + "/" + pair.Key.Layer,
                    Name = pair.Value.Label, PngPath = pair.Value.File.FilePath,
                    Status = pair.Value.File.Status, LastImportedUtc = pair.Value.LastImportedUtc
                }).ToList() : new List<LinkedTextureSnapshot>()
            });
        }
        catch (Exception error)
        {
            // Companion connectivity must never stop texture imports.
            if (clock.Elapsed >= nextError) { nextError = clock.Elapsed + TimeSpan.FromSeconds(5); log.LogWarning("Companion state write failed: " + error.Message); }
        }
    }

    private void CreateInputBlocker()
    {
        blockerCanvas = new GameObject("VRoidXYTool Texture Controls");
        UnityEngine.Object.DontDestroyOnLoad(blockerCanvas);
        var canvas = blockerCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;
        blockerCanvas.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        blocker = new GameObject("Texture Controls Input Blocker");
        var rect = blocker.AddComponent<RectTransform>();
        rect.SetParent(blockerCanvas.transform, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(20, -20);
        rect.sizeDelta = new Vector2(780, 260);
        var image = blocker.AddComponent<UnityEngine.UI.Image>();
        image.color = UnityEngine.Color.clear;
        image.raycastTarget = true;
        blocker.SetActive(false);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (active == this) active = null;
        harmony.UnpatchSelf();
        if (blockerCanvas != null) UnityEngine.Object.Destroy(blockerCanvas);
        ClearLinks();
        PublishSnapshot(false);
        editor = null;
        document = null;
    }

    private sealed class LinkedLayer
    {
        public readonly EditableImageRasterLayerPath Path;
        public readonly SettledFileLink File;
        public readonly string Label;
        public DateTimeOffset? LastImportedUtc;
        public LinkedLayer(EditableImageRasterLayerPath path, string label, SettledFileLink file) { Path = path; Label = label; File = file; }
    }
}
