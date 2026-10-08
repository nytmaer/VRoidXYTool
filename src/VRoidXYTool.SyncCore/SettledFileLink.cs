using System.Security.Cryptography;

namespace VRoidXYTool.SyncCore;

public sealed record LayerIdentity(string Document, string Texture, string Layer);

/// <summary>
/// Main-thread polling link. The bridge must invalidate links on document close/switch,
/// validate identity and decode the complete PNG before executing its document command.
/// No Unity or VRoid objects are accessed by this class.
/// </summary>
public sealed class SettledFileLink : IDisposable
{
    private readonly TimeSpan quietPeriod;
    private readonly int maxBytes;
    private string? appliedHash;
    private string? candidateHash;
    private TimeSpan candidateSince;
    private TimeSpan lastPoll;
    private bool disposed;

    public LayerIdentity Identity { get; }
    public string FilePath { get; }

    public SettledFileLink(LayerIdentity identity, string filePath, byte[] exportedPng,
        TimeSpan quietPeriod, int maxBytes = 64 * 1024 * 1024)
    {
        if (quietPeriod < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(quietPeriod));
        if (maxBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maxBytes));
        Identity = identity;
        FilePath = Path.GetFullPath(filePath);
        this.quietPeriod = quietPeriod;
        this.maxBytes = maxBytes;
        appliedHash = Hash(exportedPng);
    }

    /// <summary>
    /// Pass elapsed monotonic time (e.g. Stopwatch.Elapsed), never wall-clock time.
    /// A false bridge result leaves the file pending; failures must not acknowledge it.
    /// </summary>
    public bool Poll(TimeSpan elapsed, Func<LayerIdentity, byte[], bool> apply)
    {
        if (disposed) return false;
        if (elapsed < lastPoll) throw new ArgumentOutOfRangeException(nameof(elapsed));
        lastPoll = elapsed;
        byte[] bytes;
        try
        {
            // Exclusive reads retry locked saves, including editors that replace files by rename.
            using var stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.None);
            if (stream.Length == 0 || stream.Length > maxBytes)
            {
                candidateHash = null;
                return false;
            }
            bytes = new byte[checked((int)stream.Length)];
            var offset = 0;
            while (offset < bytes.Length)
            {
                int read = stream.Read(bytes, offset, bytes.Length - offset);
                if (read == 0) { candidateHash = null; return false; }
                offset += read;
            }
        }
        catch (IOException) { candidateHash = null; return false; }
        catch (UnauthorizedAccessException) { candidateHash = null; return false; }

        string hash = Hash(bytes);
        if (hash == appliedHash) { candidateHash = null; return false; }
        if (hash != candidateHash)
        {
            candidateHash = hash;
            candidateSince = elapsed;
            return false;
        }
        if (elapsed - candidateSince < quietPeriod) return false;
        if (!apply(Identity, bytes)) return false;
        // Commit only the bytes actually imported. A subsequent external save is caught next poll.
        appliedHash = hash;
        candidateHash = null;
        return true;
    }

    // Call after an explicit export to suppress importing our own output.
    public void AcknowledgeExport(byte[] png)
    {
        if (disposed) throw new ObjectDisposedException(nameof(SettledFileLink));
        appliedHash = Hash(png);
        candidateHash = null;
    }

    public void Dispose() { disposed = true; candidateHash = null; }
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}
