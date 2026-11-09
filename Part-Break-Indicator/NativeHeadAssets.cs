using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Reloaded.Hooks.Definitions;

namespace PartIndicator;

public sealed class NativeHeadAssets
{
    public const uint Atlas = 0xe193ab00, Sprite = 0x9a7f2306;
    public static readonly HudGlyph Glyph = new(' ', Sprite, 1254, 0, 0, 0, 0, 1254, 1254, 0, .3876953125f, .6123046875f, 1);
    private readonly NativeBindings _b;
    private readonly Func<bool> _enabled;
    private readonly Action<string> _log;
    private readonly IHook<NativeBindings.AtlasRequest>? _hook;
    private readonly NativeBindings.AtlasRequest? _callback;
    private readonly nint _name;
    private int _requested, _readyReported;
    private long _nextCheck, _checkEpoch = -1;
    private nint _checkedMaterial, _readyMaterial;
    [DllImport("kernel32")] private static extern uint GetCurrentThreadId();

    public static bool FilesValid(string directory)
    {
        try
        {
            foreach (var (path, expected) in new[] {
                ("GBFR/data/ui/atlas/pbi_angra_head.tex.texb", "56A391452E970A93F7BC192DBE9C45FE5DDD5D92C949A3C15E55BEB42E2EDF44"),
                ("GBFR/data/ui/atlas/pbi_angra_head.wtb", "89EE702B35A2E7D4CFE85089D55F887523EAD09762200408E61C7933878BB1D3"),
                ("GBFR/data/ui/fhd/atlas/pbi_angra_head.wtb", "89EE702B35A2E7D4CFE85089D55F887523EAD09762200408E61C7933878BB1D3") })
            {
                using var file = File.OpenRead(Path.Combine(directory, path));
                if (Convert.ToHexString(SHA256.HashData(file)) != expected) return false;
            }
            return true;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }
    public static bool CanRequest(nint module, nint context, byte flags, int threadId)
    {
        return flags == 0 && threadId > 0 && context != 0 &&
            ProcessMemory.TryRead<nint>(module + 0x7c17858, out var expected) && expected == context &&
            ProcessMemory.TryRead<nint>(module + 0x7ab3bf8, out var manager) && manager != 0 &&
            ProcessMemory.TryRead<uint>(manager + 0x8e8, out var type) && type == 0x102 &&
            ProcessMemory.TryRead<int>(manager + 0x930, out var owner) && owner == threadId &&
            ProcessMemory.TryRead<int>(manager + 0x934, out var count) && count is > 0 and < 1024 &&
            ProcessMemory.TryRead<nint>(module + 0x7ab3bf8, out var after) && after == manager &&
            ProcessMemory.TryRead<nint>(module + 0x7c17858, out expected) && expected == context;
    }
    public NativeHeadAssets(IReloadedHooks hooks, NativeBindings bindings, string directory, Func<bool> enabled, Action<string> log)
    {
        _b = bindings; _enabled = enabled; _log = log;
        if (!FilesValid(directory)) { Log("Angra head resources missing or changed; head panel unavailable."); return; }
        // The module cannot unload: this one request and its name stay valid until process exit.
        _name = Marshal.StringToCoTaskMemUTF8("atlas/pbi_angra_head");
        _callback = (context, path, hash, flags) =>
        {
            try
            {
                int thread = unchecked((int)GetCurrentThreadId());
                if (Volatile.Read(ref _requested) == 0 && _enabled() &&
                    CanRequest(_b.ModuleBase, context, flags, thread) && Interlocked.CompareExchange(ref _requested, 1, 0) == 0)
                {
                    _hook!.OriginalFunction(context, _name, Atlas, 0);
                    Volatile.Write(ref _requested, 2);
                    Log($"Head atlas requested once on native loader thread {thread}; retained for module lifetime.");
                }
            }
            catch (Exception error) { Volatile.Write(ref _requested, -1); Log("Head atlas request disabled: " + error.Message); }
            return _hook!.OriginalFunction(context, path, hash, flags);
        };
        _hook = hooks.CreateHook(_callback, _b.At("atlasRequest"));
    }
    public void Activate() => _hook?.Activate();
    public nint Ready(long frame, long epoch)
    {
        if (Volatile.Read(ref _requested) != 2 || !HudAtlas.TryMaterial(_b.ModuleBase, Atlas, out var current))
        { _readyMaterial = 0; return 0; }
        // Recheck a proven material on the first safe frame after resource work.
        bool recheckAfterResourceWork = _readyMaterial == current && epoch != _checkEpoch;
        if (current != _checkedMaterial || epoch != _checkEpoch) _readyMaterial = 0;
        if (recheckAfterResourceWork || frame >= _nextCheck)
        {
            _nextCheck = frame + 60; _checkEpoch = epoch; _checkedMaterial = current; _readyMaterial = 0;
            if (HudAtlas.TryValidate(_b.ModuleBase, Atlas, [Glyph], out var verified, 1254) && verified == current &&
                _b.GetAtlasStatus(_name, 20) != 0) _readyMaterial = current;
        }
        if (_readyMaterial != 0 && Interlocked.Exchange(ref _readyReported, 1) == 0)
            Log("Head atlas metadata, material and native readiness verified.");
        return _readyMaterial;
    }
    private void Log(string message) { try { _log(message); } catch { } }
}
