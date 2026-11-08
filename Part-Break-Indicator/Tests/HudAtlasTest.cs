using System.Runtime.InteropServices;
using PartIndicator;

static unsafe class HudAtlasTest
{
    [DllImport("kernel32")] private static extern nint VirtualAlloc(nint address, nuint size, uint kind, uint protection);
    [DllImport("kernel32")] private static extern bool VirtualFree(nint address, nuint size, uint kind);
    public static void Run()
    {
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        nint module = VirtualAlloc(0, 0x8000000, 0x2000, 4);
        var allocations = new List<nint>();
        nint Alloc(int size) { var p = (nint)NativeMemory.AllocZeroed((nuint)size); allocations.Add(p); return p; }
        try
        {
            Check(module != 0 && VirtualAlloc(module + 0x73b9000, 0x1000, 0x1000, 4) != 0, "atlas fixture reserve/commit");
            var sentinel = Alloc(32); var buckets = Alloc(16); var node = Alloc(64); var rows = Alloc(12 * 0x50);
            var msentinel = Alloc(32); var mbuckets = Alloc(16); var mnode = Alloc(64); var material = Alloc(16);
            *(nint*)(module + 0x73b9e98) = sentinel; *(nint*)(module + 0x73b9ea8) = buckets;
            *(nint*)buckets = node; *(nint*)(buckets + 8) = node; *(uint*)(node + 0x10) = PercentageText.Atlas;
            *(nint*)(node + 0x20) = rows; *(nint*)(node + 0x28) = rows + 11 * 0x50;
            *(nint*)(module + 0x73b9d80) = msentinel; *(nint*)(module + 0x73b9d90) = mbuckets;
            *(nint*)mbuckets = mnode; *(nint*)(mbuckets + 8) = mnode; *(uint*)(mnode + 0x10) = PercentageText.Atlas;
            *(nint*)(mnode + 0x20) = material;
            for (int i = 0; i < 11; i++)
            {
                var g = PercentageText.Glyphs[i]; nint row = rows + i * 0x50;
                *(uint*)(row + 0x48) = g.Hash;
                *(float*)(row + 8) = g.Advance; *(float*)(row + 12) = 296;
                *(float*)(row + 0x20) = g.PadLeft; *(float*)(row + 0x24) = g.PadTop;
                *(float*)(row + 0x28) = g.PadRight; *(float*)(row + 0x2c) = g.PadBottom;
                *(float*)(row + 0x30) = g.U0; *(float*)(row + 0x34) = g.V0; *(float*)(row + 0x38) = g.U1; *(float*)(row + 0x3c) = g.V1;
            }
            bool Ready() => HudAtlas.TryValidate(module, PercentageText.Atlas, PercentageText.Glyphs);
            Check(Ready(), "all original glyphs and material available");
            *(nint*)(mnode + 0x20) = 0; Check(!Ready(), "null material defers creation");
            *(nint*)(mnode + 0x20) = material; Check(Ready(), "resource readiness can recover");
            *(nint*)(mbuckets + 8) = msentinel; Check(!Ready(), "missing material defers creation"); *(nint*)(mbuckets + 8) = mnode;
            *(nint*)(buckets + 8) = sentinel; Check(!Ready(), "missing metadata defers creation"); *(nint*)(buckets + 8) = node;
            *(uint*)(module + 0x73b9ec0) = 1048576; Check(!Ready(), "oversized registry rejected"); *(uint*)(module + 0x73b9ec0) = 0;
            *(nint*)(node + 0x28) = rows + 4097 * 0x50; Check(!Ready(), "oversized vector rejected");
            *(nint*)(node + 0x28) = rows + 11 * 0x50 + 1; Check(!Ready(), "misaligned vector rejected");
            *(nint*)(node + 0x28) = rows + 10 * 0x50; Check(!Ready(), "missing percent glyph rejected");
            *(nint*)(node + 0x28) = rows + 11 * 0x50;
            *(float*)(rows + 0x30) = float.NaN; Check(!Ready(), "nonfinite UV rejected");
            *(float*)(rows + 0x30) = PercentageText.Glyphs[0].U0 + .01f; Check(!Ready(), "mismatched UV rejected");
            *(float*)(rows + 0x30) = PercentageText.Glyphs[0].U0;
            *(float*)(rows + 0x20) = 100; Check(!Ready(), "wrong authored padding rejected");
            *(float*)(rows + 0x20) = PercentageText.Glyphs[0].PadLeft;
            *(float*)(rows + 8) = 100; Check(!Ready(), "wrong authored advance rejected");
            *(float*)(rows + 8) = PercentageText.Glyphs[0].Advance;
            Buffer.MemoryCopy((void*)rows, (void*)(rows + 11 * 0x50), 0x50, 0x50);
            *(nint*)(node + 0x28) = rows + 12 * 0x50; Check(!Ready(), "duplicate glyph rejected");
            *(nint*)(node + 0x28) = rows + 11 * 0x50; Check(Ready(), "valid resource accepted after invalid snapshots");
            Check(!HudAtlas.TryValidate(1, PercentageText.Atlas, PercentageText.Glyphs), "unmapped registry fails closed");
            var head = new HudGlyph(' ', 0x9a7f2306, 1254, 0, 0, 0, 0, 1254, 1254, 0, .3876953125f, .6123046875f, 1);
            *(uint*)(node + 0x10) = *(uint*)(mnode + 0x10) = 0xe193ab00;
            NativeMemory.Clear((void*)rows, 2 * 0x50);
            *(nint*)(node + 0x28) = rows + 0x50;
            *(float*)(rows + 8) = *(float*)(rows + 12) = 1254;
            *(float*)(rows + 0x34) = .3876953125f; *(float*)(rows + 0x38) = .6123046875f; *(float*)(rows + 0x3c) = 1;
            *(uint*)(rows + 0x48) = 0x9a7f2306;
            bool HeadReady() => HudAtlas.TryValidate(module, 0xe193ab00, [head], 1254);
            Check(HeadReady(), "dedicated head atlas with its own authored height accepted");
            Check(!HudAtlas.TryValidate(module, 0xe193ab00, [head]), "head atlas cannot masquerade as296px numeric glyphs");
            *(nint*)(mnode + 0x20) = 0; Check(!HeadReady(), "head metadata alone cannot publish a native Image");
            *(nint*)(mnode + 0x20) = material;
            *(uint*)(rows + 0x48) ^= 1; Check(!HeadReady(), "wrong head sprite hash rejected"); *(uint*)(rows + 0x48) ^= 1;
            *(float*)(rows + 12) = 1253; Check(!HeadReady(), "wrong head dimensions rejected"); *(float*)(rows + 12) = 1254;
            *(float*)(rows + 0x20) = 1; Check(!HeadReady(), "trimmed head cannot use untrimmed UV regions"); *(float*)(rows + 0x20) = 0;
            Buffer.MemoryCopy((void*)rows, (void*)(rows + 0x50), 0x50, 0x50);
            *(nint*)(node + 0x28) = rows + 2 * 0x50; Check(!HeadReady(), "duplicate head sprite rejected");
            *(nint*)(node + 0x28) = rows + 0x50; Check(HeadReady(), "head resource readiness recovers after an invalid snapshot");
            Console.WriteLine("PASS: actual-memory atlas readiness; missing/null resources recover; malformed/missing/duplicate glyphs and invalid UV fail closed");
        }
        finally { foreach (var p in allocations) NativeMemory.Free((void*)p); if (module != 0) VirtualFree(module, 0, 0x8000); }
    }
}
