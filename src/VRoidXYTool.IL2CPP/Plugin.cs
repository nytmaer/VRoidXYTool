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

    public override void Load()
    {
        stopped = false;
        lifecycle = AddComponent<BootstrapLifecycle>();
        lifecycle.Owner = this;
        lifecycle.QuitAfterSeconds = Config.Bind("Diagnostics", "QuitAfterSeconds", 0,
            "Test-only automatic normal application exit; 0 disables it.").Value;
        if (lifecycle.QuitAfterSeconds > 0) Application.runInBackground = true;
        Log.LogInfo($"Bootstrap initialized; CLR {Environment.Version}; process {Environment.ProcessId}.");
        if (Config.Bind("TextureSync", "Enabled", true, "Enable the experimental VRoid 2.14.0 texture bridge.").Value)
        {
            if (Application.version == "2.14.0")
            {
                try
                {
                    Bridge = new VRoid214Bridge(Log, Config.Bind("TextureSync", "Directory",
                        Path.Combine(Paths.GameRootPath, "LinkTextureIL2CPP"), "Directory for exported PNG files.").Value);
                }
                catch (Exception error) { Log.LogError($"Texture bridge disabled: {error}"); }
            }
            else Log.LogWarning($"Texture bridge requires VRoid 2.14.0; detected {Application.version}.");
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
        Owner?.Bridge?.Update();
        if (QuitAfterSeconds > 0 && elapsed.Elapsed.TotalSeconds >= QuitAfterSeconds)
        {
            QuitAfterSeconds = 0;
            Application.Quit();
        }
    }

    public void OnApplicationQuit() => Owner?.Stop();
    public void OnGUI() => Owner?.Bridge?.OnGUI();
}
