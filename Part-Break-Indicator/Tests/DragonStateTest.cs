using System.Runtime.InteropServices;
using PartIndicator;

static unsafe class DragonStateTest
{
    [DllImport("kernel32")] private static extern nint VirtualAlloc(nint address, nuint size, uint kind, uint protection);
    [DllImport("kernel32")] private static extern bool VirtualFree(nint address, nuint size, uint kind);
    public static void Run()
    {
        var allocations = new List<nint>();
        nint Alloc(int size) { nint p = (nint)NativeMemory.AllocZeroed((nuint)size); allocations.Add(p); return p; }
        nint module = VirtualAlloc(0, 0x8000000, 0x2000, 4);
        if (module == 0) throw new Exception("Dragon fixture reservation");
        try
        {
            foreach (int page in new[] { 0x7362000, 0x7035000, 0x7020000, 0x7c53000 })
                if (VirtualAlloc(module + page, 0x1000, 0x1000, 4) == 0) throw new Exception("Dragon fixture commit");
            nint vector = Alloc(8), player = Alloc(0x240), a = Alloc(8), b = Alloc(0x30), c = Alloc(0x20),
                localScene = Alloc(0x80), localActor = Alloc(8), target = Alloc(0x80), actor = Alloc(0x3000),
                manager = Alloc(0x60), scenes = Alloc(80 * 8), stamps = Alloc(80 * 8), session = Alloc(8), entries = Alloc(7 * 48),
                parameter = Alloc(0x1000), otherParameter = Alloc(0x1000), history = Alloc(8),
                secondTarget = Alloc(0x80), secondActor = Alloc(0x3000), unknownActor = Alloc(8);
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
            *(uint*)(actor + 0x160) = 41914120; *(nint*)(actor + 0x1598) = parameter;
            *(nint*)(actor + 0x788) = entries; *(nint*)(actor + 0x790) = entries + 7 * 48;
            int[] ids = [5, 2, 514, 322, 386, 354, 7];
            for (int i = 0; i < ids.Length; i++) *(int*)(entries + i * 48) = ids[i];
            *(float*)(parameter + 0xf50) = 1500; *(float*)(otherParameter + 0xf50) = 1500;
            var reader = new NativeStateReader(module);
            if (!reader.TryReadWorld(out var world)) throw new Exception("Dragon owner fixture");
            PartSnapshot Read()
            {
                *(int*)(player + 0x118) = 5;
                if (!reader.TryReadSnapshot(world, 1, out var value) || value is null) throw new Exception("verified Dragon head unavailable");
                return value;
            }
            void Body() { *(int*)(player + 0x118) = 7; if (reader.TryReadSnapshot(world, 1, out _)) throw new Exception("Dragon body displayed"); }
            foreach (var (vt, type) in new (int, uint)[] { (0x61791e8,0x1800), (0x617b868,0x1801), (0x617aec8,0x1802),
                         (0x617c208,0x1803), (0x6179b88,0x1804), (0x617cba8,0x1805), (0x617a528,0x1806) })
            {
                *(nint*)actor = module + vt; *(float*)(actor + 0x27a4) = 1500;
                *(nint*)(actor + 0xcd8) = 0; *(nint*)(actor + 0xce0) = 0; reader = new NativeStateReader(module);
                Body(); var first = Read();
                if (first.ClassId != type || first.Initial != 1500 || PercentageText.Percentage(PartStatePolicy.Evaluate(first)) != 100)
                    throw new Exception("Dragon untouched float initial source not established");
                *(float*)(actor + 0x27a4) = 1250.0250244140625f;
                if (PercentageText.Percentage(PartStatePolicy.Evaluate(Read())) != 83) throw new Exception("Dragon native float decrease");
                Body(); if (Read().Initial != 1500) throw new Exception("body selection lost Dragon proof");
                *(nint*)(player + 0x108) = 0; *(int*)(player + 0x118) = -1;
                if (reader.TryReadSnapshot(world, 1, out _)) throw new Exception("no-lock gap displayed");
                *(nint*)(player + 0x108) = target;
                if (Read().Initial != 1500) throw new Exception("no-lock transition discarded proved Dragon maximum");
                *(uint*)(secondTarget + 0x38) = 5; *(nint*)(secondTarget + 0x40) = secondTarget; *(ulong*)(secondTarget + 0x48) = 300;
                *(nint*)(scenes + 32) = secondTarget; *(ulong*)(stamps + 32) = 300;
                *(nint*)(secondTarget + 0x70) = unknownActor; *(nint*)unknownActor = module + 0x1234;
                *(nint*)(player + 0x108) = secondTarget;
                if (reader.TryReadSnapshot(world, 1, out _)) throw new Exception("unrelated unsupported monster displayed");
                *(nint*)(player + 0x108) = target;
                if (Read().Initial != 1500) throw new Exception("unrelated monster discarded Dragon maximum");
                NativeMemory.Copy((void*)actor, (void*)secondActor, 0x3000);
                *(nint*)(secondTarget + 0x70) = secondActor; *(nint*)(secondActor + 0x1598) = otherParameter;
                *(float*)(otherParameter + 0xf50) = 2000; *(float*)(secondActor + 0x27a4) = 2000;
                *(nint*)(player + 0x108) = secondTarget;
                if (Read().Initial != 2000) throw new Exception("second Dragon maximum not established independently");
                *(float*)(secondActor + 0x27a4) = 500;
                if (PercentageText.Percentage(PartStatePolicy.Evaluate(Read())) != 25) throw new Exception("second Dragon percentage swapped");
                *(nint*)(player + 0x108) = target;
                if (Read().Initial != 1500) throw new Exception("second Dragon discarded first maximum");
                *(float*)(otherParameter + 0xf50) = 2400; *(nint*)(player + 0x108) = secondTarget;
                if (Read().Initial != null) throw new Exception("cached Dragon proof survived hidden parameter-value change");
                *(nint*)(player + 0x108) = target; *(float*)(otherParameter + 0xf50) = 1500;
                *(float*)(actor + 0x27a4) = .1500549316f;
                if (PartStatePolicy.Evaluate(Read()).Broken) throw new Exception("Dragon positive float inferred broken");
                *(float*)(actor + 0x27a4) = 0;
                if (PartStatePolicy.Evaluate(Read()).Broken) throw new Exception("Dragon zero inferred broken without history");
                *(int*)history = 0; *(nint*)(actor + 0xcd8) = history; *(nint*)(actor + 0xce0) = history + 4;
                if (!PartStatePolicy.Evaluate(Read()).Broken || PartStatePolicy.Evaluate(Read()).Fraction != null) throw new Exception("Dragon history did not drive destruction");
                *(nint*)(actor + 0xcd8) = 0; *(nint*)(actor + 0xce0) = 0; *(float*)(actor + 0x27a4) = 750;
                if (Read().Initial != null) throw new Exception("Dragon proof survived history reset");
                reader = new NativeStateReader(module); if (Read().Initial != null) throw new Exception("late damaged Dragon invented maximum");
                *(float*)(actor + 0x27a4) = 1500; Read();
                *(byte*)(actor + 0xe) = 0x40; Body(); *(byte*)(actor + 0xe) = 0; *(float*)(actor + 0x27a4) = 750;
                if (Read().Initial != null) throw new Exception("body missed alternate Dragon mode invalidation");
                *(float*)(actor + 0x27a4) = 1500; Read(); *(nint*)(actor + 0x1598) = otherParameter; *(float*)(actor + 0x27a4) = 750;
                if (Read().Initial != null) throw new Exception("Dragon proof survived parameter replacement");
                *(nint*)(actor + 0x1598) = parameter; *(float*)(actor + 0x27a4) = 1500; Read();
                *(ulong*)(stamps + 24) = 201;
                if (reader.TryReadSnapshot(world, 1, out _)) throw new Exception("stale Dragon scene accepted");
                *(ulong*)(stamps + 24) = 200; *(float*)(actor + 0x27a4) = 750;
                if (Read().Initial != null) throw new Exception("stale Dragon proof retained");
                *(int*)(entries + 6 * 48) = 5;
                if (reader.TryReadSnapshot(world, 1, out _)) throw new Exception("duplicate Dragon head selection accepted");
                *(int*)(entries + 6 * 48) = 7;
                *(float*)(parameter + 0xf50) = float.NaN;
                if (Read().Initial != null) throw new Exception("invalid Dragon maximum used");
                *(float*)(parameter + 0xf50) = 1500;
            }
            *(float*)(parameter + 0xf50) = 1.2f; *(float*)(actor + 0x27a4) = 1.2f;
            reader = new NativeStateReader(module); Body(); *(float*)(actor + 0x27a4) = .6f;
            if (PercentageText.Percentage(PartStatePolicy.Evaluate(Read())) != 50) throw new Exception("Dragon initial/current float was truncated or independently rounded");
            *(float*)(parameter + 0xf50) = 1500; *(float*)(actor + 0x27a4) = 1500; reader = new NativeStateReader(module); Read();
            *(ulong*)(target + 0x48) = 201; *(ulong*)(stamps + 24) = 201; *(float*)(actor + 0x27a4) = 750;
            if (Read().Initial != null) throw new Exception("same-address new Dragon generation retained old maximum proof");
            *(float*)(actor + 0x27a4) = 1500; Read();
            *(nint*)actor = module + 0x1234;
            if (reader.TryReadSnapshot(world, 1, out _)) throw new Exception("unknown Dragon type displayed");
            *(nint*)actor = module + 0x617a528; *(float*)(actor + 0x27a4) = 750;
            if (Read().Initial != null) throw new Exception("abandoned Dragon target retained maximum proof");
            foreach (float bad in new[] { -1f, float.NaN, float.PositiveInfinity })
            {
                *(float*)(actor + 0x27a4) = bad;
                if (reader.TryReadSnapshot(world, 1, out _)) throw new Exception("invalid Dragon current accepted");
            }
            *(float*)(actor + 0x27a4) = 1500; reader = new NativeStateReader(module); Read();
            *(float*)(actor + 0x27a4) = 750;
            if (!reader.TryReadSnapshot(world, 2, out var generationBack) || generationBack?.Initial != null) throw new Exception("Dragon cache survived generation change");
            *(float*)(actor + 0x27a4) = 1500; Read();
            *(nint*)(module + 0x7362698) = vector;
            if (reader.TryReadWorld(out _)) throw new Exception("Dragon cache accepted invalid world");
            *(nint*)(module + 0x7362698) = vector + 8; *(float*)(actor + 0x27a4) = 750;
            if (Read().Initial != null) throw new Exception("Dragon cache survived invalid-world gap");
            *(float*)(actor + 0x27a4) = 1500; Read();
            for (int i = 0; i < 65; i++)
            {
                nint nextTarget = Alloc(0x80), nextActor = Alloc(0x3000); NativeMemory.Copy((void*)actor, (void*)nextActor, 0x3000);
                uint nextId = (uint)(6 + i); ulong nextStamp = (ulong)(500 + i);
                *(uint*)(nextTarget + 0x38) = nextId; *(nint*)(nextTarget + 0x40) = nextTarget;
                *(ulong*)(nextTarget + 0x48) = nextStamp; *(nint*)(nextTarget + 0x70) = nextActor;
                *(nint*)(scenes + (nint)((nextId - 1) * 8)) = nextTarget; *(ulong*)(stamps + (nint)((nextId - 1) * 8)) = nextStamp;
                *(nint*)(player + 0x108) = nextTarget;
                if (Read().Initial != 1500) throw new Exception("bounded-cache fresh proof unavailable");
                if (i == 62)
                {
                    *(nint*)(secondTarget + 0x70) = unknownActor; *(nint*)(player + 0x108) = secondTarget;
                    if (reader.TryReadSnapshot(world, 1, out _)) throw new Exception("unsupported target at cache limit displayed");
                    *(nint*)(player + 0x108) = target; *(float*)(actor + 0x27a4) = 750;
                    if (Read().Initial != 1500) throw new Exception("unsupported target unnecessarily expired a full proof cache");
                    *(float*)(actor + 0x27a4) = 1500;
                }
            }
            *(nint*)(player + 0x108) = target; *(float*)(actor + 0x27a4) = 750;
            if (Read().Initial != null) throw new Exception("proof cache did not conservatively expire at 64 targets");
            Console.WriteLine("PASS: seven Dragon layouts, no-lock/other-monster/multiple-Dragon proof retention, bounded cache and stale/parameter/alternate guards");
        }
        finally
        {
            foreach (var p in allocations) NativeMemory.Free((void*)p);
            VirtualFree(module, 0, 0x8000);
        }
    }
}
