#nullable disable
using System.Diagnostics;
using System.Security.Cryptography;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;
using VRoid.Studio;
using VRoidCore.Common.SpecificTypes;
using VRoidCore.Editing.Query;
using VRoidStudio.GUI.AvatarEditor;

namespace VRoidXYTool.IL2CPP;

// Explicit, local-only integration test. Never opens or saves arbitrary original documents.
internal sealed class DocumentDiagnostic
{
    private readonly string modelPath;
    private readonly ManualLogSource log;
    private readonly Plugin plugin;
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly string output = Path.Combine(Paths.GameRootPath, "diagnostic");
    private Il2CppSystem.Threading.Tasks.Task opening;
    private EditableImageRasterLayerPath path;
    private EditableImageRasterLayerPath otherPath;
    private string otherBaseline;
    private string pngPath;
    private string baseline;
    private string expected;
    private int stage;
    private int saves;
    private TimeSpan next;
    private bool complete;

    internal DocumentDiagnostic(Plugin plugin, ManualLogSource log, string requestedPath)
    {
        this.plugin = plugin;
        this.log = log;
        modelPath = Path.GetFullPath(requestedPath);
        string allowed = Path.GetFullPath(Path.Combine(Paths.GameRootPath, "..", "test-models")) + Path.DirectorySeparatorChar;
        if (!modelPath.StartsWith(allowed, StringComparison.OrdinalIgnoreCase) || Path.GetExtension(modelPath) != ".vroid")
            throw new InvalidOperationException("Diagnostic model must be a .vroid inside .local/test-models.");
        if (!File.Exists(modelPath)) throw new FileNotFoundException("Diagnostic copy missing.");
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "result.txt"), "RUNNING");
        next = TimeSpan.FromSeconds(5);
    }

    internal void Update()
    {
        if (complete || clock.Elapsed < next) return;
        next = clock.Elapsed + TimeSpan.FromMilliseconds(500);
        try
        {
            if (clock.Elapsed > TimeSpan.FromMinutes(3)) throw new TimeoutException("Document diagnostic timed out at stage " + stage);
            var avatar = VRoid214Bridge.FindAvatar();
            var main = avatar?._viewModel;
            if (main == null) return;
            if (stage == 0)
            {
                log.LogInfo("DIAGNOSTIC: opening disposable project copy.");
                opening = main.ActionHandler.Open(modelPath, false);
                stage = 1;
                return;
            }
            if (!opening.IsCompleted) return;
            if (opening.IsFaulted) throw new InvalidOperationException("Document open failed: " + opening.Exception);
            var document = main.CurrentFile?.model;
            if (document == null || document.engine.Disposed) return;
            if (!string.Equals(Path.GetFullPath(document.path), modelPath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Diagnostic current document is not the disposable copy.");
            var context = document.engine.Context;
            if (!context.IsQueryExecutable || !context.IsCommandExecutable) return;

            if (stage == 1)
            {
                var keys = context.ActiveModel._transferables.Keys.GetEnumerator();
                while (keys.MoveNext() && otherPath == null)
                {
                    var images = new GetEditableImagePathsForTransferableQuery(keys.Current).Execute(context.ActiveModel);
                    foreach (var image in images)
                    {
                        var layers = new GetEditableImageRasterLayersQuery(image).Execute(context.ActiveModel);
                        foreach (var layer in layers)
                        {
                            if (path == null) path = layer;
                            else if (layer.NodeId != path.NodeId || layer.EditableImagePath.ToString() != path.EditableImagePath.ToString())
                            { otherPath = layer; break; }
                        }
                        if (otherPath != null) break;
                    }
                }
                if (path == null) throw new InvalidOperationException("No raster layer found in diagnostic model.");
                if (otherPath == null) throw new InvalidOperationException("No second raster layer available for isolation assertion.");
                baseline = PixelsHash(context);
                if (otherPath != null) otherBaseline = PixelsHash(context, otherPath);
                pngPath = plugin.Bridge.ExportPath(path, "diagnostic layer");
                if (pngPath == null) throw new InvalidOperationException("Export did not create a file.");
                log.LogInfo("DIAGNOSTIC: raster export PASS; PNG=" + pngPath);
                stage = 2;
            }
            else if (stage == 2)
            {
                CreateExternalSave();
                stage = 3;
            }
            else if (stage == 3)
            {
                string actual = PixelsHash(context);
                if (actual == baseline) return;
                AssertMarker(context);
                if (otherPath != null && PixelsHash(context, otherPath) != otherBaseline)
                    throw new InvalidOperationException("Unlinked layer pixels changed.");
                expected = actual;
                saves++;
                log.LogInfo("DIAGNOSTIC: external file save " + saves + " imported PASS.");
                baseline = actual;
                if (saves < 5) stage = 2;
                else
                {
                    if (!main.ActionHandler.SaveSync(modelPath, false)) throw new InvalidOperationException("SaveSync failed.");
                    log.LogInfo("DIAGNOSTIC: project saved; reopening copy.");
                    opening = main.ActionHandler.Open(modelPath, false);
                    stage = 4;
                }
            }
            else if (stage == 4)
            {
                if (PixelsHash(context) != expected) throw new InvalidOperationException("Saved raster pixels changed after reopen.");
                log.LogInfo("DIAGNOSTIC: save/reopen pixel persistence PASS.");
                CreateExternalSave();
                next = clock.Elapsed + TimeSpan.FromSeconds(4);
                stage = 5;
            }
            else if (stage == 5)
            {
                if (PixelsHash(context) != expected) throw new InvalidOperationException("Old link imported into reopened document.");
                if (otherPath != null && PixelsHash(context, otherPath) != otherBaseline)
                    throw new InvalidOperationException("Unlinked layer changed after reopen.");
                log.LogInfo("DIAGNOSTIC: old-link invalidation and unlinked-layer isolation PASS.");
                File.WriteAllText(Path.Combine(output, "result.txt"), "PASS: export, five externally saved PNG imports with marker verification, save/reopen pixel persistence, old-link invalidation, unlinked-layer isolation.");
                complete = true;
                Application.Quit();
            }
        }
        catch (Exception error)
        {
            log.LogError("DIAGNOSTIC FAILED: " + error);
            File.WriteAllText(Path.Combine(output, "result.txt"), "FAIL: " + error);
            complete = true;
            Application.Quit();
        }
    }

    private string PixelsHash(VRoidCore.Editing.Context context, EditableImageRasterLayerPath target = null)
    {
        var query = new GetRasterLayerContentQuery(target ?? path).Cast<ISyncQuery<GetRasterLayerContentQuery.Result>>();
        return Convert.ToHexString(SHA256.HashData(context.ExecuteSyncQuery<GetRasterLayerContentQuery.Result>(query).rgbaBytes.ToArray()));
    }

    private void AssertMarker(VRoidCore.Editing.Context context)
    {
        Texture2D image = null;
        try
        {
            var result = context.ExecuteSyncQuery<GetRasterLayerContentQuery.Result>(new GetRasterLayerContentQuery(path).Cast<ISyncQuery<GetRasterLayerContentQuery.Result>>());
            image = new Texture2D(1, 1, TextureFormat.RGBA32, false, false);
            if (!ImageConversion.LoadImage(image, VRoid.Studio.Util.ImageEncodingUtil.EncodeToPNG(result.size, result.rgbaBytes), false))
                throw new InvalidDataException("Imported raster failed roundtrip decode.");
            var color = image.GetPixel(0, 0);
            if (Math.Abs(color.r - (saves + 1) / 8f) > 1f / 255 || Math.Abs(color.g - 0.25f) > 1f / 255 || Math.Abs(color.b - 0.75f) > 1f / 255 || color.a < 0.99f)
                throw new InvalidOperationException("Imported marker pixels do not match externally saved PNG.");
        }
        finally { if (image != null) UnityEngine.Object.Destroy(image); }
    }

    private void CreateExternalSave()
    {
        Texture2D image = null;
        try
        {
            image = new Texture2D(1, 1, TextureFormat.RGBA32, false, false);
            if (!ImageConversion.LoadImage(image, File.ReadAllBytes(pngPath), false)) throw new InvalidDataException("Exported PNG failed decode.");
            image.SetPixel(0, 0, new UnityEngine.Color((saves + 1) / 8f, 0.25f, 0.75f, 1f));
            image.Apply();
            // Produce a fixture only. The shell driver performs the external file save.
            string candidate = Path.Combine(output, "candidate.png");
            File.WriteAllBytes(candidate, ImageConversion.EncodeToPNG(image).ToArray());
            File.WriteAllText(Path.Combine(output, "request.txt"), pngPath);
            log.LogInfo("DIAGNOSTIC: waiting for external save " + (saves + 1) + ".");
        }
        finally { if (image != null) UnityEngine.Object.Destroy(image); }
    }
}
