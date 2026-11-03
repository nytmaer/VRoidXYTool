using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace VRoidXYTool.Setup;

public sealed record InstalledFile(string Path, string Hash, string? Backup);
public sealed class InstallManifest
{
    public int Schema { get; set; } = 1;
    public string Product { get; set; } = "VRoidXYTool-local-candidate";
    public string BackupId { get; set; } = Guid.NewGuid().ToString("N");
    public string DataDirectory { get; set; } = "";
    public bool OwnsLoader { get; set; }
    public List<InstalledFile> Files { get; set; } = new();
}
public sealed record InstallRequest(string SourceExecutable, string Destination, string DataDirectory, bool CopyApplication);
public sealed record Layout(string Root, string Description);

public static class Installer
{
    public const string LoaderUrl = "https://builds.bepinex.dev/projects/bepinex_be/788/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.788%2B5b766a3.zip";
    public const string LoaderSha256 = "F4CC496BD098A0DF4164B81E3737297707F13A47C2478DBA2F60EEFAB784817A";
    private const string State = ".VRoidXYTool.setup";
    private const string ManifestName = ".VRoidXYTool.setup/installed.json";
    private const string Config = "BepInEx/config/io.github.nytmaer.vroidxytool.il2cpp.cfg";
    private static readonly Dictionary<string,string> LoaderIdentity = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BepInEx/core/BepInEx.Unity.IL2CPP.dll"]="46CF1EF802BDF8CB6587FC1E4D98F7AF1073A60487B39A64E230E6F6E4C23AEC",
        ["BepInEx/core/BepInEx.Core.dll"]="1D492EEECD40CD4FEB7DC35AFF7F08839928AC1637D4993BBDE0060DE36B7E0A",
        ["winhttp.dll"]="8C6CDBC38836DEE87E3368F5DE1994D7C0CCEBF29E4CE7ABA3C0981F9375412C",
        ["dotnet/coreclr.dll"]="04377968C540F704AAE92ECB0EC9BBFD5FDDFE226DFCA30AA3EEB52006A44FD8"
    };
    public static string Hash(string path)
    {
        using var stream=File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
    public static string SafePath(string root,string relative)
    {
        if(string.IsNullOrWhiteSpace(relative) || relative.Contains(':') || relative.Contains('\\') || Path.IsPathRooted(relative)) throw new InvalidDataException("Unsafe package path.");
        string[] parts=relative.Split('/');
        if(parts.Any(p=>p is "" or "." or ".." || p.EndsWith('.') || p.EndsWith(' ') || p.IndexOfAny(Path.GetInvalidFileNameChars())>=0)) throw new InvalidDataException("Unsafe package path.");
        string fullRoot=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
        string full=Path.GetFullPath(Path.Combine(fullRoot,Path.Combine(parts)));
        if(!full.StartsWith(fullRoot,StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Package path escapes its destination.");
        string current=Path.GetPathRoot(full)!;
        foreach(string component in full[ current.Length.. ].Split(Path.DirectorySeparatorChar))
        {
            current=Path.Combine(current,component);
            FileSystemInfo item=Directory.Exists(current)?new DirectoryInfo(current):new FileInfo(current);
            if(item.Exists && item.LinkTarget!=null)throw new IOException("Installation cannot traverse symbolic links or junctions: "+current);
        }
        return full;
    }
    public static Layout Inspect(string executable)
    {
        string path=Path.GetFullPath(executable);
        if(!Path.GetFileName(path).Equals("VRoidStudio.exe",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Choose VRoidStudio.exe.");
        string root=Path.GetDirectoryName(path)!;
        foreach(string file in new[]{"VRoidStudio.exe","GameAssembly.dll","UnityPlayer.dll","VRoidStudio_Data/app.info","VRoidStudio_Data/boot.config","VRoidStudio_Data/il2cpp_data/Metadata/global-metadata.dat"})
            if(!File.Exists(SafePath(root,file)))throw new InvalidDataException("Required VRoid file is missing: "+file);
        string app=File.ReadAllText(SafePath(root,"VRoidStudio_Data/app.info"));
        if(!app.Contains("pixiv",StringComparison.OrdinalIgnoreCase) || !app.Contains("VRoid Studio",StringComparison.Ordinal))throw new InvalidDataException("This folder is not recognized as VRoid Studio.");
        using(var stream=File.OpenRead(path))
        using(var reader=new BinaryReader(stream))
        {
            if(stream.Length<256 || reader.ReadUInt16()!=0x5a4d)throw new InvalidDataException("Invalid VRoid executable.");
            stream.Position=0x3c;int offset=reader.ReadInt32();
            if(offset<64 || offset>stream.Length-6)throw new InvalidDataException("Invalid executable header.");
            stream.Position=offset;
            if(reader.ReadUInt32()!=0x00004550 || reader.ReadUInt16()!=0x8664)throw new InvalidDataException("Only Windows x64 is supported.");
        }
        string unity=FileVersionInfo.GetVersionInfo(path).ProductVersion??"";
        if(!unity.StartsWith("6000.0.62f1",StringComparison.Ordinal))throw new InvalidDataException("Unsupported Unity build. This candidate supports VRoid 2.14.0 and 2.16.0 / Unity 6000.0.62f1.");
        string boot=File.ReadAllText(SafePath(root,"VRoidStudio_Data/boot.config"));
        string[] buildIds=boot.Split('\n').Select(line=>line.Trim()).Where(line=>line.StartsWith("build-guid=",StringComparison.OrdinalIgnoreCase)).Select(line=>line[11..]).ToArray();
        string version=buildIds.Length==1 ? buildIds[0].ToLowerInvariant() switch
        {
            "49044bb6ab464dbc934e49a426e6a25b" => "2.14.0",
            "a34c8843981a4aada647acf24a55dece" => "2.16.0",
            _ => ""
        } : "";
        if(version.Length==0)throw new InvalidDataException("Unrecognized VRoid build. This candidate accepts the explicit 2.14.0 and 2.16.0 build profiles only.");
        return new(root,$"Windows x64 build profile: VRoid {version} / Unity 6000.0.62f1. The plugin checks the actual application and Unity versions at startup.");
    }
    public static void EnsureClosed(string root)
    {
        foreach(var process in Process.GetProcessesByName("VRoidStudio").Concat(Process.GetProcessesByName("VRoidXYTool.Companion")))
        using(process)
        {
            try
            {
                string? exe=process.MainModule?.FileName;
                string targetRoot=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
                if(exe!=null && exe.StartsWith(targetRoot,StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Close VRoid and its Companion in this installation before changing files.");
            }
            catch(System.ComponentModel.Win32Exception){throw new InvalidOperationException("Cannot inspect a running VRoid instance. Close VRoid before continuing.");}
        }
    }
    private static bool LoaderPresent(string root,bool allowOwnedRepair=false)
    {
        if(!Directory.Exists(Path.Combine(root,"BepInEx")) && !Directory.Exists(Path.Combine(root,"dotnet")) && !File.Exists(Path.Combine(root,"winhttp.dll")) && !File.Exists(Path.Combine(root,"doorstop_config.ini")))return false;
        bool missing=false;
        if(File.Exists(Path.Combine(root,"BepInEx","core","BepInEx.dll")))throw new InvalidDataException("BepInEx 5 is incompatible; use a clean copy.");
        foreach(var entry in LoaderIdentity)
        {
            if(!File.Exists(SafePath(root,entry.Key))) {missing=true;continue;}
            if(!Hash(SafePath(root,entry.Key)).Equals(entry.Value,StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("An incompatible or incomplete loader exists. Use a clean writable copy; setup will not overwrite another BepInEx installation.");
        }
        string doorstop=Path.Combine(root,"doorstop_config.ini");
        if(!File.Exists(doorstop))missing=true;
        else if(!File.ReadAllText(doorstop).Contains("BepInEx\\core\\BepInEx.Unity.IL2CPP.dll",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("The existing loader configuration is incompatible.");
        if(missing && !allowOwnedRepair)throw new InvalidDataException("An incomplete loader exists; use a clean copy rather than overlaying it.");
        return !missing;
    }
    public static bool NeedsLoader(InstallRequest request)
    {
        string root=Path.GetFullPath(request.Destination);
        var previous=ReadManifest(root);
        return !LoaderPresent(root,previous?.OwnsLoader==true);
    }
    public static async Task DownloadLoader(string destination,IProgress<string>? progress,CancellationToken cancellation)
    {
        progress?.Report("Downloading the pinned BepInEx loader from its official source…");
        using var client=new HttpClient{Timeout=TimeSpan.FromMinutes(5)};
        using var response=await client.GetAsync(LoaderUrl,HttpCompletionOption.ResponseHeadersRead,cancellation);
        response.EnsureSuccessStatusCode();
        await using var input=await response.Content.ReadAsStreamAsync(cancellation);
        await using(var output=new FileStream(destination,FileMode.CreateNew,FileAccess.Write,FileShare.None))
        {
            byte[] buffer=new byte[65536];long total=0;int read;
            while((read=await input.ReadAsync(buffer,cancellation))>0)
            {
                total+=read;if(total>64L*1024*1024)throw new InvalidDataException("Loader download exceeds its size limit.");
                await output.WriteAsync(buffer.AsMemory(0,read),cancellation);
            }
        }
        VerifyLoader(destination);
    }
    public static void VerifyLoader(string path)
    {
        if(!Hash(path).Equals(LoaderSha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("BepInEx checksum mismatch; no loader files were installed.");
    }
    public static InstallManifest? ReadManifest(string root)
    {
        string path=SafePath(root,ManifestName);
        if(!File.Exists(path))return null;
        if(new FileInfo(path).Length>1024*1024)throw new InvalidDataException("Install manifest is too large.");
        var data=JsonSerializer.Deserialize<InstallManifest>(File.ReadAllText(path))??throw new InvalidDataException("Invalid install manifest.");
        if(data.Schema!=1 || data.Product!="VRoidXYTool-local-candidate" || data.Files==null || data.Files.Count>4096 || data.BackupId.Length!=32 || !data.BackupId.All(Uri.IsHexDigit))throw new InvalidDataException("Invalid install manifest.");
        var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var file in data.Files)
        {
            SafePath(root,file.Path);
            if(!Allowed(file.Path,true) || !paths.Add(file.Path) || file.Hash.Length!=64 || !file.Hash.All(Uri.IsHexDigit))throw new InvalidDataException("Invalid installed file record.");
            if(file.Backup!=null && !file.Backup.StartsWith(State+"/backups/"+data.BackupId+"/",StringComparison.Ordinal))throw new InvalidDataException("Invalid backup record.");
            if(file.Backup!=null)SafePath(root,file.Backup);
        }
        return data;
    }
    private static bool Allowed(string path,bool loader)=>path.StartsWith("BepInEx/plugins/VRoidXYTool.IL2CPP/",StringComparison.Ordinal) || path.StartsWith("VRoidXYTool/",StringComparison.Ordinal) || path==Config || loader && (path.StartsWith("BepInEx/core/",StringComparison.Ordinal) || path.StartsWith("dotnet/",StringComparison.Ordinal) || path is "winhttp.dll" or "doorstop_config.ini" or ".doorstop_version" or "changelog.txt");
    public static void Install(InstallRequest request,Stream payload,string? loaderZip,IProgress<string>? progress=null,CancellationToken cancellation=default)
    {
        var source=Inspect(request.SourceExecutable);
        string root=Path.GetFullPath(request.Destination).TrimEnd(Path.DirectorySeparatorChar);
        string data=Path.GetFullPath(request.DataDirectory).TrimEnd(Path.DirectorySeparatorChar);
        if(root.Length<4 || data.Length<4)throw new InvalidDataException("Choose a full installation and data folder.");
        if(request.CopyApplication && (root.Equals(source.Root,StringComparison.OrdinalIgnoreCase) || root.StartsWith(source.Root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase) || source.Root.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)))throw new InvalidDataException("A writable copy must use a separate, non-nested folder.");
        if(!request.CopyApplication && !root.Equals(source.Root,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("In-place destination must match the selected VRoid folder.");
        if(data.Equals(root,StringComparison.OrdinalIgnoreCase) || data.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Keep user data outside the application folder.");
        EnsureClosed(source.Root);EnsureClosed(root);
        var previous=ReadManifest(root);
        bool clone=request.CopyApplication && !File.Exists(Path.Combine(root,"VRoidStudio.exe"));
        if(clone && Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any())throw new IOException("The new copy destination must be empty.");
        bool existingLoader=!clone && LoaderPresent(root,previous?.OwnsLoader==true);
        if(!existingLoader){if(loaderZip==null)throw new InvalidOperationException("The pinned loader ZIP is required for this installation.");VerifyLoader(loaderZip);}
        using var package=new ZipArchive(payload,ZipArchiveMode.Read,true);
        var hashEntry=package.GetEntry("package-hashes.json")??throw new InvalidDataException("Payload manifest is missing.");
        Dictionary<string,string> hashes;
        using(var reader=new StreamReader(hashEntry.Open()))hashes=JsonSerializer.Deserialize<Dictionary<string,string>>(reader.ReadToEnd())??throw new InvalidDataException("Invalid payload manifest.");
        var files=package.Entries.Where(e=>!e.FullName.EndsWith('/') && e.FullName!="package-hashes.json").ToArray();
        ValidateEntries(root,files,false);
        if(files.Length!=hashes.Count || files.Any(e=>!hashes.ContainsKey(e.FullName)))throw new InvalidDataException("Payload manifest differs from packaged files.");
        using ZipArchive? loader=existingLoader?null:ZipFile.OpenRead(loaderZip!);
        var loaderFiles=loader?.Entries.Where(e=>!e.FullName.EndsWith('/')).ToArray()??Array.Empty<ZipArchiveEntry>();
        ValidateEntries(root,loaderFiles,true);
        foreach(var entry in loaderFiles.Concat(files))
        {
            string target=SafePath(root,entry.FullName);
            if(File.Exists(target) && (File.GetAttributes(target)&FileAttributes.ReadOnly)!=0)throw new IOException("A destination file is read-only; use a clean writable copy: "+entry.FullName);
        }
        var manifest=previous??new InstallManifest();manifest.DataDirectory=data;manifest.OwnsLoader=manifest.OwnsLoader || !existingLoader;
        var owned=manifest.Files.ToDictionary(f=>f.Path,StringComparer.OrdinalIgnoreCase);
        var rollback=new List<(string Path,string? Backup)>();var copied=new List<string>();
        string transaction=State+"/transactions/"+Guid.NewGuid().ToString("N");
        try
        {
            Directory.CreateDirectory(root);
            if(clone)CloneApplication(source.Root,root,copied,progress,cancellation);
            Inspect(Path.Combine(root,"VRoidStudio.exe"));
            Directory.CreateDirectory(data);
            foreach(string folder in new[]{"Textures","Bridge","Recordings"})Directory.CreateDirectory(Path.Combine(data,folder));
            foreach(var entry in loaderFiles.Concat(files))
            {
                cancellation.ThrowIfCancellationRequested();
                progress?.Report("Installing "+entry.FullName);
                InstallFile(entry.FullName,entry.Open(),hashes.GetValueOrDefault(entry.FullName));
            }
            string configPath=SafePath(root,Config);
            // Existing user configuration is preserved. Clean installs use per-user data.
            if(!File.Exists(configPath))
            {
                string config=$"[TextureSync]\nDirectory = {Path.Combine(data,"Textures")}\nEnabled = true\nShowControlsOnStartup = true\nPollMilliseconds = 250\n\n[Companion]\nStatePath = {Path.Combine(data,"Bridge","bridge-state.json")}\n\n[Recording]\nDirectory = {Path.Combine(data,"Recordings")}\n\n[Diagnostics]\nModelPath =\nOpenOnly = false\nQuitAfterSeconds = 0\n";
                using var stream=new MemoryStream(Encoding.UTF8.GetBytes(config));InstallFile(Config,stream,null);
            }
            manifest.Files=owned.Values.OrderBy(f=>f.Path,StringComparer.Ordinal).ToList();
            string statePath=SafePath(root,ManifestName);Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
            string temporary=statePath+".tmp";File.WriteAllText(temporary,JsonSerializer.Serialize(manifest,new JsonSerializerOptions{WriteIndented=true}));File.Move(temporary,statePath,true);
            progress?.Report("Installed. Start VRoid from this folder to generate interop files. First startup may require internet and take longer. Companion is in VRoidXYTool/Companion. Recording requires your own FFmpeg.");
        }
        catch
        {
            foreach(var item in rollback.AsEnumerable().Reverse())
            {
                string target=SafePath(root,item.Path);
                if(item.Backup==null){if(File.Exists(target))File.Delete(target);}
                else File.Copy(SafePath(root,item.Backup),target,true);
            }
            foreach(string file in copied.AsEnumerable().Reverse())if(File.Exists(file))File.Delete(file);
            throw;
        }
        finally
        {
            foreach(var item in rollback)if(item.Backup!=null && File.Exists(SafePath(root,item.Backup)))File.Delete(SafePath(root,item.Backup));
        }
        void InstallFile(string relative,Stream input,string? expected)
        {
            using(input)
            {
                string target=SafePath(root,relative);Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                string? temporaryBackup=null;
                if(File.Exists(target)){temporaryBackup=transaction+"/"+relative;string backup=SafePath(root,temporaryBackup);Directory.CreateDirectory(Path.GetDirectoryName(backup)!);File.Copy(target,backup);}
                string? original=owned.GetValueOrDefault(relative)?.Backup;
                if(!owned.ContainsKey(relative) && File.Exists(target))
                {
                    original=State+"/backups/"+manifest.BackupId+"/"+Guid.NewGuid().ToString("N")+"/"+relative;
                    string backup=SafePath(root,original);Directory.CreateDirectory(Path.GetDirectoryName(backup)!);File.Copy(target,backup);
                }
                using(var output=new FileStream(target,FileMode.Create,FileAccess.Write,FileShare.None)) {rollback.Add((relative,temporaryBackup));input.CopyTo(output);}
                string digest=Hash(target);
                if(expected!=null && !digest.Equals(expected,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Packaged file checksum mismatch: "+relative);
                owned[relative]=new(relative,digest,original);
            }
        }
    }
    private static void ValidateEntries(string root,IEnumerable<ZipArchiveEntry> entries,bool loader)
    {
        long total=0;var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var entry in entries)
        {
            SafePath(root,entry.FullName);
            if(!Allowed(entry.FullName,loader) || !paths.Add(entry.FullName))throw new InvalidDataException("Unexpected or duplicate package entry: "+entry.FullName);
            total+=entry.Length;if(entry.Length>256L*1024*1024 || total>512L*1024*1024)throw new InvalidDataException("Package exceeds installation size limits.");
        }
    }
    private static void CloneApplication(string source,string root,List<string> copied,IProgress<string>? progress,CancellationToken cancellation)
    {
        progress?.Report("Copying VRoid application files; your original installation is preserved…");
        var top=new[]{"VRoidStudio.exe","GameAssembly.dll","UnityPlayer.dll","baselib.dll","UnityCrashHandler64.exe","License.txt","steam_api64.dll","steam_appid.txt","vcruntime140.dll","msvcp140.dll"}.Where(f=>File.Exists(Path.Combine(source,f)));
        byte[] buffer=new byte[1024*1024];
        foreach(string relative in top.Concat(Directory.EnumerateFiles(Path.Combine(source,"VRoidStudio_Data"),"*",SearchOption.AllDirectories).Select(f=>Path.GetRelativePath(source,f).Replace('\\','/'))))
        {
            cancellation.ThrowIfCancellationRequested();
            string input=SafePath(source,relative),output=SafePath(root,relative);
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            using var original=File.OpenRead(input);
            using var copy=new FileStream(output,FileMode.CreateNew,FileAccess.Write,FileShare.None);
            copied.Add(output);
            int read;long copiedBytes=0;
            while((read=original.Read(buffer,0,buffer.Length))>0)
            {
                cancellation.ThrowIfCancellationRequested();copy.Write(buffer,0,read);copiedBytes+=read;
                if(copiedBytes%(32L*1024*1024)==0)progress?.Report("Copying "+relative+" ("+(copiedBytes/1024/1024)+" MiB)");
            }
        }
    }
    public static IReadOnlyList<string> Uninstall(string destination,IProgress<string>? progress=null)
    {
        string root=Path.GetFullPath(destination);EnsureClosed(root);
        var manifest=ReadManifest(root)??throw new InvalidOperationException("No installation record exists in this folder.");
        var preserved=new List<string>();
        string plugins=Path.Combine(root,"BepInEx","plugins");
        var ownedPlugins=manifest.Files.Where(f=>f.Path.StartsWith("BepInEx/plugins/",StringComparison.Ordinal)).ToDictionary(f=>SafePath(root,f.Path),StringComparer.OrdinalIgnoreCase);
        bool otherPlugins=Directory.Exists(plugins) && Directory.EnumerateFiles(plugins,"*",SearchOption.AllDirectories).Any(path=>!ownedPlugins.TryGetValue(path,out var record) || !Hash(path).Equals(record.Hash,StringComparison.OrdinalIgnoreCase));
        foreach(var item in manifest.Files.AsEnumerable().Reverse())
        {
            if(manifest.OwnsLoader && otherPlugins && !(item.Path.StartsWith("VRoidXYTool/",StringComparison.Ordinal) || item.Path.StartsWith("BepInEx/plugins/VRoidXYTool.IL2CPP/",StringComparison.Ordinal) || item.Path==Config))
            {preserved.Add(item.Path);continue;}
            string path=SafePath(root,item.Path);
            if(File.Exists(path) && (File.GetAttributes(path)&FileAttributes.ReadOnly)!=0){preserved.Add(item.Path);continue;}
            if(File.Exists(path) && !Hash(path).Equals(item.Hash,StringComparison.OrdinalIgnoreCase)) {preserved.Add(item.Path);continue;}
            if(item.Backup!=null)
            {
                string backup=SafePath(root,item.Backup);
                if(!File.Exists(backup)){preserved.Add(item.Path);continue;}
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.Copy(backup,path,true);
            }
            else if(File.Exists(path))File.Delete(path);
            progress?.Report("Removed/restored "+item.Path);
        }
        if(preserved.Count==0)File.Delete(SafePath(root,ManifestName));
        progress?.Report(preserved.Count==0?"Uninstalled tracked files. VRoid, generated interop, settings and user data remain.":"Uninstall preserved changed files. Installation record retained for recovery: "+string.Join(", ",preserved));
        return preserved;
    }
}
