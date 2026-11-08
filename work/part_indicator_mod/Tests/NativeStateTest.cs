using System.Runtime.InteropServices;
using PartIndicator;

static unsafe class NativeStateTest
{
    [DllImport("kernel32", SetLastError = true)] private static extern nint VirtualAlloc(nint address, nuint size, uint kind, uint protection);
    [DllImport("kernel32")] private static extern bool VirtualFree(nint address, nuint size, uint kind);
    public static void Run()
    {
        var allocations = new List<nint>();
        nint Alloc(int size) { nint p = (nint)NativeMemory.AllocZeroed((nuint)size); allocations.Add(p); return p; }
        nint module = VirtualAlloc(0, 0x8000000, 0x2000, 4);
        if (module == 0) throw new Exception("fixture address reservation");
        try
        {
            foreach (int page in new[] { 0x7362000, 0x7035000, 0x7020000, 0x7c53000 })
                if (VirtualAlloc(module + page, 0x1000, 0x1000, 4) == 0) throw new Exception("fixture page commit");
            nint vector = Alloc(128), player = Alloc(0x240), a = Alloc(8), b = Alloc(0x30), c = Alloc(0x20), worldScene = Alloc(0x80),
                worldActor = Alloc(8), target = Alloc(0x80), actor = Alloc(0x3000), other = Alloc(0x3000), parameter = Alloc(0x900);
            *(nint*)(module + 0x7362690) = vector; *(nint*)(module + 0x7362698) = vector + 8;
            *(nint*)vector = player; *(nint*)(player + 8) = a; *(nint*)a = b; *(nint*)(b + 0x28) = c;
            *(nint*)(c + 0x18) = worldScene; *(nint*)(worldScene + 0x70) = worldActor;
            *(nint*)(player + 0x230) = worldActor;
            nint sceneManager = Alloc(0x60), scenes = Alloc(64), stamps = Alloc(64);
            *(nint*)(module + 0x7020988) = sceneManager; *(nint*)(sceneManager + 0x20) = scenes; *(nint*)(sceneManager + 0x48) = stamps;
            *(uint*)(module + 0x7035ca0) = 3; *(nint*)(module + 0x7035ca8) = worldScene; *(ulong*)(module + 0x7035cb0) = 100;
            *(nint*)(scenes + 16) = worldScene; *(ulong*)(stamps + 16) = 100;
            *(uint*)(worldScene + 0x38) = 3; *(nint*)(worldScene + 0x40) = worldScene; *(ulong*)(worldScene + 0x48) = 100;
            *(nint*)(module + 0x7035d00) = worldActor;
            nint session = Alloc(8); *(nint*)(module + 0x7c53d10) = session;
            *(nint*)(player + 0x108) = target; *(int*)(player + 0x118) = 9; *(nint*)(target + 0x70) = actor;
            *(uint*)(target + 0x38) = 4; *(nint*)(target + 0x40) = target; *(ulong*)(target + 0x48) = 200;
            *(nint*)(scenes + 24) = target; *(ulong*)(stamps + 24) = 200;
            nint entries = Alloc(4 * 48);
            int[] partIds = [2628, 9, 13], maxima = [1500, 1200, 1200];
            *(nint*)(actor + 0x788) = entries; *(nint*)(actor + 0x790) = entries + 4 * 48;
            for (int i = 0; i < 3; i++)
            {
                *(int*)(entries + i * 48) = partIds[i];
                *(float*)(parameter + 0x8b0 + i * 4) = maxima[i]; *(int*)(actor + 0x2a98 + i * 4) = maxima[i];
            }
            *(int*)(entries + 3 * 48) = 3;
            *(nint*)actor = module + 0x6150b68; *(uint*)(actor + 0x160) = 10000; *(nint*)(actor + 0x1598) = parameter;
            *(float*)(parameter + 0x8b4) = 1200; *(int*)(actor + 0x2a9c) = 1200;
            var reader = new NativeStateReader(module);
            if (!reader.TryReadWorld(out var world) || !reader.TryReadSnapshot(world, 1, out var first) || first?.Initial != 1200)
                throw new Exception("untouched real-memory proof not established");
            *(int*)(actor + 0x2a9c) = 348;
            if (!reader.TryReadSnapshot(world, 1, out var exact) || PercentageText.Percentage(PartStatePolicy.Evaluate(exact)) != 29)
                throw new Exception("verified integer percentage 348/1200 must be 29");
            *(int*)(actor + 0x2a9c) = 950;
            if (!reader.TryReadSnapshot(world, 1, out var damaged) || damaged?.Initial != 1200) throw new Exception("same instance lost proof");
            *(nint*)(player + 0x108) = 0; *(int*)(player + 0x118) = -1;
            if (reader.TryReadSnapshot(world, 1, out _)) throw new Exception("crocodile no-lock gap displayed");
            *(nint*)(player + 0x108) = target; *(int*)(player + 0x118) = 9;
            if (!reader.TryReadSnapshot(world, 1, out var gapBack) || gapBack?.Initial != 1200) throw new Exception("no-lock transition discarded crocodile proof");
            nint otherTarget = Alloc(0x80);
            *(nint*)(otherTarget + 0x70) = other; *(nint*)(player + 0x108) = otherTarget;
            if (reader.TryReadSnapshot(world, 1, out _)) throw new Exception("unrelated unknown target visible");
            *(nint*)(player + 0x108) = target;
            if (!reader.TryReadSnapshot(world, 1, out var monsterBack) || monsterBack?.Initial != 1200) throw new Exception("unrelated monster discarded crocodile proof");
            nint secondCrocodile = Alloc(0x3000), secondParameter = Alloc(0x900);
            NativeMemory.Copy((void*)actor, (void*)secondCrocodile, 0x3000); NativeMemory.Copy((void*)parameter, (void*)secondParameter, 0x900);
            *(nint*)(secondCrocodile + 0x1598) = secondParameter; *(float*)(secondParameter + 0x8b4) = 2400;
            *(int*)(secondCrocodile + 0x2a9c) = 2400; *(nint*)(otherTarget + 0x70) = secondCrocodile;
            *(uint*)(otherTarget + 0x38) = 5; *(nint*)(otherTarget + 0x40) = otherTarget; *(ulong*)(otherTarget + 0x48) = 300;
            *(nint*)(scenes + 32) = otherTarget; *(ulong*)(stamps + 32) = 300; *(nint*)(player + 0x108) = otherTarget;
            if (!reader.TryReadSnapshot(world, 1, out var secondFull) || secondFull?.Initial != 2400) throw new Exception("second crocodile proof not independent");
            *(int*)(secondCrocodile + 0x2a9c) = 600; *(nint*)(player + 0x108) = target;
            if (!reader.TryReadSnapshot(world, 1, out var firstReturn) || firstReturn?.Initial != 1200) throw new Exception("second crocodile discarded first proof");
            *(nint*)(player + 0x108) = otherTarget;
            if (!reader.TryReadSnapshot(world, 1, out var secondReturn) || PercentageText.Percentage(PartStatePolicy.Evaluate(secondReturn)) != 25)
                throw new Exception("second crocodile percentage swapped on return");
            *(nint*)(player + 0x108) = target;
            *(nint*)(target + 0x70) = other;
            if (reader.TryReadSnapshot(world, 1, out _)) throw new Exception("unknown target visible");
            *(nint*)(target + 0x70) = actor;
            if (!reader.TryReadSnapshot(world, 1, out var back) || back?.Initial != null) throw new Exception("proof from abandoned target was reused");
            *(int*)(actor + 0x2a9c) = 1200;
            if (!reader.TryReadSnapshot(world, 2, out var fresh) || fresh?.Initial != 1200) throw new Exception("new generation cannot establish fresh proof");
            *(int*)(actor + 0x2a9c) = 950;
            *(int*)(player + 0x118) = 3;
            if (reader.TryReadSnapshot(world, 2, out _)) throw new Exception("unverified selection visible");
            *(int*)(player + 0x118) = 9;
            if (!reader.TryReadSnapshot(world, 2, out var sameActor) || sameActor?.Initial != 1200) throw new Exception("same actor selection lost verified proof");
            *(nint*)(module + 0x7362698) = vector;
            if (reader.TryReadWorld(out _)) throw new Exception("empty world accepted");
            *(nint*)(module + 0x7362698) = vector + 8;
            if (!reader.TryReadSnapshot(world, 2, out var afterGap) || afterGap?.Initial != null) throw new Exception("proof survived a world gap");
            *(int*)(actor + 0x2a9c) = 1200;
            if (!reader.TryReadSnapshot(world, 3, out _)) throw new Exception("fresh generation setup");
            nint history = Alloc(4); *(int*)history = 1;
            *(nint*)(actor + 0xcd8) = history; *(nint*)(actor + 0xce0) = history + 4; *(int*)(actor + 0x2a9c) = 0;
            if (!reader.TryReadSnapshot(world, 3, out var broken) || !PartStatePolicy.Evaluate(broken).Broken) throw new Exception("natural history destruction");
            *(nint*)(actor + 0xcd8) = 0; *(nint*)(actor + 0xce0) = 0; *(int*)(actor + 0x2a9c) = 600;
            if (!reader.TryReadSnapshot(world, 3, out var reset) || reset?.Initial != null) throw new Exception("proof survived broken history reset");
            *(int*)(actor + 0x2a9c) = 1200; reader.TryReadSnapshot(world, 3, out _);
            *(int*)(actor + 0x2a9c) = 1300; reader.TryReadSnapshot(world, 3, out _);
            *(int*)(actor + 0x2a9c) = 600;
            if (!reader.TryReadSnapshot(world, 3, out var range) || range?.Initial != null) throw new Exception("proof survived invalid current range");
            nint threeHistory = Alloc(12);
            PartSnapshot ReadPart(int selected, long generation = 5)
            {
                *(int*)(player + 0x118) = selected;
                if (!reader.TryReadSnapshot(world, generation, out var value) || value is null) throw new Exception("verified crocodile part unavailable: " + selected);
                return value;
            }
            foreach (int vt in new[] { 0x61528a8, 0x6150b68, 0x6151ee8 })
            {
                *(nint*)actor = module + vt; *(nint*)(actor + 0xcd8) = 0; *(nint*)(actor + 0xce0) = 0;
                for (int i = 0; i < 3; i++) *(int*)(actor + 0x2a98 + i * 4) = maxima[i];
                reader = new NativeStateReader(module);
                if (PercentageText.Percentage(PartStatePolicy.Evaluate(ReadPart(2628))) != 100) throw new Exception("crocodile horn full percentage");
                for (int i = 0; i < 3; i++) *(int*)(actor + 0x2a98 + i * 4) = maxima[i] / 2;
                for (int i = 0; i < 3; i++)
                    if (PercentageText.Percentage(PartStatePolicy.Evaluate(ReadPart(partIds[i]))) != 50) throw new Exception("per-part initial proof lost or swapped");
                *(int*)(player + 0x118) = 3;
                if (reader.TryReadSnapshot(world, 5, out _)) throw new Exception("crocodile body displayed");
                if (ReadPart(2628).Initial != 1500) throw new Exception("unsupported selection discarded same-target proof");
                for (int i = 0; i < 3; i++)
                {
                    *(int*)(actor + 0x2a98 + i * 4) = 0; *(int*)(threeHistory + i * 4) = i;
                    *(nint*)(actor + 0xcd8) = threeHistory; *(nint*)(actor + 0xce0) = threeHistory + (i + 1) * 4;
                    var display = PartStatePolicy.Evaluate(ReadPart(partIds[i]));
                    if (!display.Broken || PercentageText.Percentage(display) != null) throw new Exception("wrong crocodile destruction history index");
                }
                *(nint*)(actor + 0xcd8) = 0; *(nint*)(actor + 0xce0) = 0;
                for (int i = 0; i < 3; i++) *(int*)(actor + 0x2a98 + i * 4) = maxima[i] / 2;
                for (int i = 0; i < 3; i++) if (ReadPart(partIds[i]).Initial != null) throw new Exception("all-part proof survived history reset");
                reader = new NativeStateReader(module);
                for (int i = 0; i < 3; i++) if (ReadPart(partIds[i]).Initial != null) throw new Exception("late attachment invented initial proof");
                for (int i = 0; i < 3; i++) *(int*)(actor + 0x2a98 + i * 4) = maxima[i];
                ReadPart(2628);
                *(ulong*)(stamps + 24) = 201;
                if (reader.TryReadSnapshot(world, 5, out _)) throw new Exception("stale crocodile target stamp accepted");
                *(ulong*)(stamps + 24) = 200; *(int*)(actor + 0x2a98) = 750;
                if (ReadPart(2628).Initial != null) throw new Exception("stale target retained initial proof");
                *(int*)(actor + 0x2a98) = 1500; ReadPart(2628);
                *(int*)(entries + 3 * 48) = 2628;
                if (reader.TryReadSnapshot(world, 5, out _)) throw new Exception("ambiguous crocodile lock selected");
                *(int*)(entries + 3 * 48) = 3;
                *(byte*)(actor + 0xe) = 0x40;
                if (ReadPart(2628).Initial != null) throw new Exception("unverified alternate initial enabled");
                *(byte*)(actor + 0xe) = 0; *(float*)(parameter + 0x8b0) = float.NaN;
                if (ReadPart(2628).Initial != null) throw new Exception("invalid configured initial accepted");
                *(float*)(parameter + 0x8b0) = 1500;
                for (int i = 0; i < 3; i++) *(int*)(actor + 0x2a98 + i * 4) = maxima[i];
                reader = new NativeStateReader(module); *(int*)(player + 0x118) = 3;
                if (reader.TryReadSnapshot(world, 5, out _)) throw new Exception("body baseline unexpectedly displayed");
                *(int*)(actor + 0x2a9c) = 600;
                if (ReadPart(9).Initial != 1200) throw new Exception("body selection failed to collect untouched part proof");
                *(int*)(actor + 0x2a9c) = 1200; reader = new NativeStateReader(module); ReadPart(9);
                *(int*)(player + 0x118) = 3; *(byte*)(actor + 0xe) = 0x40;
                if (reader.TryReadSnapshot(world, 5, out _)) throw new Exception("alternate body displayed");
                *(byte*)(actor + 0xe) = 0; *(int*)(actor + 0x2a9c) = 600;
                if (ReadPart(9).Initial != null) throw new Exception("body selection missed alternate-mode proof invalidation");
                *(int*)(actor + 0x2a9c) = 1200; reader = new NativeStateReader(module); ReadPart(9);
                *(int*)(player + 0x118) = 3; *(int*)(actor + 0x2a9c) = 1300;
                if (reader.TryReadSnapshot(world, 5, out _)) throw new Exception("out-of-range body displayed");
                *(int*)(actor + 0x2a9c) = 600;
                if (ReadPart(9).Initial != null) throw new Exception("body selection missed range proof invalidation");
            }
            *(nint*)actor = module + 0x6150b68; *(int*)(player + 0x118) = 9;
            *(nint*)(actor + 0xcd8) = 0; *(nint*)(actor + 0xce0) = 0;
            for (int i = 0; i < 3; i++) *(int*)(actor + 0x2a98 + i * 4) = maxima[i];
            reader = new NativeStateReader(module);
            Console.WriteLine("PASS: three crocodile types, horn/arms, independent proofs, natural history, late/gap/stale/ambiguous/alternate guards");
            Console.WriteLine("PASS: real ReadProcessMemory snapshots, target switch and world-gap proof invalidation");

            // Removing the solo-only guard must select the controlled scene, even when a companion also has a target.
            nint companion = Alloc(0x240), companionA = Alloc(8), companionB = Alloc(0x30), companionC = Alloc(0x20), companionScene = Alloc(0x80), companionActor = Alloc(8);
            *(nint*)(companion + 8) = companionA; *(nint*)companionA = companionB; *(nint*)(companionB + 0x28) = companionC;
            *(nint*)(companionC + 0x18) = companionScene; *(nint*)(companionScene + 0x70) = companionActor; *(nint*)(companion + 0x230) = companionActor;
            *(nint*)(companion + 0x108) = target; *(int*)(companion + 0x118) = 9;
            *(nint*)vector = companion; *(nint*)(vector + 8) = player; *(nint*)(module + 0x7362698) = vector + 16;
            if (!reader.TryReadWorld(out var partyWorld) || partyWorld.Player != player || partyWorld.Scene != worldScene || partyWorld.Actor != worldActor)
                throw new Exception("AI-party controlled owner was not selected independently of registry order and active targets");
            for (int i = 2; i < 5; i++)
            {
                nint extra = Alloc(0x240); *(nint*)(extra + 0x230) = Alloc(8); *(nint*)(vector + i * 8) = extra;
            }
            *(nint*)(module + 0x7362698) = vector + 40;
            if (!reader.TryReadWorld(out var fiveRegistrations) || fiveRegistrations.Player != player) throw new Exception("registered component count was capped at party size");
            *(int*)(actor + 0x2a9c) = 1200;
            if (!reader.TryReadSnapshot(partyWorld, 4, out var partyFull) || partyFull?.Initial != 1200) throw new Exception("AI-party initial proof unavailable");
            *(int*)(actor + 0x2a9c) = 600;
            if (!reader.TryReadSnapshot(partyWorld, 4, out var partyHalf) || partyHalf?.Initial != 1200) throw new Exception("AI-party proof lost before handle change");
            *(ulong*)(module + 0x7035cb0) = 101; *(ulong*)(stamps + 16) = 101; *(ulong*)(worldScene + 0x48) = 101;
            if (!reader.TryReadWorld(out var reusedWorld) || reusedWorld == partyWorld) throw new Exception("same-address scene reuse was not a new world identity");
            if (!reader.TryReadSnapshot(reusedWorld, 4, out var reused) || reused?.Initial != null) throw new Exception("old initial proof survived scene handle reuse");
            *(ulong*)(stamps + 16) = 102;
            if (reader.TryReadWorld(out _)) throw new Exception("stale engine scene handle accepted");
            *(ulong*)(stamps + 16) = 101;
            *(nint*)(vector + 16) = player; *(nint*)(module + 0x7362698) = vector + 24;
            if (reader.TryReadWorld(out _)) throw new Exception("duplicate controlled owner accepted");
            *(nint*)(module + 0x7362698) = vector + 8;
            if (reader.TryReadWorld(out _)) throw new Exception("missing controlled owner fell back to companion");
            *(nint*)vector = player; *(nint*)(player + 0x230) = companionActor;
            if (reader.TryReadWorld(out _)) throw new Exception("mismatched lock-on actor accepted");
            *(nint*)(player + 0x230) = worldActor;
            *(int*)(session + 4) = 3;
            if (reader.TryReadWorld(out _)) throw new Exception("unvalidated online owner scope accepted");
            *(int*)(session + 4) = 0;
            *(uint*)(module + 0x7035ca0) = 0;
            if (reader.TryReadWorld(out _)) throw new Exception("zero control handle fell back to solo registry");
            Console.WriteLine("PASS: AI-party local selection, duplicate/stale/missing owner rejection and reused-handle proof invalidation");
        }
        finally
        {
            foreach (var p in allocations) NativeMemory.Free((void*)p);
            VirtualFree(module, 0, 0x8000);
        }
    }
}
