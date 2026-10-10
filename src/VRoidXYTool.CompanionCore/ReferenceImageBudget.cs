namespace VRoidXYTool.CompanionCore;

public static class ReferenceImageBudget
{
    public const long MaximumBytes = 256L * 1024 * 1024;
    public static long Validate(int width, int height, long residentBytes)
    {
        if (width is < 1 or > 4096 || height is < 1 or > 4096 || residentBytes < 0)
            throw new InvalidDataException("Invalid reference image dimensions or memory usage.");
        long bytes = (long)width * height * 4;
        if (residentBytes > MaximumBytes - bytes)
            throw new InvalidDataException("Reference images exceed the 256 MiB texture budget. Remove an image or use a smaller resolution.");
        return bytes;
    }
}
