using System.Text.Json;

namespace VRoidXYTool.CompanionCore;

public sealed class CompanionPreferences
{
    public string? BridgePath { get; set; }
    public string? EditorPath { get; set; }
    public bool UseDefaultEditor { get; set; }

    public static CompanionPreferences Load(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            if (stream.Length > 16384) return new();
            return JsonSerializer.Deserialize<CompanionPreferences>(stream) ?? new();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException) { return new(); }
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, JsonSerializer.Serialize(this)); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
