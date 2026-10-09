namespace VRoidXYTool.CompanionCore;

public static class JpegGuard
{
    // Check dimensions before the native decoder allocates its texture.
    public static bool IsBoundedJpeg(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 12 || bytes.Length > 64 * 1024 * 1024 || bytes[0] != 255 || bytes[1] != 216 || bytes[^2] != 255 || bytes[^1] != 217) return false;
        int offset = 2;
        while (offset + 3 < bytes.Length)
        {
            if (bytes[offset++] != 255) return false;
            while (offset < bytes.Length && bytes[offset] == 255) offset++;
            if (offset >= bytes.Length) return false;
            int marker = bytes[offset++];
            if (marker is 0 or 216 or 217 or 218) return false;
            if (marker == 1 || marker is >= 208 and <= 215) continue;
            if (offset + 2 > bytes.Length) return false;
            int length = bytes[offset] * 256 + bytes[offset + 1];
            if (length < 2 || length > bytes.Length - offset) return false;
            if (marker is 192 or 193 or 194)
            {
                if (length < 8) return false;
                int height = bytes[offset + 3] * 256 + bytes[offset + 4];
                int width = bytes[offset + 5] * 256 + bytes[offset + 6];
                return width is > 0 and <= 4096 && height is > 0 and <= 4096;
            }
            offset += length;
        }
        return false;
    }
}
