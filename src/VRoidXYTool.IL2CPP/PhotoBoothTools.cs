#nullable disable
using UnityEngine;
using VRoidStudio.GUI.AvatarEditor.PhotoBooth;
using VRoidStudio.PhotoBooth;
using VRoidXYTool.CompanionCore;
using VRoidXYTool.MMD;

namespace VRoidXYTool.IL2CPP;

internal sealed class PhotoBoothTools : IDisposable
{
    private readonly string directory = Path.Combine(BepInEx.Paths.GameRootPath,"Pose");
    private string[] saved = Array.Empty<string>();
    private int selected;
    private readonly NativeFilePicker picker = new();
    private UnityVMDPlayer player;
    private PhotoBoothViewModel motionPhoto;
    private PoseFile originalPose;
    private VMDReader motion;
    private bool loop;
    private IntPtr pickerPhoto;
    private static void ResetPose(PhotoBoothViewModel photo)
    {
        // Reset the selected preset as well as the controls; ResetPose alone
        // reapplies the imported custom pose on VRoid 2.14.
        var poses = photo.PosesViewModel;
        poses._loadedPoseFile = null;
        poses.SelectedIndex = 0;
        poses._posesModel.PlayPoseAnimation(0);
        poses.Reset();
        photo.HandPosesViewModel.Reset();
    }
    private void ImportLegacy(PhotoBoothViewModel photo, string path)
    {
        var imported = LegacyPoseImport.Read(path);
        var pose = CurrentPose(photo);
        var definitions = pose.VRoidCustomData.PoseGizmoDefinitions;
        var replacements = new List<(PoseFile.VRoidCustomPoseData.PoseGizmoDefinition Definition, string Value)>();
        foreach(var entry in imported)
        {
            PoseFile.VRoidCustomPoseData.PoseGizmoDefinition match = null;
            foreach(var candidate in definitions)
                if(candidate.Key == entry.Name && PoseFile.VRoidCustomPoseData.DecodeType(candidate.Type)?.Name == entry.TypeName) { match=candidate;break; }
            if(match == null) throw new InvalidDataException("Legacy control is unavailable on this rig: " + entry.Name);
            replacements.Add((match,LegacyPoseImport.MergeWithCurrent(match.Value,entry.Value)));
        }
        // Validate every control before applying any change to the active pose.
        foreach(var replacement in replacements) replacement.Definition.Value=replacement.Value;
        byte[] nativeBytes=pose.Serialize().ToArray();
        photo.ApplyPoseFile(pose);
        SaveBytes(nativeBytes);
        Message="Imported legacy pose and saved a native copy; source preserved.";
    }
    private static PoseFile CurrentPose(PhotoBoothViewModel photo)
    {
        string snapshot=Path.Combine(BepInEx.Paths.CachePath,"vroidxytool-pose-"+Guid.NewGuid().ToString("N")+"."+PoseFile.PoseFileExtension.TrimStart('.'));
        Directory.CreateDirectory(BepInEx.Paths.CachePath);
        try { File.WriteAllBytes(snapshot,photo.GeneratePoseFile().ToArray()); return PoseFile.LoadPoseFile(snapshot) ?? throw new InvalidDataException("Current pose could not be captured."); }
        finally { if(File.Exists(snapshot))File.Delete(snapshot); }
    }
    internal string Message = "Enable pose editing to save, load or animate a pose.";
    private static PhotoBoothViewModel Active()
    {
        var photo = VRoid214Bridge.FindAvatar()?._instantiatedPhotoBoothViewModel;
        return photo?.IsActive == true ? photo : null;
    }
    private void Run(Action action) { try { action(); } catch(Exception error) { Message = error.Message; } }
    private void Refresh()
    {
        Directory.CreateDirectory(directory);
        string extension = "." + PoseFile.PoseFileExtension.TrimStart('.');
        saved = Directory.GetFiles(directory,"*"+extension).OrderBy(p=>p,StringComparer.OrdinalIgnoreCase).ToArray();
        selected = saved.Length == 0 ? 0 : Math.Min(selected,saved.Length-1);
    }
    private void Save(PhotoBoothViewModel photo)
    {
        byte[] bytes = photo.GeneratePoseFile().ToArray();
        SaveBytes(bytes);
    }
    private void SaveBytes(byte[] bytes)
    {
        if (bytes.Length == 0 || bytes.Length > 1024*1024) throw new InvalidDataException("Invalid pose size.");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory,"Pose-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")[..6]+"."+PoseFile.PoseFileExtension.TrimStart('.'));
        File.WriteAllBytes(path,bytes); Refresh(); selected=Array.IndexOf(saved,path);
        Message="Saved current pose: "+Path.GetFileName(path);
    }
    private void Load(PhotoBoothViewModel photo, string path)
    {
        var file = new FileInfo(path);
        if(!file.Exists || file.Length > 1024*1024 || file.Length == 0) throw new InvalidDataException("Pose is missing or exceeds 1 MiB.");
        var pose = PoseFile.LoadPoseFile(path) ?? throw new InvalidDataException("Pose file could not be parsed.");
        photo.ApplyPoseFile(pose); Message="Applied "+Path.GetFileName(path);
    }
    private void LoadMotion(PhotoBoothViewModel photo, string path)
    {
        using var stream = File.OpenRead(path);
        if(stream.Length>64*1024*1024)throw new InvalidDataException("VMD exceeds 64 MiB.");
        using var memory = new MemoryStream();stream.CopyTo(memory);VmdGuard.Validate(memory.ToArray());
        var reader=new VMDReader(path);
        if(reader.FrameCount<1)throw new InvalidDataException("VMD has no supported humanoid motion.");
        StopMotion();
        Directory.CreateDirectory(BepInEx.Paths.CachePath);
        string snapshot=Path.Combine(BepInEx.Paths.CachePath,"vroidxytool-motion-"+Guid.NewGuid().ToString("N")+"."+PoseFile.PoseFileExtension.TrimStart('.'));
        try {File.WriteAllBytes(snapshot,photo.GeneratePoseFile().ToArray());originalPose=PoseFile.LoadPoseFile(snapshot);}
        finally {if(File.Exists(snapshot))File.Delete(snapshot);}
        motionPhoto=photo;
        try {player=new UnityVMDPlayer(photo.animator){IsLoop=loop};player.Play(reader);motion=reader;Message="Playing "+Path.GetFileName(path);}
        catch {StopMotion();throw;}
    }
    private void StopMotion()
    {
        var previousPlayer = player;
        var previousPhoto = motionPhoto;
        var previousPose = originalPose;
        player=null;motion=null;motionPhoto=null;originalPose=null;
        try { previousPlayer?.Dispose(); }
        finally
        {
            if(previousPhoto!=null && previousPhoto.IsActive && previousPose!=null)
                previousPhoto.ApplyPoseFile(previousPose);
        }
        if(previousPlayer!=null)Message="Motion stopped; original pose restored.";
    }
    internal void Update()
    {
        var active = Active();
        if(motionPhoto!=null && active?.Pointer!=motionPhoto.Pointer)Run(StopMotion);
        if(pickerPhoto!=IntPtr.Zero && active?.Pointer!=pickerPhoto) { picker.CancelPending(); pickerPhoto=IntPtr.Zero; }
        picker.Update();
    }
    internal void LateUpdate() {try{player?.Update();}catch(Exception e){Run(StopMotion);Message="Motion stopped: "+e.Message;}}
    internal void DocumentChanged() {picker.CancelPending();Run(StopMotion);}
    public void Dispose() => DocumentChanged();
    internal void Controls(GUIStyle text, GUIStyle button)
    {
        var photo = Active(); bool enabled = GUI.enabled;
        try
        {
            bool poseReady = photo != null && photo.PoseModeIndex == (int)PoseMode.ManualPose && photo.PosesViewModel?._posesModel?._poseController != null;
            GUI.enabled = enabled && poseReady && player==null;
            if(GUI.Button(new Rect(35,112,235,36),"Save current pose",button)) Run(()=>Save(photo));
            if(GUI.Button(new Rect(285,112,235,36),"Load pose file…",button)) Run(()=>photo.OpenLoadPoseWindow());
            if(GUI.Button(new Rect(535,112,235,36),"Reset pose",button)) Run(()=>{ResetPose(photo);Message="Pose reset.";});
            if(GUI.Button(new Rect(35,158,355,36),"Export pose file…",button)) Run(()=>photo.OpenSavePoseWindow());
            GUI.enabled = enabled;
            if(GUI.Button(new Rect(405,158,355,36),"Refresh saved poses",button)) Run(Refresh);
            GUI.enabled = enabled && poseReady && player==null;
            if(GUI.Button(new Rect(35,605,355,34),"Import legacy .posejson…",button)) Run(()=>{pickerPhoto=photo.Pointer;picker.Open("Legacy pose",new[]{"posejson"},path=>ImportLegacy(photo,path),error=>Message=error.Message);});
            GUI.enabled = enabled;
            if(GUI.Button(new Rect(405,605,355,34),"Open pose folder",button)) Run(()=>{Directory.CreateDirectory(directory);System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(directory){UseShellExecute=true});});
            GUI.Label(new Rect(35,210,740,45),saved.Length==0?"No saved poses. Save the current pose to begin.":$"{selected+1}/{saved.Length}: {Path.GetFileName(saved[selected])}",text);
            GUI.enabled = enabled && saved.Length>0;
            if(GUI.Button(new Rect(35,265,235,36),"Next saved pose",button)) selected=(selected+1)%saved.Length;
            GUI.enabled = enabled && saved.Length>0 && poseReady && player==null;
            if(GUI.Button(new Rect(285,265,235,36),"Apply saved pose",button)) Run(()=>Load(photo,saved[selected]));
            GUI.enabled = enabled && saved.Length>0;
            if(GUI.Button(new Rect(535,265,235,36),"Move to trash",button)) Run(()=>
            {
                // Recoverable deletion stays inside this tool's own pose directory.
                string trash=Path.Combine(directory,"Trash");Directory.CreateDirectory(trash);
                File.Move(saved[selected],Path.Combine(trash,Guid.NewGuid().ToString("N")+"-"+Path.GetFileName(saved[selected])));
                Refresh();Message="Pose moved to Pose/Trash.";
            });
            GUI.enabled = enabled && poseReady;
            if(GUI.Button(new Rect(35,320,235,36),"Load VMD…",button)) Run(()=>{pickerPhoto=photo.Pointer;picker.Open("VMD motion",new[]{"vmd"},path=>LoadMotion(photo,path),error=>Message=error.Message);});
            if(GUI.Button(new Rect(285,320,235,36),"Paste VMD path",button)) Run(()=>LoadMotion(photo,Path.GetFullPath(GUIUtility.systemCopyBuffer.Trim().Trim('"'))));
            GUI.enabled = enabled && player!=null;
            if(GUI.Button(new Rect(35,365,235,36),player?.IsPlaying==true?"Pause":"Play",button))Run(()=>{if(player.IsPlaying)player.Pause();else player.Play();});
            if(GUI.Button(new Rect(285,365,235,36),"Stop motion",button))Run(StopMotion);
            GUI.enabled=enabled;
            if(GUI.Button(new Rect(535,365,235,36),loop?"Loop: on":"Loop: off",button)){loop=!loop;if(player!=null)player.IsLoop=loop;}
            GUI.Label(new Rect(35,414,740,36),player==null?"No VMD loaded.":$"Motion frame {player.FrameNumber}/{motion.FrameCount}",text);
            GUI.enabled=enabled && player!=null;
            for(int i=0;i<4;i++){int percentage=i*25;if(GUI.Button(new Rect(35+i*185,455,175,36),"Seek "+percentage+"%",button))Run(()=>player.JumpToFrame(motion.FrameCount*percentage/100));}
            GUI.enabled=enabled;
            GUI.Label(new Rect(35,515,740,40),photo==null?"Open photo booth to use pose tools.":player!=null?(player.IsPlaying?"Motion playing. Stop motion before editing poses.":"Motion paused. Stop motion before editing poses."):Message,text);
            GUI.enabled=enabled && photo!=null && !poseReady;
            if(GUI.Button(new Rect(35,560,740,36),"Enable pose editing",button))Run(()=>{photo.PoseModeIndex=(int)PoseMode.ManualPose;Message="Pose editing enabled.";});
        }
        finally {GUI.enabled=enabled;}
    }
}
