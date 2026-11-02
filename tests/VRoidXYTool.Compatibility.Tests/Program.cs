using VRoidXYTool.IL2CPP;

foreach (string version in new[] { "2.14.0", "2.16.0" })
{
    if (!RuntimeCompatibility.Supports(version, "6000.0.62f1")) throw new Exception("Supported profile rejected: " + version);
    if (RuntimeCompatibility.Supports(version, "6000.0.63f1")) throw new Exception("Unknown engine allowed: " + version);
}
foreach (string version in new[] { "", "2.15.0", "2.16.1", "2.17.0", "3.0.0", "2.16.0-preview" })
    if (RuntimeCompatibility.Supports(version, "6000.0.62f1")) throw new Exception("Unknown application allowed: " + version);
Console.WriteLine("PASS: explicit runtime profiles; unknown versions, previews and engines stay disabled.");
