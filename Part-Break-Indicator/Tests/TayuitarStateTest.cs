using System.Runtime.InteropServices;
using PartIndicator;

static unsafe class TayuitarStateTest
{
    [DllImport("kernel32")] private static extern nint VirtualAlloc(nint address, nuint size, uint kind, uint protection);
    [DllImport("kernel32")] private static extern bool VirtualFree(nint address, nuint size, uint kind);
    public static void Run()
    {
        var allocations = new List<nint>();
        nint Alloc(int size) { nint p = (nint)NativeMemory.AllocZeroed((nuint)size); allocations.Add(p); return p; }
        nint module = VirtualAlloc(0, 0x8000000, 0x2000, 4);
        if (module == 0) throw new Exception("Tayu fixture reservation");
        try
        {
            foreach (int page in new[] { 0x7362000, 0x7035000, 0x7020000, 0x7c53000 })
                if (VirtualAlloc(module + page, 0x1000, 0x1000, 4) == 0) throw new Exception("Tayu fixture commit");
            nint vector = Alloc(8), player = Alloc(0x240), a = Alloc(8), b = Alloc(0x30), c = Alloc(0x20),
                localScene = Alloc(0x80), localActor = Alloc(8), target = Alloc(0x80), actor = Alloc(0xa100),
                parameter = Alloc(0x1600), entries = Alloc(5 * 48), manager = Alloc(0x60),
                scenes = Alloc(64), stamps = Alloc(64), session = Alloc(8),
                descriptorSlot = Alloc(8), descriptor = Alloc(0x20), descriptorVtable = Alloc(0x20);
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
            *(nint*)actor = module + 0x5b77238; *(uint*)(actor + 0x160) = 26427997;
            *(nint*)(actor + 0x28) = descriptorSlot; *(nint*)descriptorSlot = descriptor;
            *(nint*)descriptor = descriptorVtable; *(nint*)(descriptorVtable + 0x18) = module + 0x205c2f0;
            *(int*)(descriptor + 0x14) = 0x21900;
            *(nint*)(actor + 0x1598) = parameter; *(int*)(parameter + 0xbf0) = 160;
            *(int*)(parameter + 0x8d8) = 4;
            *(nint*)(actor + 0x788) = entries; *(nint*)(actor + 0x790) = entries + 5 * 48;
            int[] ids = [1296, 7, 1552, 787, 1043]; // Deliberately different from native record order.
            for (int i = 0; i < ids.Length; i++) { *(int*)(entries + i * 48) = ids[i]; *(byte*)(entries + i * 48 + 0x29) = 1; }
            for (int index = 2; index <= 5; index++)
            { nint row = actor + 0x6a68 + index * 40; *(int*)row = 140; *(int*)(row + 4) = 140; *(byte*)(row + 8) = 1; *(int*)(row + 16) = index; }
            var reader = new NativeStateReader(module);
            if (!reader.TryReadWorld(out var world)) throw new Exception("Tayu local fixture");
            PartDisplay Read(string message)
            { if (!reader.TryReadSnapshot(world, 1, out var snapshot) || snapshot is null) throw new Exception(message); return PartStatePolicy.Evaluate(snapshot); }
            void Reject(string message) { if (reader.TryReadSnapshot(world, 1, out _)) throw new Exception(message); }
            *(byte*)(actor + 0x9435) = 1;
            foreach (var (selected, index) in new[] { (787, 2), (1043, 3), (1296, 4), (1552, 5) })
            {
                *(int*)(player + 0x118) = selected; nint row = actor + 0x6a68 + index * 40;
                if (PercentageText.Percentage(Read("Tayu root leg unavailable")) != 100) throw new Exception("Tayu initial cycle needs native 140 maximum");
                *(int*)row = 70;
                if (PercentageText.Percentage(Read("Tayu initial leg damaged")) != 50) throw new Exception("Tayu initial cycle damage");
                *(int*)(actor + 0x9410) = 1; *(byte*)(actor + 0x9435) = 0;
                foreach (int hp in new[] { 200, 40, 1 })
                { *(int*)row = hp; var value = Read("Tayu phased leg unavailable"); if (value.Broken || PercentageText.Percentage(value) != null) throw new Exception("Tayu stale140 used as cycle maximum"); }
                *(int*)row = 0; Reject("Tayu zero published before active flag"); *(byte*)(row + 8) = 0;
                if (!Read("Tayu processed leg unavailable").Broken) throw new Exception("Tayu current/active break");
                *(int*)row = 200; Reject("Tayu inconsistent regeneration published"); *(byte*)(row + 8) = 1;
                if (Read("Tayu restored leg unavailable").Broken) throw new Exception("Tayu current leg stayed broken");
                *(int*)(row + 16) = 7; Reject("Tayu wrong kind accepted"); *(int*)(row + 16) = index;
                *(int*)(row + 12) = 1; Reject("Tayu equipment attachment accepted as leg"); *(int*)(row + 12) = 0;
                *(int*)(row + 4) = 0; Reject("Tayu uninitialized leg accepted"); *(int*)(row + 4) = 140;
                *(int*)row = -1; Reject("Tayu negative current accepted"); *(int*)row = 140;
                *(int*)(actor + 0x9410) = 0; *(byte*)(actor + 0x9435) = 1;
            }
            *(int*)(actor + 0x9410) = 1; *(byte*)(actor + 0x9435) = 0;
            *(int*)(parameter + 0x97c) = 50; *(int*)(parameter + 0x980) = 100;
            *(int*)(parameter + 0x984) = 140; *(int*)(parameter + 0x988) = 200;
            *(float*)(parameter + 0xc00) = .5f;
            *(int*)(player + 0x118) = 787;
            if (Read("Tayu ambiguous reset unavailable").Fraction != null) throw new Exception("Tayu ambiguous reset reused stale 140");
            *(int*)(actor + 0x6a68 + 5 * 40) = 200;
            if (PercentageText.Percentage(Read("Tayu first reset unavailable")) != 70) throw new Exception("Tayu first reset needs 200 maximum");
            *(int*)(actor + 0x6a68 + 5 * 40) = 140; *(int*)(player + 0x118) = 1552;
            if (PercentageText.Percentage(Read("Tayu changed leg unavailable")) != 70) throw new Exception("Tayu lost proven maximum after retarget");
            *(ulong*)(target + 0x48) = *(ulong*)(stamps + 24) = 201;
            if (Read("Tayu reused target unavailable").Fraction != null) throw new Exception("Tayu leaked maximum to reused actor");
            *(int*)(actor + 0x6a68 + 5 * 40) = 200;
            if (PercentageText.Percentage(Read("Tayu replacement first reset unavailable")) != 100) throw new Exception("Tayu replacement reset proof");
            *(int*)(actor + 0x9430) = 1;
            if (Read("Tayu later cycle unavailable").Fraction != null) throw new Exception("Tayu later cycle reused first reset maximum");
            *(int*)(parameter + 0x8d8) = 1;
            *(int*)(actor + 0x6a68 + 2 * 40) = 0; *(byte*)(actor + 0x6a68 + 2 * 40 + 8) = 0;
            *(int*)(actor + 0x6a68 + 5 * 40) = 100;
            *(int*)(actor + 0x9410) = 3; *(int*)(actor + 0x9430) = 0; *(byte*)(actor + 0x9435) = 1;
            if (Read("Tayu in-flight round increment unavailable").Fraction != null) throw new Exception("Tayu break transition read stale initial as fresh");
            *(int*)(parameter + 0x8d8) = 4;
            *(int*)(actor + 0x6a68 + 2 * 40) = 140; *(byte*)(actor + 0x6a68 + 2 * 40 + 8) = 1;
            *(ulong*)(target + 0x48) = *(ulong*)(stamps + 24) = 200;
            *(int*)(player + 0x118) = 7; *(int*)(actor + 0x9420) = 160;
            if (PercentageText.Percentage(Read("Tayu exposed head unavailable")) != 100) throw new Exception("Tayu head used unrelated1000 record");
            *(int*)(actor + 0x9420) = 80;
            if (PercentageText.Percentage(Read("Tayu damaged head unavailable")) != 50) throw new Exception("Tayu head fraction");
            *(int*)(actor + 0x9420) = 0;
            var zero = Read("Tayu depleted unprocessed head unavailable");
            if (zero.Broken || PercentageText.Percentage(zero) != 0) throw new Exception("Tayu first hit incorrectly became break");
            *(byte*)(actor + 0x9996) = 1;
            var broken = Read("Tayu processed head unavailable");
            if (!broken.Broken || PercentageText.Percentage(broken) != null) throw new Exception("Tayu second hit processed marker");
            *(byte*)(actor + 0x9996) = 2; Reject("Tayu invalid processed marker accepted"); *(byte*)(actor + 0x9996) = 0;
            *(int*)(actor + 0x9420) = 160; *(byte*)(actor + 0xe) = 0x40;
            if (PercentageText.Percentage(Read("Carnage head source unavailable")) != 100) throw new Exception("Carnage native head initial omitted");
            *(int*)(actor + 0x9420) = 80;
            if (PercentageText.Percentage(Read("Carnage damaged head unavailable")) != 50) throw new Exception("Carnage head used a leg maximum");
            *(nint*)(player + 0x108) = 0; Reject("Carnage no-lock displayed"); *(nint*)(player + 0x108) = target;
            if (PercentageText.Percentage(Read("Carnage head relock unavailable")) != 50) throw new Exception("Carnage head relock lost source");
            *(int*)(actor + 0x9410) = 3; *(int*)(actor + 0x9430) = 0; *(byte*)(actor + 0x9435) = 1;
            for (int index = 2; index <= 5; index++)
            { nint row = actor + 0x6a68 + index * 40; *(int*)row = *(int*)(row + 4) = 1000; }
            foreach (var (selected, index) in new[] { (787, 2), (1043, 3), (1296, 4), (1552, 5) })
            {
                *(int*)(player + 0x118) = selected; nint row = actor + 0x6a68 + index * 40;
                if (PercentageText.Percentage(Read("Shared-class alternate leg unavailable")) != 100) throw new Exception("Alternate flags or phase alone hid a normal identity");
                *(int*)row = 750;
                if (PercentageText.Percentage(Read("Shared-class alternate damaged leg unavailable")) != 75) throw new Exception("Alternate initial cycle damage");
            }
            *(int*)(actor + 0x6a68 + 2 * 40) = 0; *(byte*)(actor + 0x6a68 + 2 * 40 + 8) = 0;
            if (PercentageText.Percentage(Read("Shared-class alternate surviving leg unavailable")) != 75) throw new Exception("Alternate lost initial maximum before break threshold");
            *(int*)(descriptor + 0x14) = 0x21910;
            foreach (int selected in new[] { 787, 1043, 1296, 1552 })
            {
                *(int*)(player + 0x118) = selected;
                Reject("Carnage native identity still showed a leg icon or percentage");
            }
            *(byte*)(actor + 0xe) = 0;
            Reject("Carnage native identity exclusion depended on a difficulty flag");
            *(byte*)(actor + 0xe) = 0x40;
            *(int*)(player + 0x118) = 7;
            if (PercentageText.Percentage(Read("Carnage identity head unavailable")) != 50) throw new Exception("Carnage leg exclusion affected the head");
            *(int*)(actor + 0x9420) = 0;
            if (Read("Carnage unprocessed zero unavailable").Broken) throw new Exception("Carnage zero bypassed processed marker");
            *(byte*)(actor + 0x9996) = 1;
            if (Read("Carnage processed head unavailable") is not { Broken: true, Fraction: null }) throw new Exception("Carnage processed head retained percentage");
            *(byte*)(actor + 0x9996) = 0; *(int*)(actor + 0x9420) = 80;
            *(int*)(parameter + 0xbf0) = 0;
            if (Read("Tayu invalid source should keep icon").Fraction != null) throw new Exception("Tayu invalid denominator"); *(int*)(parameter + 0xbf0) = 160;
            *(int*)(actor + 0x9420) = 161;
            if (Read("Tayu unexpected maximum should keep icon").Fraction != null) throw new Exception("Tayu exceeded source denominator"); *(int*)(actor + 0x9420) = 80;
            *(byte*)(entries + 48 + 0x29) = 0; Reject("Tayu disabled head lock accepted"); *(byte*)(entries + 48 + 0x29) = 1;
            *(int*)entries = 7; Reject("Tayu duplicate selected lock accepted"); *(int*)entries = ids[0];
            *(ulong*)(stamps + 24) = 201; Reject("Tayu stale actor accepted"); *(ulong*)(stamps + 24) = 200;
            *(nint*)(actor + 0x790) = entries + 49; Reject("Tayu misaligned lock vector accepted"); *(nint*)(actor + 0x790) = entries + 5 * 48;
            foreach (int id in new[] { -1, 0, 1792, 265 }) { *(int*)(player + 0x118) = id; Reject("Tayu metadata/equipment/unmapped selection accepted"); }
            *(int*)(player + 0x118) = 7; *(nint*)actor = module + 0x5b77240; Reject("unknown Tayu class accepted");
            Console.WriteLine("PASS: Tayu leg cycles, Carnage-only leg exclusion, head percentages, and stale actor rejection");
        }
        finally { foreach (nint p in allocations) NativeMemory.Free((void*)p); VirtualFree(module, 0, 0x8000); }
    }
}
