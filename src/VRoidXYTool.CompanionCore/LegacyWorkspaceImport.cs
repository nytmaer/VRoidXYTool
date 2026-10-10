using System.Text.Json;

namespace VRoidXYTool.CompanionCore;

public static class LegacyWorkspaceImport
{
    private static JsonDocument Read(string path)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length > 131072) throw new InvalidDataException("Legacy preset exceeds size limit.");
        return JsonDocument.Parse(stream);
    }
    private static Point3 Vector(JsonElement value) => new(value.GetProperty("x").GetSingle(), value.GetProperty("y").GetSingle(), value.GetProperty("z").GetSingle());
    public static WorkspacePresets Cameras(string path)
    {
        using var json = Read(path);
        var result = new WorkspacePresets();
        foreach (bool ortho in new[] { false, true })
        {
            var array = json.RootElement.GetProperty(ortho ? "OrthographicCameraPosPresets" : "PerspectiveCameraPosPresets");
            if (array.GetArrayLength() != 10) throw new InvalidDataException("Legacy camera bank must have ten slots.");
            int index = 0;
            foreach (var element in array.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Null)
                {
                    var position = Vector(element.GetProperty("Pos")); var rotation = Vector(element.GetProperty("Rot"));
                    float yaw = rotation.Y * MathF.PI / 180, pitch = rotation.X * MathF.PI / 180;
                    var target = new Point3(position.X + MathF.Sin(yaw) * MathF.Cos(pitch), position.Y - MathF.Sin(pitch), position.Z + MathF.Cos(yaw) * MathF.Cos(pitch));
                    result.Bank(ortho)[index] = new CameraPreset(position, target, ortho, ortho ? element.GetProperty("OrthographicSize").GetSingle() : .8f) { Rotation = rotation };
                }
                index++;
            }
        }
        result.Validate(); return result;
    }
    public static GuidePresets Guides(string path)
    {
        using var json = Read(path);
        var result = new GuidePresets();
        foreach (var element in json.RootElement.GetProperty("Images").EnumerateArray())
        {
            if (result.Images.Count >= 16) throw new InvalidDataException("Too many legacy reference images.");
            string imagePath = element.GetProperty("Path").GetString() ?? throw new InvalidDataException("Missing legacy image path.");
            if (!System.IO.Path.IsPathRooted(imagePath)) imagePath = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))!, imagePath));
            result.Images.Add(new GuideImagePreset { Path = imagePath, WorldSpace = true, Position = Vector(element.GetProperty("Pos")), Rotation = Vector(element.GetProperty("Rot")), Scale = element.GetProperty("Scale").GetSingle(), Alpha = element.GetProperty("Alpha").GetSingle() });
        }
        result.Validate(); return result;
    }
}
