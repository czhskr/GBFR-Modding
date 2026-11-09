using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Reloaded.Hooks.Definitions;
using Reloaded.Mod.Interfaces;
using Reloaded.Mod.Interfaces.Internal;
using SharedHooks = Reloaded.Hooks.ReloadedII.Interfaces.IReloadedHooks;

namespace FateSkip;

public sealed unsafe class Mod : IMod
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int Evaluator(nint self);

    private IHook<Evaluator>? _hook;
    private ILoggerV1? _logger;
    private nint _stateVtable, _managerPointer;
    private volatile bool _enabled;
    private int _reported;
    public Action Disposing => Suspend;

    public void Start(IModLoaderV1 loader) => StartEx(loader, null!);
    public void StartEx(IModLoaderV1 loader, IModConfigV1 config)
    {
        if (_hook != null) return;
        _logger = (ILoggerV1)loader.GetLogger();
        try
        {
            using var process = Process.GetCurrentProcess();
            var module = process.MainModule ?? throw new InvalidOperationException("실행 모듈을 찾을 수 없습니다.");
            using var file = File.OpenRead(module.FileName);
            var hash = Convert.ToHexString(SHA256.HashData(file));
            if (!BuildGuard.CanReadEvaluator(hash, module.ModuleMemorySize))
            {
                Log("지원하는 실행 파일이 아니므로 적용하지 않았습니다.");
                return;
            }
            nint address = module.BaseAddress + 0x1D53970;
            byte[] prefix = new byte[7]; Marshal.Copy(address, prefix, 0, prefix.Length);
            if (!BuildGuard.Matches(hash, prefix))
            {
                Log("지원 빌드 또는 원본 함수와 일치하지 않아 적용하지 않았습니다.");
                return;
            }
            if (!loader.GetController<SharedHooks>().TryGetTarget(out var hooks))
            {
                Log("Reloaded.Hooks 컨트롤러가 없어 적용하지 않았습니다.");
                return;
            }
            _stateVtable = module.BaseAddress + 0x5901748;
            _managerPointer = module.BaseAddress + 0x7C21C58;
            // Assign before activation: another game thread may enter the detour immediately.
            _hook = hooks.CreateHook<Evaluator>(Evaluate, address.ToInt64());
            _hook.Activate();
            _enabled = true;
            Log("적용됨: 첫 시청 Skip 표시 및 확인창의 두 조건만 허용합니다.");
        }
        catch (Exception error)
        {
            _enabled = false;
            Log($"초기화 실패: {error.Message}");
        }
    }

    private int Evaluate(nint self)
    {
        int original = _hook!.OriginalFunction(self);
        if (!_enabled || original != 3 || self == 0 || *(nint*)self != _stateVtable) return original;
        uint id = *(uint*)(self + 8);
        if (!FateSkipPolicy.IsTarget(id)) return original;
        uint state = *(uint*)(self + 0x40); byte invert = *(byte*)(self + 0x30);
        if (state != 0 || invert != 0) return original;
        if (!NativeState.TryGetUnreadFate(*(nint*)_managerPointer, out uint fateId, out byte flags)) return original;
        if (!FateSkipPolicy.ShouldOverride(_enabled, original, id, state, invert, fateId, flags)) return original;
        int mask = id == 0x4EA41E80 ? 1 : 2;
        if ((Interlocked.Or(ref _reported, mask) & mask) == 0)
            Log($"조건 0x{id:X8} 허용, 에피소드 0x{fateId:X8}, 플래그 0x{flags:X2} 유지.");
        return 2;
    }

    private void Log(string message)
    {
        // Logging must never throw through the game's native call stack.
        try { _logger?.PrintMessage($"[Fate Skip] {message}", Color.LightGreen); }
        catch (Exception) { }
    }
    public void Suspend() { _enabled = false; Log("일시 중지됨."); }
    public void Resume() { _reported = 0; _enabled = _hook?.IsHookActivated == true; Log(_enabled ? "재개됨." : "후크가 없어 재개하지 않았습니다."); }
    public void Unload() => Suspend();
    public bool CanSuspend() => true;
    // A live managed trampoline must retain its delegate and assembly until process exit.
    public bool CanUnload() => false;
}
