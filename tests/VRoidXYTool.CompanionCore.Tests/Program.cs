using VRoidXYTool.CompanionCore;

string root = Path.Combine(Path.GetTempPath(), "vroid-companion-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    string png = Path.Combine(root, "layer.png"); File.WriteAllBytes(png, new byte[] { 1 });
    string state = Path.Combine(root, "state.json");
    var now = DateTimeOffset.UtcNow;
    var snapshot = new BridgeSnapshot { PublishedUtc = now, Running = true, DocumentSession = "session1", ExportRoot = root,
        Layers = new() { new LinkedTextureSnapshot { Id = "session1/layer1", Name = "Layer", PngPath = png } } };
    SnapshotFile.Write(state, snapshot);
    var read = SnapshotFile.Read(state);
    Check(read.Layers.Single().Id == "session1/layer1", "state roundtrip retains document/layer identity");
    Check(SnapshotFile.GetPngToOpen(read, "session1/layer1", now) == png, "live registered PNG can open");
    // Real PNG fixture: history must preserve artwork and refuse a changed document or damaged archive.
    byte[] originalPng = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+ip1sAAAAASUVORK5CYII=");
    File.WriteAllBytes(png, originalPng);
    string projectFolder = Path.Combine(root, "Character Ω"); Directory.CreateDirectory(projectFolder);
    string projectModel = Path.Combine(projectFolder, "Avatar.vroid"); File.WriteAllText(projectModel, "disposable model fixture");
    string projectFile = Path.Combine(projectFolder, "character-project.json");
    var character = CharacterProject.Create(projectFile, projectModel, "Avatar Ω");
    character.Notes = "Clothing ideas\nPalette: ocean blue"; character.Save(projectFile);
    var projectRead = CharacterProject.Load(projectFile);
    Check(projectRead.ModelPath == "Avatar.vroid" && projectRead.Notes == character.Notes && projectRead.Id == character.Id, "project identity, relative model association and notes survive restart");
    Reject(() => CharacterProject.Create(projectFile, projectModel, "Duplicate"), "creating a project cannot overwrite an existing manifest");
    var imageAsset = character.AddAsset(projectFile, png);
    var textureAsset = character.ArchiveLinkedTexture(projectFile, state, "session1", "session1/layer1");
    Check(textureAsset.Kind == "Texture" && textureAsset.SourceSession == "session1" && textureAsset.SourceLayerId == "session1/layer1", "project archives linked texture with explicit source identity");
    Reject(() => character.ArchiveLinkedTexture(projectFile, state, "other-session", "session1/layer1"), "project archive rejects changed document session");
    File.WriteAllBytes(png, new byte[] { 0 });
    Check(File.ReadAllBytes(character.ResolveAsset(projectFile, imageAsset.Id)).SequenceEqual(originalPng), "project image remains independent when live PNG changes");
    File.WriteAllBytes(png, originalPng);
    string relocated = Path.Combine(root, "Moved project"); Directory.Move(projectFolder, relocated);
    projectFile = Path.Combine(relocated, "character-project.json"); character = CharacterProject.Load(projectFile);
    Check(File.Exists(character.ResolveModel(projectFile)) && File.Exists(character.ResolveAsset(projectFile, imageAsset.Id)), "moving a project preserves contained model and asset paths");
    File.Delete(character.ResolveAsset(projectFile, imageAsset.Id));
    File.Delete(character.ResolveModel(projectFile));
    Check(CharacterProject.Load(projectFile).Assets.Count == 2, "missing models and images remain listed for recovery");
    string externalModel = Path.Combine(root, "External.vroid"); File.WriteAllText(externalModel, "external fixture");
    character.AssociateModel(projectFile, externalModel);
    Check(Path.IsPathFullyQualified(CharacterProject.Load(projectFile).ModelPath) && File.ReadAllText(externalModel) == "external fixture", "explicit external model association preserves original model bytes");
    character.Notes = new string('x', 65537);
    Reject(() => character.Save(projectFile), "oversized notes cannot overwrite project");
    character = CharacterProject.Load(projectFile);
    Check(character.Notes == "Clothing ideas\nPalette: ocean blue", "invalid project save preserves prior notes");
    character.Assets[0] = character.Assets[0] with { RelativePath = "../escaped.png" };
    Reject(() => character.Save(projectFile), "project asset path traversal is rejected");
    character = CharacterProject.Load(projectFile); character.SchemaVersion = 99;
    Reject(() => character.Save(projectFile), "unknown project schemas cannot overwrite metadata");
    File.WriteAllText(projectFile, new string(' ', 1024 * 1024 + 1));
    Reject(() => CharacterProject.Load(projectFile), "oversized project metadata is rejected");
    var version = TextureSnapshots.Capture(snapshot, "session1/layer1", "Before painting");
    Check(TextureSnapshots.List(snapshot, "session1/layer1").Single().Name == "Before painting", "named texture snapshot persists");
    // A second valid PNG is not needed to verify restoration: damaged current data must not be discarded.
    File.WriteAllBytes(png, new byte[] { 1, 2, 3 });
    Reject(() => TextureSnapshots.Restore(state, "session1", "session1/layer1", version.Id), "restore refuses when current artwork cannot be backed up");
    Check(File.ReadAllBytes(png).SequenceEqual(new byte[] { 1, 2, 3 }), "failed restore preserves current bytes");
    File.WriteAllBytes(png, originalPng);
    TextureSnapshots.Restore(state, "session1", "session1/layer1", version.Id);
    Check(TextureSnapshots.List(snapshot, "session1/layer1").Count == 2 && File.ReadAllBytes(png).SequenceEqual(originalPng), "restore creates recovery snapshot before replacing PNG");
    Reject(() => TextureSnapshots.Restore(state, "other-session", "session1/layer1", version.Id), "restore rejects changed document session");
    string archived = TextureSnapshots.GetImage(snapshot, "session1/layer1", version.Id);
    File.WriteAllBytes(archived, new byte[] { 1 });
    Reject(() => TextureSnapshots.Restore(state, "session1", "session1/layer1", version.Id), "corrupt archived PNG cannot overwrite artwork");
    File.WriteAllBytes(png, originalPng[..24]);
    Reject(() => TextureSnapshots.Capture(snapshot, "session1/layer1", "Partial save"), "partial PNG cannot become a snapshot");
    File.WriteAllBytes(png, originalPng);
    Reject(() => SnapshotFile.GetPngToOpen(read, "session1/layer1", now.AddSeconds(7)), "stale heartbeat cannot open PNG");
    read.Running = false;
    Reject(() => SnapshotFile.GetPngToOpen(read, "session1/layer1", now), "normal shutdown cannot open PNG");
    read.Running = true;
    Reject(() => SnapshotFile.GetPngToOpen(read, "old-session/layer1", now), "old document identity cannot open PNG");
    read.Layers[0].PngPath = Path.Combine(root, "..", "escaped.png");
    Reject(() => SnapshotFile.GetPngToOpen(read, "session1/layer1", now), "path traversal rejected");
    read.Layers[0].PngPath = Path.Combine(root, "program.exe"); File.WriteAllText(read.Layers[0].PngPath, "fixture");
    Reject(() => SnapshotFile.GetPngToOpen(read, "session1/layer1", now), "non-PNG cannot be launched as a texture");
    read.Layers[0].PngPath = Path.Combine(root, "missing.png");
    Reject(() => SnapshotFile.GetPngToOpen(read, "session1/layer1", now), "missing PNG handled");
    read.SchemaVersion = 2; SnapshotFile.Write(state, read);
    Reject(() => SnapshotFile.Read(state), "unknown schema rejected");
    read.SchemaVersion = 1; read.Layers.Clear(); SnapshotFile.Write(state, read);
    Check(SnapshotFile.Read(state).Layers.Count == 0, "atomic replacement clears old document rows");
    File.WriteAllText(state, "{partial"); Reject(() => SnapshotFile.Read(state), "malformed state handled");
    File.WriteAllText(state, "{\"SchemaVersion\":1,\"Layers\":[null]}"); Reject(() => SnapshotFile.Read(state), "null layer entries rejected");
    File.WriteAllText(state, new string(' ', 1024 * 1024 + 1)); Reject(() => SnapshotFile.Read(state), "oversized state rejected");
    Console.WriteLine("PASS: all Companion contract regressions");
    string settings = Path.Combine(root, "preferences", "settings.json");
    Check(CompanionPreferences.Load(settings).BridgePath == null, "missing preferences use defaults");
    new CompanionPreferences { BridgePath = state, EditorPath = "C:/editor path/editor.exe", ProjectPath = "C:/Character/character-project.json" }.Save(settings);
    var preferences = CompanionPreferences.Load(settings);
    Check(preferences.BridgePath == state && preferences.EditorPath == "C:/editor path/editor.exe", "bridge and editor preferences survive restart");
    Check(preferences.ProjectPath == "C:/Character/character-project.json", "last project selection survives Companion restart");
    new CompanionPreferences { BridgePath = state, UseDefaultEditor = true }.Save(settings);
    Check(CompanionPreferences.Load(settings).UseDefaultEditor, "explicit default editor choice survives restart");
    File.WriteAllText(settings, "{partial"); Check(CompanionPreferences.Load(settings).EditorPath == null, "corrupt preferences recover to defaults");
    File.WriteAllText(settings, new string(' ', 16385)); Check(CompanionPreferences.Load(settings).BridgePath == null, "oversized preferences recover to defaults");
    string cameraFile = Path.Combine(root, "camera.json");
    var cameraPresets = new WorkspacePresets();
    cameraPresets.Cameras[0] = new CameraPreset(new Point3(0, 1, 4), new Point3(0, 1, 0), true, .9f);
    cameraPresets.Save(cameraFile);
    Check(WorkspacePresets.Load(cameraFile).Cameras[0] == cameraPresets.Cameras[0], "camera view and projection persist");
    cameraPresets.Cameras[0] = cameraPresets.Cameras[0]! with { Size = -1 };
    Reject(() => cameraPresets.Save(cameraFile), "invalid camera values cannot overwrite presets");
    Check(WorkspacePresets.Load(cameraFile).Cameras[0]!.Size == .9f, "failed save preserves previous valid view");
    File.WriteAllText(cameraFile, "{\"SchemaVersion\":99}"); Reject(() => WorkspacePresets.Load(cameraFile), "unknown camera schema rejected");
    File.WriteAllText(cameraFile, "{\"SchemaVersion\":1,\"Cameras\":[{\"Position\":{\"X\":0,\"Y\":1,\"Z\":4},\"Target\":{\"X\":0,\"Y\":1,\"Z\":0},\"Orthographic\":true,\"Size\":0.9},null,null,null]}");
    var migrated = WorkspacePresets.Load(cameraFile);
    Check(migrated.SchemaVersion == 2 && migrated.Orthographic[0]?.Size == .9f && migrated.Perspective[0] == null, "old shared camera slots migrate into the correct projection bank");
    migrated.Perspective[9] = new CameraPreset(new Point3(0, 1, 4), new Point3(0, 1, 0), false, .8f) { Rotation = new Point3(10, 180, 25) };
    migrated.Save(cameraFile);
    Check(WorkspacePresets.Load(cameraFile).Perspective[9]?.Rotation?.Z == 25, "tenth camera slot preserves roll independently of orthographic bank");
    string guideFile = Path.Combine(root, "guides.json");
    var guides = new GuidePresets { Images = new() { new GuideImagePreset { Path = Path.Combine(root, "missing.jpg"), WorldSpace = true, Position = new Point3(0, 1, -1), Rotation = new Point3(0, 90, 0) } } };
    guides.Save(guideFile);
    Check(GuidePresets.Load(guideFile).Images[0] == guides.Images[0], "missing reference retains its settings for later recovery");
    guides.Images[0] = guides.Images[0] with { Alpha = float.NaN };
    Reject(() => guides.Save(guideFile), "non-finite guide opacity rejected before overwrite");
    Check(GuidePresets.Load(guideFile).Images[0].Alpha == .35f, "failed guide save preserves previous preset");
    File.WriteAllText(guideFile, new string(' ', 131073));
    Reject(() => GuidePresets.Load(guideFile), "oversized guide preset rejected");
    byte[] jpegHeader = { 255,216,255,192,0,8,8,0,32,0,64,0,255,217 };
    Check(JpegGuard.IsBoundedJpeg(jpegHeader), "JPEG dimensions inspected before native allocation");
    Check(JpegGuard.TryGetDimensions(jpegHeader,out int jpegWidth,out int jpegHeight) && jpegWidth==64 && jpegHeight==32,"JPEG budget uses actual decoded dimensions");
    Check(ReferenceImageBudget.Validate(4096,4096,192L*1024*1024)==64L*1024*1024,"reference texture budget accepts its exact boundary");
    Reject(()=>ReferenceImageBudget.Validate(4096,4096,192L*1024*1024+1),"combined reference texture memory cannot exceed 256 MiB");
    Reject(()=>ReferenceImageBudget.Validate(int.MaxValue,int.MaxValue,0),"reference dimensions cannot overflow memory accounting");
    jpegHeader[9] = 17;
    Check(!JpegGuard.IsBoundedJpeg(jpegHeader), "JPEG wider than 4096 rejected");
    Check(!JpegGuard.IsBoundedJpeg(jpegHeader.AsSpan(0,10)), "truncated JPEG header rejected");
    var emptyVmd = new byte[66];
    System.Text.Encoding.ASCII.GetBytes("Vocaloid Motion Data 0002").CopyTo(emptyVmd,0);
    VmdGuard.Validate(emptyVmd);
    Check(true,"empty VMD sections remain structurally valid");
    emptyVmd[50]=255;emptyVmd[51]=255;emptyVmd[52]=255;emptyVmd[53]=127;
    Reject(()=>VmdGuard.Validate(emptyVmd),"VMD claimed keyframe count cannot overrun the input");
    Reject(()=>VmdGuard.Validate(new byte[58]),"invalid VMD signature rejected");
    string legacyPose = Path.Combine(root,"legacy.posejson");
    File.WriteAllText(legacyPose,"""
        {"Data":{"HeadIK":{"TypeName":"SerializedHeadIK","Name":"HeadIK","FloatDict":{},"Vector3Dict":{},"QuaternionDict":{"CurrentRollRotation":{"x":0,"y":0,"z":0.2,"w":0.98}}}}}
        """);
    var controls = LegacyPoseImport.Read(legacyPose);
    using(var merged=System.Text.Json.JsonDocument.Parse(LegacyPoseImport.MergeWithCurrent("{\"Name\":\"HeadIK\",\"StartDir\":{\"x\":0,\"y\":1,\"z\":0},\"CurrentRollRotation\":{\"x\":0,\"y\":0,\"z\":0,\"w\":1}}",controls[0].Value)))
        Check(merged.RootElement.GetProperty("StartDir").GetProperty("y").GetSingle()==1 && merged.RootElement.GetProperty("CurrentRollRotation").GetProperty("z").GetSingle()==.2f,"partial legacy controls retain native fields while replacing imported values");
    Reject(()=>LegacyPoseImport.MergeWithCurrent("{\"Name\":\"HeadIK\"}",controls[0].Value),"unsupported native pose members are rejected before application");
    Check(controls.Count==1 && controls[0].Name=="HeadIK" && System.Text.Json.JsonDocument.Parse(controls[0].Value).RootElement.GetProperty("CurrentRollRotation").GetProperty("z").GetSingle()==.2f,"legacy pose control and quaternion retain their values");
    string validLegacy = File.ReadAllText(legacyPose);
    File.WriteAllText(legacyPose,validLegacy.Replace("SerializedHeadIK","ArbitraryRuntimeType"));
    Reject(()=>LegacyPoseImport.Read(legacyPose),"unknown legacy pose types cannot be instantiated");
    File.WriteAllText(legacyPose,validLegacy.Replace("0.2","1e30"));
    Reject(()=>LegacyPoseImport.Read(legacyPose),"unbounded legacy pose values rejected");
    File.WriteAllText(legacyPose,"{\"Data\":{}}");
    Reject(()=>LegacyPoseImport.Read(legacyPose),"empty legacy poses rejected before native application");
    File.WriteAllText(legacyPose,new string(' ',1024*1024+1));
    Reject(()=>LegacyPoseImport.Read(legacyPose),"oversized legacy poses rejected");
    // Connected file workflow; this does not replace live WPF/VRoid acceptance.
    string flowRoot = Path.Combine(root, "workflow"); Directory.CreateDirectory(flowRoot);
    string flowPng = Path.Combine(flowRoot, "live.png"), flowState = Path.Combine(flowRoot, "bridge.json");
    string flowModel = Path.Combine(flowRoot, "Disposable.vroid"), flowProject = Path.Combine(flowRoot, "character-project.json");
    File.WriteAllText(flowModel, "original disposable model bytes");
    byte[] textureA = FixturePng(255, 0, 0), textureB = FixturePng(0, 0, 255);
    File.WriteAllBytes(flowPng, textureA);
    var flowBridge = new BridgeSnapshot { Running = true, PublishedUtc = DateTimeOffset.UtcNow, DocumentSession = "workflow-session", ExportRoot = flowRoot,
        Layers = new() { new LinkedTextureSnapshot { Id = "workflow-layer", Name = "Body", PngPath = flowPng } } };
    SnapshotFile.Write(flowState, flowBridge);
    var flow = CharacterProject.Create(flowProject, flowModel, "Workflow character");
    flow.Notes = "End-to-end file workflow"; flow.Save(flowProject);
    var savedA = TextureSnapshots.Capture(flowBridge, "workflow-layer", "Red before edit");
    File.WriteAllBytes(flowPng, textureB);
    var storedB = flow.ArchiveLinkedTexture(flowProject, flowState, "workflow-session", "workflow-layer");
    Check(File.ReadAllBytes(TextureSnapshots.GetImage(flowBridge, "workflow-layer", savedA.Id)).SequenceEqual(textureA) &&
        File.ReadAllBytes(flowPng).SequenceEqual(textureB), "workflow comparison reads distinct saved A and edited B");
    TextureSnapshots.Restore(flowState, "workflow-session", "workflow-layer", savedA.Id);
    var recovery = TextureSnapshots.List(flowBridge, "workflow-layer").Single(x => x.Id != savedA.Id);
    Check(File.ReadAllBytes(flowPng).SequenceEqual(textureA) &&
        File.ReadAllBytes(TextureSnapshots.GetImage(flowBridge, "workflow-layer", recovery.Id)).SequenceEqual(textureB), "workflow restore returns A and preserves edited B in recovery snapshot");
    var reopened = CharacterProject.Load(flowProject);
    Check(reopened.Notes == flow.Notes && File.ReadAllBytes(reopened.ResolveAsset(flowProject, storedB.Id)).SequenceEqual(textureB),
        "workflow project restart retains notes and archived B after live PNG restore");
    Check(File.ReadAllText(flowModel) == "original disposable model bytes", "workflow never changes model bytes");
    flowBridge.DocumentSession = "different-model"; flowBridge.PublishedUtc = DateTimeOffset.UtcNow; SnapshotFile.Write(flowState, flowBridge);
    Reject(() => reopened.ArchiveLinkedTexture(flowProject, flowState, "workflow-session", "workflow-layer"), "workflow document switch blocks archive from prior session");
    Reject(() => TextureSnapshots.Restore(flowState, "workflow-session", "workflow-layer", savedA.Id), "workflow document switch blocks prior snapshot restore");
    Check(File.ReadAllBytes(flowPng).SequenceEqual(textureA), "workflow rejected actions preserve current PNG");
}
finally { Directory.Delete(root, true); }

static void Check(bool condition, string name) { if (!condition) throw new Exception(name); Console.WriteLine("PASS: " + name); }
static void Reject(Action action, string name)
{
    bool rejected = false;
    try { action(); } catch (Exception error) when (error is InvalidOperationException or IOException or InvalidDataException or System.Text.Json.JsonException) { rejected = true; }
    Check(rejected, name);
}

static byte[] FixturePng(byte red, byte green, byte blue)
{
    using var output = new MemoryStream();
    output.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
    Chunk("IHDR", new byte[] { 0, 0, 0, 1, 0, 0, 0, 1, 8, 6, 0, 0, 0 });
    using var compressed = new MemoryStream();
    using (var zlib = new System.IO.Compression.ZLibStream(compressed, System.IO.Compression.CompressionLevel.Fastest, true))
        zlib.Write(new byte[] { 0, red, green, blue, 255 });
    Chunk("IDAT", compressed.ToArray()); Chunk("IEND", Array.Empty<byte>());
    return output.ToArray();

    void Chunk(string type, byte[] bytes)
    {
        Span<byte> number = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(number, (uint)bytes.Length); output.Write(number);
        byte[] kind = System.Text.Encoding.ASCII.GetBytes(type); output.Write(kind); output.Write(bytes);
        uint checksum = 0xffffffff;
        foreach (byte value in kind.Concat(bytes))
        {
            checksum ^= value;
            for (int i = 0; i < 8; i++) checksum = (checksum >> 1) ^ ((checksum & 1) == 0 ? 0 : 0xedb88320u);
        }
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(number, ~checksum); output.Write(number);
    }
}
