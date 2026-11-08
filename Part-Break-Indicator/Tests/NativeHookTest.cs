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
        Console.WriteLine("PASS: installed Reloaded.Hooks x64 mid-instruction trampoline and disable in isolated test process");
    }
}
