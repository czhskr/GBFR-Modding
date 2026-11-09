using FateSkip;
using System.Runtime.InteropServices;

int checks = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); checks++; }
const uint guide = 0x4EA41E80, input = 0x635542C2, fate = 0x2EE0D215;
foreach (var id in new[] { guide, input })
{
    Check(FateSkipPolicy.ShouldOverride(true, 3, id, 0, 0, fate, 0x14), "unread gate");
    Check(FateSkipPolicy.ShouldOverride(true, 3, id, 0, 0, 0x97E68AF6, 0x15), "another valid episode");
    Check(FateSkipPolicy.ShouldOverride(true, 3, id, 0, 0, fate, 0x14), "cancel/reopen is repeatable");
    Check(!FateSkipPolicy.ShouldOverride(false, 3, id, 0, 0, fate, 0x14), "suspended");
    foreach (var result in new[] { 0, 1, 2, 4, -1 })
        Check(!FateSkipPolicy.ShouldOverride(true, result, id, 0, 0, fate, 0x14), "preserve other results/replay");
    Check(!FateSkipPolicy.ShouldOverride(true, 3, id, 1, 0, fate, 0x14), "other enum");
    Check(!FateSkipPolicy.ShouldOverride(true, 3, id, 0, 1, fate, 0x14), "inverse gate");
    Check(!FateSkipPolicy.ShouldOverride(true, 3, id, 0, 0, fate, 0x1E), "already read");
    Check(!FateSkipPolicy.ShouldOverride(true, 3, id, 0, 0, fate, null), "missing record");
    foreach (var invalid in new uint[] { 0, uint.MaxValue, 0x887AE0B0 })
        Check(!FateSkipPolicy.ShouldOverride(true, 3, id, 0, 0, invalid, 0x14), "invalid episode ID");
}
foreach (var id in new uint[] { 0x241B3591, 0x3C59730F, 0x2A6E05BB, 0 })
    Check(!FateSkipPolicy.ShouldOverride(true, 3, id, 0, 0, fate, 0x14), "unrelated condition");
byte[] prefix = [86, 87, 83, 72, 131, 236, 48];
Check(BuildGuard.Matches(BuildGuard.ExpectedSha256, prefix), "supported build");
Check(!BuildGuard.Matches(new string('0', 64), prefix), "wrong build");
Check(!BuildGuard.Matches(BuildGuard.ExpectedSha256, [0xE9, 87, 83, 72, 131, 236, 48]), "existing hook");
Check(!BuildGuard.Matches(BuildGuard.ExpectedSha256, []), "missing prefix");
Check(BuildGuard.CanReadEvaluator(BuildGuard.ExpectedSha256, 0x8000000), "supported image size");
Check(!BuildGuard.CanReadEvaluator(new string('0', 64), 0x8000000), "reject hash before memory read");
Check(!BuildGuard.CanReadEvaluator(BuildGuard.ExpectedSha256, 1024), "reject out-of-module address");
using (var memory = new Fixture())
{
    Check(NativeState.TryGetUnreadFate(memory.Manager, out var id, out var flags) && id == fate && flags == 0x14, "native lookup");
    Check(Marshal.ReadByte(memory.Record, 4) == 0x14, "reader never writes flags");
    Marshal.WriteInt32(memory.Manager, 0x1B08, 7);
    Marshal.WriteIntPtr(memory.Buckets, 5 * 16, memory.Node);
    Marshal.WriteIntPtr(memory.Buckets, 5 * 16 + 8, memory.Node);
    Check(NativeState.TryGetUnreadFate(memory.Manager, out _, out _), "masked bucket index");
    Marshal.WriteInt32(memory.Manager, 0x1B08, 0);
    Marshal.WriteByte(memory.Record, 4, 0x1E);
    Check(!NativeState.TryGetUnreadFate(memory.Manager, out _, out _), "reader rejects replay");
    Marshal.WriteByte(memory.Record, 4, 0x14);
    Marshal.WriteInt32(memory.Manager, 0x1B38, 2);
    Check(!NativeState.TryGetUnreadFate(memory.Manager, out _, out _), "non-story state");
    Marshal.WriteInt32(memory.Manager, 0x1B38, 1);
    Marshal.WriteIntPtr(memory.Node, 24, 0);
    Check(!NativeState.TryGetUnreadFate(memory.Manager, out _, out _), "null record");
    Marshal.WriteIntPtr(memory.Node, 24, memory.Record);
    Marshal.WriteInt32(memory.Record, 0, unchecked((int)0xDEADBEEF));
    Check(!NativeState.TryGetUnreadFate(memory.Manager, out _, out _), "record identity mismatch");
    Marshal.WriteInt32(memory.Record, 0, unchecked((int)fate));
    // The tail is a collision; follow +8 to the matching head node.
    Marshal.WriteInt32(memory.Collision, 16, unchecked((int)0xDEADBEEF));
    Marshal.WriteIntPtr(memory.Collision, 8, memory.Node);
    Marshal.WriteIntPtr(memory.Buckets, 8, memory.Collision);
    Check(NativeState.TryGetUnreadFate(memory.Manager, out _, out _), "collision chain");
    Marshal.WriteInt32(memory.Node, 16, unchecked((int)0xDEADBEEF));
    Marshal.WriteIntPtr(memory.Node, 8, memory.Collision);
    Marshal.WriteIntPtr(memory.Buckets, 0, memory.Sentinel);
    Check(!NativeState.TryGetUnreadFate(memory.Manager, out _, out _), "cycle is bounded");
    Marshal.WriteIntPtr(memory.Buckets, 8, memory.Sentinel);
    Check(!NativeState.TryGetUnreadFate(memory.Manager, out _, out _), "empty bucket");
}
Check(!NativeState.TryGetUnreadFate(0, out _, out _), "null manager");
Console.WriteLine($"PASS: {checks} checks");

sealed class Fixture : IDisposable
{
    readonly List<nint> allocations = [];
    public nint Manager, Node, Collision, Sentinel, Buckets, Record;
    nint Allocate(int size) { var p = Marshal.AllocHGlobal(size); Marshal.Copy(new byte[size], 0, p, size); allocations.Add(p); return p; }
    public Fixture()
    {
        Manager = Allocate(0x1C50); Node = Allocate(40); Collision = Allocate(40);
        Sentinel = Allocate(40); Buckets = Allocate(128); Record = Allocate(8);
        Marshal.WriteInt32(Manager, 0x1B30, unchecked((int)0x2EE0D215));
        Marshal.WriteInt32(Manager, 0x1B38, 1);
        Marshal.WriteIntPtr(Manager, 0x1AE0, Sentinel); Marshal.WriteIntPtr(Manager, 0x1AF0, Buckets);
        Marshal.WriteIntPtr(Buckets, Node); Marshal.WriteIntPtr(Buckets, 8, Node);
        Marshal.WriteInt32(Node, 16, unchecked((int)0x2EE0D215)); Marshal.WriteIntPtr(Node, 24, Record);
        Marshal.WriteInt32(Record, 0, unchecked((int)0x2EE0D215));
        Marshal.WriteByte(Record, 4, 0x14);
    }
    public void Dispose() { foreach (var p in allocations) Marshal.FreeHGlobal(p); }
}
