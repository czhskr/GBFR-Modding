using System.Runtime.InteropServices;
using PartIndicator;

static unsafe class ProtoStateTest
{
    [DllImport("kernel32")] private static extern nint VirtualAlloc(nint address, nuint size, uint kind, uint protection);
    [DllImport("kernel32")] private static extern bool VirtualFree(nint address, nuint size, uint kind);
    public static void Run()
    {
        var allocations = new List<nint>();
        nint Alloc(int size) { nint p = (nint)NativeMemory.AllocZeroed((nuint)size); allocations.Add(p); return p; }
        nint module = VirtualAlloc(0, 0x8000000, 0x2000, 4);
        if (module == 0) throw new Exception("Proto fixture reservation");
        try
        {
            foreach (int page in new[] { 0x7362000, 0x7035000, 0x7020000, 0x7c53000, 0x7c28000 })
                if (VirtualAlloc(module + page, 0x1000, 0x1000, 4) == 0) throw new Exception("Proto fixture commit");
            nint vector = Alloc(8), player = Alloc(0x240), a = Alloc(8), b = Alloc(0x30), c = Alloc(0x20),
                localScene = Alloc(0x80), localActor = Alloc(8), target = Alloc(0x80), actor = Alloc(0x2000),
                parameter = Alloc(0x6000), locks = Alloc(48), history = Alloc(8), manager = Alloc(0x60),
                scenes = Alloc(64), stamps = Alloc(64), session = Alloc(8), owner = Alloc(8), context = Alloc(0x20),
                treeHead = Alloc(0x30), treeNode = Alloc(0x30), component = Alloc(0x120),
                intHead = Alloc(0x20), counter = Alloc(0x20), other = Alloc(0x20);
            *(nint*)(module + 0x7362690) = vector; *(nint*)(module + 0x7362698) = vector + 8; *(nint*)vector = player;
            *(nint*)(player + 8) = a; *(nint*)a = b; *(nint*)(b + 0x28) = c; *(nint*)(c + 0x18) = localScene;
            *(nint*)(player + 0x230) = localActor; *(nint*)(localScene + 0x70) = localActor;
            *(nint*)(module + 0x7020988) = manager; *(nint*)(manager + 0x20) = scenes; *(nint*)(manager + 0x48) = stamps;
            *(uint*)(module + 0x7035ca0) = 3; *(nint*)(module + 0x7035ca8) = localScene; *(ulong*)(module + 0x7035cb0) = 100;
            *(nint*)(scenes + 16) = localScene; *(ulong*)(stamps + 16) = 100;
            *(uint*)(localScene + 0x38) = 3; *(nint*)(localScene + 0x40) = localScene; *(ulong*)(localScene + 0x48) = 100;
            *(nint*)(module + 0x7035d00) = localActor; *(nint*)(module + 0x7c53d10) = session;
            *(nint*)(player + 0x108) = target; *(int*)(player + 0x118) = 2048;
            *(nint*)(target + 0x70) = actor; *(uint*)(target + 0x38) = 4; *(nint*)(target + 0x40) = target; *(ulong*)(target + 0x48) = 200;
            *(nint*)(scenes + 24) = target; *(ulong*)(stamps + 24) = 200;
            *(nint*)actor = module + 0x5a9d688; *(uint*)(actor + 0x160) = 175025733;
            *(nint*)(actor + 0x1598) = parameter; *(int*)(parameter + 0x5978) = 17000;
            *(nint*)(actor + 0x788) = locks; *(nint*)(actor + 0x790) = locks + 48; *(int*)locks = 2048; *(byte*)(locks + 0x29) = 1;
            *(nint*)(actor + 0xcd8) = history; *(nint*)(actor + 0xce0) = history;
            *(nint*)(actor + 0x648) = owner; *(nint*)owner = actor; *(nint*)(actor + 0x28) = context; *(nint*)(context + 0x18) = target;
            *(int*)(module + 0x7c28684) = int.MinValue + 1; *(uint*)(module + 0x7c28680) = 0;
            *(nint*)(actor + 0xd0) = treeHead; *(nint*)(treeHead + 8) = treeNode; *(byte*)(treeHead + 0x19) = 1;
            *(nint*)treeNode = treeHead; *(nint*)(treeNode + 0x10) = treeHead; *(nint*)(treeNode + 0x28) = component;
            *(nint*)(component + 0x98) = intHead; *(nint*)intHead = other; *(nint*)other = counter; *(nint*)counter = intHead;
            *(uint*)(other + 0x10) = 0x2c14cd48; *(uint*)(counter + 0x10) = 0x76033489; *(int*)(counter + 0x14) = 17000;
            var reader = new NativeStateReader(module);
            if (!reader.TryReadWorld(out var world)) throw new Exception("Proto local fixture");
            PartDisplay Read(string message)
            { if (!reader.TryReadSnapshot(world, 1, out var snapshot) || snapshot is null) throw new Exception(message); return PartStatePolicy.Evaluate(snapshot); }
            void Reject(string message) { if (reader.TryReadSnapshot(world, 1, out _)) throw new Exception(message); }
            if (PercentageText.Percentage(Read("Proto head unavailable")) != 100) throw new Exception("Proto initial17000 or initialized registryID0");
            *(int*)(counter + 0x14) = 16625;
            if (PercentageText.Percentage(Read("Proto natural hit unavailable")) != 97) throw new Exception("Proto captured head damage");
            *(int*)(counter + 0x14) = 1;
            var protectedHead = Read("Proto protected minimum unavailable");
            if (protectedHead.Broken || PercentageText.Percentage(protectedHead) != 1) throw new Exception("Proto floor1 became destruction");
            *(int*)(counter + 0x14) = 0; Reject("Proto reset zero without history published");
            *(int*)history = 0; *(nint*)(actor + 0xce0) = history + 4; *(int*)(other + 0x14) = 1;
            var broken = Read("Proto processed break unavailable");
            if (!broken.Broken || PercentageText.Percentage(broken) != null) throw new Exception("Proto processed history0");
            *(int*)history = 1; Reject("Proto wrong processed index accepted"); *(int*)history = 0;
            *(nint*)(actor + 0xce0) = history + 8; Reject("Proto duplicate/unknown history accepted"); *(nint*)(actor + 0xce0) = history;
            *(int*)(counter + 0x14) = 16625;
            *(int*)(module + 0x7c28684) = 0; Reject("Proto uninitialized metadata accepted");
            *(int*)(module + 0x7c28684) = -1; Reject("Proto initializing metadata accepted"); *(int*)(module + 0x7c28684) = int.MinValue + 1;
            *(uint*)(module + 0x7c28680) = 1; Reject("Proto missing component accepted"); *(uint*)(module + 0x7c28680) = 0;
            *(uint*)(actor + 0xc8) = 1; Reject("Proto locked registry accepted"); *(uint*)(actor + 0xc8) = 0;
            *(nint*)(actor + 0xd0) = 0; *(nint*)(actor + 0xe0) = treeHead;
            if (!Read("Proto alternate registry tree unavailable").Visible) throw new Exception("Proto second component tree");
            *(nint*)(actor + 0xd0) = treeHead; *(nint*)(actor + 0xe0) = 0;
            *(nint*)treeNode = treeNode; Reject("Proto cyclic component tree accepted"); *(nint*)treeNode = treeHead;
            *(nint*)counter = counter; Reject("Proto cyclic integer list accepted"); *(nint*)counter = intHead;
            *(uint*)(other + 0x10) = 0x76033489; Reject("Proto duplicate counter accepted"); *(uint*)(other + 0x10) = 0x2c14cd48;
            *(uint*)(counter + 0x10) = 0x76033488; Reject("Proto missing counter accepted"); *(uint*)(counter + 0x10) = 0x76033489;
            *(nint*)(actor + 0x648) = 0; Reject("Proto missing native owner accepted"); *(nint*)(actor + 0x648) = owner;
            *(nint*)(context + 0x18) = localScene; Reject("Proto wrong native owner scene accepted"); *(nint*)(context + 0x18) = target;
            *(byte*)(actor + 0xe) = 0x40;
            *(int*)(counter + 0x14) = 17000;
            if (PercentageText.Percentage(Read("Proto Chaos++ initial unavailable")) != 100) throw new Exception("Proto verified Chaos++ initial omitted");
            *(int*)(counter + 0x14) = 16700;
            if (PercentageText.Percentage(Read("Proto Chaos++ damage unavailable")) != 98) throw new Exception("Proto captured Chaos++ denominator");
            *(nint*)(player + 0x108) = 0; Reject("Proto Chaos++ no-lock displayed"); *(nint*)(player + 0x108) = target;
            if (PercentageText.Percentage(Read("Proto Chaos++ relock unavailable")) != 98) throw new Exception("Proto Chaos++ relock lost current/source");
            *(int*)(counter + 0x14) = 1;
            if (Read("Proto Chaos++ protected head unavailable") is not { Broken: false } || PercentageText.Percentage(Read("Proto Chaos++ protected value unavailable")) != 1)
                throw new Exception("Proto Chaos++ minimum became destruction");
            *(int*)(counter + 0x14) = 0; Reject("Proto Chaos++ reset zero without history published");
            *(int*)history = 0; *(nint*)(actor + 0xce0) = history + 4;
            if (Read("Proto Chaos++ processed head unavailable") is not { Broken: true, Fraction: null }) throw new Exception("Proto Chaos++ processed head retained percentage");
            *(nint*)(actor + 0xce0) = history; *(int*)(counter + 0x14) = 16700;
            *(int*)(parameter + 0x5978) = 0;
            if (Read("Proto unknown source should retain icon").Fraction != null) throw new Exception("Proto invalid initial percentage"); *(int*)(parameter + 0x5978) = 17000;
            *(int*)(parameter + 0x5978) = 1000000001;
            if (Read("Proto oversized source should retain icon").Fraction != null) throw new Exception("Proto oversized initial percentage"); *(int*)(parameter + 0x5978) = 17000;
            *(int*)(counter + 0x14) = 17001;
            if (Read("Proto current above source should retain icon").Fraction != null) throw new Exception("Proto current above source percentage");
            *(byte*)(actor + 0xe) = 0;
            *(int*)(counter + 0x14) = -1; Reject("Proto negative current accepted"); *(int*)(counter + 0x14) = 16625;
            *(byte*)(locks + 0x29) = 0; Reject("Proto disabled lock accepted"); *(byte*)(locks + 0x29) = 1;
            *(ulong*)(stamps + 24) = 201; Reject("Proto stale target accepted"); *(ulong*)(stamps + 24) = 200;
            foreach (int id in new[] { -1, 256, 512, 912, 2049, 2614, 2672 })
            { *(int*)(player + 0x118) = id; *(int*)locks = id; Reject("Proto metadata/unverified selection accepted"); }
            *(int*)(player + 0x118) = 2048; *(int*)locks = 2048; *(nint*)actor = module + 0x5a9d690; Reject("unknown Proto class accepted");
            Console.WriteLine("PASS: real-memory Proto normal/Chaos++ owned counter and typed source, relock, protected1% and processed history0; initialization, cycles, duplicates, stale/unknown state rejected");
        }
        finally { foreach (nint p in allocations) NativeMemory.Free((void*)p); VirtualFree(module, 0, 0x8000); }
    }
}
