using Reloaded.Hooks.Definitions;
using System.Runtime.InteropServices;

namespace PartIndicator;

public enum GameEvent { Ui, Render, Resource, ObjectDestroy, CanvasDelete, ImageDestroy, ImageRender, ImageCommand, Consume, Controller }
public interface IGameEvents
{
    object? Enter(GameEvent kind, nint a, nint b, nint c, nint d);
    void Leave(GameEvent kind, object? token, nint a, nint b, nint c, nint d);
    void Failed(Exception error);
}

public sealed class GameHooks
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate nint F1(nint a);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate nint F2(nint a, nint b);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate nint F3(nint a, nint b, nint c);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate nint F4(nint a, nint b, nint c, nint d);
    private readonly List<object> _roots = [];
    private readonly List<Action> _activate = [];
    public GameHooks(IReloadedHooks hooks, NativeBindings b, IGameEvents events)
    {
        void Fail(Exception error) { try { events.Failed(error); } catch { } }
        void Before(GameEvent k, nint a, nint c, nint d, nint e, out object? token)
        { token=null; try { token=events.Enter(k,a,c,d,e); } catch(Exception error) { Fail(error); } }
        void After(GameEvent k,object? t,nint a,nint c,nint d,nint e)
        { try { events.Leave(k,t,a,c,d,e); } catch(Exception error) { Fail(error); } }
        void One(string name,GameEvent kind)
        {
            IHook<F1>? h=null;
            F1 callback=a=> { Before(kind,a,0,0,0,out var t); try { return h!.OriginalFunction(a); } finally { After(kind,t,a,0,0,0); } };
            h=hooks.CreateHook(callback,b.At(name)); _roots.Add(h); _roots.Add(callback); _activate.Add(()=>h.Activate());
        }
        void Two(string name,GameEvent kind)
        {
            IHook<F2>? h=null;
            F2 callback=(a,c)=> { Before(kind,a,c,0,0,out var t); try { return h!.OriginalFunction(a,c); } finally { After(kind,t,a,c,0,0); } };
            h=hooks.CreateHook(callback,b.At(name)); _roots.Add(h); _roots.Add(callback); _activate.Add(()=>h.Activate());
        }
        void Three(string name,GameEvent kind)
        {
            IHook<F3>? h=null;
            F3 callback=(a,c,d)=> { Before(kind,a,c,d,0,out var t); try { return h!.OriginalFunction(a,c,d); } finally { After(kind,t,a,c,d,0); } };
            h=hooks.CreateHook(callback,b.At(name)); _roots.Add(h); _roots.Add(callback); _activate.Add(()=>h.Activate());
        }
        void Four(string name,GameEvent kind)
        {
            IHook<F4>? h=null;
            F4 callback=(a,c,d,e)=> { Before(kind,a,c,d,e,out var t); try { return h!.OriginalFunction(a,c,d,e); } finally { After(kind,t,a,c,d,e); } };
            h=hooks.CreateHook(callback,b.At(name)); _roots.Add(h); _roots.Add(callback); _activate.Add(()=>h.Activate());
        }
        // Preserve raw RAX even for void entries; UI update decompiles to longlong. No floating/aggregate returns here.
        One("ui",GameEvent.Ui); One("render",GameEvent.Render); Two("resource",GameEvent.Resource);
        One("dtor",GameEvent.ObjectDestroy); Three("canvasDelete",GameEvent.CanvasDelete); One("imageDtor",GameEvent.ImageDestroy);
        One("imageRender",GameEvent.ImageRender); Two("imageCommand",GameEvent.ImageCommand);
        Four("commandConsumer",GameEvent.Consume); One("controller",GameEvent.Controller);
    }
    public void Activate() { foreach(var activate in _activate) activate(); }
}
