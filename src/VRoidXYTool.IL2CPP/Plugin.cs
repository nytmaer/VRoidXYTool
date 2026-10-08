#nullable disable
using BepInEx;
using BepInEx.Unity.IL2CPP;
using UnityEngine;

namespace VRoidXYTool.IL2CPP;

// Separate identity prevents accidental coexistence with the legacy plugin's patches.
[BepInPlugin(Id, "VRoidXYTool IL2CPP Bootstrap", "0.1.0")]
[BepInProcess("VRoidStudio.exe")]
public sealed class Plugin : BasePlugin
{
    public const string Id = "io.github.nytmaer.vroidxytool.il2cpp";
    private bool stopped;
    private BootstrapLifecycle lifecycle;

    public override void Load()
    {
        stopped = false;
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        lifecycle = AddComponent<BootstrapLifecycle>();
        lifecycle.Owner = this;
        lifecycle.QuitAfterSeconds = Config.Bind("Diagnostics", "QuitAfterSeconds", 0,
            "Test-only automatic normal application exit; 0 disables it.").Value;
        if (lifecycle.QuitAfterSeconds > 0) Application.runInBackground = true;
        Log.LogInfo($"Bootstrap initialized; CLR {Environment.Version}; process {Environment.ProcessId}.");
        Log.LogInfo("Texture integration is not enabled. This bootstrap does not modify projects.");
    }

    public override bool Unload()
    {
        Stop();
        if (lifecycle != null) UnityEngine.Object.Destroy(lifecycle);
        AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
        return true;
    }

    private void OnProcessExit(object sender, EventArgs args) => Stop();

    internal void Stop()
    {
        if (stopped) return;
        stopped = true;
        Log.LogInfo("Bootstrap shutdown completed.");
    }
}

public sealed class BootstrapLifecycle : MonoBehaviour
{
    public BootstrapLifecycle(IntPtr pointer) : base(pointer) { }
    internal Plugin Owner;
    internal int QuitAfterSeconds;
    private readonly System.Diagnostics.Stopwatch elapsed = System.Diagnostics.Stopwatch.StartNew();

    public void Update()
    {
        if (QuitAfterSeconds > 0 && elapsed.Elapsed.TotalSeconds >= QuitAfterSeconds)
        {
            QuitAfterSeconds = 0;
            Application.Quit();
        }
    }

    public void OnApplicationQuit() => Owner?.Stop();
}
