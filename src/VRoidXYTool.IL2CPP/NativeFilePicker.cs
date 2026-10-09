#nullable disable
using VRoid.Studio.Util;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace VRoidXYTool.IL2CPP;

// Poll native tasks on Unity's thread; never run scene operations on continuations.
internal sealed class NativeFilePicker
{
    private Il2CppSystem.Threading.Tasks.Task<Il2CppStringArray> task;
    private Action<string> completed;
    private Action<Exception> failed;
    private string[] allowedExtensions;
    private static readonly BepInEx.Logging.ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("VRoidXYTool File Picker");
    internal bool Busy => task != null;
    internal void Open(string title, string[] extensions, Action<string> completion, Action<Exception> failure)
    {
        if (Busy) throw new InvalidOperationException("A file picker is already open.");
        var filters = new Il2CppSystem.Collections.Generic.List<FileDialogCommonUtil.ExtensionFilter>();
        // ExtensionFilter is a boxed native value type. Passing it through the generated
        // generic List.Add loses its array field in this runtime. Use the native all-files
        // dialog and enforce the requested extensions before dispatching a selection.
        allowedExtensions = extensions.Select(x => "." + x.TrimStart('.')).ToArray();
        task = OpenFileDialogUtil.OpenFilePanel(title, BepInEx.Paths.GameRootPath, filters.Cast<Il2CppSystem.Collections.Generic.IEnumerable<FileDialogCommonUtil.ExtensionFilter>>(), false);
        completed = completion; failed = failure;
    }
    internal void Update()
    {
        if (task == null || !task.IsCompleted) return;
        var result = task; var callback = completed; var errorCallback = failed;
        task = null; completed = null; failed = null;
        try
        {
            if (result.IsCanceled) return;
            if (result.IsFaulted) throw new IOException("File picker failed: " + result.Exception.InnerException?.Message + "\n" + result.Exception.InnerException?.StackTrace);
            var paths = result.Result;
            if (paths != null && paths.Length > 0 && !string.IsNullOrWhiteSpace(paths[0]))
            {
                if (!allowedExtensions.Contains(Path.GetExtension(paths[0]), StringComparer.OrdinalIgnoreCase))
                    throw new InvalidDataException("Choose a file ending in " + string.Join(" or ", allowedExtensions) + ".");
                callback(paths[0]);
            }
        }
        catch (Exception error)
        {
            Log.LogError(error);
            errorCallback(error is InvalidDataException ? error : new IOException("File could not be opened. See the BepInEx log for details."));
        }
    }
    internal void CancelPending() { completed = _ => { }; }
}
