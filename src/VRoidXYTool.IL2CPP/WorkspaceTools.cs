#nullable disable
using BepInEx.Logging;
using UnityEngine;
using VRoid.UI.Component;
using VRoidXYTool.CompanionCore;
using VRoidXYTool.SyncCore;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using VRoidStudio.GUI.UserControls;

namespace VRoidXYTool.IL2CPP;

// Editor-only views and screen references. These never execute model-editing commands.
internal sealed class WorkspaceTools : IDisposable
{
    private readonly ManualLogSource log;
    private readonly string presetPath;
    private WorkspacePresets presets = new();
    private CameraPreset original;
    private IntPtr originalController;
    private Texture2D reference;
    private string referencePath;
    private bool grid, imageVisible = true;
    private float imageScale = .65f, imageAlpha = .35f, imageX = .5f, imageY = .5f;
    internal string Message = "Camera presets and viewport references do not change your model.";

    internal WorkspaceTools(ManualLogSource log, string presetPath, string referencePath)
    {
        this.log = log; this.presetPath = presetPath; this.referencePath = referencePath;
        try { if (File.Exists(presetPath)) presets = WorkspacePresets.Load(presetPath); }
        catch (Exception e) { Message = "Preset file could not be loaded; existing file preserved."; log.LogWarning(e); }
    }

    private static AvatarCameraPosition Controller()
    {
        var vm = VRoid214Bridge.FindAvatar()?._viewModel;
        if (vm?.CurrentFile?.model == null || !vm.IsEditModelActive) return null;
        foreach (var view in UnityEngine.Object.FindObjectsOfType<VRoidStudioAvatarCameraViewController>())
            if (view.gameObject.activeInHierarchy && view.cameraPosition != null) return view.cameraPosition;
        return vm.GlobalBus.EditAvatarCameraPosition;
    }
    private static Vector3 Vector(Point3 value) => new(value.X, value.Y, value.Z);
    private static Point3 Point(Vector3 value) => new(value.x, value.y, value.z);
    private static Camera RenderCamera(AvatarCameraPosition controller)
    {
        // VRoid 2.14 leaves the legacy _unityCamera field empty in edit mode.
        foreach (var group in controller._multipleRenderTextureCamera.InstantiatedCameras.Keys)
            if (group.PrimaryCamera != null) return group.PrimaryCamera;
        throw new InvalidOperationException("The model viewport is not ready.");
    }
    private static CameraPreset Capture(AvatarCameraPosition controller) => new(Point(controller._multipleRenderTextureCamera.transform.position),
        Point(controller._rotationCenter), RenderCamera(controller).orthographic, RenderCamera(controller).orthographicSize);
    private static void SetSize(AvatarCameraPosition controller, float size)
    {
        controller._multipleRenderTextureCamera._currentOrthographicSize = size;
        foreach (var group in controller._multipleRenderTextureCamera.InstantiatedCameras.Keys)
            if (group.PrimaryCamera != null) group.PrimaryCamera.orthographicSize = size;
    }
    private static void Apply(AvatarCameraPosition controller, CameraPreset preset)
    {
        if (!preset.IsValid) throw new InvalidDataException("Invalid camera values.");
        if (RenderCamera(controller).orthographic != preset.Orthographic) controller.SwitchCameraProjection();
        controller.SetCustomView(Vector(preset.Position), Vector(preset.Target));
        SetSize(controller, preset.Size);
    }
    private void Remember(AvatarCameraPosition controller)
    {
        if (original != null && originalController == controller.Pointer) return;
        original = Capture(controller); originalController = controller.Pointer;
    }
    private void Run(Action action)
    {
        try { action(); }
        catch (Exception e) { Message = "Operation unavailable: " + e.Message; log.LogWarning(e); }
    }
    internal void DrawControls(int tab, GUIStyle text, GUIStyle button)
    {
        var controller = Controller();
        bool enabled = GUI.enabled;
        GUI.enabled = enabled && controller != null;
        if (tab == 1)
        {
            GUI.Label(new Rect(35, 112, 740, 30), "Body views", text);
            string[] names = { "Front", "Back", "Left", "Right" };
            for (int i = 0; i < 4; i++)
            {
                int direction = i;
                if (GUI.Button(new Rect(35 + i * 185, 148, 175, 36), names[i], button)) Run(() => Builtin(controller, false, direction));
                if (GUI.Button(new Rect(35 + i * 185, 220, 175, 36), names[i], button)) Run(() => Builtin(controller, true, direction));
            }
            GUI.Label(new Rect(35, 188, 740, 30), "Head views", text);
            if (GUI.Button(new Rect(35, 265, 355, 36), "Perspective / orthographic", button)) Run(() => { Remember(controller); controller.SwitchCameraProjection(); Message = "Projection switched."; });
            if (GUI.Button(new Rect(405, 265, 355, 36), "Restore session view", button)) Run(() => { if (original != null) Apply(controller, original); Message = "Original view restored."; });
            GUI.Label(new Rect(35, 310, 740, 30), "Custom views (saved outside the .vroid)", text);
            for (int i = 0; i < 4; i++)
            {
                int slot = i;
                GUI.enabled = enabled && controller != null && presets.Cameras[i] != null;
                if (GUI.Button(new Rect(35 + i * 185, 345, 175, 36), "Recall " + (i + 1), button)) Run(() => { Remember(controller); Apply(controller, presets.Cameras[slot]); Message = "Recalled view " + (slot + 1); });
                GUI.enabled = enabled && controller != null;
                if (GUI.Button(new Rect(35 + i * 185, 388, 175, 36), "Save " + (i + 1), button)) Run(() => { presets.Cameras[slot] = Capture(controller); presets.Save(presetPath); Message = "Saved view " + (slot + 1); });
            }
        }
        else
        {
            // Unity's IMGUI TextEditor is stripped in this IL2CPP build. Use clipboard input.
            GUI.Label(new Rect(35, 112, 740, 36), Path.GetFileName(referencePath ?? "") + " (paste a path to change)", text);
            if (GUI.Button(new Rect(35, 156, 235, 36), "Paste PNG path", button)) referencePath = GUIUtility.systemCopyBuffer.Trim().Trim('"');
            if (GUI.Button(new Rect(285, 156, 235, 36), "Load reference PNG", button)) Run(LoadReference);
            if (GUI.Button(new Rect(535, 156, 235, 36), "Clear references", button)) ClearGuides();
            if (GUI.Button(new Rect(35, 201, 355, 36), grid ? "Hide alignment grid" : "Show alignment grid", button)) grid = !grid;
            if (GUI.Button(new Rect(405, 201, 355, 36), imageVisible ? "Hide reference image" : "Show reference image", button)) imageVisible = !imageVisible;
            GUI.Label(new Rect(35, 248, 740, 30), "Scale " + imageScale.ToString("F2") + "   Opacity " + imageAlpha.ToString("F2"), text);
            if (GUI.Button(new Rect(35, 286, 175, 36), "Smaller", button)) imageScale = Math.Max(.1f, imageScale - .1f);
            if (GUI.Button(new Rect(220, 286, 175, 36), "Larger", button)) imageScale = Math.Min(2f, imageScale + .1f);
            if (GUI.Button(new Rect(405, 286, 175, 36), "Less opaque", button)) imageAlpha = Math.Max(.05f, imageAlpha - .1f);
            if (GUI.Button(new Rect(590, 286, 175, 36), "More opaque", button)) imageAlpha = Math.Min(1f, imageAlpha + .1f);
            string[] names = { "Move left", "Move right", "Move up", "Move down" };
            for (int i = 0; i < 4; i++)
                if (GUI.Button(new Rect(35 + i * 185, 334, 175, 36), names[i], button))
                {
                    if (i < 2) imageX = Math.Clamp(imageX + (i == 0 ? -.05f : .05f), 0, 1);
                    else imageY = Math.Clamp(imageY + (i == 2 ? -.05f : .05f), 0, 1);
                }
            GUI.Label(new Rect(35, 384, 740, 50), "References stay inside the model viewport. Tab hides controls; images and grid remain visible.", text);
        }
        GUI.enabled = enabled;
        GUI.Label(new Rect(35, 438, 740, 60), controller == null ? "Open a model in edit mode to use these tools." : Message, text);
    }
    private void Builtin(AvatarCameraPosition controller, bool head, int direction)
    {
        Remember(controller);
        var vm = VRoid214Bridge.FindAvatar()._viewModel;
        var bone = GameObject.Find(head ? "J_Bip_C_Head" : "J_Bip_C_Hips");
        Vector3 target = bone != null ? bone.transform.position : vm.GlobalBus.ModelOrigin.position + new Vector3(0, head ? 1.5f : .85f, 0);
        float radius = RenderCamera(controller).orthographic ? 1 : head ? 1 : 4;
        Vector3 offset = direction switch { 0 => Vector3.forward, 1 => Vector3.back, 2 => Vector3.left, _ => Vector3.right };
        controller.SetCustomView(target + offset * radius, target);
        if (RenderCamera(controller).orthographic)
            SetSize(controller, head ? .3f : 1.15f);
        Message = (head ? "Head" : "Body") + " view applied.";
        log.LogInfo("Workspace camera view: " + Message + " direction=" + direction);
    }
    private void LoadReference()
    {
        string path = Path.GetFullPath(referencePath);
        if (!string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Choose a PNG reference.");
        using var stream = File.OpenRead(path);
        if (stream.Length > 64 * 1024 * 1024) throw new InvalidDataException("Reference exceeds 64 MiB.");
        byte[] bytes = new byte[checked((int)stream.Length)];
        int offset = 0;
        while (offset < bytes.Length) { int read = stream.Read(bytes, offset, bytes.Length - offset); if (read == 0) throw new EndOfStreamException(); offset += read; }
        if (!PngGuard.IsCompleteBoundedPng(bytes)) throw new InvalidDataException("Reference PNG is incomplete or exceeds 4096 pixels.");
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!ImageConversion.LoadImage(texture, bytes, false)) { UnityEngine.Object.Destroy(texture); throw new InvalidDataException("Reference cannot be decoded."); }
        if (reference != null) UnityEngine.Object.Destroy(reference);
        reference = texture; referencePath = path; imageVisible = true;
        Message = "Loaded " + Path.GetFileName(path); log.LogInfo("Workspace reference loaded: " + texture.width + "x" + texture.height);
    }
    internal void DrawGuides()
    {
        if ((!grid && (reference == null || !imageVisible)) || Controller() == null) return;
        var controller = Controller();
        foreach (var view in UnityEngine.Object.FindObjectsOfType<VRoidStudioAvatarCameraViewController>())
        {
            if (!view.gameObject.activeInHierarchy || view.cameraPosition?.Pointer != controller.Pointer) continue;
            var rect = view.transform.TryCast<RectTransform>(); if (rect == null) continue;
            var corners = new Il2CppStructArray<Vector3>(4); rect.GetWorldCorners(corners);
            var low = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            var high = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
            var area = new Rect(low.x, Screen.height - high.y, high.x - low.x, high.y - low.y);
            if (area.width <= 0 || area.height <= 0) continue;
            var color = GUI.color;
            GUI.BeginGroup(area);
            if (reference != null && imageVisible)
            {
                float width = area.width * imageScale, height = width * reference.height / reference.width;
                GUI.color = new Color(1, 1, 1, imageAlpha);
                GUI.DrawTexture(new Rect(area.width * imageX - width / 2, area.height * imageY - height / 2, width, height), reference, ScaleMode.ScaleToFit, true);
            }
            if (grid)
            {
                GUI.color = new Color(.3f, 1, .85f, .45f);
                for (int i = 1; i < 8; i++)
                {
                    GUI.DrawTexture(new Rect(area.width * i / 8, 0, 1, area.height), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(0, area.height * i / 8, area.width, 1), Texture2D.whiteTexture);
                }
            }
            GUI.EndGroup(); GUI.color = color; return;
        }
    }
    internal void ClearGuides() { if (reference != null) UnityEngine.Object.Destroy(reference); reference = null; grid = false; Message = "References cleared."; }
    internal void DocumentChanged() { ClearGuides(); original = null; originalController = IntPtr.Zero; }
    public void Dispose()
    {
        Run(() => { var controller = Controller(); if (original != null && controller?.Pointer == originalController) Apply(controller, original); });
        ClearGuides();
    }
}
