#nullable disable
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;
using VRoidStudio.GUI.AvatarEditor.PhotoBooth;
using VRoidXYTool.CompanionCore;
using System.Diagnostics;

namespace VRoidXYTool.IL2CPP;

internal sealed class RecordingTools : IDisposable
{
    private readonly ManualLogSource log;
    private readonly ConfigEntry<string> executable, directory;
    private readonly ConfigEntry<int> fps, bitrate;
    private readonly ConfigEntry<KeyCode> hotkey;
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private VideoEncoder encoder;
    private Task<string> finishing;
    private PhotoBoothViewModel photo;
    private bool starting;
    private double next;
    private int captured, width, height;
    internal string Message = "Record the photo-booth view as a silent MP4.";
    private static PhotoBoothViewModel Active()
    {
        var vm=VRoid214Bridge.FindAvatar()?._instantiatedPhotoBoothViewModel;
        return vm?.IsActive==true?vm:null;
    }
    internal RecordingTools(ManualLogSource log,ConfigFile config)
    {
        this.log=log;
        executable=config.Bind("Recording","FFmpegPath",Path.Combine(BepInEx.Paths.GameRootPath,"FFmpeg","ffmpeg.exe"),"Path to an existing FFmpeg executable. The tool does not install or download it.");
        directory=config.Bind("Recording","Directory",Path.Combine(BepInEx.Paths.GameRootPath,"Recordings"),"Directory for completed MP4 recordings.");
        fps=config.Bind("Recording","FPS",30,"Requested frames per second: 1–240.");
        bitrate=config.Bind("Recording","BitrateKbps",20000,"Video bitrate in kilobits per second: 256–100000.");
        hotkey=config.Bind("Recording","Hotkey",KeyCode.G,"Toggle recording while photo booth is active.");
    }
    private void Run(Action action){try{action();}catch(Exception e){Message=e.Message.Split('\n')[0];log.LogWarning(e);}}
    private void Start()
    {
        if(encoder!=null || starting)throw new InvalidOperationException("The previous recording is still finishing.");
        photo=Active()??throw new InvalidOperationException("Open photo booth before recording.");
        if(!File.Exists(executable.Value))throw new FileNotFoundException("Choose an FFmpeg executable before recording.");
        if(fps.Value<1 || fps.Value>240 || bitrate.Value<256 || bitrate.Value>100000)throw new InvalidDataException("FPS or bitrate is outside the supported range.");
        width=photo.CaptureSizeSetting.HorizontalResolution;height=photo.CaptureSizeSetting.VerticalResolution;
        if(width<2 || height<2 || width>4096 || height>4096 || width%2!=0 || height%2!=0)throw new InvalidDataException("Choose an even photo-booth resolution up to 4096 pixels per side.");
        starting=true;captured=0;Message="Starting recording…";
    }
    private void Stop()
    {
        starting=false;photo=null;
        if(encoder==null)return;
        if(finishing==null){finishing=encoder.StopAsync();Message="Finalizing MP4…";}
    }
    internal void Update()
    {
        if((starting || encoder!=null && finishing==null) && Active()?.Pointer!=photo?.Pointer)Run(Stop);
        if(finishing?.IsCompleted==true)
        {
            var task=finishing;
            try{Message="Saved "+Path.GetFileName(task.GetAwaiter().GetResult())+" ("+captured+" captures).";log.LogInfo("Recording finalized: "+task.Result);}
            catch(Exception e){Message="Recording failed; partial data retained. "+e.Message.Split('\n')[0];log.LogWarning(e);}
            finally{try{encoder.Dispose();}catch(Exception e){log.LogWarning(e);}encoder=null;finishing=null;}
        }
        if(Active()!=null && BepInEx.UnityInput.Current.GetKeyDown(hotkey.Value))Run(()=>{if(starting || encoder!=null)Stop();else Start();});
    }
    internal void LateUpdate()
    {
        if(!starting && (encoder==null || finishing!=null))return;
        if(!starting && clock.Elapsed.TotalSeconds<next)return;
        Texture2D texture=null;
        try
        {
            texture=photo.CaptureImage();
            if(texture==null || texture.width!=width || texture.height!=height)throw new InvalidDataException("Capture resolution changed; recording stopped.");
            var pixels=texture.GetPixels32().ToArray();
            byte[] rgba=new byte[checked(width*height*4)];
            // Unity pixels start at the bottom; raw video starts at the top.
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                var p=pixels[(height-1-y)*width+x];int offset=(y*width+x)*4;
                rgba[offset]=p.r;rgba[offset+1]=p.g;rgba[offset+2]=p.b;rgba[offset+3]=p.a;
            }
            if(starting)
            {
                string output=Path.Combine(Path.GetFullPath(directory.Value),"VRoid-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")[..6]+".mp4");
                encoder=new VideoEncoder(executable.Value,output,width,height,fps.Value,checked(bitrate.Value*1000));
                starting=false;clock.Restart();Message="Recording. Press "+hotkey.Value+" to stop.";
                log.LogInfo($"Recording started: {width}x{height}, {fps.Value} FPS, {bitrate.Value} kbps.");
            }
            if(encoder.Submit(rgba))captured++;
            next=clock.Elapsed.TotalSeconds+1.0/fps.Value;
        }
        catch(Exception e){Run(Stop);Message="Capture stopped: "+e.Message.Split('\n')[0];log.LogWarning(e);}
        finally{if(texture!=null)UnityEngine.Object.Destroy(texture);}
    }
    internal void Controls(GUIStyle text,GUIStyle button)
    {
        bool enabled=GUI.enabled;
        try
        {
            GUI.enabled=enabled && Active()!=null && finishing==null;
            if(GUI.Button(new Rect(35,112,355,36),starting || encoder!=null?"Stop recording":"Start recording",button))Run(()=>{if(starting || encoder!=null)Stop();else Start();});
            GUI.enabled=enabled;
            if(GUI.Button(new Rect(405,112,355,36),"Open recording folder",button))Run(()=>{Directory.CreateDirectory(directory.Value);Process.Start(new ProcessStartInfo(Path.GetFullPath(directory.Value)){UseShellExecute=true});});
            GUI.Label(new Rect(35,170,740,36),$"FPS: {fps.Value}   Bitrate: {bitrate.Value} kbps   Hotkey: {hotkey.Value}",text);
            GUI.enabled=enabled && encoder==null && !starting;
            if(GUI.Button(new Rect(35,215,355,36),"Paste FFmpeg executable path",button))Run(()=>{string path=Path.GetFullPath(GUIUtility.systemCopyBuffer.Trim().Trim('"'));if(!File.Exists(path) || !Path.GetExtension(path).Equals(".exe",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Copy an existing FFmpeg .exe path.");executable.Value=path;Message="Encoder selected.";});
            if(GUI.Button(new Rect(405,215,355,36),"Paste FPS, bitrate kbps",button))Run(()=>{var parts=GUIUtility.systemCopyBuffer.Split(',');if(parts.Length!=2 || !int.TryParse(parts[0],out int rate) || !int.TryParse(parts[1],out int bits) || rate<1 || rate>240 || bits<256 || bits>100000)throw new InvalidDataException("Copy FPS,bitrate, for example 30,20000.");fps.Value=rate;bitrate.Value=bits;});
            GUI.enabled=enabled;
            GUI.Label(new Rect(35,280,740,110),Message,text);
            GUI.Label(new Rect(35,410,740,90),$"Encoder: {Path.GetFileName(executable.Value)}\nCaptured: {captured}   Repeated frames preserve timing when captures are skipped.",text);
        }
        finally{GUI.enabled=enabled;}
    }
    internal void DocumentChanged()=>Run(Stop);
    public void Dispose()
    {
        Stop();if(encoder!=null){try{encoder.Dispose();}catch(Exception e){log.LogWarning(e);}encoder=null;finishing=null;}
    }
}
