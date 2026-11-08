namespace PartIndicator;

public readonly record struct HudGlyph(char Character, uint Hash, float Advance, float PadLeft, float PadTop, float PadRight, float PadBottom,
    float Width, float Height, float U0, float V0, float U1, float V1);
public readonly record struct GlyphPlacement(HudGlyph Glyph, float X, float Y, float Width, float Height);

public static class PercentageText
{
    public const uint Atlas = 0x66dde1b0;
    private const float Scale = 20f / 227;
    private static readonly HudGlyph[] OriginalGlyphs =
    [
        new('0', 0x758aa3f7u, 152.0f, 3.0761204f, 63.076122f, 3.076126f, 67.076126f, 146.0f, 166.0f, 0.29052734f, 0.8227539f, 0.32617188f, 0.9038086f),
        new('1', 0x7a1c38ffu, 88.0f, 3.0513167f, 61.042908f, 3.0761185f, 56.02675f, 82.0f, 179.0f, 0.6364746f, 0.07519531f, 0.65649414f, 0.16259766f),
        new('2', 0x8a0e96a0u, 148.0f, 2.0362852f, 63.026752f, 5.076126f, 62.076126f, 141.0f, 171.0f, 0.48266602f, 0.08544922f, 0.51708984f, 0.16894531f),
        new('3', 0xaf24a2c5u, 140.0f, 4.026751f, 7.0761204f, 3.012909f, 63.076126f, 133.0f, 226.0f, 0.3623047f, 0.80029297f, 0.3947754f, 0.91064453f),
        new('4', 0x7c05a50eu, 140.0f, 0.026750982f, 7.0761204f, 2.0267487f, 62.02675f, 138.0f, 227.0f, 0.107421875f, 0.77197266f, 0.14111328f, 0.8828125f),
        new('5', 0x5273fccfu, 148.0f, 0.026750982f, 7.0761204f, 4.0267487f, 62.02675f, 144.0f, 227.0f, 0.0f, 0.76708984f, 0.03515625f, 0.8779297f),
        new('6', 0x4ec5f825u, 148.0f, 1.0129125f, 63.076122f, 0.026748657f, 10.026764f, 147.0f, 223.0f, 0.15112305f, 0.4375f, 0.18701172f, 0.5463867f),
        new('7', 0xe3132405u, 144.0f, 2.0761204f, 7.0761204f, 0.026748657f, 65.01291f, 142.0f, 224.0f, 0.14575195f, 0.65966797f, 0.18041992f, 0.76904297f),
        new('8', 0x9fdfba32u, 160.0f, 4.0761204f, 63.076122f, 4.076126f, 13.076111f, 152.0f, 220.0f, 0.038085938f, 0.4375f, 0.07519531f, 0.5449219f),
        new('9', 0x55cb9aecu, 148.0f, 0.026750982f, 11.026751f, 2.076126f, 61.076126f, 146.0f, 224.0f, 0.22485352f, 0.48583984f, 0.26049805f, 0.59521484f),
        new('%', 0xdf896405u, 148.0f, 7.0129128f, 57.026752f, 7.012909f, 108.01291f, 134.0f, 131.0f, 0.5788574f, 0.14111328f, 0.61157227f, 0.20507812f),
    ];
    public static ReadOnlySpan<HudGlyph> Glyphs => OriginalGlyphs;
    public static int? Percentage(PartDisplay display)
    {
        if (!display.Visible || display.Broken || display.Fraction is not { } fraction ||
            !double.IsFinite(fraction) || fraction < 0 || fraction > 1 || display.WholePercentage is < 0 or > 100) return null;
        return fraction > 0 ? Math.Max(1, display.WholePercentage ?? (int)Math.Floor(fraction * 100)) : 0;
    }
    public static GlyphPlacement[] Layout(int percentage)
    {
        if (percentage is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(percentage));
        string text = percentage.ToString(System.Globalization.CultureInfo.InvariantCulture) + "%";
        var glyphs = text.Select(c => OriginalGlyphs[c == '%' ? 10 : c - '0']).ToArray();
        float cursor = -glyphs.Sum(g => g.Advance) * Scale / 2;
        var result = new GlyphPlacement[glyphs.Length];
        for (int i = 0; i < glyphs.Length; i++)
        {
            var g = glyphs[i];
            result[i] = new(g, cursor + (g.PadLeft + g.Width / 2) * Scale,
                (g.PadTop + g.Height / 2) * Scale, g.Width * Scale, g.Height * Scale);
            cursor += g.Advance * Scale;
        }
        return result;
    }
}
