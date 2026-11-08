using System.Runtime.InteropServices;
using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.Definitions.Enums;

namespace PartIndicator;

public sealed class NativeServiceBridge
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void Callback();
    private readonly Callback _callback;
    private readonly IAsmHook _hook;
    public NativeServiceBridge(IReloadedHooks hooks, nint address, Action service, Action<Exception> failed)
    {
        _callback = () => { try { service(); } catch (Exception error) { try { failed(error); } catch { } } };
        _hook = hooks.CreateAsmHook(BuildThunk(Marshal.GetFunctionPointerForDelegate(_callback)), address,
            new AsmHookOptions { Behaviour = AsmHookBehaviour.ExecuteFirst, PreferRelativeJump = true, MaxOpcodeSize = 5, hookLength = 5 });
    }
    public void Activate() => _hook.Activate();
    // The loader retains this object and its delegate until process exit; suspension requests UI retirement.
    public static byte[] BuildThunk(nint callback)
    {
        if (callback == 0 || IntPtr.Size != 8) throw new ArgumentException("A Windows x64 callback is required.");
        // Preserve volatile GPRs, flags and legacy SIMD state; align stack and reserve Win64 shadow space.
        // shortcut: selected service point precedes a native call; do not reuse where upper YMM lanes are live.
        var bytes = new List<byte>(Convert.FromHexString("9C50515241504151415241534989E34883E4F04881EC300200004C899C2420020000480FAE44242048B8"));
        bytes.AddRange(BitConverter.GetBytes((long)callback));
        bytes.AddRange(Convert.FromHexString("FFD0480FAE4C2420488BA42420020000415B415A415941585A59589D"));
        return bytes.ToArray();
    }
}
