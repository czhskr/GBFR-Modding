using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Reloaded.Mod.Interfaces;
using Reloaded.Mod.Interfaces.Internal;
using SharedHooks = Reloaded.Hooks.ReloadedII.Interfaces.IReloadedHooks;

namespace PartIndicator;

public sealed class Mod : IMod
{
    private ILoggerV1? _logger;
    private NativeHud? _hud;
    private NativeHeadAssets? _headAssets;
    private NativeBindings? _bindings;
    private GameHooks? _hooks;
    private NativeServiceBridge? _service;
    private NativeServiceBridge? _tayuitarReset;
    private GCHandle _lifetimeRoot;
    private bool _started;
    public Action Disposing => Suspend;
    public void Start(IModLoaderV1 loader) => StartEx(loader, null!);
    public void StartEx(IModLoaderV1 loader, IModConfigV1 config)
    {
        if (_started) return;
        _started = true; _logger = (ILoggerV1)loader.GetLogger();
        try
        {
            using var process = Process.GetCurrentProcess();
            var module = process.MainModule ?? throw new IOException("Main module unavailable");
            using var file = File.OpenRead(module.FileName);
            if (!BuildGuard.Matches(Convert.ToHexString(SHA256.HashData(file)), module.ModuleMemorySize))
            { Log("Unsupported executable; native hooks not installed."); return; }
            if (!loader.GetController<SharedHooks>().TryGetTarget(out var hooks))
            { Log("Reloaded.Hooks controller unavailable."); return; }
            _bindings = new(module.BaseAddress);
            string directory = config != null && loader is IModLoaderV2 loaderV2 ? loaderV2.GetDirectoryForModId(config.ModId) :
                Path.GetDirectoryName(typeof(Mod).Assembly.Location)!;
            _headAssets = new(hooks, _bindings, directory, () => _hud?.Enabled == true, Log);
            _hud = new(_bindings, Log, _headAssets) { Enabled = false };
            _hooks = new(hooks, _bindings, _hud);
            _service = new(hooks, _bindings.At("service"), _hud.Service, _hud.Failed);
            // Native callbacks may run as soon as activation begins, and must survive loader disposal.
            _lifetimeRoot = GCHandle.Alloc(this);
            _hooks.Activate(); _headAssets.Activate(); _service.Activate();
            try
            {
                nint reset = module.BaseAddress + 0x2d35b84;
                // The seven-byte active-flag store follows the fourth leg's current-value write.
                if (ProcessMemory.Read<ulong>(reset - 6) != 0x86c600006b308689UL ||
                    ProcessMemory.Read<ulong>(reset) != 0xeb0100006b3886c6UL)
                    throw new IOException("Native Tayuitar reset site mismatch");
                _tayuitarReset = new(hooks, reset, _hud.RecordTayuitarLegReset, error => Log("Tayuitar reset sample skipped: " + error.Message));
                _tayuitarReset.Activate();
            }
            catch (Exception error) { Log("Tayuitar later-cycle observation unavailable: " + error.Message); }
            _hud.Enabled = true;
            Log("Execution connected; waiting for a verified HUD and supported solo lock-on target.");
        }
        catch (Exception error)
        {
            if (_hud != null) _hud.Enabled = false;
            Log("Startup disabled: " + error.Message);
        }
    }
    private void Log(string message)
    { try { _logger?.PrintMessage("[Part Indicator] " + message, Color.LightGreen); } catch { } }
    public void Suspend() { if (_hud != null) _hud.Enabled = false; Log("Suspended; UI retirement queued."); }
    public void Resume() { if (_hud != null) _hud.Enabled = true; Log("Resume requested; fresh scene generation required."); }
    public void Unload() => Suspend();
    public bool CanSuspend() => true;
    // Live reverse-P/Invoke delegates and trampolines remain rooted until process exit.
    public bool CanUnload() => false;
}
