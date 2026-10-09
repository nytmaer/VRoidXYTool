#nullable disable
using System.Globalization;
using System.Reflection;
using UnityEngine;
using Il2CppInterop.Runtime;
using VRoidXYTool.CompanionCore;
using VRoidXYTool.SyncCore;

namespace VRoidXYTool.IL2CPP;

internal sealed class ReferenceWorkspace : IDisposable
{
    private sealed class Image : IDisposable
    {
        internal GuideImagePreset Data;
        internal Texture2D Texture;
        internal GameObject Object;
        internal Material Material;
        internal string Error;
        public void Dispose() { if (Object != null) UnityEngine.Object.Destroy(Object); if (Material != null) UnityEngine.Object.Destroy(Material); if (Texture != null) UnityEngine.Object.Destroy(Texture); }
    }
    private readonly List<Image> images = new();
    private readonly string presetPath;
    private readonly NativeFilePicker picker = new();
    private static readonly BepInEx.Logging.ManualLogSource log = BepInEx.Logging.Logger.CreateLogSource("ReferenceWorkspace");
    private int selected;
    private AssetBundle bundle;
    private GameObject ruler;
    private bool screenGrid;
    internal string Message = "Paste an image path, then add a reference.";
    internal ReferenceWorkspace(string cameraPath) => presetPath = Path.Combine(Path.GetDirectoryName(cameraPath), "references.json");
    private static Vector3 V(Point3 p) => new(p.X, p.Y, p.Z);
    private void EnsureBundle()
    {
        if (bundle != null) return;
        using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("VRoidXYTool.guide");
        using var memory = new MemoryStream(); resource.CopyTo(memory);
        // Unity 6's memory overload crosses a Span<byte> signature that this pinned
        // interop runtime cannot marshal. Its file overload uses the same bundle.
        string cache = Path.Combine(BepInEx.Paths.CachePath, "VRoidXYTool-guide.bundle");
        Directory.CreateDirectory(Path.GetDirectoryName(cache));
        File.WriteAllBytes(cache, memory.ToArray());
        bundle = AssetBundle.LoadFromFile(cache);
        if (bundle == null) throw new InvalidDataException("Reference asset bundle could not be loaded.");
    }
    private T Asset<T>(string name) where T : UnityEngine.Object { EnsureBundle(); return bundle.LoadAsset(name, Il2CppType.Of<T>()).TryCast<T>() ?? throw new InvalidDataException("Missing reference asset: " + name); }
    private Image Load(GuideImagePreset data)
    {
        data.Validate();
        var item = new Image { Data = data };
        try
        {
            using var stream = File.OpenRead(data.Path);
            if (stream.Length > 64 * 1024 * 1024) throw new InvalidDataException("Reference exceeds 64 MiB.");
            using var memory = new MemoryStream(); stream.CopyTo(memory); byte[] bytes = memory.ToArray();
            bool png = Path.GetExtension(data.Path).Equals(".png", StringComparison.OrdinalIgnoreCase);
            if (!(png ? PngGuard.IsCompleteBoundedPng(bytes) : JpegGuard.IsBoundedJpeg(bytes))) throw new InvalidDataException("Invalid image or dimensions exceed 4096 pixels.");
            item.Texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(item.Texture, bytes, false)) throw new InvalidDataException("Image cannot be decoded.");
            if (data.WorldSpace)
            {
                item.Object = UnityEngine.Object.Instantiate(Asset<GameObject>("GuideImagePrefab")).TryCast<GameObject>();
                item.Material = new Material(Asset<Material>("GuideImageMat"));
                item.Object.GetComponent<Renderer>().material = item.Material;
                item.Material.mainTexture = item.Texture;
            }
            Apply(item); return item;
        }
        catch { item.Dispose(); throw; }
    }
    private static void Apply(Image image)
    {
        if (image.Object == null) return;
        image.Object.SetActive(image.Data.Visible);
        image.Object.transform.position = V(image.Data.Position);
        image.Object.transform.eulerAngles = V(image.Data.Rotation);
        image.Object.transform.localScale = new Vector3(image.Texture.width / 1000f * image.Data.Scale, image.Texture.height / 1000f * image.Data.Scale, 1);
        image.Material.SetColor("_Color", new Color(1, 1, 1, image.Data.Alpha));
    }
    private void Run(Action action) { try { action(); } catch (Exception e) { Message = e.Message; log.LogWarning(e); } }
    private static string ClipboardPath() => Path.GetFullPath(GUIUtility.systemCopyBuffer.Trim().Trim('"'));
    private void Add(bool world, string path)
    {
        if (images.Count >= 16) throw new InvalidOperationException("Limit: 16 reference images.");
        var image = Load(new GuideImagePreset { Path = Path.GetFullPath(path), WorldSpace = world, Position = world ? new Point3(0, 1, 0) : new Point3(.5f, .5f, 0) });
        images.Add(image); selected = images.Count - 1; Message = "Added " + Path.GetFileName(image.Data.Path);
    }
    private void SetRuler(bool visible)
    {
        if (visible && ruler == null)
        {
            ruler = UnityEngine.Object.Instantiate(Asset<GameObject>("box")).TryCast<GameObject>();
            ruler.transform.position = new Vector3(0, -.05f, 0); ruler.transform.localScale = Vector3.one * .36f;
        }
        if (ruler != null) ruler.SetActive(visible);
    }
    private void Save()
    {
        new GuidePresets { ScreenGrid = screenGrid, WorldGrid = ruler != null && ruler.activeSelf, Images = images.Select(i => i.Data).ToList() }.Save(presetPath);
        GUIUtility.systemCopyBuffer = presetPath; Message = "Saved references; preset path copied.";
    }
    private void Restore(bool legacy, string path = null)
    {
        var data = legacy ? LegacyWorkspaceImport.Guides(path ?? ClipboardPath()) : GuidePresets.Load(path ?? presetPath);
        var loaded = new List<Image>();
        try
        {
            foreach (var entry in data.Images)
            {
                try { loaded.Add(Load(entry)); }
                catch (Exception error) { loaded.Add(new Image { Data = entry, Error = error.Message }); }
            }
            if (data.WorldGrid) SetRuler(true);
        }
        catch { foreach (var item in loaded) item.Dispose(); throw; }
        foreach (var image in images) image.Dispose(); images.Clear(); images.AddRange(loaded);
        selected = 0; screenGrid = data.ScreenGrid; SetRuler(data.WorldGrid);
        Message = loaded.Any(i => i.Error != null) ? "Loaded settings; unavailable images can be retried." : "Reference preset loaded.";
    }
    internal void Controls(GUIStyle text, GUIStyle button)
    {
        bool B(int x, int y, int width, string label) => GUI.Button(new Rect(x, y, width, 34), label, button);
        if (B(35,112,235,"Add screen image")) Run(() => PickImage(false));
        if (B(285,112,235,"Add world image")) Run(() => PickImage(true));
        if (B(535,112,235,"Clear all")) DisposeImages();
        if (B(35,154,235,screenGrid ? "Hide screen grid" : "Show screen grid")) screenGrid = !screenGrid;
        if (B(285,154,235,ruler != null && ruler.activeSelf ? "Hide world ruler" : "Show world ruler")) Run(() => SetRuler(ruler == null || !ruler.activeSelf));
        if (B(535,154,235,"Save references")) Run(Save);
        if (B(35,196,235,"Load references")) Run(() => Restore(false));
        if (B(285,196,235,"Import legacy JSON")) Run(() => picker.Open("Import legacy references", new[] { "json" }, path => Restore(true,path), Failure));
        if (B(535,196,235,"Next reference")) selected = images.Count == 0 ? 0 : (selected + 1) % images.Count;
        if (B(35,520,355,"Paste path: add screen image")) Run(() => Add(false,ClipboardPath()));
        if (B(405,520,355,"Paste path: add world image")) Run(() => Add(true,ClipboardPath()));
        if (images.Count == 0) { GUI.Label(new Rect(35,248,740,70),Message,text); return; }
        var item = images[selected];
        GUI.Label(new Rect(35,240,740,36),$"{selected+1}/{images.Count}: {Path.GetFileName(item.Data.Path)} ({(item.Data.WorldSpace ? "world" : "screen")})",text);
        if (B(35,280,235,item.Data.Visible ? "Hide image" : "Show image")) { item.Data = item.Data with { Visible = !item.Data.Visible }; Apply(item); }
        if (B(285,280,235,"Delete image")) { item.Dispose(); images.RemoveAt(selected); selected = Math.Max(0,selected-1); return; }
        if (B(535,280,235,"Retry image")) Run(() => { var replacement = Load(item.Data); item.Dispose(); images[selected] = replacement; });
        if (B(35,322,175,"Smaller")) Change(item,item.Data with { Scale = Math.Max(.01f,item.Data.Scale-.1f) });
        if (B(220,322,175,"Larger")) Change(item,item.Data with { Scale = Math.Min(5,item.Data.Scale+.1f) });
        if (B(405,322,175,"Less opaque")) Change(item,item.Data with { Alpha = Math.Max(0,item.Data.Alpha-.1f) });
        if (B(590,322,175,"More opaque")) Change(item,item.Data with { Alpha = Math.Min(1,item.Data.Alpha+.1f) });
        if (B(35,364,355,"Paste position X,Y,Z")) Run(() => Change(item,item.Data with { Position = ParsePoint() }));
        if (B(405,364,355,"Paste rotation X,Y,Z")) Run(() => Change(item,item.Data with { Rotation = ParsePoint() }));
        GUI.Label(new Rect(35,410,740,100),$"Position: {item.Data.Position.X:F2}, {item.Data.Position.Y:F2}, {item.Data.Position.Z:F2}\nScale: {item.Data.Scale:F2}   Opacity: {item.Data.Alpha:F2}\n{item.Error ?? Message}",text);
    }
    private static Point3 ParsePoint()
    {
        var parts = GUIUtility.systemCopyBuffer.Split(',');
        if (parts.Length != 3) throw new InvalidDataException("Copy three comma-separated numbers: X,Y,Z.");
        return new Point3(float.Parse(parts[0],CultureInfo.InvariantCulture),float.Parse(parts[1],CultureInfo.InvariantCulture),float.Parse(parts[2],CultureInfo.InvariantCulture));
    }
    private void Failure(Exception error) { Message = error.Message; log.LogWarning(error); }
    private void PickImage(bool world) => picker.Open("Reference image",new[] { "png", "jpg", "jpeg" }, path => Add(world,path), Failure);
    internal void Update() => picker.Update();
    private static void Change(Image item, GuideImagePreset data) { data.Validate(); item.Data = data; Apply(item); }
    internal void DrawScreen(Rect area)
    {
        var color = GUI.color; GUI.BeginGroup(area);
        try
        {
            foreach (var item in images)
            {
                if (item.Data.WorldSpace || !item.Data.Visible || item.Texture == null) continue;
                float width = area.width * item.Data.Scale, height = width * item.Texture.height / item.Texture.width;
                GUI.color = new Color(1,1,1,item.Data.Alpha);
                GUI.DrawTexture(new Rect(area.width*item.Data.Position.X-width/2,area.height*item.Data.Position.Y-height/2,width,height),item.Texture,ScaleMode.ScaleToFit,true);
            }
            if (screenGrid)
            {
                GUI.color = new Color(.3f,1,.85f,.45f);
                for (int i=1;i<8;i++) { GUI.DrawTexture(new Rect(area.width*i/8,0,1,area.height),Texture2D.whiteTexture); GUI.DrawTexture(new Rect(0,area.height*i/8,area.width,1),Texture2D.whiteTexture); }
            }
        }
        finally { GUI.EndGroup(); GUI.color=color; }
    }
    internal void DisposeImages() { picker.CancelPending(); foreach (var image in images) image.Dispose(); images.Clear(); selected=0; screenGrid=false; if(ruler!=null) UnityEngine.Object.Destroy(ruler); ruler=null; Message="References cleared."; }
    public void Dispose() { DisposeImages(); if(bundle!=null) bundle.Unload(false); bundle=null; }
}
