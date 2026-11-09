using System.Runtime.InteropServices;
using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.Definitions.Enums;

namespace PartIndicator;

public sealed class NativeServiceBridge
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void Callback();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void TayuitarResetCallback(nint actor, int maximum);
    private readonly Delegate _callback;
    private readonly IAsmHook _hook;
    public NativeServiceBridge(IReloadedHooks hooks, nint address, Action service, Action<Exception> failed)
        : this(hooks, address, new Callback(() => Invoke(service, failed)), tayuitarReset: false) { }

    public NativeServiceBridge(IReloadedHooks hooks, nint address, Action<nint, int> recordReset, Action<Exception> failed)
        : this(hooks, address, new TayuitarResetCallback((actor, maximum) =>
        {
            try { recordReset(actor, maximum); }
            catch (Exception error) { try { failed(error); } catch { } }
        }), tayuitarReset: true) { }

    private NativeServiceBridge(IReloadedHooks hooks, nint address, Delegate callback, bool tayuitarReset)
    {
        _callback = callback;
        _hook = hooks.CreateAsmHook(BuildThunk(Marshal.GetFunctionPointerForDelegate(_callback), tayuitarReset), address,
            new AsmHookOptions { Behaviour = AsmHookBehaviour.ExecuteFirst, PreferRelativeJump = true, MaxOpcodeSize = 5, hookLength = tayuitarReset ? 7 : 5 });
    }
    private static void Invoke(Action action, Action<Exception> failed)
    { try { action(); } catch (Exception error) { try { failed(error); } catch { } } }
    public void Activate() => _hook.Activate();
    // The loader retains this object and its delegate until process exit; suspension requests UI retirement.
    public static byte[] BuildThunk(nint callback, bool tayuitarReset = false)
    {
        if (callback == 0 || IntPtr.Size != 8) throw new ArgumentException("A Windows x64 callback is required.");
        // Preserve volatile GPRs, flags and legacy SIMD state; align stack and reserve Win64 shadow space.
        // shortcut: both sites precede an integer-argument call; do not reuse where upper YMM lanes are live.
        var bytes = new List<byte>(Convert.FromHexString("9C50515241504151415241534989E34883E4F04881EC300200004C899C2420020000480FAE442420"));
        if (tayuitarReset) bytes.AddRange(Convert.FromHexString("4889F189C2")); // Actor from RSI and the just-written maximum from EAX.
        bytes.AddRange(Convert.FromHexString("48B8"));
        bytes.AddRange(BitConverter.GetBytes((long)callback));
        bytes.AddRange(Convert.FromHexString("FFD0480FAE4C2420488BA42420020000415B415A415941585A59589D"));
        return bytes.ToArray();
    }
}
