using System.Text.RegularExpressions;
using Microsoft.Win32;
using VRoidXYTool.Setup;

namespace VRoidXYTool.LauncherCore;

public sealed record Installation(string Executable, string Version, string? Error)
{
    public override string ToString() => Error == null ? $"VRoid Studio {Version}" : "Unsupported VRoid installation";
}

public static class InstallDiscovery
{
    public static Installation Inspect(string executable)
    {
        try { return new(Path.GetFullPath(executable), WorkspaceManager.VersionLabel(WorkspaceManager.BuildId(executable)), null); }
        catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException or UnauthorizedAccessException) { return new(executable, "unknown", e.Message); }
    }

    public static IReadOnlyList<Installation> Find(string? remembered = null)
    {
        var paths = new List<string>();
        if (!string.IsNullOrWhiteSpace(remembered)) paths.Add(remembered);
        foreach (string root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs") })
            foreach (string relative in new[] { "VRoidStudio", "pixiv/VRoidStudio" }) paths.Add(Path.Combine(root, relative, "VRoidStudio.exe"));
        if (OperatingSystem.IsWindows())
        {
            using var steam = Registry.CurrentUser.OpenSubKey("Software\\Valve\\Steam");
            string? steamPath = steam?.GetValue("SteamPath") as string;
            if (steamPath != null)
            {
                var libraries = new List<string> { steamPath };
                string file = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
                if (File.Exists(file))
                    foreach (Match match in Regex.Matches(File.ReadAllText(file), "\"path\"\\s+\"([^\"]+)\"")) libraries.Add(match.Groups[1].Value.Replace("\\\\", "\\"));
                foreach (string library in libraries) paths.Add(Path.Combine(library, "steamapps", "common", "VRoid Studio", "VRoidStudio.exe"));
            }
        }
        return paths.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).Select(Inspect)
            .OrderBy(i => i.Error != null).ThenByDescending(i => i.Version, StringComparer.Ordinal).ToArray();
    }
}
