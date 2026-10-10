using System.Text.Json;

namespace VRoidXYTool.CompanionCore;

public sealed record LegacyPoseGizmo(string TypeName, string Name, string Value);

public static class LegacyPoseImport
{
    public static string MergeWithCurrent(string current, string imported)
    {
        using var template = JsonDocument.Parse(current, new JsonDocumentOptions { MaxDepth = 16 });
        using var replacement = JsonDocument.Parse(imported, new JsonDocumentOptions { MaxDepth = 16 });
        if (template.RootElement.ValueKind != JsonValueKind.Object || replacement.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Invalid serialized pose control.");
        var fields = template.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone(), StringComparer.Ordinal);
        foreach (var property in replacement.RootElement.EnumerateObject())
        {
            if (!fields.TryGetValue(property.Name, out var original) || original.ValueKind != property.Value.ValueKind)
                throw new InvalidDataException("Legacy pose member is unavailable on this rig: " + property.Name);
            fields[property.Name] = property.Value.Clone();
        }
        return JsonSerializer.Serialize(fields);
    }
    private static readonly HashSet<string> Types = new(StringComparer.Ordinal)
    {
        "SerializedFootIK", "SerializedLookAtTargetGizmoDefinition", "SerializedRollControlHandle",
        "SerializedUpperChestFK", "SerializedUpperLegsFK", "SerializedArmIK",
        "SerializedBodyEditablePoseGizmo", "SerializedFullBodyCenter", "SerializedHeadIK",
        "SerializedShouldersRotator", "SerializedTorsoRotator"
    };

    public static IReadOnlyList<LegacyPoseGizmo> Read(string path)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length == 0 || stream.Length > 1024 * 1024) throw new InvalidDataException("Legacy pose must be between 1 byte and 1 MiB.");
        using var json = JsonDocument.Parse(stream, new JsonDocumentOptions { MaxDepth = 16 });
        if (json.RootElement.ValueKind != JsonValueKind.Object || !json.RootElement.TryGetProperty("Data", out var data))
            throw new InvalidDataException("Legacy pose has no Data object.");
        if (data.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Legacy pose Data must be an object.");
        var result = new List<LegacyPoseGizmo>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in data.EnumerateObject())
        {
            if (result.Count >= 128) throw new InvalidDataException("Too many legacy pose controls.");
            string type = entry.Value.GetProperty("TypeName").GetString() ?? "";
            string name = entry.Value.GetProperty("Name").GetString() ?? "";
            if (!Types.Contains(type) || string.IsNullOrWhiteSpace(name) || name.Length > 128 || !names.Add(name))
                throw new InvalidDataException("Unsupported type or duplicate/invalid legacy pose control.");
            var values = new Dictionary<string, object>(StringComparer.Ordinal) { ["Name"] = name };
            Add("FloatDict", 1); Add("Vector3Dict", 3); Add("QuaternionDict", 4);
            result.Add(new LegacyPoseGizmo(type, name, JsonSerializer.Serialize(values)));

            void Add(string dictionary, int dimensions)
            {
                if (!entry.Value.TryGetProperty(dictionary, out var members) || members.ValueKind == JsonValueKind.Null) return;
                if (members.ValueKind != JsonValueKind.Object || members.EnumerateObject().Count() > 32)
                    throw new InvalidDataException("Invalid legacy pose member dictionary.");
                foreach (var member in members.EnumerateObject())
                {
                    if (member.Name.Length > 128 || values.ContainsKey(member.Name)) throw new InvalidDataException("Duplicate or invalid pose member.");
                    if (dimensions == 1) values.Add(member.Name, Number(member.Value));
                    else
                    {
                        var vector = new Dictionary<string, float>();
                        foreach (string axis in dimensions == 3 ? new[] { "x", "y", "z" } : new[] { "x", "y", "z", "w" })
                            vector.Add(axis, Number(member.Value.GetProperty(axis)));
                        values.Add(member.Name, vector);
                    }
                }
            }
        }
        if (result.Count == 0) throw new InvalidDataException("Legacy pose has no controls.");
        return result;
    }

    private static float Number(JsonElement element)
    {
        float value = element.GetSingle();
        if (!float.IsFinite(value) || Math.Abs(value) > 10000) throw new InvalidDataException("Legacy pose values must be finite and bounded.");
        return value;
    }
}
