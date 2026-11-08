using System.Runtime.InteropServices;
using PartIndicator;

static unsafe class FuryHumanoidStateTest
{
    [DllImport("kernel32")] private static extern nint VirtualAlloc(nint address, nuint size, uint kind, uint protection);
    [DllImport("kernel32")] private static extern bool VirtualFree(nint address, nuint size, uint kind);
    public static void Run()
    {
        Run(0x7201, 0x6175aa0);
        Run(0x7211, 0x6177780);
    }

    private static void Run(uint type, int vtableRva)
    {
        var allocations = new List<nint>();
        nint Alloc(int size) { nint p = (nint)NativeMemory.AllocZeroed((nuint)size); allocations.Add(p); return p; }
        nint module = VirtualAlloc(0, 0x8000000, 0x2000, 4);
        if (module == 0) throw new Exception("Furycane fixture reservation");
        try
        {
            foreach (int page in new[] { 0x7362000, 0x7035000, 0x7020000, 0x7c53000 })
                if (VirtualAlloc(module + page, 0x1000, 0x1000, 4) == 0) throw new Exception("Furycane fixture commit");
            nint vector = Alloc(8), player = Alloc(0x240), a = Alloc(8), b = Alloc(0x30), c = Alloc(0x20),
                localScene = Alloc(0x80), localActor = Alloc(8), target = Alloc(0x80), actor = Alloc(0x3000),
                manager = Alloc(0x60), scenes = Alloc(64), stamps = Alloc(64), session = Alloc(8), entries = Alloc(7 * 48);
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
            *(nint*)actor = module + vtableRva; *(uint*)(actor + 0x160) = 26018323;
            *(nint*)(actor + 0x788) = entries; *(nint*)(actor + 0x790) = entries + 7 * 48;
            int[] ids = [2, 0x883, 0xc60, 0x613, 0xc0c, 5, 0x13];
            int[] initial = [0, 740, 740, 740, 550, 500, 0];
            for (int i = 0; i < ids.Length; i++)
            {
                *(int*)(entries + i * 48) = ids[i];
                *(int*)(actor + 0x2604 + i * 12) = initial[i]; *(int*)(actor + 0x2608 + i * 12) = initial[i];
            }
            // The humanoid constructor uses direct lock rows, not Em7200's mapping array/history.
            *(int*)(actor + 0x25bc) = int.MaxValue;
            *(nint*)(actor + 0xcd8) = 1; *(nint*)(actor + 0xce0) = 5;
            var reader = new NativeStateReader(module);
            if (!reader.TryReadWorld(out var world)) throw new Exception("Fury humanoid owner fixture");
            PartSnapshot Read()
            {
                if (!reader.TryReadSnapshot(world, 1, out var value) || value is null) throw new Exception("Fury humanoid native armor unavailable");
                if (value.ClassId != type) throw new Exception("Fury humanoid identity lost");
                return value;
            }
            void Reject(string message) { if (reader.TryReadSnapshot(world, 1, out _)) throw new Exception(message); }
            int Percent() => PercentageText.Percentage(PartStatePolicy.Evaluate(Read())) ?? -1;
            for (int i = 1; i <= 5; i++)
            {
                *(int*)(player + 0x118) = ids[i]; nint record = actor + 0x2604 + i * 12;
                if (Read().Initial != initial[i] || Percent() != 100) throw new Exception("Fury humanoid stored initial mismatch");
                *(int*)record -= 250;
                if (Percent() != (int)(100d * (initial[i] - 250) / initial[i])) throw new Exception("Fury humanoid damage percentage mismatch");
                *(int*)record = 0;
                if (PartStatePolicy.Evaluate(Read()).Broken || Percent() != 0) throw new Exception("Fury humanoid zero alone became destruction");
                *(byte*)(record + 8) = 1;
                if (!PartStatePolicy.Evaluate(Read()).Broken || Percent() != -1) throw new Exception("Fury humanoid processed break missing");
                *(byte*)(record + 8) = 0; *(int*)record = initial[i];
                if (PartStatePolicy.Evaluate(Read()).Broken || Percent() != 100) throw new Exception("Fury humanoid restored armor stayed broken");
            }
            *(int*)(player + 0x118) = ids[1]; nint selectedRecord = actor + 0x2604 + 12;
            *(int*)(entries + 48) = ids[2]; *(int*)(entries + 96) = ids[1];
            Reject("Fury humanoid unverified row reordering accepted");
            *(int*)(entries + 48) = ids[1]; *(int*)(entries + 96) = ids[2];
            *(int*)(entries + 96) = ids[1]; Reject("Fury humanoid duplicate selected lock accepted"); *(int*)(entries + 96) = ids[2];
            *(byte*)(selectedRecord + 8) = 2; Reject("Fury humanoid invalid broken byte accepted"); *(byte*)(selectedRecord + 8) = 0;
            *(int*)(selectedRecord + 4) = 0; Reject("Fury humanoid missing initial accepted"); *(int*)(selectedRecord + 4) = initial[1];
            *(int*)selectedRecord = initial[1] + 1; Reject("Fury humanoid current above initial accepted"); *(int*)selectedRecord = initial[1];
            *(ulong*)(stamps + 24) = 201; Reject("Fury humanoid stale scene stamp accepted"); *(ulong*)(stamps + 24) = 200;
            *(nint*)(actor + 0x790) = entries + 513 * 48; Reject("Fury humanoid oversized lock vector accepted"); *(nint*)(actor + 0x790) = entries + 7 * 48;
            foreach (int unsupported in new[] { ids[0], ids[6], -1, 999 })
            { *(int*)(player + 0x118) = unsupported; Reject("Fury humanoid cosmetic/core/unknown point accepted"); }
            *(int*)(player + 0x118) = ids[1]; *(nint*)actor = module + vtableRva + 8; Reject("Fury humanoid unknown adjacent vtable accepted");
            Console.WriteLine($"PASS: real-memory Em{type:x4} five native armor rows, stored initials, break/restore and row/identity/range guards");
        }
        finally
        {
            foreach (nint p in allocations) NativeMemory.Free((void*)p);
            VirtualFree(module, 0, 0x8000);
        }
    }
}
