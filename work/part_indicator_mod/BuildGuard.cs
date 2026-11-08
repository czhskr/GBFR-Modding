namespace PartIndicator;

public static class BuildGuard
{
    public const string ExpectedSha256 = "4B3ACE0FD03DF5D3D08C6453E12DF2279C6259ABF7D2FB3622921FCBEA775145";
    public static bool Matches(string hash, int imageSize) => imageSize == 136413184 &&
        string.Equals(hash, ExpectedSha256, StringComparison.OrdinalIgnoreCase);
}
