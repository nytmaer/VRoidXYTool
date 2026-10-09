#nullable disable
using UnityEngine;
using VRoid.UI.Component;

namespace VRoidXYTool.IL2CPP;

internal sealed class RenderingTools : IDisposable
{
    private readonly Dictionary<IntPtr,(Camera Camera,bool MSAA,bool HDR)> cameras=new();
    private readonly Dictionary<IntPtr,(RenderTexture Texture,int Samples)> textures=new();
    private readonly Dictionary<IntPtr,(Camera Camera,CameraClearFlags Flags)> wireCameras=new();
    private int? originalAA;
    private bool wire;
    private Camera.CameraCallback before,after;
    internal string Message="Rendering changes are restored when disabled or when the model closes.";
    private static IEnumerable<CameraGroup> Groups()
    {
        foreach(var view in UnityEngine.Object.FindObjectsOfType<VRoidStudio.GUI.UserControls.VRoidStudioAvatarCameraViewController>())
            if(view.gameObject.activeInHierarchy && view.cameraPosition?._multipleRenderTextureCamera!=null)
                foreach(var group in view.cameraPosition._multipleRenderTextureCamera.InstantiatedCameras.Keys)yield return group;
    }
    private static bool ModelCamera(Camera camera)
    {
        foreach(var group in Groups())if(group.PrimaryCamera?.Pointer==camera.Pointer)return true;
        var photo=VRoid214Bridge.FindAvatar()?._instantiatedPhotoBoothViewModel;
        return photo?.IsActive==true && photo.PhotoBoothCamera?.Pointer==camera.Pointer;
    }
    private void Pre(Camera camera)
    {
        if(!wire || !ModelCamera(camera))return;
        wireCameras[camera.Pointer]=(camera,camera.clearFlags);
        camera.clearFlags=CameraClearFlags.Color;GL.wireframe=true;
    }
    private void Post(Camera camera)
    {
        if(wireCameras.Remove(camera.Pointer,out var entry)){camera.clearFlags=entry.Flags;GL.wireframe=false;}
    }
    private void SetWire(bool enabled)
    {
        if(wire==enabled)return;
        if(enabled)
        {
            before=Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<Camera.CameraCallback>((Action<Camera>)Pre);
            after=Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<Camera.CameraCallback>((Action<Camera>)Post);
            Camera.onPreRender=Il2CppSystem.Delegate.Combine(Camera.onPreRender,before).Cast<Camera.CameraCallback>();
            Camera.onPostRender=Il2CppSystem.Delegate.Combine(Camera.onPostRender,after).Cast<Camera.CameraCallback>();
            wire=true;
        }
        else
        {
            wire=false;
            Camera.onPreRender=Il2CppSystem.Delegate.Remove(Camera.onPreRender,before)?.Cast<Camera.CameraCallback>();
            Camera.onPostRender=Il2CppSystem.Delegate.Remove(Camera.onPostRender,after)?.Cast<Camera.CameraCallback>();
            before=null;after=null;GL.wireframe=false;
            foreach(var entry in wireCameras.Values)if(entry.Camera!=null)entry.Camera.clearFlags=entry.Flags;
            wireCameras.Clear();
        }
        Message=enabled?"Wireframe enabled.":"Wireframe disabled.";
    }
    private static void Samples(RenderTexture texture,int samples)
    {
        if(texture==null || texture.antiAliasing==samples)return;
        texture.Release();texture.antiAliasing=samples;texture.Create();
    }
    private void AntiAlias(int samples)
    {
        originalAA??=QualitySettings.antiAliasing;QualitySettings.antiAliasing=samples;
        foreach(var group in Groups())
        {
            var camera=group.PrimaryCamera;
            if(camera!=null){if(!cameras.ContainsKey(camera.Pointer))cameras.Add(camera.Pointer,(camera,camera.allowMSAA,camera.allowHDR));camera.allowMSAA=samples>0;if(samples>0)camera.allowHDR=true;}
            var target=group.targetTexture;
            if(target!=null){if(!textures.ContainsKey(target.Pointer))textures.Add(target.Pointer,(target,target.antiAliasing));Samples(target,Math.Max(1,samples));}
        }
        Message=samples==0?"Anti-aliasing disabled.":"Requested "+samples+"× anti-aliasing; hardware may limit samples.";
    }
    private void Run(Action action){try{action();}catch(Exception e){Message=e.Message;}}
    internal void Controls(GUIStyle text,GUIStyle button)
    {
        if(GUI.Button(new Rect(35,112,355,36),wire?"Disable wireframe":"Enable wireframe",button))Run(()=>SetWire(!wire));
        if(GUI.Button(new Rect(405,112,355,36),"Restore rendering",button))Run(Dispose);
        GUI.Label(new Rect(35,170,740,36),"Anti-aliasing",text);
        int[] levels={0,2,4,8};
        for(int i=0;i<levels.Length;i++){int samples=levels[i];if(GUI.Button(new Rect(35+i*185,210,175,36),samples==0?"Off":samples+"×",button))Run(()=>AntiAlias(samples));}
        GUI.Label(new Rect(35,270,740,140),Message,text);
    }
    public void Dispose()
    {
        SetWire(false);
        foreach(var entry in textures.Values)if(entry.Texture!=null)Samples(entry.Texture,entry.Samples);
        textures.Clear();
        foreach(var entry in cameras.Values)if(entry.Camera!=null){entry.Camera.allowMSAA=entry.MSAA;entry.Camera.allowHDR=entry.HDR;}
        cameras.Clear();if(originalAA.HasValue)QualitySettings.antiAliasing=originalAA.Value;originalAA=null;
        Message="Original rendering settings restored.";
    }
}
