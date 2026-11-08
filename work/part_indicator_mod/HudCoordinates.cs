namespace PartIndicator;

public static class HudCoordinates
{
    public const float AngraContentScale = .66f;
    private const float HeadScale = .7f * AngraContentScale;
    public static (float X, float Y, float Width, float Height, float U0, float V0, float U1, float V1) AngraRegion(int region)
    {
        const float uv = .6123046875f, seam = uv * .52f;
        return region switch {
            0 => (-56 * HeadScale, 53.76f * HeadScale, 112 * HeadScale, 116.48f * HeadScale, uv, seam, uv / 2, 0),
            1 => (56 * HeadScale, 53.76f * HeadScale, 112 * HeadScale, 116.48f * HeadScale, uv / 2, seam, 0, 0),
            2 => (0, -58.24f * HeadScale, 224 * HeadScale, 107.52f * HeadScale, uv, uv, 0, seam),
            _ => throw new ArgumentOutOfRangeException(nameof(region))
        };
    }
    public static (float X, float Y) AngraText(int column) => column switch {
        0 => (-60 * AngraContentScale, -112 * HeadScale - 16 * AngraContentScale),
        1 => (60 * AngraContentScale, -112 * HeadScale - 16 * AngraContentScale), _ => throw new ArgumentOutOfRangeException(nameof(column))
    };
    public static (float Level, uint Packed) AngraTint(int region, bool broken)
    {
        if (region is < 0 or > 2) throw new ArgumentOutOfRangeException(nameof(region));
        return region != 2 && broken ? (.35f, 0x595959ffu) : (1f, 0xffffffffu);
    }
    public static (float X, float Y, float Width, float Height) Icon(bool broken, bool paired, int column)
    {
        float height = 120f * 76 / 79 * (broken ? 1 : 1.2f);
        float width = broken ? 120 : height * 66 / 72;
        return paired ? (column == 0 ? -56 : 56, 24, width * .7f, height * .7f) : (0, 0, width, height);
    }
    public static float TextTop(bool broken, bool paired) => paired ? 22.5f : Icon(broken, false, 0).Height / 2 - 10;

    public static bool TryConvert(float pixelX, float pixelY, float sx, float sy, float width, float height, out float x, out float y)
    {
        x = y = 0;
        if (!new[] { pixelX, pixelY, sx, sy, width, height }.All(float.IsFinite) || sx == 0 || sy == 0 || width <= 0 || height <= 0) return false;
        x = pixelX / sx - width / 2; y = pixelY / sy + height / 2;
        return float.IsFinite(x) && float.IsFinite(y);
    }
}
