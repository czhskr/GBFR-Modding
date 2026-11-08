using System.ComponentModel;
using System.Runtime.InteropServices;

namespace PartIndicator;

public sealed class ExecutableBuffer : IDisposable
{
    [DllImport("kernel32", SetLastError = true)] private static extern nint VirtualAlloc(nint address, nuint size, uint kind, uint protection);
    [DllImport("kernel32", SetLastError = true)] private static extern bool VirtualProtect(nint address, nuint size, uint protection, out uint old);
    [DllImport("kernel32", SetLastError = true)] private static extern bool VirtualFree(nint address, nuint size, uint kind);
    [DllImport("kernel32", SetLastError = true)] private static extern bool FlushInstructionCache(nint process, nint address, nuint size);
    public nint Address { get; private set; }
    public ExecutableBuffer(byte[] bytes)
    {
        Address = VirtualAlloc(0, (nuint)bytes.Length, 0x3000, 4);
        if (Address == 0) throw new Win32Exception();
        try
        {
            Marshal.Copy(bytes, 0, Address, bytes.Length);
            if (!VirtualProtect(Address, (nuint)bytes.Length, 0x20, out _) || !FlushInstructionCache(-1, Address, (nuint)bytes.Length)) throw new Win32Exception();
        }
        catch { Dispose(); throw; }
    }
    public void Dispose()
    {
        if (Address != 0 && !VirtualFree(Address, 0, 0x8000)) throw new Win32Exception();
        Address = 0;
    }
}
