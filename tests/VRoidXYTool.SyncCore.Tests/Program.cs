using VRoidXYTool.SyncCore;

// Dependency-free regression harness for filesystem behavior, not PNG decode or VRoid integration.
string directory = Path.Combine(Path.GetTempPath(), "vroidxy-sync-" + Guid.NewGuid());
Directory.CreateDirectory(directory);
string path = Path.Combine(directory, "layer.png");
var identity = new LayerIdentity("doc-a", "texture-a", "layer-a");
byte[] initial = { 1, 2, 3 };
File.WriteAllBytes(path, initial);
using var link = new SettledFileLink(identity, path, initial, TimeSpan.FromMilliseconds(200), 100);
int calls = 0;
bool Apply(LayerIdentity actual, byte[] data)
{
    Check(actual == identity, "original layer identity retained");
    calls++;
    return true;
}
bool Poll(int ms, Func<LayerIdentity, byte[], bool>? apply = null) => link.Poll(TimeSpan.FromMilliseconds(ms), apply ?? Apply);
try
{
    Check(!Poll(0) && calls == 0, "export does not import itself");
    var timestamp = File.GetLastWriteTimeUtc(path);
    File.WriteAllBytes(path, new byte[] { 4, 5, 6 });
    File.SetLastWriteTimeUtc(path, timestamp);
    Check(!Poll(10) && !Poll(100), "save waits for quiet period");
    Check(Poll(210) && calls == 1, "same timestamp and size change imports");
    Check(!Poll(500) && calls == 1, "unchanged content deduplicated");

    File.WriteAllBytes(path, new byte[] { 7 });
    Check(!Poll(600), "new save detected");
    File.WriteAllBytes(path, new byte[] { 7, 8 });
    Check(!Poll(750) && !Poll(800), "continued write resets quiet period");
    Check(Poll(950) && calls == 2, "completed repeated save imports");

    File.WriteAllBytes(path, new byte[] { 9 });
    using (var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        Check(!Poll(1000), "locked save deferred");
    Check(!Poll(1100) && Poll(1300), "locked save recovered");

    string replacement = Path.Combine(directory, "replacement.png");
    File.WriteAllBytes(replacement, new byte[] { 10 });
    File.Move(replacement, path, true);
    Check(!Poll(1400), "atomic replacement detected");
    Check(!Poll(1600, (_, _) => false), "rejected decode or identity is not acknowledged");
    Check(Poll(1700), "failed apply retried");

    File.Delete(path);
    Check(!Poll(1800), "missing file tolerated");
    File.WriteAllBytes(path, Array.Empty<byte>());
    Check(!Poll(1900), "empty intermediate save deferred");
    File.WriteAllBytes(path, new byte[101]);
    Check(!Poll(2000), "oversized file rejected");
    File.WriteAllBytes(path, new byte[] { 11 });
    link.AcknowledgeExport(new byte[] { 11 });
    Check(!Poll(2100), "reexport loop suppressed");
    File.WriteAllBytes(path, new byte[] { 12 });
    link.Dispose();
    Check(!Poll(2200), "shutdown prevents callbacks");
    Console.WriteLine("PASS: all filesystem synchronization regressions");
}
finally
{
    // Delete only the files created by this harness; never recursively remove a computed directory.
    if (File.Exists(path)) File.Delete(path);
    string replacement = Path.Combine(directory, "replacement.png");
    if (File.Exists(replacement)) File.Delete(replacement);
    Directory.Delete(directory);
}

static void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
}
