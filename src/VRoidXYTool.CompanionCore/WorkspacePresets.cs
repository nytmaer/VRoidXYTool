using System.Text.Json;

namespace VRoidXYTool.CompanionCore;

public sealed record Point3(float X, float Y, float Z)
{
    public bool IsValid => float.IsFinite(X) && float.IsFinite(Y) && float.IsFinite(Z) && Math.Abs(X) <= 100 && Math.Abs(Y) <= 100 && Math.Abs(Z) <= 100;
}
public sealed record CameraPreset(Point3 Position, Point3 Target, bool Orthographic, float Size)
{
    public bool IsValid => Position != null && Target != null && Position.IsValid && Target.IsValid && float.IsFinite(Size) && Size >= 0.01f && Size <= 100;
}
public sealed class WorkspacePresets
{
    public int SchemaVersion { get; set; } = 1;
    public CameraPreset?[] Cameras { get; set; } = new CameraPreset?[4];
    public void Validate()
    {
        if (SchemaVersion != 1 || Cameras == null || Cameras.Length != 4 || Cameras.Any(x => x != null && !x.IsValid)) throw new InvalidDataException("Invalid camera preset file.");
    }
    public static WorkspacePresets Load(string path)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length > 65536) throw new InvalidDataException("Preset file exceeds size limit.");
        var data = JsonSerializer.Deserialize<WorkspacePresets>(stream) ?? throw new InvalidDataException("Empty preset file.");
        data.Validate(); return data;
    }
    public void Save(string path)
    {
        Validate(); Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, JsonSerializer.Serialize(this)); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
