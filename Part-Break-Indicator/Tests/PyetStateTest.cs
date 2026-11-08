using System.Runtime.InteropServices;
using PartIndicator;

static unsafe class PyetStateTest
{
    [DllImport("kernel32")] private static extern nint VirtualAlloc(nint address, nuint size, uint kind, uint protection);
    [DllImport("kernel32")] private static extern bool VirtualFree(nint address, nuint size, uint kind);
    public static void Run()
    {
        var allocations = new List<nint>();
        nint Alloc(int size) { nint p = (nint)NativeMemory.AllocZeroed((nuint)size); allocations.Add(p); return p; }
        nint module = VirtualAlloc(0, 0x8000000, 0x2000, 4);
        if (module == 0) throw new Exception("Pyet fixture reservation");
        try
        {
            foreach (int page in new[] { 0x7362000, 0x7035000, 0x7020000, 0x7c53000 })
                if (VirtualAlloc(module + page, 0x1000, 0x1000, 4) == 0) throw new Exception("Pyet fixture commit");
            nint vector = Alloc(8), player = Alloc(0x240), a = Alloc(8), b = Alloc(0x30), c = Alloc(0x20),
                localScene = Alloc(0x80), localActor = Alloc(8), target = Alloc(0x80), actor = Alloc(0x4000),
                parameter = Alloc(0x1600), history = Alloc(32), entries = Alloc(7 * 48),
                manager = Alloc(0x60), scenes = Alloc(64), stamps = Alloc(64), session = Alloc(8);
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
            *(nint*)actor = module + 0x5b09148; *(uint*)(actor + 0x160) = 166029911;
            *(nint*)(actor + 0x1598) = parameter; *(nint*)(actor + 0xcd8) = history; *(nint*)(actor + 0xce0) = history;
            *(nint*)(actor + 0x788) = entries; *(nint*)(actor + 0x790) = entries + 7 * 48;
            int[] ids = [772, 2602, 2740, 784, 800, 934, 901];
            int[] initial = [5000, 3300, 3300, 6000, 6000, 3000, 3300];
            int[] offsets = [0x20, 0x10, 0x14, 0x18, 0x1c, 0xc, 8];
            for (int i = 0; i < 7; i++)
            {
                *(int*)(entries + i * 48) = ids[i];
                *(int*)(actor + 0x25b4 + 4 * i) = initial[i]; *(int*)(actor + 0x25d0 + 4 * i) = initial[i];
                *(float*)(parameter + 0xf60 + offsets[i]) = initial[i];
            }
            var reader = new NativeStateReader(module);
            if (!reader.TryReadWorld(out var world)) throw new Exception("Pyet local fixture");
            PartDisplay Read(string message)
            {
                if (!reader.TryReadSnapshot(world, 1, out var snapshot) || snapshot is null) throw new Exception(message);
                return PartStatePolicy.Evaluate(snapshot);
            }
            void Reject(string message) { if (reader.TryReadSnapshot(world, 1, out _)) throw new Exception(message); }
            *(int*)(player + 0x118) = 772;
            if (PercentageText.Percentage(Read("verified Pyet head unavailable")) != 100) throw new Exception("Pyet head initial");
            *(int*)(actor + 0x25cc) = 3050;
            if (PercentageText.Percentage(Read("damaged Pyet head unavailable")) != 92) throw new Exception("Pyet captured head damage");
            *(int*)(actor + 0x25b8) = 2800; *(int*)(player + 0x118) = 800;
            if (PercentageText.Percentage(Read("damaged Pyet right arm unavailable")) != 84) throw new Exception("Pyet arm incorrectly uses lock-list index");
            *(int*)(player + 0x118) = 772;
            if (PercentageText.Percentage(Read("Pyet head return unavailable")) != 92) throw new Exception("Pyet head return lost stored maximum");
            *(int*)(actor + 0x25cc) = 50;
            if (Read("positive Pyet residual unavailable").Broken || PercentageText.Percentage(Read("Pyet residual percentage")) != 1) throw new Exception("positive Pyet HP became destruction");
            *(int*)(actor + 0x25cc) = 0;
            Reject("zero without processed Pyet break published");
            *(int*)history = 6; *(nint*)(actor + 0xce0) = history + 4;
            var broken = Read("confirmed Pyet head break unavailable");
            if (!broken.Broken || PercentageText.Percentage(broken) != null) throw new Exception("Pyet native history6 break");
            *(int*)(player + 0x118) = 800; *(int*)(actor + 0x25b8) = 0;
            Reject("Pyet head history used for right-arm break");
            *(int*)history = 1;
            if (!Read("Pyet right-arm history1 break unavailable").Broken) throw new Exception("Pyet right-arm history index");
            *(int*)(actor + 0x25b8) = 2800; *(int*)(player + 0x118) = 772; *(int*)history = 6;
            *(int*)(actor + 0x25cc) = 3050;
            if (Read("positive Pyet current with old history unavailable").Broken) throw new Exception("old reward history treated as current Pyet destruction");
            *(nint*)(actor + 0xce0) = history;
            foreach (byte flags in new byte[] { 0x40, 0 })
            {
                *(byte*)(actor + 0xe) = flags;
                if (PercentageText.Percentage(Read("Pyet flags unavailable")) != (flags == 0 ? 92 : null)) throw new Exception("unvalidated Pyet alternate percentage");
            }
            *(int*)(actor + 0x25e8) = 3200;
            if (Read("Pyet max mismatch should keep icon").Fraction != null) throw new Exception("Pyet denominator not source-verified");
            *(int*)(actor + 0x25e8) = 3300; *(float*)(parameter + 0xf68) = float.NaN;
            if (Read("Pyet invalid source should keep icon").Fraction != null) throw new Exception("Pyet NaN source denominator");
            *(float*)(parameter + 0xf68) = 3300;
            *(int*)(actor + 0x25b4) = 5001;
            if (Read("Pyet inconsistent unselected state should keep icon").Fraction != null) throw new Exception("Pyet other current exceeds stored source");
            *(int*)(actor + 0x25b4) = 5000;
            *(int*)(player + 0x118) = 784;
            if (PercentageText.Percentage(Read("Pyet exact-bone left arm unavailable")) != 100) throw new Exception("Pyet left arm initial");
            *(int*)(actor + 0x25bc) = 1650;
            if (PercentageText.Percentage(Read("Pyet left arm damage unavailable")) != 50) throw new Exception("Pyet left arm used selection-list index or damage UID");
            *(int*)(actor + 0x25bc) = 0; Reject("Pyet left arm zero without processed break accepted");
            *(int*)history = 2; *(nint*)(actor + 0xce0) = history + 4;
            if (!Read("Pyet left arm processed break unavailable").Broken) throw new Exception("Pyet left arm history index");
            *(int*)(actor + 0x25bc) = 3300; *(nint*)(actor + 0xce0) = history;
            *(int*)(player + 0x118) = 772;
            if (PercentageText.Percentage(Read("Pyet other-part return unavailable")) != 92) throw new Exception("Pyet left arm changed head state");
            foreach (var (selected, index, expectedPercent) in new[] { (2602, 3, 95), (2740, 4, 95), (934, 5, 91), (901, 0, 95) })
            {
                *(int*)(player + 0x118) = selected;
                if (PercentageText.Percentage(Read("Pyet remaining correlated part unavailable")) != 100) throw new Exception("Pyet remaining part initial");
                *(int*)(actor + 0x25b4 + index * 4) = initial[index] - 250;
                if (PercentageText.Percentage(Read("Pyet remaining part damage unavailable")) != expectedPercent) throw new Exception("Pyet lock/state order mismatch");
                *(int*)(actor + 0x25b4 + index * 4) = 0; *(int*)history = 6; *(nint*)(actor + 0xce0) = history + 4;
                Reject("another part's history published a Pyet break");
                *(int*)history = index;
                var partBroken = Read("Pyet remaining processed part break unavailable");
                if (!partBroken.Broken || PercentageText.Percentage(partBroken) != null) throw new Exception("Pyet remaining part history mismatch");
                *(int*)(actor + 0x25b4 + index * 4) = initial[index];
                if (Read("Pyet positive current with old part history unavailable").Broken) throw new Exception("Pyet old history overrode current state");
                *(nint*)(actor + 0xce0) = history;
            }
            *(int*)(player + 0x118) = 772;
            *(ulong*)(stamps + 24) = 201; Reject("stale Pyet target accepted"); *(ulong*)(stamps + 24) = 200;
            *(int*)(entries + 48) = 772; Reject("duplicate Pyet selection accepted"); *(int*)(entries + 48) = 2602;
            *(int*)history = 7; *(nint*)(actor + 0xce0) = history + 4; Reject("out-of-range Pyet history accepted");
            *(int*)history = 6; *(int*)(history + 4) = 6; *(nint*)(actor + 0xce0) = history + 8; Reject("duplicate Pyet history accepted");
            *(nint*)(actor + 0xce0) = history;
            *(int*)(actor + 0x25cc) = -1; Reject("negative Pyet current accepted"); *(int*)(actor + 0x25cc) = 3050;
            *(nint*)(actor + 0x790) = entries + 49; Reject("misaligned Pyet lock vector accepted"); *(nint*)(actor + 0x790) = entries + 7 * 48;
            foreach (int id in new[] { 0, 3, 5, 785, -1 })
            { *(int*)(player + 0x118) = id; Reject("unverified Pyet mapping published"); }
            *(int*)(player + 0x118) = 772; *(nint*)actor = module + 0x5b09150; Reject("unverified Pyet class accepted");
            Console.WriteLine("PASS: real-memory Pyet seven-part routing, stored/source initial, positive residual and independent processed breaks; unsupported mappings and invalid state rejected");
        }
        finally
        {
            foreach (nint p in allocations) NativeMemory.Free((void*)p);
            VirtualFree(module, 0, 0x8000);
        }
    }
}
