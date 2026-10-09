#nullable disable
using BepInEx;
using BepInEx.Unity.IL2CPP;
using UnityEngine;

namespace VRoidXYTool.IL2CPP;

// Separate identity prevents accidental coexistence with the legacy plugin's patches.
[BepInPlugin(Id, "VRoidXYTool Live Texture Prototype", "0.2.0")]
[BepInProcess("VRoidStudio.exe")]
public sealed class Plugin : BasePlugin
{
    public const string Id = "io.github.nytmaer.vroidxytool.il2cpp";
    private bool stopped;
    private BootstrapLifecycle lifecycle;
    internal VRoid214Bridge Bridge;
    internal DocumentDiagnostic Diagnostic;

    public override void Load()
    {
        stopped = false;
        lifecycle = AddComponent<BootstrapLifecycle>();
        BootstrapLifecycle.Owner = this;
        BootstrapLifecycle.QuitAfterSeconds = Config.Bind("Diagnostics", "QuitAfterSeconds", 0,
            "Test-only automatic normal application exit; 0 disables it.").Value;
        if (BootstrapLifecycle.QuitAfterSeconds > 0) Application.runInBackground = true;
        lifecycle.enabled = true;
        lifecycle.gameObject.SetActive(true);
        Log.LogInfo($"Lifecycle component active={lifecycle.gameObject.activeInHierarchy}, enabled={lifecycle.enabled}.");
        Log.LogInfo($"Bootstrap initialized; CLR {Environment.Version}; process {Environment.ProcessId}.");
        if (Config.Bind("TextureSync", "Enabled", true, "Enable the experimental VRoid 2.14.0 texture bridge.").Value)
        {
            if (Application.version == "2.14.0")
            {
                try
                {
                    Bridge = new VRoid214Bridge(Log, Config.Bind("TextureSync", "Directory",
                        Path.Combine(Paths.GameRootPath, "LinkTextureIL2CPP"), "Directory for exported PNG files.").Value,
                        Config.Bind("TextureSync", "ShowControlsOnStartup", true, "Show the texture control panel when the plugin loads.").Value,
                        Config.Bind("Companion", "StatePath", Path.Combine(Paths.GameRootPath, "Companion", "bridge-state.json"),
                            "Local state file for the second-monitor Companion.").Value,
                        Config.Bind("TextureSync", "PollMilliseconds", 250, "Polling interval, clamped to 100–5000 milliseconds. A file must settle before import.").Value,
                        Config.Bind("TextureSync", "GroupByModel", false, "Group export sessions in stable model folders. Session isolation remains active.").Value);
                }
                catch (Exception error) { Log.LogError($"Texture bridge disabled: {error}"); }
            }
            else Log.LogWarning($"Texture bridge requires VRoid 2.14.0; detected {Application.version}.");
        }
        string diagnosticPath = Config.Bind("Diagnostics", "ModelPath", "", "Opt-in disposable document test; only .local/test-models paths accepted.").Value;
        if (Bridge != null && !string.IsNullOrWhiteSpace(diagnosticPath))
        {
            try { Diagnostic = new DocumentDiagnostic(this, Log, diagnosticPath, Config.Bind("Diagnostics", "OpenOnly", false, "Open the disposable model for manual workspace acceptance without executing texture edits.").Value); Application.runInBackground = true; }
            catch (Exception error) { Log.LogError(error); }
        }
    }

    public override bool Unload()
    {
        Stop();
        if (lifecycle != null) UnityEngine.Object.Destroy(lifecycle);
        return true;
    }

    internal void Stop()
    {
        if (stopped) return;
        stopped = true;
        Bridge?.Dispose();
        Bridge = null;
        Diagnostic = null;
        if (BootstrapLifecycle.Owner == this) BootstrapLifecycle.Owner = null;
        Log.LogInfo("Bootstrap shutdown completed.");
    }
}

public sealed class BootstrapLifecycle : MonoBehaviour
{
    public BootstrapLifecycle(IntPtr pointer) : base(pointer) { }
    internal static Plugin Owner;
    internal static int QuitAfterSeconds;
    private static readonly System.Diagnostics.Stopwatch elapsed = System.Diagnostics.Stopwatch.StartNew();
    private static bool tickLogged;
    private static bool guiLogged;

    public void Update()
    {
        if (!tickLogged) { tickLogged = true; Owner?.Log.LogInfo("Unity lifecycle Update callback confirmed."); }
        Owner?.Bridge?.Update();
        Owner?.Diagnostic?.Update();
        if (QuitAfterSeconds > 0 && elapsed.Elapsed.TotalSeconds >= QuitAfterSeconds)
        {
            QuitAfterSeconds = 0;
            Application.Quit();
        }
    }

    public void OnApplicationQuit() => Owner?.Stop();
    public void LateUpdate() => Owner?.Bridge?.LateUpdate();
    public void OnGUI()
    {
        if (!guiLogged) { guiLogged = true; Owner?.Log.LogInfo("Unity lifecycle OnGUI callback confirmed."); }
        Owner?.Bridge?.OnGUI();
    }
}
