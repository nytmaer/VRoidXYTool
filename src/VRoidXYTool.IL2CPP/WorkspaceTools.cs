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
    private readonly ReferenceWorkspace references;
    private int cameraPage;
    internal string Message = "Camera presets and viewport references do not change your model.";

    internal WorkspaceTools(ManualLogSource log, string presetPath, string referencePath)
    {
        this.log = log; this.presetPath = presetPath; references = new ReferenceWorkspace(presetPath);
        try { if (File.Exists(presetPath)) presets = WorkspacePresets.Load(presetPath); }
        catch (Exception e) { Message = "Preset file could not be loaded; existing file preserved."; log.LogWarning(e); }
    }

    private static AvatarCameraPosition Controller()
    {
        var vm = VRoid214Bridge.FindAvatar()?._viewModel;
        if (vm?.CurrentFile?.model == null) return null;
        var photo = VRoid214Bridge.FindAvatar()?._instantiatedPhotoBoothViewModel;
        if (photo?.IsActive == true) return vm.GlobalBus.PhotoBoothAvatarCameraPosition;
        if (!vm.IsEditModelActive) return null;
        foreach (var view in UnityEngine.Object.FindObjectsOfType<VRoidStudioAvatarCameraViewController>())
            if (view.gameObject.activeInHierarchy && view.cameraPosition != null) return view.cameraPosition;
        return vm.GlobalBus.EditAvatarCameraPosition;
    }
    private static Vector3 Vector(Point3 value) => new(value.X, value.Y, value.Z);
    private static Point3 Point(Vector3 value) => new(value.x, value.y, value.z);
    private static Camera RenderCamera(AvatarCameraPosition controller)
    {
        if (controller._unityCamera != null) return controller._unityCamera;
        // VRoid 2.14 leaves the legacy _unityCamera field empty in edit mode.
        foreach (var group in controller._multipleRenderTextureCamera.InstantiatedCameras.Keys)
            if (group.PrimaryCamera != null) return group.PrimaryCamera;
        throw new InvalidOperationException("The model viewport is not ready.");
    }
    private static Transform CameraTransform(AvatarCameraPosition controller) => controller._multipleRenderTextureCamera != null ? controller._multipleRenderTextureCamera.transform : controller._unityCamera.transform;
    private static CameraPreset Capture(AvatarCameraPosition controller) => new(Point(CameraTransform(controller).position),
        Point(controller._rotationCenter), RenderCamera(controller).orthographic, RenderCamera(controller).orthographicSize) { Rotation = Point(CameraTransform(controller).eulerAngles) };
    private static void SetSize(AvatarCameraPosition controller, float size)
    {
        if (controller._multipleRenderTextureCamera == null) { RenderCamera(controller).orthographicSize = size; return; }
        controller._multipleRenderTextureCamera._currentOrthographicSize = size;
        foreach (var group in controller._multipleRenderTextureCamera.InstantiatedCameras.Keys)
            if (group.PrimaryCamera != null) group.PrimaryCamera.orthographicSize = size;
    }
    private static void Apply(AvatarCameraPosition controller, CameraPreset preset)
    {
        if (!preset.IsValid) throw new InvalidDataException("Invalid camera values.");
        if (RenderCamera(controller).orthographic != preset.Orthographic) controller.SwitchCameraProjection();
        controller.SetCustomView(Vector(preset.Position), Vector(preset.Target));
        if (preset.Rotation != null) CameraTransform(controller).eulerAngles = Vector(preset.Rotation);
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
            var bank = presets.Bank(controller != null && RenderCamera(controller).orthographic);
            if (GUI.Button(new Rect(35, 310, 175, 32), "Slots " + (cameraPage == 0 ? "1–5" : "6–10"), button)) cameraPage = 1 - cameraPage;
            GUI.Label(new Rect(220, 310, 550, 30), "Separate perspective / orthographic banks", text);
            for (int i = 0; i < 5; i++)
            {
                int slot = i + cameraPage * 5;
                GUI.enabled = enabled && controller != null && bank[slot] != null;
                if (GUI.Button(new Rect(35 + i * 148, 345, 140, 36), "Recall " + (slot + 1), button)) Run(() => { Remember(controller); Apply(controller, bank[slot]); Message = "Recalled view " + (slot + 1); });
                GUI.enabled = enabled && controller != null;
                if (GUI.Button(new Rect(35 + i * 148, 388, 140, 36), "Save " + (slot + 1), button)) Run(() => { var previous = bank[slot]; bank[slot] = Capture(controller); try { presets.Save(presetPath); } catch { bank[slot] = previous; throw; } Message = "Saved view " + (slot + 1); });
                GUI.enabled = enabled && bank[slot] != null;
                if (GUI.Button(new Rect(35 + i * 148, 431, 140, 36), "Clear " + (slot + 1), button)) Run(() => { var previous = bank[slot]; bank[slot] = null; try { presets.Save(presetPath); } catch { bank[slot] = previous; throw; } Message = "Cleared view " + (slot + 1); });
            }
            GUI.enabled = enabled && controller != null;
            GUI.Label(new Rect(35, 480, 330, 30), "Orthographic size", text);
            if (GUI.Button(new Rect(405, 474, 355, 34), "Import legacy camera JSON", button)) Run(() =>
            {
                var imported = LegacyWorkspaceImport.Cameras(Path.GetFullPath(GUIUtility.systemCopyBuffer.Trim().Trim('"')));
                imported.Save(presetPath); presets = imported;
                Message = "Imported both legacy camera banks.";
            });
            float[] sizes = { .15f, .2f, .3f, .8f };
            for (int i = 0; i < sizes.Length; i++) { float size = sizes[i]; if (GUI.Button(new Rect(35 + i * 123, 514, 115, 36), size.ToString("0.##"), button)) Run(() => { Remember(controller); SetSize(controller, size); }); }
            if (GUI.Button(new Rect(527, 514, 115, 36), "Smaller", button)) Run(() => { Remember(controller); SetSize(controller, Math.Max(.01f, RenderCamera(controller).orthographicSize * .9f)); });
            if (GUI.Button(new Rect(650, 514, 115, 36), "Larger", button)) Run(() => { Remember(controller); SetSize(controller, Math.Min(100, RenderCamera(controller).orthographicSize * 1.1f)); });
        }
        else
        {
            references.Controls(text, button);
        }
        GUI.enabled = enabled;
        GUI.Label(new Rect(35, 560, 740, 60), controller == null ? "Open a model to use these tools." : Message, text);
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
    internal void DrawGuides()
    {
        if (Controller() == null) return;
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
            references.DrawScreen(area); return;
        }
    }
    internal void ClearGuides() { references.DisposeImages(); Message = "References cleared."; }
    internal void DocumentChanged() { ClearGuides(); original = null; originalController = IntPtr.Zero; }
    internal void Update() => references.Update();
    public void Dispose()
    {
        Run(() => { var controller = Controller(); if (original != null && controller?.Pointer == originalController) Apply(controller, original); });
        references.Dispose();
    }
}
