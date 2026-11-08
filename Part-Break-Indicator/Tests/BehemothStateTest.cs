using System.Runtime.InteropServices;
using PartIndicator;

static unsafe class BehemothStateTest
{
    [DllImport("kernel32")] private static extern nint VirtualAlloc(nint address, nuint size, uint kind, uint protection);
    [DllImport("kernel32")] private static extern bool VirtualFree(nint address, nuint size, uint kind);
    public static void Run()
    {
        var allocations = new List<nint>();
        nint Alloc(int size) { nint p = (nint)NativeMemory.AllocZeroed((nuint)size); allocations.Add(p); return p; }
        nint module = VirtualAlloc(0, 0x8000000, 0x2000, 4);
        if (module == 0) throw new Exception("Behemoth fixture reservation");
        try
        {
            foreach (int page in new[] { 0x7362000, 0x7035000, 0x7020000, 0x7c53000 })
                if (VirtualAlloc(module + page, 0x1000, 0x1000, 4) == 0) throw new Exception("Behemoth fixture commit");
            nint vector = Alloc(8), player = Alloc(0x240), a = Alloc(8), b = Alloc(0x30), c = Alloc(0x20),
                localScene = Alloc(0x80), localActor = Alloc(8), target = Alloc(0x80), actor = Alloc(0x3000),
                manager = Alloc(0x60), scenes = Alloc(64), stamps = Alloc(64), session = Alloc(8),
                parameter = Alloc(0x1000), entries = Alloc(3 * 48), history = Alloc(8);
            *(nint*)(module + 0x7362690) = vector; *(nint*)(module + 0x7362698) = vector + 8; *(nint*)vector = player;
            *(nint*)(player + 8) = a; *(nint*)a = b; *(nint*)(b + 0x28) = c; *(nint*)(c + 0x18) = localScene;
            *(nint*)(player + 0x230) = localActor; *(nint*)(localScene + 0x70) = localActor;
            *(nint*)(module + 0x7020988) = manager; *(nint*)(manager + 0x20) = scenes; *(nint*)(manager + 0x48) = stamps;
            *(uint*)(module + 0x7035ca0) = 3; *(nint*)(module + 0x7035ca8) = localScene; *(ulong*)(module + 0x7035cb0) = 100;
            *(nint*)(scenes + 16) = localScene; *(ulong*)(stamps + 16) = 100;
            *(uint*)(localScene + 0x38) = 3; *(nint*)(localScene + 0x40) = localScene; *(ulong*)(localScene + 0x48) = 100;
            *(nint*)(module + 0x7035d00) = localActor; *(nint*)(module + 0x7c53d10) = session;
            *(nint*)(player + 0x108) = target; *(nint*)(target + 0x70) = actor;
            *(uint*)(target + 0x38) = 4; *(nint*)(target + 0x40) = target; *(ulong*)(target + 0x48) = 200;
            *(nint*)(scenes + 24) = target; *(ulong*)(stamps + 24) = 200;
            *(nint*)actor = module + 0x5b02098; *(uint*)(actor + 0x160) = 243439824; *(nint*)(actor + 0x1598) = parameter;
            *(nint*)(actor + 0x788) = entries; *(nint*)(actor + 0x790) = entries + 3 * 48;
            *(int*)entries = 5; *(int*)(entries + 48) = 1; *(int*)(entries + 96) = 1;
            *(nint*)(actor + 0xcd8) = history; *(nint*)(actor + 0xce0) = history;
            *(int*)(parameter + 0x8b0) = 14000; *(float*)(actor + 0x2660) = 14000;
            *(float*)(parameter + 0xf50) = 1500; *(float*)(actor + 0x27a4) = 1500;
            var reader = new NativeStateReader(module);
            if (!reader.TryReadWorld(out var world)) throw new Exception("Behemoth owner fixture");
            PartSnapshot Read()
            {
                *(int*)(player + 0x118) = 5;
                if (!reader.TryReadSnapshot(world, 1, out var value) || value is null) throw new Exception("verified Behemoth head unavailable");
                return value;
            }
            void Body() { *(int*)(player + 0x118) = 1; if (reader.TryReadSnapshot(world, 1, out _)) throw new Exception("Behemoth body displayed"); }
            var first = Read();
            if (first.ClassId != 0x1700 || first.Initial != 14000 || PercentageText.Percentage(PartStatePolicy.Evaluate(first)) != 100)
                throw new Exception("Behemoth native int-to-float initial source");
            *(float*)(actor + 0x2660) = 13750.025390625f;
            if (PercentageText.Percentage(PartStatePolicy.Evaluate(Read())) != 98) throw new Exception("Behemoth captured float damage");
            Body();
            if (PercentageText.Percentage(PartStatePolicy.Evaluate(Read())) != 98) throw new Exception("duplicate non-head body IDs discarded Behemoth head maximum");
            *(nint*)(player + 0x108) = 0;
            if (reader.TryReadSnapshot(world, 1, out _)) throw new Exception("Behemoth no-lock visible");
            *(nint*)(player + 0x108) = target;
            if (Read().Initial != 14000) throw new Exception("Behemoth relock lost maximum");
            *(float*)(actor + 0x2660) = 201.38690185546875f;
            if (Read().Current != 201.38690185546875 || PartStatePolicy.Evaluate(Read()).Broken) throw new Exception("Behemoth positive float residual changed");
            *(float*)(actor + 0x2660) = 0;
            if (PartStatePolicy.Evaluate(Read()).Broken) throw new Exception("zero without Behemoth history became destruction");
            *(int*)history = 0; *(nint*)(actor + 0xce0) = history + 4;
            var broken = PartStatePolicy.Evaluate(Read());
            if (!broken.Broken || PercentageText.Percentage(broken) != null) throw new Exception("Behemoth processed history0 break");
            Body(); if (!PartStatePolicy.Evaluate(Read()).Broken) throw new Exception("Behemoth broken return lost");
            *(nint*)(actor + 0xce0) = history; *(float*)(actor + 0x2660) = 7000;
            if (Read().Initial != null) throw new Exception("Behemoth proof survived broken-history reset");
            reader = new NativeStateReader(module);
            if (Read().Initial != null) throw new Exception("late damaged Behemoth invented maximum");
            *(byte*)(actor + 0xe) = 0x40; *(float*)(actor + 0x2660) = 14000;
            reader = new NativeStateReader(module);
            if (PercentageText.Percentage(PartStatePolicy.Evaluate(Read())) != 100) throw new Exception("Ennugi verified integer initial omitted");
            *(float*)(actor + 0x2660) = 13700.0302734375f;
            if (PercentageText.Percentage(PartStatePolicy.Evaluate(Read())) != 97) throw new Exception("Ennugi captured float denominator");
            Body();
            if (Read().Initial != 14000) throw new Exception("Ennugi body return lost shared source proof");
            *(nint*)(player + 0x108) = 0;
            if (reader.TryReadSnapshot(world, 1, out _)) throw new Exception("Ennugi no-lock visible");
            *(nint*)(player + 0x108) = target;
            if (PercentageText.Percentage(PartStatePolicy.Evaluate(Read())) != 97) throw new Exception("Ennugi relock lost shared source proof");
            reader = new NativeStateReader(module);
            if (Read().Initial != null) throw new Exception("late damaged Ennugi invented maximum");
            *(float*)(actor + 0x2660) = 14000; Read(); *(int*)(parameter + 0x8b0) = 0;
            if (Read().Initial != null) throw new Exception("invalid Ennugi integer source retained proof");
            *(int*)(parameter + 0x8b0) = 14000; Read(); Body();
            *(byte*)(actor + 0xe) = 0; *(float*)(actor + 0x2660) = 7000;
            if (Read().Initial != 14000) throw new Exception("shared Behemoth integer source lost across flag change");
            *(float*)(actor + 0x2660) = 14000; Read(); *(int*)(parameter + 0x8b0) = 16000; *(float*)(actor + 0x2660) = 7000;
            if (Read().Initial != null) throw new Exception("Behemoth int parameter change retained old proof");
            *(int*)(parameter + 0x8b0) = 1000000001; *(float*)(actor + 0x2660) = 1e9f;
            if (Read().Initial != null) throw new Exception("Behemoth raw int above limit rounded into a valid float proof");
            *(int*)(parameter + 0x8b0) = 14000; *(float*)(actor + 0x2660) = 14000; Read();
            *(ulong*)(stamps + 24) = 201;
            if (reader.TryReadSnapshot(world, 1, out _)) throw new Exception("stale Behemoth scene accepted");
            *(ulong*)(stamps + 24) = 200; *(float*)(actor + 0x2660) = 7000;
            if (Read().Initial != null) throw new Exception("stale Behemoth maximum retained");
            *(int*)(entries + 96) = 5;
            if (reader.TryReadSnapshot(world, 1, out _)) throw new Exception("ambiguous Behemoth head accepted");
            *(int*)(entries + 96) = 1; *(int*)history = 1; *(nint*)(actor + 0xce0) = history + 4;
            if (reader.TryReadSnapshot(world, 1, out _)) throw new Exception("unrelated Behemoth history accepted");
            *(nint*)(actor + 0xce0) = history;
            foreach (float invalid in new[] { -1f, float.NaN, float.PositiveInfinity })
            {
                *(float*)(actor + 0x2660) = invalid;
                if (reader.TryReadSnapshot(world, 1, out _)) throw new Exception("invalid Behemoth current accepted");
            }
            *(float*)(actor + 0x2660) = 14000; *(nint*)actor = module + 0x5b020a0;
            if (reader.TryReadSnapshot(world, 1, out _)) throw new Exception("unverified Behemoth class accepted");
            Console.WriteLine("PASS: real-memory Behemoth/Ennugi typed initial/float damage, relock/source proof, processed head break and body duplicate IDs; stale/unknown/invalid inputs rejected");
        }
        finally
        {
            foreach (nint p in allocations) NativeMemory.Free((void*)p);
            VirtualFree(module, 0, 0x8000);
        }
    }
}
