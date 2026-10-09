using System.Text.Json;

namespace VRoidXYTool.CompanionCore;

public sealed record GuideImagePreset
{
    public string Path { get; init; } = "";
    public bool Visible { get; init; } = true;
    public bool WorldSpace { get; init; }
    public Point3 Position { get; init; } = new(.5f, .5f, 0);
    public Point3 Rotation { get; init; } = new(0, 0, 0);
    public float Scale { get; init; } = .65f;
    public float Alpha { get; init; } = .35f;
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Path) || Path.Length > 4096 || Position == null || !Position.IsValid || Rotation == null || !float.IsFinite(Rotation.X) || !float.IsFinite(Rotation.Y) || !float.IsFinite(Rotation.Z) || Math.Abs(Rotation.X) > 360 || Math.Abs(Rotation.Y) > 360 || Math.Abs(Rotation.Z) > 360 || !float.IsFinite(Scale) || Scale < .01f || Scale > 5 || !float.IsFinite(Alpha) || Alpha < 0 || Alpha > 1) throw new InvalidDataException("Invalid reference settings.");
        string extension = System.IO.Path.GetExtension(Path);
        if (!extension.Equals(".png", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Reference must be PNG or JPEG.");
        if (!WorldSpace && (Position.X < 0 || Position.X > 1 || Position.Y < 0 || Position.Y > 1)) throw new InvalidDataException("Screen reference position is outside viewport.");
    }
}
public sealed class GuidePresets
{
    public int SchemaVersion { get; set; } = 1;
    public bool ScreenGrid { get; set; }
    public bool WorldGrid { get; set; }
    public List<GuideImagePreset> Images { get; set; } = new();
    public void Validate()
    {
        if (SchemaVersion != 1 || Images == null || Images.Count > 16 || Images.Any(x => x == null)) throw new InvalidDataException("Invalid reference preset file.");
        foreach (var image in Images) image.Validate();
    }
    public static GuidePresets Load(string path)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length > 131072) throw new InvalidDataException("Reference preset exceeds size limit.");
        var data = JsonSerializer.Deserialize<GuidePresets>(stream) ?? throw new InvalidDataException("Empty reference preset.");
        data.Validate(); return data;
    }
    public void Save(string path)
    {
        Validate(); Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))!);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, JsonSerializer.Serialize(this)); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
