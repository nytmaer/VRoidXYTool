using System.Buffers.Binary;

namespace VRoidXYTool.SyncCore;

/// <summary>Preflight before allocating Unity pixels; the image decoder must still validate PNG data.</summary>
public static class PngGuard
{
    public static bool IsCompleteBoundedPng(ReadOnlySpan<byte> data)
    {
        ReadOnlySpan<byte> signature = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
        if (data.Length < 45 || !data[..8].SequenceEqual(signature)) return false;
        if (BinaryPrimitives.ReadUInt32BigEndian(data[8..]) != 13 || !data.Slice(12, 4).SequenceEqual("IHDR"u8)) return false;
        uint width = BinaryPrimitives.ReadUInt32BigEndian(data[16..]);
        uint height = BinaryPrimitives.ReadUInt32BigEndian(data[20..]);
        if (width == 0 || height == 0 || width > 4096 || height > 4096) return false;
        bool hasData = false;
        int offset = 8;
        while (offset <= data.Length - 12)
        {
            uint length = BinaryPrimitives.ReadUInt32BigEndian(data[offset..]);
            if (length > (uint)(data.Length - offset - 12)) return false;
            var type = data.Slice(offset + 4, 4);
            if (type.SequenceEqual("IDAT"u8)) hasData = true;
            offset += checked((int)length + 12);
            if (type.SequenceEqual("IEND"u8)) return length == 0 && hasData && offset == data.Length;
        }
        return false;
    }
}
