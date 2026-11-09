using System.Reflection;
using System.Runtime.CompilerServices;
using PartIndicator;

static class StartupLifecycleTest
{
    public static void Run()
    {
        var mod = new Mod();
        var hud = (NativeHud)RuntimeHelpers.GetUninitializedObject(typeof(NativeHud));
        const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(Mod).GetField("_hud", fields)!.SetValue(mod, hud);
        mod.Resume();
        if (hud.Enabled) throw new Exception("resume after incomplete startup enabled HUD");
        typeof(Mod).GetField("_initialized", fields)!.SetValue(mod, true);
        mod.Resume();
        if (!hud.Enabled) throw new Exception("resume after successful startup did not enable HUD");
        mod.Suspend();
        if (hud.Enabled) throw new Exception("suspend did not disable HUD");
        mod.Resume();
        if (!hud.Enabled) throw new Exception("resume after suspension did not enable HUD");
        Console.WriteLine("PASS: startup failure blocks resume; successful startup permits suspend and resume");
    }
}
