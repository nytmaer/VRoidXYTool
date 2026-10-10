using System.Diagnostics;
using System.Security.Cryptography;
using VRoidXYTool.SyncCore;

// Measure real unchanged-file polls, including hashing, file handles and callbacks.
// Allocation limits are deterministic; elapsed times are reported, not asserted.
string directory = Path.Combine(Path.GetTempPath(), "vroid-performance-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
string path = Path.Combine(directory, "layer.png");
try
{
    foreach (int size in new[] { 1024 * 1024, 8 * 1024 * 1024 })
    {
        byte[] content = new byte[size]; Random.Shared.NextBytes(content);
        File.WriteAllBytes(path, content);
        using var link = new SettledFileLink(new("performance", "texture", "layer"), path, content, TimeSpan.FromMilliseconds(200));
        long tick = 0;
        void Poll() => link.Poll(TimeSpan.FromMilliseconds(tick++), (_, _) => throw new Exception("An unchanged file was imported."));
        void Baseline() => _ = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        for (int i = 0; i < 4; i++) { Poll(); Baseline(); }
        var baseline = Measure(Baseline, 40);
        var current = Measure(Poll, 40);
        Console.WriteLine($"{size / 1024 / 1024} MiB, 40 polls: baseline {baseline.Bytes:N0} B / {baseline.Milliseconds:F1} ms; current {current.Bytes:N0} B / {current.Milliseconds:F1} ms.");
        if (current.Bytes > 1024 * 1024 || current.Bytes >= baseline.Bytes / 10)
            throw new Exception("Unchanged texture polling allocates too much memory.");
    }
    Console.WriteLine("PASS: unchanged polling avoids full-file allocations at both file sizes.");
}
finally
{
    if (File.Exists(path)) File.Delete(path);
    Directory.Delete(directory);
}
static (long Bytes, double Milliseconds) Measure(Action action, int count)
{
    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
    long start = GC.GetAllocatedBytesForCurrentThread();
    var clock = Stopwatch.StartNew();
    for (int i = 0; i < count; i++) action();
    return (GC.GetAllocatedBytesForCurrentThread() - start, clock.Elapsed.TotalMilliseconds);
}
