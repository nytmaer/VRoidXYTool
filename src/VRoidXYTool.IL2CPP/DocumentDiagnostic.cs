#nullable disable
using System.Diagnostics;
using System.Security.Cryptography;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;
using VRoid.Studio;
using VRoidCore.Common.SpecificTypes;
using VRoidCore.Editing.Query;
using VRoidCore.Editing;
using VRoidCore.Editing.History.Command;
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
    private bool historyFinished;
    private string historyFailure;
    private byte[] exportedBaseline;
    private float expectedBlue;

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
                exportedBaseline = File.ReadAllBytes(pngPath);
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
                    // Keep the first link alive while editing a second structural layer.
                    var first = path;
                    path = otherPath;
                    otherPath = first;
                    otherBaseline = expected;
                    baseline = PixelsHash(context);
                    pngPath = plugin.Bridge.ExportPath(path, "second diagnostic layer");
                    exportedBaseline = File.ReadAllBytes(pngPath);
                    File.WriteAllBytes(Path.Combine(output, "candidate.png"), new byte[] { 1, 2, 3, 4 });
                    File.WriteAllText(Path.Combine(output, "request.txt"), pngPath);
                    next = clock.Elapsed + TimeSpan.FromSeconds(4);
                    stage = 6;
                }
            }
            else if (stage == 6)
            {
                if (PixelsHash(context) != baseline || PixelsHash(context, otherPath) != otherBaseline)
                    throw new InvalidOperationException("Invalid PNG changed document pixels.");
                log.LogInfo("DIAGNOSTIC: invalid PNG rejection PASS; testing valid recovery with two links.");
                CreateExternalSave();
                stage = 7;
            }
            else if (stage == 7)
            {
                if (PixelsHash(context) == baseline) return;
                AssertMarker(context);
                if (PixelsHash(context, otherPath) != otherBaseline) throw new InvalidOperationException("First linked layer changed during second-layer import.");
                expected = PixelsHash(context);
                log.LogInfo("DIAGNOSTIC: simultaneous links and valid PNG recovery PASS.");
                context.FlushPendingHistory(false);
                if (!context.CanUndo) throw new InvalidOperationException("Imported edit has no undo history.");
                historyFinished = false;
                context.UndoAsync((Il2CppSystem.Action)(() => historyFinished = true),
                    (Il2CppSystem.Action<Il2CppSystem.Exception>)(e => { historyFailure = e.ToString(); historyFinished = true; }));
                stage = 8;
            }
            else if (stage == 8)
            {
                if (!historyFinished) return;
                if (historyFailure != null) throw new InvalidOperationException(historyFailure);
                if (PixelsHash(context) != baseline) throw new InvalidOperationException("Undo did not restore previous raster pixels.");
                if (!context.CanRedo) throw new InvalidOperationException("Imported edit cannot be redone.");
                historyFinished = false;
                context.RedoAsync((Il2CppSystem.Action)(() => historyFinished = true),
                    (Il2CppSystem.Action<Il2CppSystem.Exception>)(e => { historyFailure = e.ToString(); historyFinished = true; }));
                stage = 9;
            }
            else if (stage == 9)
            {
                if (!historyFinished) return;
                if (historyFailure != null) throw new InvalidOperationException(historyFailure);
                if (PixelsHash(context) != expected) throw new InvalidOperationException("Redo did not restore imported raster pixels.");
                log.LogInfo("DIAGNOSTIC: native undo/redo pixel roundtrip PASS.");
                if (!main.ActionHandler.SaveSync(modelPath, false)) throw new InvalidOperationException("SaveSync failed.");
                opening = main.ActionHandler.Open(modelPath, false);
                stage = 4;
            }
            else if (stage == 4)
            {
                if (PixelsHash(context) != expected) throw new InvalidOperationException("Saved raster pixels changed after reopen.");
                log.LogInfo("DIAGNOSTIC: save/reopen pixel persistence PASS.");
                saves = 6; // The old-link write must differ from the sixth imported marker.
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
                pngPath = plugin.Bridge.ExportPath(path, "deleted-layer diagnostic");
                exportedBaseline = File.ReadAllBytes(pngPath);
                context.ExecuteSyncCommand(new DeleteEditableImageRasterLayerCommand(path).Cast<ISyncCommand<Context.HistoryManagerContext, Model>>());
                context.FlushPendingHistory(false);
                saves = 6;
                CreateExternalSave();
                next = clock.Elapsed + TimeSpan.FromSeconds(4);
                stage = 10;
            }
            else if (stage == 10)
            {
                foreach (var layer in new GetEditableImageRasterLayersQuery(path.EditableImagePath).Execute(context.ActiveModel))
                    if (layer.NodeId == path.NodeId) throw new InvalidOperationException("Deleted linked layer was recreated by import.");
                if (PixelsHash(context, otherPath) != otherBaseline) throw new InvalidOperationException("Deleted link redirected to another layer.");
                plugin.Bridge.ClearLinks();
                historyFinished = false;
                context.UndoAsync((Il2CppSystem.Action)(() => historyFinished = true),
                    (Il2CppSystem.Action<Il2CppSystem.Exception>)(e => { historyFailure = e.ToString(); historyFinished = true; }));
                stage = 11;
            }
            else if (stage == 11)
            {
                if (!historyFinished) return;
                if (historyFailure != null) throw new InvalidOperationException(historyFailure);
                if (PixelsHash(context) != expected) throw new InvalidOperationException("Undo of test deletion did not restore saved pixels.");
                log.LogInfo("DIAGNOSTIC: deleted-layer destination isolation PASS.");
                File.WriteAllText(Path.Combine(output, "result.txt"), "PASS: six imports, simultaneous links, invalid PNG recovery, undo/redo, persistence, document invalidation, deleted-layer isolation.");
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
            if (Math.Abs(color.r - (saves + 1) / 8f) > 1f / 255 || Math.Abs(color.g - 0.25f) > 1f / 255 || Math.Abs(color.b - expectedBlue) > 1f / 255 || color.a < 0.99f)
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
            if (!ImageConversion.LoadImage(image, exportedBaseline, false)) throw new InvalidDataException("Exported PNG failed decode.");
            // Ensure rerunning against an already marked disposable model still changes pixels.
            expectedBlue = image.GetPixel(0, 0).b > 0.5f ? 0.25f : 0.75f;
            image.SetPixel(0, 0, new UnityEngine.Color((saves + 1) / 8f, 0.25f, expectedBlue, 1f));
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
