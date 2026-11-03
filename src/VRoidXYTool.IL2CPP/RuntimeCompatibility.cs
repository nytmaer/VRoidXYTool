namespace VRoidXYTool.IL2CPP;

// Explicit profiles only: a matching Unity version alone does not establish editor API compatibility.
internal static class RuntimeCompatibility
{
    internal const string SupportedVersions = "2.14.0 and 2.16.0 / Unity 6000.0.62f1";

    internal static bool Supports(string version, string unityVersion) =>
        (version == "2.14.0" || version == "2.16.0") && unityVersion == "6000.0.62f1";
}
