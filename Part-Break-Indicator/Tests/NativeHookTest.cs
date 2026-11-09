using System.Runtime.InteropServices;
using PartIndicator;
using Reloaded.Hooks;
using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.Definitions.Enums;

static class NativeHookTest
{
    public static void Run()
    {
        int calls = 0;
        NativeServiceBridge.Callback callback = () => { calls++; _ = Math.Sin(calls); };
        var bytes = new List<byte>(Convert.FromHexString("48B888776655443322114839C066480F6EC0"));
        int point = bytes.Count;
        bytes.AddRange(Convert.FromHexString("B960000000")); // Same overwritten instruction as service+0: mov ecx,0x60.
        bytes.AddRange(Enumerable.Repeat((byte)0x90, 11));
        bytes.AddRange(Convert.FromHexString("750B66480F7EC14839C17501C331C0C3"));
        using var code = new ExecutableBuffer(bytes.ToArray());
        var invoke = Marshal.GetDelegateForFunctionPointer<ReturnUlong>(code.Address);
        var hook = ReloadedHooks.Instance.CreateAsmHook(NativeServiceBridge.BuildThunk(Marshal.GetFunctionPointerForDelegate(callback)),
            code.Address + point, new AsmHookOptions { Behaviour = AsmHookBehaviour.ExecuteFirst, PreferRelativeJump = true, MaxOpcodeSize = 5, hookLength = 5 });
        hook.Activate();
        try
        {
            for (int i = 0; i < 100; i++) if (invoke() != 0x1122334455667788UL) throw new Exception("SDK trampoline altered return state");
            if (calls != 100) throw new Exception("SDK service callback count");
        }
        finally { hook.Disable(); }
        if (invoke() != 0x1122334455667788UL || calls != 100) throw new Exception("SDK disable did not restore original path");
        GC.KeepAlive(callback); GC.KeepAlive(hook);
        CheckTayuitarResetBridge();
        Console.WriteLine("PASS: installed Reloaded.Hooks x64 mid-instruction trampoline and disable in isolated test process");
    }

    private static void CheckTayuitarResetBridge()
    {
        nint actor = Marshal.AllocHGlobal(0x6b40);
        try
        {
            const int maximum = 225;
            Marshal.WriteByte(actor, 0x6b38, 0);
            bool captured = false;
            NativeServiceBridge.TayuitarResetCallback callback = (root, hp) =>
                captured = root == actor && hp == maximum && Marshal.ReadByte(actor, 0x6b38) == 0;

            var bytes = new List<byte>(Convert.FromHexString("5648BE")); // Save RSI and load the simulated actor.
            bytes.AddRange(BitConverter.GetBytes((long)actor));
            bytes.Add(0xb8);
            bytes.AddRange(BitConverter.GetBytes(maximum));
            bytes.AddRange(Convert.FromHexString("39C0"));
            int point = bytes.Count;
            bytes.AddRange(Convert.FromHexString("C686386B000001")); // Original seven-byte active flag store.
            bytes.AddRange(Enumerable.Repeat((byte)0x90, 9));
            bytes.AddRange(Convert.FromHexString("75025EC331C05EC3"));
            using var code = new ExecutableBuffer(bytes.ToArray());
            var invoke = Marshal.GetDelegateForFunctionPointer<ReturnUlong>(code.Address);
            var hook = ReloadedHooks.Instance.CreateAsmHook(NativeServiceBridge.BuildThunk(Marshal.GetFunctionPointerForDelegate(callback), tayuitarReset: true),
                code.Address + point, new AsmHookOptions { Behaviour = AsmHookBehaviour.ExecuteFirst, PreferRelativeJump = true, MaxOpcodeSize = 5, hookLength = 7 });
            hook.Activate();
            try
            {
                if (invoke() != maximum || !captured || Marshal.ReadByte(actor, 0x6b38) != 1)
                    throw new Exception("Tayu reset trampoline lost its inputs, return state or native write");
            }
            finally { hook.Disable(); }
            GC.KeepAlive(callback); GC.KeepAlive(hook);
        }
        finally { Marshal.FreeHGlobal(actor); }
    }
}
