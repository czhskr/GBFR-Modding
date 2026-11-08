using System.Runtime.InteropServices;
using PartIndicator;

static unsafe class VulkanStateTest
{
    [DllImport("kernel32")] private static extern nint VirtualAlloc(nint address, nuint size, uint kind, uint protection);
    [DllImport("kernel32")] private static extern bool VirtualFree(nint address, nuint size, uint kind);
    public static void Run()
    {
        var allocations = new List<nint>();
        nint Alloc(int size) { nint p = (nint)NativeMemory.AllocZeroed((nuint)size); allocations.Add(p); return p; }
        nint module = VirtualAlloc(0, 0x8000000, 0x2000, 4);
        if (module == 0) throw new Exception("Vulkan fixture reservation");
        try
        {
            foreach (int page in new[] { 0x7362000, 0x7035000, 0x7020000, 0x7c53000 })
                if (VirtualAlloc(module + page, 0x1000, 0x1000, 4) == 0) throw new Exception("Vulkan fixture commit");
            nint vector = Alloc(8), player = Alloc(0x240), a = Alloc(8), b = Alloc(0x30), c = Alloc(0x20),
                localScene = Alloc(0x80), localActor = Alloc(8), target = Alloc(0x80), actor = Alloc(0x3000),
                manager = Alloc(0x60), scenes = Alloc(64), stamps = Alloc(64), session = Alloc(8), entries = Alloc(5 * 48);
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
            nint parameter = Alloc(0xc20), definitions = Alloc(2 * 0xd0), handles = Alloc(2 * 24);
            nint[] childScenes = [Alloc(0x80), Alloc(0x80)], parts = [Alloc(0x600), Alloc(0x600)];
            *(nint*)actor = module + 0x5b8aed8; *(uint*)(actor + 0x160) = 24342799;
            *(nint*)(actor + 0x1598) = parameter; *(nint*)(parameter + 0xc00) = definitions; *(nint*)(parameter + 0xc08) = definitions + 2 * 0xd0;
            *(nint*)(actor + 0x788) = entries; *(nint*)(actor + 0x790) = entries + 5 * 48;
            *(nint*)(actor + 0x22c8) = handles; *(nint*)(actor + 0x22d0) = handles + 2 * 24;
            int[] ids = [9, 13, 2050, 848, 0];
            for (int i = 0; i < ids.Length; i++) *(int*)(entries + i * 48) = ids[i];
            for (int i = 0; i < 2; i++)
            {
                uint id = (uint)(5 + i); ulong stamp = (ulong)(300 + i);
                *(uint*)(handles + i * 24) = id; *(nint*)(handles + i * 24 + 8) = childScenes[i]; *(ulong*)(handles + i * 24 + 16) = stamp;
                *(nint*)(scenes + (id - 1) * 8) = childScenes[i]; *(ulong*)(stamps + (id - 1) * 8) = stamp;
                *(uint*)(childScenes[i] + 0x38) = id; *(nint*)(childScenes[i] + 0x40) = childScenes[i]; *(ulong*)(childScenes[i] + 0x48) = stamp;
                *(nint*)(childScenes[i] + 0x70) = parts[i]; *(nint*)parts[i] = module + 0x5d825e0;
                *(uint*)(parts[i] + 0x368) = 4; *(nint*)(parts[i] + 0x370) = target; *(ulong*)(parts[i] + 0x378) = 200;
                *(int*)(parts[i] + 0x5e0) = i; *(float*)(parts[i] + 0x5c0) = 250; *(float*)(parts[i] + 0x5c4) = 250;
                nint definition = definitions + i * 0xd0;
                *(int*)(definition + 0x60) = i + 1; // Coordinate override index is deliberately not the damage/selection identity.
                string model = i == 0 ? "c10_arm" : "c00_arm";
                for (int c0 = 0; c0 < model.Length; c0++) *(byte*)(definition + 0x90 + c0) = (byte)model[c0];
                *(ulong*)(definition + 0xa0) = 7; *(ulong*)(definition + 0xa8) = 15;
            }
            var reader = new NativeStateReader(module);
            if (!reader.TryReadWorld(out var world)) throw new Exception("Vulkan owner fixture");
            PartDisplay Read(string message)
            {
                if (!reader.TryReadSnapshot(world, 1, out var s) || s is null) throw new Exception(message);
                return PartStatePolicy.Evaluate(s);
            }
            void Reject(string message) { if (reader.TryReadSnapshot(world, 1, out _)) throw new Exception(message); }
            for (int i = 0; i < 2; i++)
            {
                *(int*)(player + 0x118) = ids[i];
                if (PercentageText.Percentage(Read("verified Vulkan arm unavailable")) != 100) throw new Exception("Vulkan full stored initial");
                *(float*)(parts[i] + 0x5c4) = .024993896484375f;
                var residual = Read("Vulkan float residual unavailable");
                if (residual.Broken || PercentageText.Percentage(residual) != 1) throw new Exception("positive Vulkan residual must remain intact1%");
                *(float*)(parts[i] + 0x5c4) = 0;
                var broken = Read("Vulkan broken arm unavailable");
                if (!broken.Broken || PercentageText.Percentage(broken) != null) throw new Exception("Vulkan native zero-state destruction");
                *(float*)(parts[i] + 0x5c4) = 250;
                var restored = Read("Vulkan restored arm unavailable");
                if (restored.Broken || PercentageText.Percentage(restored) != 100) throw new Exception("Vulkan same-instance regeneration stayed broken");
            }
            *(int*)(player + 0x118) = 9;
            foreach (var (current, initial, percent) in new[] { (72.5f, 250f, 29), (1.5f, 10f, 15), (.6f, 1.2f, 50), (249.99998f, 250f, 99), (.9f, 3f, 29), (float.Epsilon, 250f, 1) })
            {
                *(float*)(parts[0] + 0x5c4) = current; *(float*)(parts[0] + 0x5c0) = initial;
                if (PercentageText.Percentage(Read("Vulkan fractional percentage unavailable")) != percent) throw new Exception("float current was truncated or rounded across percentage boundary");
            }
            *(float*)(parts[0] + 0x5c0) = 250; *(float*)(parts[0] + 0x5c4) = 125;
            *(float*)(parts[1] + 0x5c4) = 200;
            void Handle(int slot0, int part)
            { *(uint*)(handles + slot0 * 24) = (uint)(5 + part); *(nint*)(handles + slot0 * 24 + 8) = childScenes[part]; *(ulong*)(handles + slot0 * 24 + 16) = (ulong)(300 + part); }
            Handle(0, 1); Handle(1, 0);
            if (PercentageText.Percentage(Read("reordered right-arm definition unavailable")) != 50) throw new Exception("Vulkan used vector order instead of definition identity");
            *(int*)(player + 0x118) = 13;
            if (PercentageText.Percentage(Read("reordered left-arm definition unavailable")) != 80) throw new Exception("Vulkan arms swapped");
            Handle(0, 0); Handle(1, 1); *(int*)(player + 0x118) = 9;
            *(ulong*)(stamps + 32) = 999; Reject("stale Vulkan child accepted"); *(ulong*)(stamps + 32) = 300;
            *(ulong*)(parts[0] + 0x378) = 999; Reject("Vulkan child from other parent generation accepted"); *(ulong*)(parts[0] + 0x378) = 200;
            *(int*)(parts[0] + 0x5e0) = 1; Reject("ambiguous Vulkan definition accepted"); *(int*)(parts[0] + 0x5e0) = 0;
            *(int*)(parts[0] + 0x5e0) = -1; Reject("negative Vulkan definition accepted"); *(int*)(parts[0] + 0x5e0) = 0;
            *(byte*)(definitions + 0x90) = (byte)'x'; Reject("unverified arm definition model accepted"); *(byte*)(definitions + 0x90) = (byte)'c';
            *(nint*)(parameter + 0xc08) = definitions + 3 * 0xd0; Reject("unverified Vulkan definition topology accepted"); *(nint*)(parameter + 0xc08) = definitions + 2 * 0xd0;
            *(nint*)(actor + 0x22d0) = handles + 65 * 24; Reject("oversized Vulkan children accepted"); *(nint*)(actor + 0x22d0) = handles + 2 * 24;
            *(int*)(entries + 4 * 48) = 9; Reject("duplicate Vulkan lock ID accepted"); *(int*)(entries + 4 * 48) = 0;
            foreach (float bad in new[] { -1f, 251f, float.NaN, float.PositiveInfinity })
            { *(float*)(parts[0] + 0x5c4) = bad; Reject("invalid Vulkan durability accepted"); }
            *(float*)(parts[0] + 0x5c4) = 250; *(float*)(parts[0] + 0x5c0) = 0; Reject("Vulkan initial fabricated"); *(float*)(parts[0] + 0x5c0) = 250;
            *(nint*)parts[0] = module + 0x5d825e8; Reject("unverified Vulkan child type accepted"); *(nint*)parts[0] = module + 0x5d825e0;
            *(ulong*)(stamps + 24) = 999; Reject("stale Vulkan root accepted"); *(ulong*)(stamps + 24) = 200;
            foreach (int id in new[] { 2050, 848, 0 }) { *(int*)(player + 0x118) = id; Reject("unsupported Vulkan selection shown"); }
            *(nint*)actor = module + 0x5be1e40;
            *(uint*)(actor + 0x1a80) = 4; *(nint*)(actor + 0x1a88) = target; *(ulong*)(actor + 0x1a90) = 200;
            *(nint*)(actor + 0x1a98) = handles; *(nint*)(actor + 0x1aa0) = handles + 24;
            *(nint*)(actor + 0x790) = entries + 48;
            *(ushort*)(definitions + 0x68) = 0x7100;
            foreach (var (vectorOffset, selected, bone, model) in new[] {
                (0x6b0, 3840, 22, "b00_scale"), (0x6c8, 282, 282, "b00_tail") })
            {
                *(nint*)(actor + 0x1ad0) = parameter + vectorOffset;
                *(nint*)(parameter + vectorOffset) = definitions; *(nint*)(parameter + vectorOffset + 8) = definitions + 0xd0;
                *(int*)entries = selected; *(int*)(player + 0x118) = selected;
                *(short*)(definitions + 0x50) = (short)bone;
                for (int j = 0; j < 16; j++) *(byte*)(definitions + 0x90 + j) = j < model.Length ? (byte)model[j] : (byte)0;
                *(ulong*)(definitions + 0xa0) = (ulong)model.Length;
                foreach (byte inactive in new byte[] { 0, 2 })
                { *(byte*)(actor + 0x1b0c) = inactive; Reject("inactive Vulkan body/tail acquired a display"); }
                *(byte*)(actor + 0x1b0c) = 1;
                *(float*)(parts[0] + 0x5c4) = 250;
                if (PercentageText.Percentage(Read("Vulkan body/tail stored initial unavailable")) != 100) throw new Exception("Vulkan body/tail initial");
                *(float*)(parts[0] + 0x5c4) = 125;
                if (PercentageText.Percentage(Read("Vulkan body/tail damage unavailable")) != 50) throw new Exception("Vulkan body/tail damage");
                *(float*)(parts[0] + 0x5c4) = 0;
                if (!Read("Vulkan body/tail break unavailable").Broken) throw new Exception("Vulkan body/tail break");
                *(float*)(parts[0] + 0x5c4) = 250;
                if (Read("Vulkan body/tail restore unavailable").Broken) throw new Exception("Vulkan body/tail restore");
                *(byte*)(actor + 0x1b0c) = 0; Reject("Vulkan deactivation retained the shell display");
                *(float*)(parts[0] + 0x5c0) = 350; *(float*)(parts[0] + 0x5c4) = 350;
                *(byte*)(actor + 0x1b0c) = 1;
                if (PercentageText.Percentage(Read("Vulkan activation reset unavailable")) != 100)
                    throw new Exception("Vulkan activation retained inactive250 as its maximum");
                *(float*)(parts[0] + 0x5c4) = 100.02499389648438f;
                if (PercentageText.Percentage(Read("Vulkan active shell damage unavailable")) != 28)
                    throw new Exception("Vulkan active shell percentage did not use its own350 maximum");
                *(float*)(parts[0] + 0x5c0) = 250; *(float*)(parts[0] + 0x5c4) = 250;
                *(int*)(player + 0x118) = selected == 282 ? 3840 : 282; Reject("inactive Vulkan form lock accepted");
                *(int*)(player + 0x118) = selected;
                *(nint*)(actor + 0x1ad0) = parameter + 0x6e0; Reject("unknown Vulkan definition owner accepted"); *(nint*)(actor + 0x1ad0) = parameter + vectorOffset;
                *(nint*)(actor + 0x1aa0) = handles + 48; Reject("unverified Vulkan child count accepted"); *(nint*)(actor + 0x1aa0) = handles + 24;
                *(ulong*)(actor + 0x1a90) = 999; Reject("stale Vulkan container owner accepted"); *(ulong*)(actor + 0x1a90) = 200;
                *(ushort*)(definitions + 0x68) = 0x7103; Reject("different shell type accepted"); *(ushort*)(definitions + 0x68) = 0x7100;
                *(short*)(definitions + 0x50) = -1; Reject("unknown shell attachment bone accepted"); *(short*)(definitions + 0x50) = (short)bone;
                *(byte*)(definitions + 0x90) = (byte)'x'; Reject("different shell model accepted"); *(byte*)(definitions + 0x90) = (byte)'b';
                *(nint*)parts[0] = module + 0x5d825e8; Reject("unverified body/tail child type accepted"); *(nint*)parts[0] = module + 0x5d825e0;
            }
            Console.WriteLine("PASS: actual-memory Vulkan arm definitions, float residual/percentage, break/regeneration and stale/ambiguous mapping rejection");
        }
        finally
        {
            foreach (var p in allocations) NativeMemory.Free((void*)p);
            VirtualFree(module, 0, 0x8000);
        }
    }
}
