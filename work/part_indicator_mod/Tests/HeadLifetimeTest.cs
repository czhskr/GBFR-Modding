using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using PartIndicator;

static unsafe class HeadLifetimeTest
{
    [DllImport("kernel32")] private static extern nint VirtualAlloc(nint address, nuint size, uint kind, uint protection);
    [DllImport("kernel32")] private static extern bool VirtualFree(nint address, nuint size, uint kind);
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static void Set(object o, string name, object? value) => o.GetType().GetField(name, Fields)!.SetValue(o, value);
    static T Get<T>(object o, string name) => (T)o.GetType().GetField(name, Fields)!.GetValue(o)!;
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static readonly PartDisplay Intact = new(true, false, 1), Broken = new(true, true, null);

    sealed class Fixture : IDisposable
    {
        readonly List<nint> _allocations = [];
        readonly List<object> _widgets = [];
        readonly Type _widgetType = typeof(NativeHud).GetNestedType("Widget", BindingFlags.NonPublic)!;
        public readonly nint Module, Owner, MaterialA, MaterialB, MaterialNode;
        public readonly NativeBindings Bindings;
        public readonly NativeHeadAssets Assets;
        public readonly NativeHud Hud;
        public int StatusCalls, PrivateReleases, Creates;
        public bool FailReacquire, FailHeadBind, FailObject, RebindDuringInit;
        public bool StatusReady = true;
        nint _pendingImage, _mapLast, _mapFirst;
        public nint Alloc(int bytes) { nint p = (nint)NativeMemory.AllocZeroed((nuint)bytes); _allocations.Add(p); return p; }
        public Fixture()
        {
            Module = VirtualAlloc(0, 0x8000000, 0x2000, 4);
            Check(Module != 0 && VirtualAlloc(Module + 0x73b9000, 0x1000, 0x1000, 4) != 0 &&
                VirtualAlloc(Module + 0x7011000, 0x1000, 0x1000, 4) != 0, "lifetime fixture memory");
            Owner = Alloc(0x2400); MaterialA = Alloc(16); MaterialB = Alloc(16);
            var buckets = Alloc(16); var sentinel = Alloc(32); var node = Alloc(64); var rows = Alloc(11 * 0x50);
            var mbuckets = Alloc(16); var msentinel = Alloc(32); MaterialNode = Alloc(64);
            *(nint*)(Module + 0x73b9e98) = sentinel; *(nint*)(Module + 0x73b9ea8) = buckets;
            *(nint*)buckets = *(nint*)(buckets + 8) = node;
            *(uint*)(node + 0x10) = PercentageText.Atlas; *(nint*)(node + 0x20) = rows; *(nint*)(node + 0x28) = rows + 11 * 0x50;
            for (int i = 0; i < 11; i++) FillGlyph(rows + i * 0x50, PercentageText.Glyphs[i], 296);
            var headNode = Alloc(64); var headRow = Alloc(0x50);
            *(nint*)(headNode + 8) = node; *(uint*)(headNode + 0x10) = NativeHeadAssets.Atlas;
            *(nint*)(headNode + 0x20) = headRow; *(nint*)(headNode + 0x28) = headRow + 0x50;
            FillGlyph(headRow, NativeHeadAssets.Glyph, 1254); *(nint*)(buckets + 8) = headNode;
            *(nint*)(Module + 0x73b9d80) = msentinel; *(nint*)(Module + 0x73b9d90) = mbuckets;
            var numberMaterial = Alloc(64); *(uint*)(numberMaterial + 0x10) = PercentageText.Atlas; *(nint*)(numberMaterial + 0x20) = MaterialA;
            *(uint*)(MaterialNode + 0x10) = NativeHeadAssets.Atlas; *(nint*)(MaterialNode + 0x20) = MaterialA; *(nint*)(MaterialNode + 8) = numberMaterial;
            *(nint*)mbuckets = numberMaterial; *(nint*)(mbuckets + 8) = MaterialNode;
            var mapBucket = Alloc(16); *(nint*)(Owner + 0x1f8) = mapBucket; *(int*)(Owner + 0x260) = 1;
            var tls = Alloc(8); var slot = Alloc(0x1640); var allocator = Alloc(0x200);
            *(nint*)tls = slot; *(nint*)(slot + 0x1630) = allocator;
            foreach (int offset in new[] { 0x1e8, 0x58, 0x148 }) *(nint*)(allocator + offset) = allocator;
            Bindings = (NativeBindings)RuntimeHelpers.GetUninitializedObject(typeof(NativeBindings));
            Set(Bindings, "<ModuleBase>k__BackingField", Module);
            Set(Bindings, "GetAtlasStatus", (NativeBindings.AtlasStatus)((_, _) => { StatusCalls++; return StatusReady ? (byte)1 : (byte)0; }));
            Set(Bindings, "TlsArray", (NativeBindings.P0)(() => tls));
            Set(Bindings, "SetActive", (NativeBindings.V2)((obj, active) => { *(byte*)(obj + 0x1d0) = *(byte*)(obj + 0x1d1) = (byte)active; }));
            Set(Bindings, "CreateObject", (NativeBindings.V1)(wrapper => {
                Check(_widgets.All(w => !Get<bool>(w, "Active")), "previous target must be hidden before native allocation");
                Creates++; var obj = Alloc(0x200); *(nint*)obj = FailObject ? 0 : Module + 0x5a629f8;
                *(nint*)(wrapper + 0x18) = obj; *(ulong*)(wrapper + 0x10) = 1; *(byte*)(obj + 0x1d3) = 1;
            }));
            Set(Bindings, "CreateCanvas", (NativeBindings.V2)((obj, output) => { var p = Alloc(0x80); *(nint*)output = p; *(nint*)p = Module + 0x552a4d8; *(nint*)(p + 0x10) = obj; *(int*)(p + 0x40) = 1000; }));
            Set(Bindings, "CreateImage", (NativeBindings.P2)((_, wrapper) => { _pendingImage = Alloc(0x150); *(nint*)(wrapper + 0x18) = _pendingImage; *(nint*)_pendingImage = Module + 0x5ca0458; return wrapper; }));
            Set(Bindings, "AttachImage", (NativeBindings.V2)((obj, wrapper) => { *(nint*)(_pendingImage + 0x10) = obj; *(nint*)(wrapper + 0x18) = 0; }));
            Set(Bindings, "InitImage", (NativeBindings.B1)(image => {
                uint atlas = *(uint*)(image + 0x78), sprite = *(uint*)(image + 0x7c);
                if (FailHeadBind && atlas == NativeHeadAssets.Atlas) return 0;
                *(uint*)(image + 0x124) = atlas; *(uint*)(image + 0x128) = sprite; *(nint*)(image + 0xf8) = MaterialA; *(byte*)(image + 0x13a) = 1;
                if (RebindDuringInit && atlas == NativeHeadAssets.Atlas) *(nint*)(image + 0xf8) = *(nint*)(MaterialNode + 0x20) = MaterialB;
                var g = atlas == NativeHeadAssets.Atlas ? NativeHeadAssets.Glyph : PercentageText.Glyphs.ToArray().First(g => g.Hash == sprite);
                float[] uv = [g.U0, 1 - g.V0, g.U1, 1 - g.V1]; for (int i = 0; i < 4; i++) *(float*)(image + 0xa0 + i * 4) = uv[i];
                if (FailReacquire) *(int*)(Owner + 0x260) = 1;
                return 1;
            }));
            Set(Bindings, "ParentLink", (NativeBindings.V2)((_, _) => { }));
            Set(Bindings, "Insert", (NativeBindings.V4)((_, pair, _, wrapper) => { AddMap(*(nint*)(wrapper + 0x18)); *(nint*)(wrapper + 0x18) = 0; *(byte*)(pair + 8) = 1; }));
            Set(Bindings, "InsertOwnerRelease", (NativeBindings.V3)((_, pointer, _) => { PrivateReleases++; *(nint*)pointer = 0; }));
            Set(Bindings, "ReleaseImage", (NativeBindings.V3)((_, pointer, _) => { *(nint*)pointer = 0; }));
            Assets = (NativeHeadAssets)RuntimeHelpers.GetUninitializedObject(typeof(NativeHeadAssets));
            Set(Assets, "_b", Bindings); Set(Assets, "_log", (Action<string>)(_ => { })); Set(Assets, "_requested", 2);
            Hud = new(Bindings, _ => { }, Assets) { Enabled = true };
            Set(Hud, "_owner", Owner); Set(Hud, "_inService", true); Set(Hud, "_ownerLocked", true); Set(Hud, "_published", true);
            Set(Hud, "_position", (960f, 540f, 1f, -1f, 3840f, 2160f));
            Set(Hud, "_intactUv", new float[4]); Set(Hud, "_brokenUv", new float[4]);
        }
        static void FillGlyph(nint row, HudGlyph g, float height)
        {
            *(uint*)(row + 0x48) = g.Hash; *(float*)(row + 8) = g.Advance; *(float*)(row + 12) = height;
            float[] metrics = [g.PadLeft, g.PadTop, g.PadRight, g.PadBottom, g.U0, g.V0, g.U1, g.V1];
            for (int i = 0; i < metrics.Length; i++) *(float*)(row + 0x20 + i * 4) = metrics[i];
        }
        void AddMap(nint obj)
        {
            var node = Alloc(32); *(nint*)(node + 0x10) = obj; *(nint*)(node + 8) = _mapLast;
            _mapLast = node; if (_mapFirst == 0) _mapFirst = node;
            nint bucket = *(nint*)(Owner + 0x1f8); *(nint*)bucket = _mapFirst; *(nint*)(bucket + 8) = _mapLast;
        }
        public object AddWidget(int region)
        {
            uint atlas = region < 0 ? 0x2d5a242c : NativeHeadAssets.Atlas, sprite = region < 0 ? 0xfcb25e27 : NativeHeadAssets.Sprite;
            object w = Activator.CreateInstance(_widgetType, Fields, null, ["fixture", sprite, atlas, null, 0, 0, region], null)!;
            nint obj = Alloc(0x200), image = Alloc(0x150);
            Set(w, "Object", obj); Set(w, "Image", image); Set(w, "Material", MaterialA);
            Set(w, "Registered", true); Set(w, "Published", true); Set(w, "Active", true);
            *(nint*)(image + 0xf8) = MaterialA; *(uint*)(image + 0x124) = atlas; *(uint*)(image + 0x128) = sprite;
            _widgets.Add(w); var array = Array.CreateInstance(_widgetType, _widgets.Count); for (int i = 0; i < _widgets.Count; i++) array.SetValue(_widgets[i], i);
            Set(Hud, "_widgets", array); AddMap(obj); return w;
        }
        public nint Ready(long frame, long epoch)
            => Assets.Ready(frame, epoch);
        public void Dispose()
        {
            foreach (object w in Get<Array>(Hud, "_widgets")) if (Get<nint>(w, "Block") is var block && block != 0) NativeMemory.Free((void*)block);
            foreach (var p in _allocations) NativeMemory.Free((void*)p);
            VirtualFree(Module, 0, 0x8000);
        }
    }
    public static void Run()
    {
        var errors = new List<string>();
        void Case(string name, Action<Fixture> test) { using var f = new Fixture(); try { test(f); } catch (Exception e) { errors.Add(name + ": " + e.Message); } }
        Case("head follows ring with upward offset and mirrors picture only", f => {
            f.AddWidget(-1);
            foreach (float scale in new[] { .5f, 2f / 3 }) foreach (var (pixelX, pixelY) in new[] { (900f,400f),(1200f,750f) })
            {
                Set(f.Hud, "_position", (pixelX, pixelY, scale, -scale, 3840f, 2160f));
                f.Hud.Apply(Intact, Intact);
                var heads = Get<Array>(f.Hud, "_widgets").Cast<object>().Where(w => Get<int>(w, "Region") >= 0).ToArray();
                float minX=float.MaxValue, maxX=float.MinValue, minY=float.MaxValue, maxY=float.MinValue;
                foreach (var head in heads)
                {
                    nint obj=Get<nint>(head,"Object");
                    float x=*(float*)(obj+0x19c), y=*(float*)(obj+0x1a0), width=*(float*)(obj+0x1b4), height=*(float*)(obj+0x1b8);
                    minX=Math.Min(minX,x-width/2); maxX=Math.Max(maxX,x+width/2);
                    minY=Math.Min(minY,y-height/2); maxY=Math.Max(maxY,y+height/2);
                }
                Check(heads.Length == 3 && Math.Abs(((minX+maxX)/2+1920)*scale-pixelX)<.01 &&
                    Math.Abs((1080-(minY+maxY)/2)*scale-(pixelY-22.5f*scale))<.01,
                    "rendered head must follow the ring with11.25px/15px upward offset at1080p/1440p without accumulation");
                Check(Math.Abs(maxX-minX-103.488f)<.01 && Math.Abs(maxY-minY-103.488f)<.01,
                    "actual head widgets must render10% larger than the previous94.08native-unit picture");
                var displayedLeft = heads.Single(w => Get<int>(w,"Region")==0);
                nint leftImage = Get<nint>(displayedLeft,"Image");
                Check(*(float*)(leftImage+0xa0)==.6123046875f && *(float*)(leftImage+0xa8)==.30615234375f,
                    "displayed left crop must sample the mirrored source right horn");
            }
            f.Hud.Apply(Broken, Intact);
            var all = Get<Array>(f.Hud,"_widgets").Cast<object>().ToArray();
            var gray = all.Single(w => Get<int>(w,"Region")==0); var white = all.Single(w => Get<int>(w,"Region")==1);
            Check(*(uint*)(Get<nint>(gray,"Image")+0x110)==0x595959ff && *(uint*)(Get<nint>(white,"Image")+0x110)==0xffffffff,
                "picture mirroring must not exchange state data");
            Check(all.Where(w => Get<int>(w,"Slot")>0 && Get<int>(w,"Column")==0).All(w => !Get<bool>(w,"Active")) &&
                all.Where(w => Get<int>(w,"Slot")>0 && Get<int>(w,"Column")==1).All(w => Get<bool>(w,"Active")),
                "broken number hidden in its original slot; other percentage remains readable");
        });
        Case("all information inside ring estimate and single text restored", f => {
            f.AddWidget(-1); Set(f.Hud,"_position",(960f,540f,.5f,-.5f,3840f,2160f));
            for(int value=0;value<=100;value++)
            {
                var part=new PartDisplay(true,false,value/100d,value); f.Hud.Apply(part,part);
                foreach(object w in Get<Array>(f.Hud,"_widgets"))
                {
                    if(!Get<bool>(w,"Active")) continue;
                    nint obj=Get<nint>(w,"Object"); float x=*(float*)(obj+0x19c),y=*(float*)(obj+0x1a0),width=*(float*)(obj+0x1b4),height=*(float*)(obj+0x1b8);
                    foreach(float dx in new[]{-width/2,width/2}) foreach(float dy in new[]{-height/2,height/2})
                        Check((x+dx)*(x+dx)+(y+dy)*(y+dy)<=110*110,"head/numeric quad must fit the screenshot-based inner ring estimate at"+value);
                    if(Get<int>(w,"Slot")>0) Check(height<=26.401f,"Angra text maximum must enlarge to13.2logical pixels");
                }
            }
            f.Hud.Apply(new(true,false,.44,44));
            object singleDigit=Get<Array>(f.Hud,"_widgets").Cast<object>().Single(w=>Get<int>(w,"Column")==0&&Get<int>(w,"Slot")==1);
            Check(Get<bool>(singleDigit,"Active")&&Math.Abs(*(float*)(Get<nint>(singleDigit,"Object")+0x1b8)-40)<.001,
                "single-boss text must immediately regain its original size after reusing paired digit widgets");
        });
        Case("bounded readiness", f => {
            Check(f.Ready(0, 0) == f.MaterialA, "initial material identity");
            for (int frame = 0; frame < 60; frame++) Check(f.Ready(frame, 0) == f.MaterialA, "cached material stays ready");
            Check(f.StatusCalls == 1, "full validation/native readiness must run at most once per60 UI frames");
            *(nint*)(f.MaterialNode + 0x20) = f.MaterialB;
            Check(f.Ready(59, 0) == 0, "replacement must hide until it is validated");
            Check(f.Ready(60, 0) == f.MaterialB && f.StatusCalls == 2, "readable replacement with identical metadata gets its own readiness proof");
        });
        Case("late readiness", f => {
            f.StatusReady = false;
            for (int frame = 0; frame < 60; frame++) Check(f.Ready(frame, frame) == 0, "pending material hidden across resource epochs");
            Check(f.StatusCalls == 1, "changing epochs must not bypass pending retry interval");
            f.StatusReady = true; Check(f.Ready(60, 60) == f.MaterialA && f.StatusCalls == 2, "pending material recovers after native completion");
        });
        Case("replacement during binding", f => {
            f.AddWidget(-1); f.RebindDuringInit = true; f.Hud.Apply(Broken, Broken);
            var widgets = Get<Array>(f.Hud, "_widgets").Cast<object>().ToArray();
            Check(widgets.All(w => !Get<bool>(w, "Active")) && Get<bool>(Get<IndicatorRuntime>(f.Hud, "_runtime"), "_restartRequested"), "new head cannot display a material lacking the captured readiness proof");
        });
        foreach (bool rebound in new[] { false, true }) Case("material replacement/rebind=" + rebound, f => {
            var w = f.AddWidget(0); *(nint*)(f.MaterialNode + 0x20) = f.MaterialB;
            if (rebound) *(nint*)(Get<nint>(w, "Image") + 0xf8) = f.MaterialB;
            f.Hud.Apply(Broken, Broken);
            Check(!Get<bool>(w, "Active") && Get<bool>(Get<IndicatorRuntime>(f.Hud, "_runtime"), "_restartRequested"), "replacement must hide and schedule normal retirement");
            Check(f.Hud.Enabled && !Get<bool>(f.Hud, "_failed"), "head replacement must not disable all HUD");
        });
        foreach (bool pair in new[] { false, true }) Case("creation lock failure/pair=" + pair, f => {
            var old = f.AddWidget(pair ? -1 : 0); f.FailReacquire = true;
            f.Hud.Apply(pair ? Broken : Intact, pair ? Broken : null);
            Check(!Get<bool>(old, "Active"), "previous mode remains hidden after owner reacquire failure");
            Check(f.Creates > 0 && f.PrivateReleases == f.Creates, "all private allocations released exactly once");
            Check(f.Hud.Enabled && !Get<bool>(f.Hud, "_failed"), "temporary contention must not disable HUD");
        });
        Case("head-only bind failure", f => {
            var old = f.AddWidget(-1); f.FailHeadBind = true; f.Hud.Apply(Broken, Broken);
            Check(!Get<bool>(old, "Active") && f.PrivateReleases == 1, "failed private head cleaned; published single hidden");
            Check(f.Hud.Enabled && !Get<bool>(f.Hud, "_failed"), "head Image failure must leave single-boss HUD enabled");
            Check(Get<bool>(Get<IndicatorRuntime>(f.Hud, "_runtime"), "_restartRequested"), "published objects must retire through runtime");
            Set(f.Hud, "_ownerLocked", true); f.FailHeadBind = false; f.Hud.Apply(Broken);
            Check(Get<bool>(old, "Active"), "normal single-boss icon still renders after head-only failure");
            f.Hud.Apply(Broken, Broken); Check(!Get<bool>(old, "Active") && f.Creates == 1, "disabled head cannot retry allocations each frame");
        });
        Case("shared factory invariant", f => {
            f.AddWidget(-1); f.FailObject = true; f.Hud.Apply(Broken, Broken);
            Check(!f.Hud.Enabled && Get<bool>(f.Hud, "_failed"), "shared ownership corruption must still fail closed");
        });
        if (errors.Count != 0) throw new Exception(string.Join(Environment.NewLine, errors));
        Console.WriteLine("PASS: real Apply/native-delegate boundary: material replacement, bounded readiness, lock contention, isolated head failure and shared fail-closed ownership");
    }
}
