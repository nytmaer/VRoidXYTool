using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace VRoidXYTool.CompanionCore;

public sealed record TextureSnapshot(string Id, string Name, DateTimeOffset CreatedUtc,
    string DocumentSession, string LayerId, string PngPath, string Sha256);

// Session-scoped history: never infer layer identity from a name or reopen a model automatically.
public static class TextureSnapshots
{
    public const long MaximumBytes = 64 * 1024 * 1024;
    private static readonly uint[] CrcTable = MakeCrcTable();
    private static string Folder(BridgeSnapshot bridge, string layerId)
    {
        string png = SnapshotFile.GetPngToOpen(bridge, layerId, DateTimeOffset.UtcNow);
        string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            bridge.DocumentSession + "\n" + layerId + "\n" + Path.GetFullPath(png))));
        return Path.Combine(bridge.ExportRoot, ".snapshots", key);
    }

    public static TextureSnapshot Capture(BridgeSnapshot bridge, string layerId, string name)
    {
        name = name.Trim();
        if (name.Length is < 1 or > 100) throw new ArgumentException("Use a snapshot name of 1–100 characters.");
        string source = SnapshotFile.GetPngToOpen(bridge, layerId, DateTimeOffset.UtcNow);
        string folder = Folder(bridge, layerId);
        Directory.CreateDirectory(folder);
        long storedBytes = Directory.EnumerateFiles(folder, "texture.png", SearchOption.AllDirectories).Sum(x => new FileInfo(x).Length);
        if (storedBytes + new FileInfo(source).Length > 256 * 1024 * 1024 || Directory.EnumerateDirectories(folder).Count() >= 100)
            throw new IOException("This layer's snapshot history is full (100 snapshots or 256 MiB). Archive older snapshots before continuing.");
        string id = Guid.NewGuid().ToString("N"), temporary = Path.Combine(folder, "." + id);
        Directory.CreateDirectory(temporary);
        try
        {
            string copy = Path.Combine(temporary, "texture.png");
            CopyPng(source, copy);
            string hash = Hash(copy);
            var entry = new TextureSnapshot(id, name, DateTimeOffset.UtcNow, bridge.DocumentSession!, layerId, source, hash);
            File.WriteAllText(Path.Combine(temporary, "snapshot.json"), JsonSerializer.Serialize(entry));
            Directory.Move(temporary, Path.Combine(folder, id));
            return entry;
        }
        finally { if (Directory.Exists(temporary)) Directory.Delete(temporary, true); }
    }

    public static IReadOnlyList<TextureSnapshot> List(BridgeSnapshot bridge, string layerId)
    {
        string folder = Folder(bridge, layerId);
        if (!Directory.Exists(folder)) return Array.Empty<TextureSnapshot>();
        var entries = new List<TextureSnapshot>();
        foreach (string directory in Directory.EnumerateDirectories(folder))
        {
            if (!Guid.TryParseExact(Path.GetFileName(directory), "N", out _)) continue;
            string metadata = Path.Combine(directory, "snapshot.json");
            using var stream = File.OpenRead(metadata);
            if (stream.Length > 16384) throw new InvalidDataException("Snapshot metadata exceeds the size limit.");
            var entry = JsonSerializer.Deserialize<TextureSnapshot>(stream) ?? throw new InvalidDataException("Snapshot metadata is empty.");
            if (entry.Id != Path.GetFileName(directory) || entry.DocumentSession != bridge.DocumentSession || entry.LayerId != layerId ||
                !string.Equals(Path.GetFullPath(entry.PngPath), SnapshotFile.GetPngToOpen(bridge, layerId, DateTimeOffset.UtcNow), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Snapshot identity does not match the linked layer.");
            entries.Add(entry);
        }
        return entries.OrderByDescending(x => x.CreatedUtc).ToArray();
    }

    public static string GetImage(BridgeSnapshot bridge, string layerId, string id)
    {
        var entry = List(bridge, layerId).Single(x => x.Id == id);
        string path = Path.Combine(Folder(bridge, layerId), entry.Id, "texture.png");
        if (Hash(path) != entry.Sha256) throw new InvalidDataException("Snapshot PNG checksum does not match.");
        return path;
    }

    // Preserve current artwork first, then replace atomically; VRoid imports through its usual sync/undo path.
    public static void Restore(string bridgePath, string expectedSession, string layerId, string id)
    {
        var bridge = SnapshotFile.Read(bridgePath);
        if (bridge.DocumentSession != expectedSession) throw new InvalidOperationException("The document changed. Select its layer again.");
        string source = GetImage(bridge, layerId, id);
        Capture(bridge, layerId, "Before restore " + DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        string destination = SnapshotFile.GetPngToOpen(bridge, layerId, DateTimeOffset.UtcNow);
        string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            CopyPng(source, temporary);
            var current = SnapshotFile.Read(bridgePath);
            if (current.DocumentSession != expectedSession ||
                SnapshotFile.GetPngToOpen(current, layerId, DateTimeOffset.UtcNow) != destination)
                throw new InvalidOperationException("The linked document changed during restore.");
            // File.Replace fails rather than recreating a deleted/unlinked destination.
            File.Replace(temporary, destination, null);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length > MaximumBytes) throw new InvalidDataException("Snapshot PNG exceeds 64 MiB.");
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(stream));
    }

    private static void CopyPng(string source, string destination)
    {
        // Deny writes during capture; an editor saving now can be retried when it finishes.
        using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length > MaximumBytes) throw new InvalidDataException("PNG exceeds 64 MiB.");
        Span<byte> header = stackalloc byte[24];
        if (input.Read(header) != 24 || !header[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
            throw new InvalidDataException("A PNG is required.");
        uint width = BinaryPrimitives.ReadUInt32BigEndian(header[16..20]), height = BinaryPrimitives.ReadUInt32BigEndian(header[20..24]);
        if (width is 0 or > 4096 || height is 0 or > 4096) throw new InvalidDataException("PNG dimensions must be within 4096×4096.");
        input.Position = 0;
        using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        input.CopyTo(output, 65536);
        output.Flush(true);
        output.Close();
        ValidateChunks(destination);
    }

    private static void ValidateChunks(string path)
    {
        using var stream = File.OpenRead(path);
        stream.Position = 8;
        byte[] buffer = new byte[65536];
        Span<byte> chunk = stackalloc byte[8];
        Span<byte> checksum = stackalloc byte[4];
        bool first = true, hasData = false;
        while (stream.Position <= stream.Length - 12)
        {
            if (stream.Read(chunk) != 8) break;
            uint length = BinaryPrimitives.ReadUInt32BigEndian(chunk);
            if (length > stream.Length - stream.Position - 4) break;
            string type = Encoding.ASCII.GetString(chunk[4..]);
            if (first && (type != "IHDR" || length != 13)) break;
            first = false;
            uint crc = 0xffffffff;
            foreach (byte value in chunk[4..]) crc = UpdateCrc(crc, value);
            long remaining = length;
            while (remaining > 0)
            {
                int read = stream.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                if (read == 0) throw new InvalidDataException("PNG data is truncated.");
                for (int i = 0; i < read; i++) crc = UpdateCrc(crc, buffer[i]);
                remaining -= read;
            }
            if (stream.Read(checksum) != 4 || BinaryPrimitives.ReadUInt32BigEndian(checksum) != ~crc) break;
            if (type == "IDAT") hasData = true;
            if (type == "IEND")
            {
                if (length == 0 && hasData && stream.Position == stream.Length) return;
                break;
            }
        }
        throw new InvalidDataException("PNG is incomplete or has an invalid checksum.");
    }

    private static uint UpdateCrc(uint crc, byte value)
        => (crc >> 8) ^ CrcTable[(crc ^ value) & 255];

    private static uint[] MakeCrcTable()
    {
        var table = new uint[256];
        for (uint index = 0; index < 256; index++)
        {
            uint crc = index;
            for (int bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0);
            table[index] = crc;
        }
        return table;
    }
}
