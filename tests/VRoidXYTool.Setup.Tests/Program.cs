using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VRoidXYTool.Setup;

if(args.Length!=3)throw new ArgumentException("Pass VRoidStudio.exe, the pinned loader ZIP and the real payload ZIP.");
string realExe=Path.GetFullPath(args[0]),loader=Path.GetFullPath(args[1]),payload=Path.GetFullPath(args[2]);
string testBase=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"VRoidXYTool-Setup-Tests"));Directory.CreateDirectory(testBase);
string root=Path.Combine(testBase,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
int checks=0;
try
{
    string source=Path.Combine(root,"Source Studio 源"),target=Path.Combine(root,"Other drive path 空白"),data=Path.Combine(root,"User data preserved");
    Fixture(source);
    Check(Installer.Inspect(Path.Combine(source,"VRoidStudio.exe")).Root==source,"verified build profile is recognized in a Unicode/custom folder");
    string latest=Path.Combine(root,"Studio 2.16");Fixture(latest);
    File.WriteAllText(Path.Combine(latest,"VRoidStudio_Data","boot.config"),"build-guid=a34c8843981a4aada647acf24a55dece");
    Check(Installer.Inspect(Path.Combine(latest,"VRoidStudio.exe")).Description.Contains("2.16.0"),"2.16 build profile recognized");
    foreach(string invalid in new[]{"build-guid=a34c8843981a4aada647acf24a55dece-extra","# build-guid=a34c8843981a4aada647acf24a55dece","build-guid=a34c8843981a4aada647acf24a55dece\nbuild-guid=other"})
    {
        File.WriteAllText(Path.Combine(latest,"VRoidStudio_Data","boot.config"),invalid);
        Reject(()=>Installer.Inspect(Path.Combine(latest,"VRoidStudio.exe")),"partial, commented and conflicting build profiles refused");
    }
    foreach(string evil in new[]{"../outside.dll","BepInEx/../../outside.dll","/absolute.dll","C:/absolute.dll","BepInEx/file:stream","BepInEx\\outside.dll","BepInEx/part./file","BepInEx//file"})Reject(()=>Installer.SafePath(target,evil),"unsafe path rejected: "+evil);
    string broken=Path.Combine(root,"invalid-loader.zip");File.WriteAllText(broken,"not the official loader");
    Reject(()=>Installer.VerifyLoader(broken),"tampered loader rejected before writes");
    Installer.VerifyLoader(loader);Check(true,"pinned loader checksum matches the inspected official artifact");
    string download=Path.Combine(root,"official-download.zip");
    await Installer.DownloadLoader(download,null,CancellationToken.None);
    Check(Installer.Hash(download)==Installer.LoaderSha256,"official loader download is reachable and passes the pinned checksum");
    File.WriteAllText(Path.Combine(source,"Personal.vroid"),"source model must survive");
    Directory.CreateDirectory(Path.Combine(data,"Textures"));File.WriteAllText(Path.Combine(data,"Textures","preserved.png"),"external artwork");
    using(var stream=File.OpenRead(payload))Installer.Install(new(Path.Combine(source,"VRoidStudio.exe"),target,data,true),stream,loader);
    Check(File.Exists(Path.Combine(target,"VRoidStudio.exe")) && !File.Exists(Path.Combine(target,"Personal.vroid")),"writable copy includes application files and excludes personal models");
    Check(File.ReadAllText(Path.Combine(source,"Personal.vroid"))=="source model must survive","original model remains unchanged");
    Check(!Directory.Exists(Path.Combine(source,"BepInEx")),"copy install leaves original application free of setup changes");
    string plugin=Path.Combine(target,"BepInEx","plugins","VRoidXYTool.IL2CPP","VRoidXYTool.IL2CPP.dll");
    string companion=Path.Combine(target,"VRoidXYTool","Companion","VRoidXYTool.Companion.exe");
    Check(File.Exists(plugin) && File.Exists(companion),"actual plugin and self-contained Companion payload installed");
    Check(File.Exists(Path.Combine(target,"VRoidXYTool","Companion","coreclr.dll")),"Companion includes its CLR and needs no separate .NET install");
    string config=Path.Combine(target,"BepInEx","config","io.github.nytmaer.vroidxytool.il2cpp.cfg");
    Check(File.ReadAllText(config).Contains(Path.Combine(data,"Bridge","bridge-state.json")) && File.ReadAllText(config).Contains("ModelPath =\n"),"new configuration uses chosen user folder and disables document diagnostics");
    string manifestPath=Path.Combine(target,".VRoidXYTool.setup","installed.json");
    var manifest=JsonSerializer.Deserialize<InstallManifest>(File.ReadAllText(manifestPath))!;
    Check(manifest.Files.Count>400 && manifest.OwnsLoader,"install journal tracks only package/loader files");
    Check(manifest.Files.All(f=>Installer.Hash(Installer.SafePath(target,f.Path))==f.Hash),"all installed files match their recorded checksums");
    Check(!Installer.NeedsLoader(new(Path.Combine(target,"VRoidStudio.exe"),target,data,false)),"compatible existing loader allows offline repair");
    string oldPluginHash=Installer.Hash(plugin),oldManifestHash=Installer.Hash(manifestPath);
    using(var bad=SmallPayload("BepInEx/plugins/VRoidXYTool.IL2CPP/VRoidXYTool.IL2CPP.dll","broken update",badHash:true))
        Reject(()=>Installer.Install(new(Path.Combine(target,"VRoidStudio.exe"),target,data,false),bad,loader),"checksum failure rolls back an update");
    Check(Installer.Hash(plugin)==oldPluginHash && Installer.Hash(manifestPath)==oldManifestHash,"rollback preserves installed plugin and previous journal");
    using(var malicious=SmallPayload("../escape.txt","bad"))
        Reject(()=>Installer.Install(new(Path.Combine(target,"VRoidStudio.exe"),target,data,false),malicious,loader),"traversal payload rejected before installation");
    Check(!File.Exists(Path.Combine(root,"escape.txt")),"package cannot write outside target");
    using(var cancelled=File.OpenRead(payload))
        Reject(()=>Installer.Install(new(Path.Combine(target,"VRoidStudio.exe"),target,data,false),cancelled,loader,cancellation:new CancellationToken(true)),"cancelled operation does not install an update");
    Check(Installer.Hash(plugin)==oldPluginHash,"cancel preserves original installed bytes");
    File.WriteAllText(config,"[TextureSync]\nPollMilliseconds = 777\n[Companion]\nStatePath = custom.json\n");
    using(var repaired=File.OpenRead(payload))Installer.Install(new(Path.Combine(target,"VRoidStudio.exe"),target,data,false),repaired,loader);
    Check(File.ReadAllText(config).Contains("777"),"repair preserves edited user settings");
    var kept=Installer.Uninstall(target);
    Check(kept.Contains("BepInEx/config/io.github.nytmaer.vroidxytool.il2cpp.cfg") && File.Exists(config),"uninstall preserves edited configuration and recovery journal");
    Check(!File.Exists(plugin) && !File.Exists(companion) && File.Exists(Path.Combine(target,"VRoidStudio.exe")),"uninstall removes unchanged tool files and preserves VRoid");
    Check(File.ReadAllText(Path.Combine(data,"Textures","preserved.png"))=="external artwork","user artwork survives installation, repair and uninstall");
    Check(Installer.NeedsLoader(new(Path.Combine(target,"VRoidStudio.exe"),target,data,false)),"owned partial uninstall can request a loader for repair");
    using(var small=SmallPayload("BepInEx/plugins/VRoidXYTool.IL2CPP/VRoidXYTool.IL2CPP.dll","restored tool"))Installer.Install(new(Path.Combine(target,"VRoidStudio.exe"),target,data,false),small,loader);
    Check(File.Exists(plugin) && File.ReadAllText(config).Contains("777"),"repair restores owned missing loader while preserving edited configuration");

    string existing=Path.Combine(root,"Existing compatible loader");Fixture(existing);ZipFile.ExtractToDirectory(loader,existing);
    string existingPlugin=Path.Combine(existing,"BepInEx","plugins","VRoidXYTool.IL2CPP","VRoidXYTool.IL2CPP.dll");Directory.CreateDirectory(Path.GetDirectoryName(existingPlugin)!);File.WriteAllText(existingPlugin,"previous plugin bytes");
    using(var small=SmallPayload("BepInEx/plugins/VRoidXYTool.IL2CPP/VRoidXYTool.IL2CPP.dll","updated bytes"))Installer.Install(new(Path.Combine(existing,"VRoidStudio.exe"),existing,data,false),small,null);
    using(var small=SmallPayload("BepInEx/plugins/VRoidXYTool.IL2CPP/VRoidXYTool.IL2CPP.dll","repaired bytes"))Installer.Install(new(Path.Combine(existing,"VRoidStudio.exe"),existing,data,false),small,null);
    Installer.Uninstall(existing);
    Check(File.ReadAllText(existingPlugin)=="previous plugin bytes","repair retains original backup and uninstall restores it");
    Check(File.Exists(Path.Combine(existing,"winhttp.dll")),"pre-existing compatible loader is never removed");

    string old=Path.Combine(root,"Old incompatible loader");Fixture(old);Directory.CreateDirectory(Path.Combine(old,"BepInEx","core"));File.WriteAllText(Path.Combine(old,"BepInEx","core","BepInEx.dll"),"old loader");
    using(var small=SmallPayload("BepInEx/plugins/VRoidXYTool.IL2CPP/VRoidXYTool.IL2CPP.dll","updated bytes"))Reject(()=>Installer.Install(new(Path.Combine(old,"VRoidStudio.exe"),old,data,false),small,loader),"existing incompatible loader is refused rather than overwritten");
    Check(File.ReadAllText(Path.Combine(old,"BepInEx","core","BepInEx.dll"))=="old loader","incompatible loader bytes remain unchanged");
    string unsupported=Path.Combine(root,"Unsupported build");Fixture(unsupported);File.WriteAllText(Path.Combine(unsupported,"VRoidStudio_Data","boot.config"),"build-guid=other");Reject(()=>Installer.Inspect(Path.Combine(unsupported,"VRoidStudio.exe")),"unknown application build refused");
    using(var small=SmallPayload("BepInEx/plugins/VRoidXYTool.IL2CPP/VRoidXYTool.IL2CPP.dll","updated bytes"))Reject(()=>Installer.Install(new(Path.Combine(source,"VRoidStudio.exe"),Path.Combine(source,"Nested copy"),data,true),small,loader),"nested copies refused");

    string shared=Path.Combine(root,"Loader used by another plugin");Fixture(shared);
    using(var small=SmallPayload("BepInEx/plugins/VRoidXYTool.IL2CPP/VRoidXYTool.IL2CPP.dll","updated bytes"))Installer.Install(new(Path.Combine(shared,"VRoidStudio.exe"),shared,data,false),small,loader);
    File.WriteAllText(Path.Combine(shared,"BepInEx","plugins","AnotherPlugin.dll"),"not ours");Installer.Uninstall(shared);
    Check(File.Exists(Path.Combine(shared,"winhttp.dll")) && File.Exists(Path.Combine(shared,"BepInEx","plugins","AnotherPlugin.dll")),"uninstall retains owned loader when another plugin depends on it");
    Console.WriteLine($"PASS: {checks} installer checks, including actual packaged payload roundtrip. Native application startup is not tested by this harness.");
}
finally
{
    string checkedRoot=Path.GetFullPath(root);
    if(!checkedRoot.StartsWith(testBase+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase) || Path.GetFileName(checkedRoot).Length!=32)throw new Exception("Unsafe test cleanup target.");
    Directory.Delete(checkedRoot,true);
}
void Fixture(string folder)
{
    Directory.CreateDirectory(folder);File.Copy(realExe,Path.Combine(folder,"VRoidStudio.exe"));
    foreach(string file in new[]{"GameAssembly.dll","UnityPlayer.dll"})File.WriteAllText(Path.Combine(folder,file),"fixture");
    Directory.CreateDirectory(Path.Combine(folder,"VRoidStudio_Data","il2cpp_data","Metadata"));
    File.WriteAllText(Path.Combine(folder,"VRoidStudio_Data","app.info"),"pixiv\nVRoid Studio");
    File.WriteAllText(Path.Combine(folder,"VRoidStudio_Data","boot.config"),"build-guid=49044bb6ab464dbc934e49a426e6a25b");
    File.WriteAllText(Path.Combine(folder,"VRoidStudio_Data","il2cpp_data","Metadata","global-metadata.dat"),"fixture");
}
MemoryStream SmallPayload(string path,string value,bool badHash=false)
{
    var stream=new MemoryStream();using(var zip=new ZipArchive(stream,ZipArchiveMode.Create,true))
    {
        byte[] bytes=Encoding.UTF8.GetBytes(value);using(var item=zip.CreateEntry(path).Open())item.Write(bytes);
        using var manifest=new StreamWriter(zip.CreateEntry("package-hashes.json").Open());manifest.Write(JsonSerializer.Serialize(new Dictionary<string,string>{{path,badHash?new string('0',64):Convert.ToHexString(SHA256.HashData(bytes))}}));
    }
    stream.Position=0;return stream;
}
void Check(bool condition,string name){if(!condition)throw new Exception(name);checks++;Console.WriteLine("PASS: "+name);}
void Reject(Action action,string name){bool rejected=false;try{action();}catch(Exception e)when(e is IOException or InvalidDataException or InvalidOperationException or OperationCanceledException){rejected=true;}Check(rejected,name);}
