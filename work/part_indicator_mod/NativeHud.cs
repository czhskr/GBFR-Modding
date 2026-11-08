using System.Runtime.InteropServices;

namespace PartIndicator;

public sealed unsafe class NativeHud : IIndicatorHud, IGameEvents
{
    private sealed class Widget(string id, uint sprite, uint atlas = 0x2d5a242c, HudGlyph? glyph = null, int column = 0, int slot = 0, int region = -1)
    {
        public readonly string Id = id;
        public readonly uint Sprite = sprite;
        public readonly uint Atlas = atlas;
        public readonly HudGlyph? Glyph = glyph;
        public readonly int Column = column, Slot = slot;
        public readonly int Region = region;
        public nint Block, Object, Canvas, Image, Material;
        public nint Wrapper => Block;
        public nint ImageWrapper => Block + 32;
        public nint OutCanvas => Block + 64;
        public nint Pair => Block + 72;
        public nint Key => Block + 88;
        public bool Registered, Published, Linked, Dead, Active;
        public int ObjectDtor, CanvasDtor, ImageDtor;
        public long LastImageFrame;
    }
    private sealed record Token(Widget? Widget = null, nint Pointer = 0, bool Tracked = false);
    private sealed record Source(long Frame, long Epoch, nint Object, nint Root, bool Visible);
    private sealed class HeadImageException(Exception inner) : IOException("Head image binding: " + inner.Message, inner);
    private readonly NativeBindings _b;
    private readonly NativeStateReader _reader;
    private readonly IndicatorRuntime _runtime;
    private readonly Action<string> _log;
    private readonly NativeHeadAssets? _headAssets;
    private readonly object _gate = new();
    private readonly Dictionary<nint, Widget> _commands = [];
    private Widget[] _widgets = [];
    private nint _owner, _manager, _graphics, _parentTable, _parent;
    private bool _ownerLocked, _inService, _published, _failed, _releaseRequested;
    private bool _headDisabled;
    private int _uiActive, _renderActive, _resourceActive, _destructorsActive, _consumersActive;
    private long _uiFrame, _renderFrame, _resourceEpoch, _uiEpoch = -1, _hideUi, _hideRender;
    private Source? _source;
    private (float X, float Y, float Sx, float Sy, float Width, float Height)? _position;
    private float[] _intactUv = [], _brokenUv = [];
    private long _nextGlyphCheck, _glyphCheckEpoch = -1;
    private readonly int?[] _percentage = [null, null];
    private readonly GlyphPlacement[][] _textLayout = [[], []];
    private int _enabled;
    private long _enableRevision, _seenEnableRevision;
    public bool Enabled
    {
        get => Volatile.Read(ref _enabled) != 0;
        set { int next = value ? 1 : 0; if (Interlocked.Exchange(ref _enabled, next) != next) Interlocked.Increment(ref _enableRevision); }
    }
    public bool HasObjects => _widgets.Length != 0;
    public long Generation => _runtime.Generation;
    private nint B(int rva) => _b.ModuleBase + rva;
    private static T R<T>(nint p) where T : unmanaged => ProcessMemory.Read<T>(p);
    private static void W<T>(nint p, T value) where T : unmanaged => *(T*)p = value;
    private static void Require(bool condition, string message) { if (!condition) throw new IOException(message); }
    public NativeHud(NativeBindings bindings, Action<string> log, NativeHeadAssets? headAssets = null)
    { _b = bindings; _log = log; _headAssets = headAssets; _reader = new(bindings.ModuleBase); _runtime = new(this); }

    public void Failed(Exception error)
    {
        Enabled = false;
        if (_failed) return;
        _failed = true;
        try { _log("HUD disabled; cleanup requested: " + error.Message); } catch { }
    }
    public void Service()
    {
        if (!Monitor.TryEnter(_gate)) return;
        bool resourceLocked = false, entered = false;
        try
        {
            if (_inService || _uiActive != 0 || _renderActive != 0 || _resourceActive != 0 || _destructorsActive != 0) return;
            _inService = true; entered = true;
            if (!BindContext()) return;
            Require(R<uint>(_manager + 0x8e8) == 0x102, "resource mutex type");
            int result = _b.TryLock(_manager + 0x8e8);
            if (result == 3) return;
            Require(result == 0, "native try-lock return"); resourceLocked = true;
            if (!EnterOwner()) return;
            long revision = Volatile.Read(ref _enableRevision);
            if (revision != _seenEnableRevision) { _runtime.RequestRestart(); _seenEnableRevision = revision; }
            if (!_reader.TryReadWorld(out var world)) world = default;
            bool ready = CapturePosition();
            PartSnapshot? snapshot = null, second = null;
            if (ready) _reader.TryReadSnapshots(world, _runtime.Generation, out snapshot, out second);
            _runtime.Tick(world, ready, snapshot, Enabled && !_failed, second);
        }
        catch (Exception error) { Failed(error); }
        finally
        {
            try
            {
                if (entered) { ExitOwner(); if (resourceLocked) _b.Unlock(_manager + 0x8e8); _inService = false; }
            }
            finally { Monitor.Exit(_gate); }
        }
    }
    private bool BindContext()
    {
        nint owner = R<nint>(B(0x7c01858)), manager = R<nint>(B(0x7ab3bf8)), graphics = R<nint>(B(0x7c48c60));
        if (owner == 0 || manager == 0 || graphics == 0) return false;
        nint table = R<nint>(owner + 0x2358); if (table == 0) return false;
        nint parent = R<nint>(table); if (parent == 0) return false;
        if (HasObjects && (_owner != owner || _manager != manager || _graphics != graphics || _parentTable != table || _parent != parent))
        {
            // Never dereference or delete an old generation through a replacement manager.
            if (_widgets.All(w => w.Dead && w.ObjectDtor == 1 && w.CanvasDtor == 1 && w.ImageDtor == 1) && _commands.Count == 0 && _consumersActive == 0)
                FreeWrappers();
            else { Failed(new IOException("Native UI owner changed before retirement completed")); return false; }
        }
        if (R<nint>(owner) != B(0x5a629f8) || R<uint>(parent + 0x1c4) != 0x8807c022 || R<byte>(parent + 0x1d1) != 1) return false;
        _owner = owner; _manager = manager; _graphics = graphics; _parentTable = table; _parent = parent;
        return true;
    }
    private bool EnterOwner()
    {
        if (_ownerLocked) return true;
        _ownerLocked = Interlocked.CompareExchange(ref *(int*)(_owner + 0x260), 1, 0) == 0;
        return _ownerLocked;
    }
    private void ExitOwner() { if (_ownerLocked) { Volatile.Write(ref *(int*)(_owner + 0x260), 0); _ownerLocked = false; } }
    private bool MapContains(nint map, nint value)
    {
        ulong hash = 0xcbf29ce484222325, key = (ulong)value;
        for (int i = 0; i < 8; i++) { hash = unchecked((hash ^ (key & 255)) * 0x100000001b3); key >>= 8; }
        ulong index = hash & R<ulong>(map + 0x30); Require(index < 1048576, "map bucket bound");
        nint sentinel = R<nint>(map + 8), bucket = R<nint>(map + 0x18) + checked((int)index * 16);
        nint first = R<nint>(bucket), node = R<nint>(bucket + 8);
        for (int i = 0; node != 0 && node != sentinel && i < 128; i++)
        {
            if (R<nint>(node + 0x10) == value) return true;
            if (node == first) return false;
            node = R<nint>(node + 8);
        }
        Require(node == 0 || node == sentinel, "map chain bound"); return false;
    }
    private bool InCanvas(Widget w)
    {
        nint begin = R<nint>(_owner + 0x2f8), end = R<nint>(_owner + 0x300); long bytes = end - begin;
        Require(bytes >= 0 && bytes <= 65536 * 16 && bytes % 16 == 0, "Canvas vector bound");
        for (int i = 0; i < bytes; i += 16) if (R<nint>(begin + i) == w.Object) return true;
        return false;
    }
    private bool CapturePosition()
    {
        _position = null;
        var s = _source;
        if (s is null || s.Frame != _uiFrame || s.Epoch != _resourceEpoch || !s.Visible || _resourceActive != 0) return false;
        if (!MapContains(_owner + 0x1e0, s.Object) || !MapContains(_owner + 0x1e0, s.Root)) return false;
        if (R<nint>(s.Object) != B(0x5a629f8) || R<nint>(s.Root) != B(0x5a629f8) || R<byte>(s.Object + 0x1d4) != 0 || R<byte>(s.Root + 0x1d4) != 0 ||
            R<byte>(s.Object + 0x1d1) != 1 || R<byte>(s.Root + 0x1d1) != 1 || R<byte>(s.Root + 0x1d0) != 1) return false;
        float x = R<float>(s.Root + 0x150), y = R<float>(s.Root + 0x154), sx = R<float>(_owner + 0x17c), sy = R<float>(_owner + 0x180), width = R<float>(_owner + 0x1bc), height = R<float>(_owner + 0x1c0);
        if (!new[] { x, y, sx, sy, width, height }.All(float.IsFinite) || sx == 0 || sy == 0 || width <= 0 || height <= 0) return false;
        _position = (x, y, sx, sy, width, height); return true;
    }
    private void VerifyAssets()
    {
        nint sentinel = R<nint>(B(0x73b9e98)), buckets = R<nint>(B(0x73b9ea8)); uint mask = R<uint>(B(0x73b9ec0));
        Require(sentinel != 0 && buckets != 0 && mask < 1048576, "atlas bounds");
        nint bucket = buckets + checked((int)(0x2d5a242cu & mask) * 16), first = R<nint>(bucket), node = R<nint>(bucket + 8);
        bool found = false;
        for (int i = 0; i < 64 && node != 0 && node != sentinel; i++)
        {
            if (R<uint>(node + 0x10) == 0x2d5a242c) { found = true; break; }
            if (node == first) break; node = R<nint>(node + 8);
        }
        Require(found, "common atlas missing"); nint begin = R<nint>(node + 0x20);
        Require(R<nint>(node + 0x28) - begin == 347 * 0x50, "common atlas size");
        float[] ReadUv(int index, float[] expected)
        {
            var row = begin + index * 0x50; float[] uv = [R<float>(row + 0x30), R<float>(row + 0x34), R<float>(row + 0x38), R<float>(row + 0x3c)];
            Require(uv.Zip(expected).All(x => Math.Abs(x.First - x.Second) < 1e-7), "sprite UV mismatch");
            return [uv[0], 1 - uv[1], uv[2], 1 - uv[3]];
        }
        _intactUv = ReadUv(33, [.67236328125f, .7890625f, .70458984375f, .82421875f]);
        _brokenUv = ReadUv(32, [.837890625f, .17822265625f, .87646484375f, .21533203125f]);
        Require(R<uint>(begin + 32 * 0x50 + 0x48) == 0xfcb25e27 && R<nint>(node + 0x20) == begin, "atlas snapshot changed");
    }
    public bool TryCreate(long generation)
    {
        Require(_inService && _ownerLocked && !HasObjects, "creation context");
        if (_failed || _position is null || _releaseRequested) return false;
        VerifyAssets();
        _nextGlyphCheck = 0; _glyphCheckEpoch = -1;
        bool created = PublishWidgets([new("icon", 0xfcb25e27)]);
        if (created) _log($"UI icon created: generation {generation}");
        return created;
    }
    private bool PublishWidgets(Widget[] additions)
    {
        Require(_inService && _ownerLocked && !_releaseRequested, "widget creation context");
        uint index = R<uint>(B(0x70111d8)); Require(index < 64, "TLS index");
        nint tls = _b.TlsArray(), slot = R<nint>(tls + checked((int)index * 8)), allocator = R<nint>(slot + 0x1630);
        Require(new[] { 0x1e8, 0x58, 0x148 }.All(o => R<nint>(allocator + o) != 0), "UI thread TLS pools");
        Hide(); // Allocation releases the owner lock; no previous target may remain visible if reacquisition fails.
        ExitOwner();
        try
        {
            foreach (var w in additions) CreatePrivate(w);
            if (!EnterOwner()) { ReleasePrivate(); return false; }
            foreach (var w in additions)
            {
                _b.ParentLink(w.Object, _parent); w.Linked = true;
                Require(R<int>(w.Canvas + 0x40) == 1000, "Canvas sort"); W(w.Key, w.Object);
                _b.Insert(_owner + 0x1e0, w.Pair, w.Key, w.Wrapper);
                w.Registered = R<nint>(w.Wrapper + 0x18) == 0; w.Published = w.Registered;
                Require(w.Registered && R<byte>(w.Pair + 8) == 1 && MapContains(_owner + 0x1e0, w.Object), "UI publication");
            }
            _published = true; return true;
        }
        catch (HeadImageException error)
        {
            ReleasePrivate(); _headDisabled = true; _runtime.RequestRestart();
            try { _log("Angra head disabled; published UI retirement requested: " + error.Message); } catch { }
            return false;
        }
        catch (Exception error) { Failed(error); ReleasePrivate(); return false; }
    }
    private void CreatePrivate(Widget w)
    {
        w.Block = (nint)NativeMemory.AllocZeroed(96); if (w.Block == 0) throw new OutOfMemoryException();
        _widgets = [.. _widgets, w]; // Track only allocated widgets before native callbacks can observe them.
        _b.CreateObject(w.Wrapper); w.Object = R<nint>(w.Wrapper + 0x18);
        Require(R<nint>(w.Object) == B(0x5a629f8) && R<ulong>(w.Wrapper + 0x10) == 1 && R<byte>(w.Object + 0x1d3) == 1, "Object factory");
        W<byte>(w.Object + 0x1d0, 0); W<byte>(w.Object + 0x1d1, 0); W(w.Object + 0x100, w.Object); W(w.Object + 0x110, w.Object);
        foreach (int o in new[] { 0x194, 0x198, 0x1a4, 0x1a8, 0x1ac, 0x1b0 }) W(w.Object + o, .5f);
        W(w.Object + 0x1b4, 120f); W(w.Object + 0x1b8, 120f); W(w.Object + 0x1bc, 120f); W(w.Object + 0x1c0, 120f);
        _b.CreateCanvas(w.Object, w.OutCanvas); w.Canvas = R<nint>(w.OutCanvas);
        Require(R<nint>(w.Canvas) == B(0x552a4d8) && R<nint>(w.Canvas + 0x10) == w.Object, "Canvas factory");
        Require(_b.CreateImage(0, w.ImageWrapper) == w.ImageWrapper, "Image factory return"); w.Image = R<nint>(w.ImageWrapper + 0x18);
        Require(R<nint>(w.Image) == B(0x5ca0458), "Image type"); W(w.Image + 0x78, w.Atlas); W(w.Image + 0x7c, w.Sprite);
        for (int i = 0; i < 4; i++) W(w.Image + 0x80 + i * 4, 1f);
        _b.AttachImage(w.Object, w.ImageWrapper); Require(R<nint>(w.ImageWrapper + 0x18) == 0 && R<nint>(w.Image + 0x10) == w.Object, "Image owner move");
        try
        {
            Require(_b.InitImage(w.Image) == 1 && R<uint>(w.Image + 0x124) == w.Atlas && R<uint>(w.Image + 0x128) == w.Sprite, "Image bind");
            w.Material = R<nint>(w.Image + 0xf8); Require(w.Material != 0, "Image material");
            if (w.Glyph is { } g)
            {
                float[] uv = [R<float>(w.Image + 0xa0), R<float>(w.Image + 0xa4), R<float>(w.Image + 0xa8), R<float>(w.Image + 0xac)];
                float[] expected = [g.U0, 1 - g.V0, g.U1, 1 - g.V1];
                Require(uv.Zip(expected).All(x => Math.Abs(x.First - x.Second) < 1e-7), "original glyph bind UV");
            }
            foreach (int o in new[] { 0xd0, 0xd4, 0xd8, 0xdc }) { Require(float.IsFinite(R<float>(w.Image + o)), "sprite padding"); W(w.Image + o, 0f); }
            Require(R<byte>(w.Image + 0x13a) == 1 && R<byte>(w.Image + 0x130) == 0 && R<nint>(w.Image + 0x100) == 0, "plain Image render path");
        }
        catch (IOException error) when (w.Region >= 0) { throw new HeadImageException(error); }
    }
    private void ReleasePrivate()
    {
        foreach (var w in _widgets.Where(w => w.Block != 0 && !w.Published && !w.Dead))
        {
            if (R<nint>(w.ImageWrapper + 0x18) != 0) _b.ReleaseImage(w.ImageWrapper + 8, w.ImageWrapper + 0x18, w.ImageWrapper + 0x10);
            if (R<nint>(w.Wrapper + 0x18) != 0)
            { if (w.Linked) _b.ParentLink(w.Object, 0); _b.InsertOwnerRelease(w.Wrapper + 8, w.Wrapper + 0x18, w.Wrapper + 0x10); }
            w.Dead = true;
        }
        foreach (var w in _widgets.Where(w => !w.Published)) NativeMemory.Free((void*)w.Block);
        _widgets = _widgets.Where(w => w.Published).ToArray();
        if (!HasObjects) { _published = false; _position = null; }
    }
    public void Apply(PartDisplay display, PartDisplay? second = null)
    {
        Require(_inService && _ownerLocked && _published && !_releaseRequested, "apply context");
        if (!display.Visible || second is { Visible: false } || _position is not { } p) { Hide(); return; }
        bool paired = second.HasValue;
        if (_widgets.Any(w => w.Region >= 0) &&
            (!HudAtlas.TryMaterial(_b.ModuleBase, NativeHeadAssets.Atlas, out var currentMaterial) ||
             _widgets.Any(w => w.Region >= 0 && (w.Material != currentMaterial || R<nint>(w.Image + 0xf8) != currentMaterial))))
        { Hide(); _runtime.RequestRestart(); return; }
        if (!HudCoordinates.TryConvert(p.X, p.Y, p.Sx, p.Sy, p.Width, p.Height, out float x, out float y)) { Hide(); return; }
        if (paired)
        {
            y += 22.5f; // 15px upward at1440p;11.25logical pixels at1080p.
            if (_headDisabled || _headAssets?.Ready(_uiFrame, _resourceEpoch) is not { } headMaterial || headMaterial == 0) { Hide(); return; }
            if (!_widgets.Any(w => w.Region >= 0) && !PublishWidgets([
                new("head.left", NativeHeadAssets.Sprite, NativeHeadAssets.Atlas, NativeHeadAssets.Glyph, region: 0),
                new("head.right", NativeHeadAssets.Sprite, NativeHeadAssets.Atlas, NativeHeadAssets.Glyph, column: 1, region: 1),
                new("head.face", NativeHeadAssets.Sprite, NativeHeadAssets.Atlas, NativeHeadAssets.Glyph, region: 2)])) return;
            if (_widgets.Any(w => w.Region >= 0 && (w.Material != headMaterial || R<nint>(w.Image + 0xf8) != headMaterial)))
            { Hide(); _runtime.RequestRestart(); return; }
        }
        PartDisplay[] parts = [display, second.GetValueOrDefault()];
        var missing = new List<Widget>();
        for (int column = 0; column < parts.Length; column++)
        {
            int? percentage = PercentageText.Percentage(parts[column]);
            if (percentage != _percentage[column])
            {
                _percentage[column] = percentage;
                _textLayout[column] = percentage is { } value ? PercentageText.Layout(value) : [];
            }
            if (!percentage.HasValue || _widgets.Any(w => w.Column == column && w.Slot == 1)) continue;
            var zero = PercentageText.Glyphs[0]; var percent = PercentageText.Glyphs[10];
            for (int slot = 1; slot <= 4; slot++)
            {
                var glyph = slot == 4 ? percent : zero;
                missing.Add(new($"text{column}.{slot}", glyph.Hash, PercentageText.Atlas, glyph, column, slot));
            }
        }
        if (missing.Count != 0 && (_resourceEpoch != _glyphCheckEpoch || _uiFrame >= _nextGlyphCheck))
        {
            _glyphCheckEpoch = _resourceEpoch; _nextGlyphCheck = _uiFrame + 60;
            if (HudAtlas.TryValidate(_b.ModuleBase, PercentageText.Atlas, PercentageText.Glyphs))
            {
                if (!PublishWidgets(missing.ToArray())) return;
                _log($"Percentage glyphs created: generation {Generation}");
            }
        }
        foreach (var w in _widgets)
        {
            Require(!w.Dead && MapContains(_owner + 0x1e0, w.Object), "owned UI lifetime");
            Require(R<nint>(w.Image + 0xf8) == w.Material && R<uint>(w.Image + 0x124) == w.Atlas && R<uint>(w.Image + 0x128) == w.Sprite, "owned material changed");
            var part = parts[w.Column]; var layout = _textLayout[w.Column];
            bool active = w.Region >= 0 ? paired : part.Visible && (w.Slot == 0 ? !paired :
                layout.Length > 0 && (w.Slot == 4 || w.Slot < layout.Length));
            if (active)
            {
                var icon = HudCoordinates.Icon(part.Broken, false, 0);
                if (w.Region >= 0)
                {
                    var region = HudCoordinates.AngraRegion(w.Region); var tint = HudCoordinates.AngraTint(w.Region, part.Broken);
                    W(w.Object + 0x19c, x + region.X); W(w.Object + 0x1a0, y + region.Y);
                    W(w.Object + 0x1b4, region.Width); W(w.Object + 0x1b8, region.Height);
                    float[] uv = [region.U0, region.V0, region.U1, region.V1];
                    for (int i = 0; i < 4; i++) W(w.Image + 0xa0 + i * 4, uv[i]);
                    for (int i = 0; i < 3; i++) W(w.Image + 0x80 + i * 4, tint.Level);
                    W(w.Image + 0x110, tint.Packed);
                }
                else if (w.Slot == 0)
                {
                    W(w.Object + 0x19c, x + icon.X); W(w.Object + 0x1a0, y + icon.Y);
                    W(w.Object + 0x1b4, icon.Width); W(w.Object + 0x1b8, icon.Height);
                    var uv = part.Broken ? _brokenUv : _intactUv;
                    for (int i = 0; i < 4; i++) W(w.Image + 0xa0 + i * 4, uv[i]);
                }
                else
                {
                    var q = layout[w.Slot == 4 ? ^1 : w.Slot - 1]; var g = q.Glyph;
                    var origin = paired ? HudCoordinates.AngraText(w.Column) : (X: icon.X, Y: -HudCoordinates.TextTop(part.Broken, false));
                    float factor = paired ? 2 * HudCoordinates.AngraContentScale : 2;
                    W(w.Object + 0x19c, x + origin.X + q.X * factor);
                    W(w.Object + 0x1a0, y + origin.Y - q.Y * factor);
                    W(w.Object + 0x1b4, q.Width * factor); W(w.Object + 0x1b8, q.Height * factor);
                    float[] uv = [g.U0, 1 - g.V0, g.U1, 1 - g.V1];
                    for (int i = 0; i < 4; i++) W(w.Image + 0xa0 + i * 4, uv[i]);
                }
            }
            if (w.Active != active) { _b.SetActive(w.Object, active ? 1 : 0); w.Active = active; }
        }
        W<byte>(_owner + 0x2370, 1);
    }
    public void Hide()
    {
        Require(_inService && _ownerLocked, "hide context");
        foreach (var w in _widgets.Where(w => w.Registered && !w.Dead && w.Active))
        { Require(MapContains(_owner + 0x1e0, w.Object), "owned hide lifetime"); _b.SetActive(w.Object, 0); w.Active = false; }
        W<byte>(_owner + 0x2370, 1);
    }
    public void BeginRelease()
    {
        Hide(); ReleasePrivate(); _releaseRequested = true; _hideUi = _uiFrame; _hideRender = _renderFrame;
    }
    public bool PollRelease()
    {
        Require(_inService && _ownerLocked, "release context");
        if (!HasObjects) { _releaseRequested = false; return true; }
        if (_uiFrame - _hideUi < 3 || _renderFrame - _hideRender < 3 || _commands.Count != 0 || _consumersActive != 0) return false;
        foreach (var w in _widgets.Where(w => !w.Dead))
        {
            if (_uiFrame - w.LastImageFrame < 3 || InCanvas(w) || MapContains(_graphics + 0x298, w.Object) || MapContains(_graphics + 0x478, w.Object)) return false;
            Require(R<byte>(w.Object + 0x1d0) == 0 && R<byte>(w.Object + 0x1d1) == 0, "retired object still active");
        }
        if (_widgets.All(w => w.Dead))
        {
            Require(_widgets.Where(w => w.Published).All(w => w.ObjectDtor == 1 && w.CanvasDtor == 1 && w.ImageDtor == 1 && !MapContains(_owner + 0x1e0, w.Object)), "native cleanup proof");
            FreeWrappers(); _releaseRequested = false; _log("UI retirement completed"); return true;
        }
        ExitOwner();
        foreach (var w in _widgets.Where(w => !w.Dead && w.Registered)) { w.Registered = false; _b.Delete(w.Object, 1); }
        EnterOwner(); return false;
    }
    private void FreeWrappers()
    {
        foreach (var w in _widgets) if (w.Block != 0) NativeMemory.Free((void*)w.Block);
        _widgets = []; _published = false; _position = null;
    }

    public object? Enter(GameEvent kind, nint a, nint b, nint c, nint d)
    {
        lock (_gate)
        {
            if (kind == GameEvent.Ui) { _uiActive++; _source = null; _uiEpoch = _resourceActive == 0 ? _resourceEpoch : -1; }
            if (kind == GameEvent.Render) _renderActive++;
            if (kind == GameEvent.Resource)
            {
                bool tracked = a == R<nint>(B(0x7ab3bf8)); if (tracked) { _resourceActive++; _resourceEpoch++; }
                return new Token(Tracked: tracked);
            }
            if (kind == GameEvent.ObjectDestroy && _source is { } s && (a == s.Object || a == s.Root)) _source = null;
            Widget? w = kind switch
            {
                GameEvent.ObjectDestroy => _widgets.FirstOrDefault(w => !w.Dead && a == w.Object),
                GameEvent.CanvasDelete => _widgets.FirstOrDefault(w => !w.Dead && ProcessMemory.TryRead<nint>(b, out var value) && value == w.Canvas),
                GameEvent.ImageDestroy or GameEvent.ImageCommand => _widgets.FirstOrDefault(w => !w.Dead && a == w.Image),
                GameEvent.ImageRender => _widgets.FirstOrDefault(w => !w.Dead && a == w.Image + 0x20),
                _ => null
            };
            if (w != null && kind == GameEvent.ImageRender) w.LastImageFrame = _uiFrame;
            if (w != null && kind == GameEvent.ObjectDestroy) _destructorsActive++;
            if (kind == GameEvent.Consume && _commands.TryGetValue(d, out var commandWidget))
            { _consumersActive++; return new Token(commandWidget, d, true); }
            return w == null || kind == GameEvent.ImageRender ? null : new Token(w, b);
        }
    }
    public void Leave(GameEvent kind, object? token, nint a, nint b, nint c, nint d)
    {
        lock (_gate)
        {
            var t = token as Token;
            if (kind == GameEvent.Ui) { _uiActive--; _uiFrame++; }
            if (kind == GameEvent.Render) { _renderActive--; _renderFrame++; }
            if (kind == GameEvent.Resource && t?.Tracked == true) _resourceActive--;
            if (kind == GameEvent.Controller && R<nint>(a) == B(0x5a75a28))
            {
                nint obj = R<nint>(a + 0x10), root = R<nint>(a + 0x1a8); int state = R<int>(a + 0x188);
                _source = new(_uiFrame + 1, _uiEpoch, obj, root, R<byte>(a + 0x1c0) == 1 && state is 1 or 2);
            }
            if (t?.Widget is not { } w) return;
            if (kind == GameEvent.ObjectDestroy) { w.ObjectDtor++; w.Dead = true; _destructorsActive--; }
            if (kind == GameEvent.CanvasDelete) w.CanvasDtor++;
            if (kind == GameEvent.ImageDestroy) w.ImageDtor++;
            if (kind == GameEvent.ImageCommand)
            {
                nint command = R<nint>(t.Pointer + 0x10);
                Require(R<nint>(command + 0x290) == 0 && !_commands.ContainsKey(command), "plain command lifetime");
                _commands.Add(command, w);
            }
            if (kind == GameEvent.Consume && t.Tracked)
            { Require(_commands.Remove(t.Pointer, out var expected) && expected == w, "command generation changed"); _consumersActive--; }
        }
    }
}
