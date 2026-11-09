using PartIndicator;
using System.Runtime.InteropServices;

StartupLifecycleTest.Run();
PercentageTextTest.Run();
HudAtlasTest.Run();
HeadAssetsTest.Run();
HeadLifetimeTest.Run();
static void Check(bool value, string message) { if (!value) throw new Exception(message); }
static PartSnapshot S(uint type = 0x501, double current = 1200, double? initial = 1200, int[]? history = null, long generation = 1) =>
    new(1, 2, type, type == 0x1803 ? 5 : 9, true, current, initial, history ?? [], true, generation);
Check(!PartStatePolicy.Evaluate(S(0x7700)).Visible, "unsupported boss hidden");
Check(PartStatePolicy.Evaluate(S(0x1803, .1500549316, null)) is { Visible: true, Broken: false, Fraction: null }, "positive float never broken");
Check(PartStatePolicy.Evaluate(S(0x1803, 0, null)) is { Broken: false }, "zero without history never broken");
Check(PartStatePolicy.Evaluate(S(0x1803, 0, null, [0])) is { Broken: true, Fraction: null }, "history drives destruction");
Check(PartStatePolicy.Evaluate(S(current: 600)).Fraction == .5, "verified normal initial gauge");
Check(PartStatePolicy.Evaluate(S(0x500, 600) with { InitialVerified = false }).Fraction == null, "Stone initial omitted without native proof");
foreach (double? initial in new double?[] { null, 0, -1, double.NaN, double.PositiveInfinity, 500 })
    Check(PartStatePolicy.Evaluate(S(current: 600, initial: initial)).Fraction == null, "invalid initial omitted");
Check(!PartStatePolicy.Evaluate(S(current: double.NaN)).Visible, "invalid current hidden");
Check(!PartStatePolicy.Evaluate(S() with { SelectedId = 3 }).Visible, "unsupported crocodile body hidden");
Check(!PartStatePolicy.Evaluate(S(history: [-1])).Visible, "invalid history hidden");
Check(PartStatePolicy.Evaluate(S() with { InitialVerified = false }).Fraction == null, "unproven initial omitted");
Check(!ProcessMemory.TryRead<int>(1, out _), "unmapped read returns false");
Check(!BuildGuard.Matches("wrong", 136413184), "unsupported build disabled");
int bridgeCalls = 0;
NativeServiceBridge.Callback bridgeCallback = () => { bridgeCalls++; _ = Math.Sin(bridgeCalls); };
var stub = new List<byte>(Convert.FromHexString("48B888776655443322114839C0"));
stub.AddRange(NativeServiceBridge.BuildThunk(Marshal.GetFunctionPointerForDelegate(bridgeCallback)));
stub.AddRange(Convert.FromHexString("7501C331C0C3"));
using (var code = new ExecutableBuffer(stub.ToArray()))
{
    var invoke = Marshal.GetDelegateForFunctionPointer<ReturnUlong>(code.Address);
    for (int i = 0; i < 100; i++) Check(invoke() == 0x1122334455667788UL, "service bridge preserves RAX, flags and stack");
}
GC.KeepAlive(bridgeCallback);
Check(bridgeCalls == 100, "managed service callback reached and returned");
using (var clobber = new ExecutableBuffer(Convert.FromHexString("660FEFC031C083C001C3")))
{
    var preservation = new List<byte>(Convert.FromHexString("48B888776655443322114839C066480F6EC0"));
    preservation.AddRange(NativeServiceBridge.BuildThunk(clobber.Address));
    preservation.AddRange(Convert.FromHexString("750B66480F7EC14839C17501C331C0C3"));
    using var code = new ExecutableBuffer(preservation.ToArray());
    var invoke = Marshal.GetDelegateForFunctionPointer<ReturnUlong>(code.Address);
    Check(invoke() == 0x1122334455667788UL, "service bridge restores clobbered XMM0, RAX and flags");
}
NativeHookTest.Run();
NativeStateTest.Run();
DragonStateTest.Run();
FurycaneStateTest.Run();
FuryHumanoidStateTest.Run();
VulkanStateTest.Run();
PyetStateTest.Run();
TayuitarStateTest.Run();
ProtoStateTest.Run();
BehemothStateTest.Run();
AngraStateTest.Run();
PairedHudTest.Run();

var hud = new FakeHud(); var runtime = new IndicatorRuntime(hud);
var a = new WorldIdentity(1, 2, 3); var b = new WorldIdentity(1, 4, 3);
runtime.Tick(a, true, S(), true);
Check(hud.Created == 1 && hud.Applied == 1 && runtime.Generation == 1, "first scene creates");
runtime.Tick(a, true, null, true);
Check(hud.Hidden == 1 && hud.Releases == 0, "missing lock/menu hides same objects");
runtime.Tick(a, true, S(), true);
Check(hud.Created == 1 && hud.Applied == 2, "same scene resumes without recreation");
runtime.Tick(b, true, S(generation: 2), true);
Check(hud.Releases == 1 && hud.Created == 1, "scene change starts retirement before new allocation");
runtime.Tick(b, true, S(generation: 2), true);
Check(hud.Applied == 2 && hud.Created == 1, "new frame blocked until old references drain");
hud.Retired = true;
runtime.Tick(b, true, S(generation: 2), true);
Check(hud.Created == 1, "cleanup completion does not reuse old frame");
runtime.Tick(b, true, S(generation: 1), true);
Check(hud.Created == 1, "old generation snapshot cannot recreate");
runtime.Tick(b, true, S(generation: 2), true);
Check(hud.Created == 2 && hud.Applied == 3, "new generation recreates after retirement");
runtime.Tick(b, true, S(generation: 2), false);
Check(hud.Releases == 2, "suspend schedules native cleanup");
hud.Retired = true;
runtime.Tick(b, true, S(generation: 2), false);
Check(hud.Applied == 3, "disabled never applies");
runtime.Tick(b, true, S(generation: 3), true);
Check(hud.Created == 3 && runtime.Generation == 3, "resume uses new generation");
runtime.RequestRestart();
runtime.Tick(b, true, S(generation: 4), true);
Check(hud.Releases == 3 && runtime.Generation == 4 && hud.Created == 3, "rapid suspend/resume still retires old generation");
var reusedHud = new FakeHud(); var reusedRuntime = new IndicatorRuntime(reusedHud);
var originalHandle = new WorldIdentity(1, 2, 3, 5, 100);
reusedRuntime.Tick(originalHandle, true, S(), true);
reusedRuntime.Tick(originalHandle with { Stamp = 101 }, true, S(generation: 2), true);
Check(reusedHud.Releases == 1 && reusedHud.Created == 1 && reusedRuntime.Generation == 2, "same-address new scene handle retires UI before reuse");
Check(!HudCoordinates.TryConvert(float.MaxValue, 1, .5f, 1, 3840, 2160, out _, out _), "overflowed layout hidden");
Check(HudCoordinates.TryConvert(1280, 720, 2f / 3, -2f / 3, 3840, 2160, out float px, out float py) && Math.Abs(px) < .001 && Math.Abs(py) < .001, "native center coordinate conversion");
Console.WriteLine("PASS: state policy, fail-closed reads, scene retirement/recreation, stale generation and suspend/resume");

sealed class FakeHud : IIndicatorHud
{
    public int Created, Applied, Hidden, Releases;
    public bool HasObjects { get; private set; }
    public bool Retired;
    public bool TryCreate(long generation) { Created++; HasObjects = true; Retired = false; return true; }
    public void Apply(PartDisplay display, PartDisplay? second = null) { if (!HasObjects) throw new Exception("use after release"); Applied++; }
    public void Hide() => Hidden++;
    public void BeginRelease() => Releases++;
    public bool PollRelease() { if (!Retired) return false; HasObjects = false; return true; }
}
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
delegate ulong ReturnUlong();
