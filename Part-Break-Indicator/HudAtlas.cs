namespace PartIndicator;

public static class HudAtlas
{
    private static T R<T>(nint p) where T : unmanaged => ProcessMemory.Read<T>(p);
    private static bool Find(nint module, uint hash, int sentinelRva, int bucketsRva, int maskRva, out nint result)
    {
        result = 0;
        nint sentinel = R<nint>(module + sentinelRva), buckets = R<nint>(module + bucketsRva);
        uint mask = R<uint>(module + maskRva);
        if (sentinel == 0 || buckets == 0 || mask >= 1048576 || (mask & (mask + 1)) != 0) return false;
        nint bucket = buckets + checked((int)(hash & mask) * 16), first = R<nint>(bucket), last = R<nint>(bucket + 8), node = last;
        for (int i = 0; i < 64 && node != 0 && node != sentinel; i++)
        {
            if (R<uint>(node + 0x10) == hash) { result = node; break; }
            if (node == first) break;
            node = R<nint>(node + 8);
        }
        return result != 0 && R<nint>(module + sentinelRva) == sentinel && R<nint>(module + bucketsRva) == buckets &&
            R<uint>(module + maskRva) == mask && R<nint>(bucket) == first && R<nint>(bucket + 8) == last && R<uint>(result + 0x10) == hash;
    }
    public static bool TryValidate(nint module, uint atlas, ReadOnlySpan<HudGlyph> glyphs, float authoredHeight = 296)
        => TryValidate(module, atlas, glyphs, out _, authoredHeight);
    public static bool TryMaterial(nint module, uint atlas, out nint material)
    {
        material = 0;
        try
        {
            if (!Find(module, atlas, 0x73b9d80, 0x73b9d90, 0x73b9da8, out var node)) return false;
            nint candidate = R<nint>(node + 0x20);
            if (candidate == 0 || !ProcessMemory.TryRead<nint>(candidate, out _) || R<nint>(node + 0x20) != candidate) return false;
            material = candidate; return true;
        }
        catch (IOException) { return false; }
        catch (OverflowException) { return false; }
    }
    public static bool TryValidate(nint module, uint atlas, ReadOnlySpan<HudGlyph> glyphs, out nint verifiedMaterial, float authoredHeight = 296)
    {
        verifiedMaterial = 0;
        try
        {
            if (!float.IsFinite(authoredHeight) || authoredHeight <= 0 || glyphs.Length is < 1 or > 64 || !Find(module, atlas, 0x73b9e98, 0x73b9ea8, 0x73b9ec0, out nint metadata) ||
                !Find(module, atlas, 0x73b9d80, 0x73b9d90, 0x73b9da8, out nint materialNode)) return false;
            nint material = R<nint>(materialNode + 0x20), begin = R<nint>(metadata + 0x20), end = R<nint>(metadata + 0x28);
            long bytes = end - begin;
            if (material == 0 || !ProcessMemory.TryRead<nint>(material, out _) || begin == 0 || bytes <= 0 || bytes > 4096 * 0x50 || bytes % 0x50 != 0) return false;
            var found = new bool[glyphs.Length];
            for (int offset = 0; offset < bytes; offset += 0x50)
            {
                nint row = begin + offset; uint hash = R<uint>(row + 0x48);
                for (int i = 0; i < glyphs.Length; i++)
                {
                    var g = glyphs[i]; if (hash != g.Hash) continue;
                    if (found[i]) return false;
                    float[] metrics = [R<float>(row), R<float>(row + 4), R<float>(row + 8), R<float>(row + 12),
                        R<float>(row + 0x20), R<float>(row + 0x24), R<float>(row + 0x28), R<float>(row + 0x2c)];
                    float[] authored = [0, 0, g.Advance, authoredHeight, g.PadLeft, g.PadTop, g.PadRight, g.PadBottom];
                    if (!metrics.Zip(authored).All(v => float.IsFinite(v.First) && Math.Abs(v.First - v.Second) < 1e-4)) return false;
                    float[] uv = [R<float>(row + 0x30), R<float>(row + 0x34), R<float>(row + 0x38), R<float>(row + 0x3c)];
                    float[] expected = [g.U0, g.V0, g.U1, g.V1];
                    if (!uv.Zip(expected).All(v => float.IsFinite(v.First) && Math.Abs(v.First - v.Second) < 1e-7) || R<uint>(row + 0x48) != hash) return false;
                    found[i] = true;
                }
            }
            bool valid = found.All(v => v) && R<nint>(metadata + 0x20) == begin && R<nint>(metadata + 0x28) == end &&
                R<nint>(materialNode + 0x20) == material &&
                Find(module, atlas, 0x73b9e98, 0x73b9ea8, 0x73b9ec0, out nint afterMetadata) && afterMetadata == metadata &&
                Find(module, atlas, 0x73b9d80, 0x73b9d90, 0x73b9da8, out nint afterMaterial) && afterMaterial == materialNode;
            if (valid) verifiedMaterial = material;
            return valid;
        }
        catch (IOException) { return false; }
        catch (OverflowException) { return false; }
    }
}
