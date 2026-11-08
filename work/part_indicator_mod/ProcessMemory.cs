using System.Runtime.InteropServices;

namespace PartIndicator;

public static unsafe class ProcessMemory
{
    [DllImport("kernel32", SetLastError = true)]
    private static extern bool ReadProcessMemory(nint process, nint address, void* buffer, nuint size, out nuint read);
    public static bool TryRead<T>(nint address, out T value) where T : unmanaged
    {
        value = default;
        if (!OperatingSystem.IsWindows() || (ulong)address < 0x10000 || (ulong)address > 0x7fffffffffffUL - (uint)sizeof(T)) return false;
        T local = default;
        if (!ReadProcessMemory(-1, address, &local, (nuint)sizeof(T), out var read) || read != (nuint)sizeof(T)) return false;
        value = local; return true;
    }
    public static T Read<T>(nint address) where T : unmanaged => TryRead<T>(address, out var value) ? value : throw new IOException($"Invalid native read: 0x{address:X}");
}
