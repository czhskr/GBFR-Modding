namespace FateSkip;

public static unsafe class NativeState
{
    public static bool TryGetUnreadFate(nint manager, out uint fateId, out byte flags)
    {
        fateId = 0; flags = 0;
        if (manager == 0 || *(uint*)(manager + 0x1B38) != 1) return false;
        fateId = *(uint*)(manager + 0x1B30);
        if (!FateSkipPolicy.IsValidFate(fateId)) return false;
        nint sentinel = *(nint*)(manager + 0x1AE0), buckets = *(nint*)(manager + 0x1AF0);
        if (sentinel == 0 || buckets == 0) return false;
        uint mask = *(uint*)(manager + 0x1B08);
        nint bucket = buckets + (nint)((ulong)(fateId & mask) * 16);
        nint node = *(nint*)(bucket + 8), last = *(nint*)bucket;
        for (int i = 0; i < 256 && node != 0 && node != sentinel; i++)
        {
            if (*(uint*)(node + 16) == fateId)
            {
                nint record = *(nint*)(node + 24);
                if (record == 0 || *(uint*)record != fateId) return false;
                flags = *(byte*)(record + 4);
                return (flags & 2) == 0;
            }
            if (node == last) break;
            node = *(nint*)(node + 8);
        }
        return false;
    }
}
