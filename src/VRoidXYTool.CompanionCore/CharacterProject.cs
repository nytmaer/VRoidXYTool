using System.Text.Json;

namespace VRoidXYTool.CompanionCore;

public sealed record ProjectAsset(string Id, string Name, string RelativePath, string Kind,
    DateTimeOffset AddedUtc, string? SourceSession = null, string? SourceLayerId = null);

// A project is an explicit user association, not evidence of which model VRoid has open.
public sealed class CharacterProject
{
    public int SchemaVersion { get; set; } = 1;
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Character project";
    public string ModelPath { get; set; } = "";
    public string Notes { get; set; } = "";
    public List<ProjectAsset> Assets { get; set; } = new();

    public static CharacterProject Create(string manifest, string model, string name)
    {
        model = Path.GetFullPath(model);
        if (!File.Exists(model) || !string.Equals(Path.GetExtension(model), ".vroid", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Choose an existing .vroid model. The model is referenced, never modified or copied.");
        var project = new CharacterProject { Name = name.Trim(), ModelPath = StorePath(manifest, model) };
        project.Save(manifest, true);
        return project;
    }

    public static CharacterProject Load(string manifest)
    {
        using var input = File.OpenRead(manifest);
        if (input.Length > 1024 * 1024) throw new InvalidDataException("Project metadata exceeds 1 MiB.");
        var project = JsonSerializer.Deserialize<CharacterProject>(input) ?? throw new InvalidDataException("Empty project.");
        project.Validate(manifest);
        return project;
    }

    public void Save(string manifest, bool create = false)
    {
        Validate(manifest);
        string content = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        if (System.Text.Encoding.UTF8.GetByteCount(content) > 1024 * 1024) throw new InvalidDataException("Project metadata exceeds 1 MiB.");
        manifest = Path.GetFullPath(manifest);
        Directory.CreateDirectory(Path.GetDirectoryName(manifest)!);
        string temporary = manifest + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, content); File.Move(temporary, manifest, !create); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public string ResolveModel(string manifest) => ResolvePath(manifest, ModelPath);

    public void AssociateModel(string manifest, string model)
    {
        model = Path.GetFullPath(model);
        if (!File.Exists(model) || !string.Equals(Path.GetExtension(model), ".vroid", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Choose an existing .vroid model.");
        string previous = ModelPath;
        ModelPath = StorePath(manifest, model);
        try { Save(manifest); } catch { ModelPath = previous; throw; }
    }

    public ProjectAsset AddAsset(string manifest, string source, string kind = "Reference", string? name = null,
        string? sourceSession = null, string? sourceLayerId = null)
    {
        source = Path.GetFullPath(source);
        string extension = Path.GetExtension(source).ToLowerInvariant();
        if (extension is not (".png" or ".jpg" or ".jpeg")) throw new InvalidDataException("Project assets must be PNG or JPG files.");
        if (kind is not ("Reference" or "Texture")) throw new InvalidDataException("Unknown asset kind.");
        if (Assets.Count >= 256) throw new IOException("Project limit: 256 assets.");
        string id = Guid.NewGuid().ToString("N");
        var asset = new ProjectAsset(id, (name ?? Path.GetFileName(source)).Trim(), "Assets/" + id + extension,
            kind, DateTimeOffset.UtcNow, sourceSession, sourceLayerId);
        string destination = ResolvePath(manifest, asset.RelativePath);
        string temporary = destination + ".tmp";
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        try
        {
            using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                long stored = Assets.Sum(x => File.Exists(ResolveAsset(manifest, x.Id)) ? new FileInfo(ResolveAsset(manifest, x.Id)).Length : 0);
                if (input.Length == 0 || input.Length > 64 * 1024 * 1024 || input.Length + stored > 256 * 1024 * 1024)
                    throw new IOException("Asset limit: 64 MiB per image and 256 MiB per project.");
                using var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                input.CopyTo(output, 65536); output.Flush(true);
            }
            File.Move(temporary, destination);
            Assets.Add(asset);
            try { Save(manifest); }
            catch { Assets.Remove(asset); File.Delete(destination); throw; }
            return asset;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public string ResolveAsset(string manifest, string id)
        => ResolvePath(manifest, Assets.Single(x => x.Id == id).RelativePath);

    public ProjectAsset ArchiveLinkedTexture(string manifest, string bridgePath, string expectedSession, string layerId)
    {
        var bridge = SnapshotFile.Read(bridgePath);
        if (bridge.DocumentSession != expectedSession) throw new InvalidOperationException("VRoid changed documents. Confirm the new session before archiving.");
        string png = SnapshotFile.GetPngToOpen(bridge, layerId, DateTimeOffset.UtcNow);
        string label = bridge.Layers.Single(x => x.Id == layerId).Name;
        if (label.Length > 150) label = label[..150];
        return AddAsset(manifest, png, "Texture", label + " " + DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss"), expectedSession, layerId);
    }

    private void Validate(string manifest)
    {
        if (SchemaVersion != 1 || !Guid.TryParseExact(Id, "N", out _)) throw new InvalidDataException("Unsupported or invalid project identity.");
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > 100 || Notes == null || Notes.Length > 65536)
            throw new InvalidDataException("Project name must be 1–100 characters; notes must fit within 65,536 characters.");
        if (string.IsNullOrWhiteSpace(ModelPath) || !string.Equals(Path.GetExtension(ModelPath), ".vroid", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Project must reference a .vroid model.");
        ResolveModel(manifest); // Missing files remain recoverable; invalid paths do not.
        if (Assets == null || Assets.Count > 256 || Assets.Any(x => x == null) || Assets.Select(x => x.Id).Distinct().Count() != Assets.Count)
            throw new InvalidDataException("Invalid project asset list.");
        foreach (var asset in Assets)
        {
            if (string.IsNullOrWhiteSpace(asset.RelativePath)) throw new InvalidDataException("Missing asset path.");
            string extension = Path.GetExtension(asset.RelativePath).ToLowerInvariant();
            if (!Guid.TryParseExact(asset.Id, "N", out _) || extension is not (".png" or ".jpg" or ".jpeg") ||
                asset.RelativePath != "Assets/" + asset.Id + extension || string.IsNullOrWhiteSpace(asset.Name) || asset.Name.Length > 200 ||
                asset.Kind is not ("Reference" or "Texture")) throw new InvalidDataException("Invalid project asset.");
            ResolveAsset(manifest, asset.Id);
        }
    }

    private static string StorePath(string manifest, string path)
    {
        string root = Path.GetDirectoryName(Path.GetFullPath(manifest))!;
        string relative = Path.GetRelativePath(root, path);
        return relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar) || Path.IsPathRooted(relative) ? path : relative;
    }

    private static string ResolvePath(string manifest, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new InvalidDataException("Missing project path.");
        if (Path.IsPathRooted(path))
        {
            if (!Path.IsPathFullyQualified(path)) throw new InvalidDataException("A complete external path is required.");
            return Path.GetFullPath(path);
        }
        string root = Path.GetDirectoryName(Path.GetFullPath(manifest))!.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string resolved = Path.GetFullPath(Path.Combine(root, path));
        if (!resolved.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Relative project path escapes its folder.");
        return resolved;
    }
}
