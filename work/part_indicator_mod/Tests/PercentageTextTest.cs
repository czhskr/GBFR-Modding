using System.Text.Json;
using PartIndicator;

static class PercentageTextTest
{
    public static void Run()
    {
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        foreach (var (fraction, expected) in new (double, int)[] { (1, 100), (.799, 79), (.5, 50), (.01, 1), (double.Epsilon, 1), (0, 0) })
            Check(PercentageText.Percentage(new(true, false, fraction)) == expected, "floor percentage with positive minimum");
        foreach (double? invalid in new double?[] { null, double.NaN, double.PositiveInfinity, double.NegativeInfinity, -.01, 1.01 })
            Check(PercentageText.Percentage(new(true, false, invalid)) == null, "invalid/unknown percentage omitted");
        Check(PercentageText.Percentage(new(false, false, 1)) == null && PercentageText.Percentage(new(true, true, 0)) == null,
            "hidden/broken display never produces percentage");

        var reference = Path.GetFullPath("work/part_break/percent_assets/resource_validation.json");
        using var manifest = JsonDocument.Parse(File.ReadAllText(reference));
        var glyphs = manifest.RootElement.GetProperty("glyphs").EnumerateArray().ToDictionary(g => g.GetProperty("character").GetString()![0]);
        for (int value = 0; value <= 100; value++)
        {
            var layout = PercentageText.Layout(value);
            string text = string.Concat(layout.Select(g => g.Glyph.Character));
            Check(text == value.ToString(System.Globalization.CultureInfo.InvariantCulture) + "%", "no leading zeros or missing percent");
            double advance = text.Sum(c => glyphs[c].GetProperty("rect")[2].GetDouble()) * (20.0 / 227);
            double cursor = -advance / 2;
            foreach (var quad in layout)
            {
                var original = glyphs[quad.Glyph.Character];
                double width = original.GetProperty("imageSize")[0].GetDouble(), height = original.GetProperty("imageSize")[1].GetDouble();
                double left = original.GetProperty("padding")[0].GetDouble(), top = original.GetProperty("padding")[1].GetDouble();
                Check(Math.Abs(quad.X - (cursor + (left + width / 2) * 20 / 227)) < .0001, "text group stays centered using authored advances");
                Check(Math.Abs(quad.Y - (top + height / 2) * 20 / 227) < .0001, "authored vertical padding preserves baseline");
                Check(Math.Abs(quad.Width - width * 20 / 227) < .0001 && Math.Abs(quad.Height - height * 20 / 227) < .0001,
                    "cropped glyph keeps native aspect and maximum ink height");
                Check(quad.Height <= 20.0001 && quad.Width > 0, "finite bounded glyph quad");
                Check(quad.Glyph.Hash == Convert.ToUInt32(original.GetProperty("hash").GetString()![2..], 16), "draws actual original sprite");
                var uv = new[] { quad.Glyph.U0, quad.Glyph.V0, quad.Glyph.U1, quad.Glyph.V1 };
                for (int i = 0; i < 4; i++) Check(Math.Abs(uv[i] - original.GetProperty("uv")[i].GetDouble()) < 1e-7, "glyph UV agrees with original resource");
                cursor += original.GetProperty("rect")[2].GetDouble() * 20 / 227;
            }
            Check(Math.Abs(cursor - advance / 2) < .0001, "advance region straddles center");
        }
        foreach (int value in new[] { -1, 101, int.MaxValue })
        {
            bool rejected = false;
            try { PercentageText.Layout(value); } catch (ArgumentOutOfRangeException) { rejected = true; }
            Check(rejected, "out of range layout rejected");
        }
        Console.WriteLine("PASS: percentage floor/minimum/hidden/invalid values; all 0..100 layouts match original glyph geometry and centered advances");
    }
}
