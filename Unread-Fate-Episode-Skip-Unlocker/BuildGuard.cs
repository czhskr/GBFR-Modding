namespace FateSkip;

public static class BuildGuard
{
    public const string ExpectedSha256 = "4B3ACE0FD03DF5D3D08C6453E12DF2279C6259ABF7D2FB3622921FCBEA775145";
    public static bool CanReadEvaluator(string hash, int imageSize) =>
        string.Equals(hash, ExpectedSha256, StringComparison.OrdinalIgnoreCase) && imageSize >= 0x1D53970 + 7;
    public static bool Matches(string hash, ReadOnlySpan<byte> prefix) =>
        string.Equals(hash, ExpectedSha256, StringComparison.OrdinalIgnoreCase) &&
        prefix.SequenceEqual(new byte[] { 86, 87, 83, 72, 131, 236, 48 });
}
