using System.Text.Json;

namespace VRoidXYTool.CompanionCore;

// File-only contract: the Companion never receives native object pointers or editing commands.
public sealed class BridgeSnapshot
{
    public int SchemaVersion { get; set; } = 1;
    public DateTimeOffset PublishedUtc { get; set; }
    public bool Running { get; set; }
    public string ApplicationVersion { get; set; } = "";
    public string? DocumentSession { get; set; }
    public string ExportRoot { get; set; } = "";
    public string Message { get; set; } = "";
    public List<LinkedTextureSnapshot> Layers { get; set; } = new();
}

public sealed class LinkedTextureSnapshot
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string PngPath { get; set; } = "";
    public string Status { get; set; } = "Linked";
    public DateTimeOffset? LastImportedUtc { get; set; }
}

public static class SnapshotFile
{
    public static void Write(string path, BridgeSnapshot snapshot)
    {
        path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(snapshot));
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public static BridgeSnapshot Read(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (stream.Length > 1024 * 1024) throw new InvalidDataException("Bridge state exceeds the size limit.");
        var snapshot = JsonSerializer.Deserialize<BridgeSnapshot>(stream) ?? throw new InvalidDataException("Bridge state is empty.");
        if (snapshot.SchemaVersion != 1) throw new InvalidDataException("This Companion requires bridge schema 1.");
        if (snapshot.Layers == null || snapshot.Layers.Count > 1000 || snapshot.Layers.Any(x => x == null))
            throw new InvalidDataException("Invalid linked-layer list.");
        return snapshot;
    }

    public static bool IsLive(BridgeSnapshot snapshot, DateTimeOffset now) => snapshot.Running &&
        now - snapshot.PublishedUtc < TimeSpan.FromSeconds(6) && snapshot.PublishedUtc - now < TimeSpan.FromSeconds(2);

    public static string GetPngToOpen(BridgeSnapshot snapshot, string id, DateTimeOffset now)
    {
        if (!IsLive(snapshot, now)) throw new InvalidOperationException("The VRoid bridge is offline. Reconnect before opening a texture.");
        var layer = snapshot.Layers.Single(x => x.Id == id);
        if (string.IsNullOrWhiteSpace(snapshot.DocumentSession)) throw new InvalidDataException("No document session.");
        string root = Path.GetFullPath(snapshot.ExportRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string path = Path.GetFullPath(layer.PngPath);
        if (!Path.IsPathRooted(layer.PngPath) || !path.StartsWith(root, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Texture path must be a PNG inside the bridge export directory.");
        if (!File.Exists(path)) throw new FileNotFoundException("The linked PNG is missing.", path);
        return path;
    }
}
