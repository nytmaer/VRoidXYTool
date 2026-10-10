using System.Text.Json;
using System.Text.Json.Serialization;

namespace VRoidXYTool.CompanionCore;

public sealed record Point3(float X, float Y, float Z)
{
    [JsonIgnore]
    public bool IsValid => float.IsFinite(X) && float.IsFinite(Y) && float.IsFinite(Z) && Math.Abs(X) <= 100 && Math.Abs(Y) <= 100 && Math.Abs(Z) <= 100;
}
public sealed record CameraPreset(Point3 Position, Point3 Target, bool Orthographic, float Size)
{
    public Point3? Rotation { get; init; }
    [JsonIgnore]
    public bool IsValid => Position != null && Target != null && Position.IsValid && Target.IsValid && (Rotation == null || Rotation.IsValid || (float.IsFinite(Rotation.X) && float.IsFinite(Rotation.Y) && float.IsFinite(Rotation.Z) && Math.Abs(Rotation.X) <= 360 && Math.Abs(Rotation.Y) <= 360 && Math.Abs(Rotation.Z) <= 360)) && float.IsFinite(Size) && Size >= 0.01f && Size <= 100;
}
public sealed class WorkspacePresets
{
    public int SchemaVersion { get; set; } = 2;
    // Schema 1 stored four shared slots. Keep them until migration is written successfully.
    public CameraPreset?[] Cameras { get; set; } = new CameraPreset?[4];
    public CameraPreset?[] Perspective { get; set; } = new CameraPreset?[10];
    public CameraPreset?[] Orthographic { get; set; } = new CameraPreset?[10];
    public CameraPreset?[] Bank(bool orthographic) => orthographic ? Orthographic : Perspective;
    public void Validate()
    {
        if ((SchemaVersion != 1 && SchemaVersion != 2) || Cameras == null || Cameras.Length != 4 || Perspective == null || Perspective.Length != 10 || Orthographic == null || Orthographic.Length != 10 || Cameras.Concat(Perspective).Concat(Orthographic).Any(x => x != null && !x.IsValid)) throw new InvalidDataException("Invalid camera preset file.");
    }
    public static WorkspacePresets Load(string path)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length > 65536) throw new InvalidDataException("Preset file exceeds size limit.");
        var data = JsonSerializer.Deserialize<WorkspacePresets>(stream) ?? throw new InvalidDataException("Empty preset file.");
        data.Validate();
        if (data.SchemaVersion == 1)
        {
            for (int i = 0; i < data.Cameras.Length; i++) if (data.Cameras[i] is { } camera) data.Bank(camera.Orthographic)[i] = camera;
            data.SchemaVersion = 2;
        }
        return data;
    }
    public void Save(string path)
    {
        Validate(); Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, JsonSerializer.Serialize(this)); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
