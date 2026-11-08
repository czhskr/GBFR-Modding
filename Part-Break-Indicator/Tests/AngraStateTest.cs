using System.Runtime.InteropServices;
using PartIndicator;

static unsafe class AngraStateTest
{
    [DllImport("kernel32")] private static extern nint VirtualAlloc(nint address, nuint size, uint kind, uint protection);
    [DllImport("kernel32")] private static extern bool VirtualFree(nint address, nuint size, uint kind);
    public static void Run()
    {
        var allocations = new List<nint>();
        nint Alloc(int size) { nint p = (nint)NativeMemory.AllocZeroed((nuint)size); allocations.Add(p); return p; }
        nint module = VirtualAlloc(0, 0x8000000, 0x2000, 4);
        if (module == 0) throw new Exception("Angra fixture reservation");
        try
        {
            foreach (int page in new[] { 0x7362000, 0x7035000, 0x7020000, 0x7c53000 })
                if (VirtualAlloc(module + page, 0x1000, 0x1000, 4) == 0) throw new Exception("Angra fixture commit");
            nint vector = Alloc(8), player = Alloc(0x240), a = Alloc(8), b = Alloc(0x30), c = Alloc(0x20),
                localScene = Alloc(0x80), localActor = Alloc(8), target = Alloc(0x80), actor = Alloc(0x3000),
                manager = Alloc(0x60), scenes = Alloc(64), stamps = Alloc(64), session = Alloc(8), entries = Alloc(4 * 48),
                states = Alloc(4 * 12), definitions = Alloc(4 * 56), damage = Alloc(4 * 56), parameter = Alloc(0x3000),
                componentOwner = Alloc(8), ownerObject = Alloc(0x30), ownerContext = Alloc(0x20);
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
            *(nint*)actor = module + 0x59e9398; *(uint*)(actor + 0x160) = 100000;
            *(nint*)(actor + 0x1598) = parameter; *(int*)(actor + 0x2288) = 1;
            *(nint*)(actor + 0x788) = entries; *(nint*)(actor + 0x790) = entries + 4 * 48;
            int[] lockIds = [0, 5, 526, 5];
            for (int i = 0; i < 4; i++) *(int*)(entries + i * 48) = lockIds[i];
            nint controller = actor + 0x21b0;
            *(nint*)controller = module + 0x6166438;
            *(nint*)(controller + 8) = componentOwner; *(nint*)componentOwner = ownerObject;
            *(nint*)(ownerObject + 0x28) = ownerContext; *(nint*)(ownerContext + 0x18) = target;
            *(nint*)(controller + 0x10) = states; *(nint*)(controller + 0x18) = states + 4 * 12;
            *(nint*)(controller + 0x28) = definitions; *(nint*)(controller + 0x30) = definitions + 4 * 56;
            *(nint*)(controller + 0x40) = damage; *(nint*)(controller + 0x48) = damage + 4 * 56;
            int[] current = [601, 0, 1500, 1500], initial = [800, 800, 1500, 1500];
            uint[] masks = [0x400000, 0x100000, 0x800000, 0x200000];
            for (int i = 0; i < 4; i++)
            {
                *(int*)(definitions + i * 56) = i; *(int*)(definitions + i * 56 + 4) = -1; *(byte*)(definitions + i * 56 + 8) = 1;
                *(int*)(states + i * 12) = current[i]; *(int*)(states + i * 12 + 4) = initial[i];
                *(int*)(parameter + 0x2df8 + i * 16) = initial[i];
                *(int*)(damage + i * 56) = i; *(uint*)(damage + i * 56 + 8) = masks[i];
            }
            *(byte*)(states + 20) = 1; *(int*)(player + 0x118) = 5;
            var reader = new NativeStateReader(module);
            if (!reader.TryReadWorld(out var world)) throw new Exception("Angra owner fixture");
            (PartSnapshot First, PartSnapshot Second) Read()
            {
                if (!reader.TryReadSnapshots(world, 1, out var first, out var second) || first is null || second is null)
                    throw new Exception("Angra shared-head pair unavailable");
                return (first, second);
            }
            void Reject(string message) { if (reader.TryReadSnapshots(world, 1, out _, out _)) throw new Exception(message); }
            int Percent(PartSnapshot s) => PercentageText.Percentage(PartStatePolicy.Evaluate(s)) ?? -1;
            var pair = Read();
            if (Percent(pair.First) != 75 || !PartStatePolicy.Evaluate(pair.Second).Broken || Percent(pair.Second) != -1)
                throw new Exception("Angra 75% and broken must remain independent");
            *(int*)states = 400;
            pair = Read(); if (Percent(pair.First) != 50 || !PartStatePolicy.Evaluate(pair.Second).Broken) throw new Exception("Angra cross-horn damage leakage");
            *(byte*)(states + 20) = 0; *(int*)(states + 12) = 800;
            pair = Read(); if (Percent(pair.Second) != 100 || PartStatePolicy.Evaluate(pair.Second).Broken) throw new Exception("Angra restored horn stayed broken");
            *(int*)states = 0;
            if (PartStatePolicy.Evaluate(Read().First).Broken || Percent(Read().First) != 0) throw new Exception("Angra zero alone became destruction");
            *(int*)(parameter + 0x2df8) = 801;
            pair = Read(); if (!PartStatePolicy.Evaluate(pair.First).Visible || Percent(pair.First) != -1 || Percent(pair.Second) != 100)
                throw new Exception("Angra denominator proof must be independent");
            *(int*)(parameter + 0x2df8) = 800;
            // Mode2 flags the normal pair broken while its alternate pair starts undamaged.
            *(int*)states = 800; *(byte*)(states + 8) = 1; *(byte*)(states + 20) = 1;
            *(int*)(actor + 0x2288) = 2;
            pair = Read();
            if (Percent(pair.First) != 100 || Percent(pair.Second) != 100 ||
                PartStatePolicy.Evaluate(pair.First).Broken || PartStatePolicy.Evaluate(pair.Second).Broken)
                throw new Exception("Angra normal broken flags leaked into alternate horns");
            *(int*)(states + 24) = 1251;
            pair = Read(); if (Percent(pair.First) != 83 || Percent(pair.Second) != 100)
                throw new Exception("Angra alternate row2 must damage only the frontal right horn");
            *(int*)(parameter + 0x2e18) = 1501;
            pair = Read(); if (!PartStatePolicy.Evaluate(pair.First).Visible || Percent(pair.First) != -1 || Percent(pair.Second) != 100)
                throw new Exception("Angra alternate denominator borrowed another horn's source");
            *(int*)(parameter + 0x2e18) = 1500;
            *(int*)(states + 24) = 0;
            pair = Read(); if (PartStatePolicy.Evaluate(pair.First).Broken || Percent(pair.First) != 0)
                throw new Exception("Angra alternate zero alone became processed destruction");
            *(byte*)(states + 32) = 1; *(int*)(states + 36) = 652;
            pair = Read(); if (!PartStatePolicy.Evaluate(pair.First).Broken || Percent(pair.First) != -1 || Percent(pair.Second) != 43)
                throw new Exception("Angra alternate break and other horn damage were not independent");
            *(int*)(states + 36) = 0; *(byte*)(states + 44) = 1;
            pair = Read(); if (!PartStatePolicy.Evaluate(pair.First).Broken || !PartStatePolicy.Evaluate(pair.Second).Broken ||
                Percent(pair.First) != -1 || Percent(pair.Second) != -1)
                throw new Exception("Angra both alternate broken horns retained a percentage");
            *(int*)states = 400; *(byte*)(states + 8) = 0; *(byte*)(states + 20) = 0;
            foreach (int normalMode in new[] { 0, 1 })
            {
                *(int*)(actor + 0x2288) = normalMode;
                pair = Read(); if (Percent(pair.First) != 50 || Percent(pair.Second) != 100 ||
                    PartStatePolicy.Evaluate(pair.First).Broken || PartStatePolicy.Evaluate(pair.Second).Broken)
                    throw new Exception("Angra alternate broken flags leaked back into normal horns");
            }
            foreach (int unknownMode in new[] { -1, 3, 7 })
            { *(int*)(actor + 0x2288) = unknownMode; Reject("Angra unknown mode accepted"); }
            *(int*)(actor + 0x2288) = 1;
            *(int*)(player + 0x118) = 526; Reject("Angra hand acquired horn display");
            *(int*)(player + 0x118) = 5;
            if (Percent(Read().Second) != 100) throw new Exception("Angra head return lost other horn");
            *(nint*)controller = module + 0x6166440; Reject("Angra controller vtable not exact"); *(nint*)controller = module + 0x6166438;
            *(int*)(definitions + 4) = 5; Reject("Angra unverified direct descriptor accepted"); *(int*)(definitions + 4) = -1;
            *(uint*)(damage + 8) = 0x100000; Reject("Angra mismatched damage mapping accepted"); *(uint*)(damage + 8) = 0x400000;
            *(byte*)(states + 8) = 2; Reject("Angra invalid break byte accepted"); *(byte*)(states + 8) = 0;
            *(int*)states = -1; Reject("Angra negative current accepted"); *(int*)states = 0;
            *(nint*)(controller + 0x18) = states + 3 * 12; Reject("Angra count mismatch accepted"); *(nint*)(controller + 0x18) = states + 4 * 12;
            *(nint*)(ownerContext + 0x18) = localScene; Reject("Angra foreign owner accepted"); *(nint*)(ownerContext + 0x18) = target;
            *(ulong*)(stamps + 24) = 201; Reject("Angra stale stamp accepted"); *(ulong*)(stamps + 24) = 200;
            *(nint*)actor = module + 0x59e93a0; Reject("Angra neighbouring class accepted");
            Console.WriteLine("PASS: actual four-row Angra normal/alternate horn pairs, independent percentage/break/source, form transitions and exact topology/identity guards");
        }
        finally { foreach (nint p in allocations) NativeMemory.Free((void*)p); VirtualFree(module, 0, 0x8000); }
    }
}
