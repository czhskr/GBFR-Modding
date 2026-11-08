using System.Runtime.InteropServices;
using PartIndicator;

static unsafe class FurycaneStateTest
{
    [DllImport("kernel32")] private static extern nint VirtualAlloc(nint address, nuint size, uint kind, uint protection);
    [DllImport("kernel32")] private static extern bool VirtualFree(nint address, nuint size, uint kind);
    public static void Run()
    {
        Run(0x7200, 0x6176de0);
        Run(0x7210, 0x6178120);
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
                manager = Alloc(0x60), scenes = Alloc(64), stamps = Alloc(64), session = Alloc(8), entries = Alloc(11 * 48);
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
            *(nint*)(actor + 0x788) = entries; *(nint*)(actor + 0x790) = entries + 11 * 48;
            int[] ids = [7, 0, 48, 80, 114, 130, 146, 162, 5, 300, 400];
            int[] initial = [800, 200, 600, 600, 400, 400, 400, 400, 500];
            for (int i = 0; i < ids.Length; i++) *(int*)(entries + i * 48) = ids[i];
            for (int i = 0; i < 9; i++)
            {
                *(int*)(actor + 0x25bc + i * 4) = i;
                *(int*)(actor + 0x25e8 + i * 20) = initial[i]; *(int*)(actor + 0x25ec + i * 20) = initial[i];
            }
            // Furycane's history is not its armor state; the adapter must not read the legacy vector.
            *(nint*)(actor + 0xcd8) = 1; *(nint*)(actor + 0xce0) = 5;
            var reader = new NativeStateReader(module);
            if (!reader.TryReadWorld(out var world)) throw new Exception("Furycane local owner fixture");
            PartSnapshot Read(string message)
            {
                if (!reader.TryReadSnapshot(world, 1, out var snapshot) || snapshot is null) throw new Exception(message);
                if (snapshot.ClassId != type) throw new Exception("Furycane variant identity lost");
                return snapshot;
            }
            void Reject(string message) { if (reader.TryReadSnapshot(world, 1, out _)) throw new Exception(message); }
            for (int i = 4; i < 8; i++)
            {
                *(int*)(player + 0x118) = ids[i];
                nint record = actor + 0x25e8 + i * 20;
                var full = PartStatePolicy.Evaluate(Read("verified Furycane leg unavailable"));
                if (!full.Visible || full.Broken || full.Fraction != 1) throw new Exception("fresh Furycane 100%");
                *(int*)record = 150;
                var damaged = PartStatePolicy.Evaluate(Read("damaged Furycane leg unavailable"));
                if (!damaged.Visible || damaged.Broken || damaged.Fraction != .375 || PercentageText.Percentage(damaged) != 37) throw new Exception("Furycane fractional percentage");
                *(int*)record = 0;
                var zero = PartStatePolicy.Evaluate(Read("zero Furycane leg unavailable"));
                if (!zero.Visible || zero.Broken || zero.Fraction != 0) throw new Exception("zero alone became Furycane destruction");
                *(byte*)(record + 8) = 1;
                var broken = PartStatePolicy.Evaluate(Read("broken Furycane leg unavailable"));
                if (!broken.Visible || !broken.Broken || PercentageText.Percentage(broken) != null) throw new Exception("current armor flag did not drive Furycane destruction");
                *(int*)record = 400; *(byte*)(record + 8) = 0;
                var restored = PartStatePolicy.Evaluate(Read("regenerated Furycane leg unavailable"));
                if (!restored.Visible || restored.Broken || PercentageText.Percentage(restored) != 100) throw new Exception("same-instance Furycane regeneration stayed broken");
            }
            for (int i = 2; i <= 3; i++)
            {
                *(int*)(player + 0x118) = ids[i];
                nint record = actor + 0x25e8 + i * 20, enabled = entries + i * 48 + 0x29;
                Reject("disabled temporary Furycane arm accepted");
                *(byte*)enabled = 1;
                var full = PartStatePolicy.Evaluate(Read("enabled temporary Furycane arm unavailable"));
                if (!full.Visible || full.Broken || PercentageText.Percentage(full) != 100) throw new Exception("fresh temporary arm percentage");
                *(int*)record = 350;
                var damaged = PartStatePolicy.Evaluate(Read("damaged temporary Furycane arm unavailable"));
                if (!damaged.Visible || damaged.Broken || PercentageText.Percentage(damaged) != 58) throw new Exception("temporary arm percentage after natural damage");
                *(byte*)enabled = 0; Reject("disabled damaged arm retained"); *(byte*)enabled = 1;
                *(int*)record = 0;
                var zero = PartStatePolicy.Evaluate(Read("zero temporary Furycane arm unavailable"));
                if (zero.Broken || PercentageText.Percentage(zero) != 0) throw new Exception("temporary arm zero alone implied destruction");
                *(byte*)(record + 8) = 1;
                var broken = PartStatePolicy.Evaluate(Read("broken temporary Furycane arm unavailable"));
                if (!broken.Visible || !broken.Broken || PercentageText.Percentage(broken) != null) throw new Exception("temporary arm processed break not shown");
                *(byte*)enabled = 0; Reject("disabled broken arm retained");
                *(int*)record = 600; *(byte*)(record + 8) = 0;
                Reject("regenerated but unavailable arm shown"); *(byte*)enabled = 1;
                if (PercentageText.Percentage(PartStatePolicy.Evaluate(Read("regenerated temporary arm unavailable"))) != 100) throw new Exception("temporary arm reset kept stale state");
                *(byte*)enabled = 2; Reject("invalid temporary arm eligibility accepted"); *(byte*)enabled = 1;
                *(int*)(actor + 0x25bc + i * 4) = 4; *(int*)(actor + 0x25bc + 4 * 4) = i;
                Reject("arm selection interpreted as a leg record");
                *(int*)(actor + 0x25bc + i * 4) = i; *(int*)(actor + 0x25bc + 4 * 4) = 4;
                *(byte*)enabled = 0;
            }
            *(int*)(player + 0x118) = 5;
            nint headRecord = actor + 0x25e8 + 8 * 20, headEnabled = entries + 8 * 48 + 0x29;
            Reject("disabled Furycane head accepted");
            *(byte*)headEnabled = 1;
            var headFull = PartStatePolicy.Evaluate(Read("enabled Furycane head unavailable"));
            if (!headFull.Visible || headFull.Broken || PercentageText.Percentage(headFull) != 100)
                throw new Exception("fresh Furycane head percentage");
            *(int*)headRecord = 250;
            if (PercentageText.Percentage(PartStatePolicy.Evaluate(Read("damaged Furycane head unavailable"))) != 50)
                throw new Exception("Furycane head read another armor record");
            *(byte*)headEnabled = 0; Reject("disabled damaged head retained"); *(byte*)headEnabled = 1;
            *(int*)headRecord = 0;
            var headZero = PartStatePolicy.Evaluate(Read("zero Furycane head unavailable"));
            if (headZero.Broken || PercentageText.Percentage(headZero) != 0)
                throw new Exception("head zero alone implied destruction");
            *(byte*)(headRecord + 8) = 1;
            var headBroken = PartStatePolicy.Evaluate(Read("broken Furycane head unavailable"));
            if (!headBroken.Visible || !headBroken.Broken || PercentageText.Percentage(headBroken) != null)
                throw new Exception("Furycane head processed break not shown");
            *(byte*)headEnabled = 0; Reject("disabled broken head retained");
            *(int*)headRecord = 750; *(int*)(headRecord + 4) = 750; *(byte*)(headRecord + 8) = 0;
            Reject("regenerated but unavailable head shown"); *(byte*)headEnabled = 1;
            if (PercentageText.Percentage(PartStatePolicy.Evaluate(Read("regenerated Furycane head unavailable"))) != 100)
                throw new Exception("Furycane head reset kept stale state");
            *(int*)headRecord = 375;
            if (PercentageText.Percentage(PartStatePolicy.Evaluate(Read("changed head initial unavailable"))) != 50)
                throw new Exception("Furycane head denominator was hardcoded");
            *(byte*)headEnabled = 2; Reject("invalid head eligibility accepted"); *(byte*)headEnabled = 1;
            *(int*)(actor + 0x25bc + 8 * 4) = 4; *(int*)(actor + 0x25bc + 4 * 4) = 8;
            Reject("head selection interpreted as a leg record");
            *(int*)(actor + 0x25bc + 8 * 4) = 8; *(int*)(actor + 0x25bc + 4 * 4) = 4;
            *(int*)(entries + 10 * 48) = 5; Reject("ambiguous Furycane head accepted"); *(int*)(entries + 10 * 48) = 400;
            *(int*)(actor + 0x25bc + 2 * 4) = 8; Reject("arm aliases the selected head"); *(int*)(actor + 0x25bc + 2 * 4) = 2;
            *(byte*)(headRecord + 8) = 2; Reject("invalid head break flag accepted"); *(byte*)(headRecord + 8) = 0;
            *(int*)headRecord = 751; Reject("head current above initial accepted");
            *(int*)headRecord = 500; *(int*)(headRecord + 4) = 500; *(byte*)headEnabled = 0;
            *(int*)(player + 0x118) = 130;
            foreach (var (current, expected) in new[] { (116, 29), (228, 57), (1, 1), (399, 99), (0, 0), (400, 100) })
            {
                *(int*)(actor + 0x25e8 + 5 * 20) = current;
                if (PercentageText.Percentage(PartStatePolicy.Evaluate(Read("Furycane percentage boundary unavailable"))) != expected)
                    throw new Exception("exact Furycane integer percentage rounded below its boundary");
            }
            *(ulong*)(stamps + 24) = 201; Reject("stale Furycane target stamp accepted"); *(ulong*)(stamps + 24) = 200;
            *(nint*)(scenes + 24) = localScene; Reject("replaced Furycane target slot accepted"); *(nint*)(scenes + 24) = target;
            *(nint*)(target + 0x40) = localScene; Reject("mismatched Furycane target self accepted"); *(nint*)(target + 0x40) = target;
            *(byte*)(actor + 0x25f0 + 5 * 20) = 2; Reject("invalid Furycane state byte accepted"); *(byte*)(actor + 0x25f0 + 5 * 20) = 0;
            *(int*)(actor + 0x25ec + 5 * 20) = 0; Reject("missing Furycane initial fabricated"); *(int*)(actor + 0x25ec + 5 * 20) = 400;
            *(int*)(actor + 0x25e8 + 5 * 20) = 401; Reject("Furycane current above initial accepted"); *(int*)(actor + 0x25e8 + 5 * 20) = 400;
            *(int*)(entries + 10 * 48) = 130; Reject("ambiguous Furycane lock ID accepted"); *(int*)(entries + 10 * 48) = 400;
            *(int*)(actor + 0x25bc + 4 * 4) = 5; Reject("two armor records mapped to one lock accepted"); *(int*)(actor + 0x25bc + 4 * 4) = 4;
            *(int*)(actor + 0x25bc + 1 * 4) = 5; Reject("non-leg armor aliases the selected leg"); *(int*)(actor + 0x25bc + 1 * 4) = 1;
            *(int*)(actor + 0x25bc + 8 * 4) = 5; Reject("other-phase armor aliases the selected leg"); *(int*)(actor + 0x25bc + 8 * 4) = 8;
            *(int*)(actor + 0x25bc) = -1; Read("unconnected non-leg must not hide a verified leg"); *(int*)(actor + 0x25bc) = 0;
            *(int*)(actor + 0x25bc + 5 * 4) = 11; Reject("out-of-range Furycane mapping accepted"); *(int*)(actor + 0x25bc + 5 * 4) = 5;
            *(nint*)(actor + 0x790) = entries + 513 * 48; Reject("oversized Furycane vector accepted"); *(nint*)(actor + 0x790) = entries + 11 * 48;
            foreach (int id in new[] { 0, 7, 300 }) { *(int*)(player + 0x118) = id; Reject("unverified Furycane body/phase selected"); }
            *(int*)(player + 0x118) = 130;
            foreach (int unknown in new[] { vtableRva + 8, 0x6175aa8, 0x6177788 })
            { *(nint*)actor = module + unknown; Reject("unverified Furycane variant accepted"); }
            Console.WriteLine($"PASS: real-memory Em{type:x4} four legs and temporary arms, eligibility/break/regeneration, stored initial and stale/ambiguous target rejection");
        }
        finally
        {
            foreach (nint p in allocations) NativeMemory.Free((void*)p);
            VirtualFree(module, 0, 0x8000);
        }
    }
}
