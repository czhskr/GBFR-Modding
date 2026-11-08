namespace PartIndicator;

public readonly record struct WorldIdentity(nint Player, nint Scene, nint Actor, uint HandleId = 0, ulong Stamp = 0)
{
    public bool Valid => Player != 0 && Scene != 0 && Actor != 0;
}
public interface IIndicatorHud
{
    bool HasObjects { get; }
    bool TryCreate(long generation);
    void Apply(PartDisplay display, PartDisplay? second = null);
    void Hide();
    void BeginRelease();
    bool PollRelease();
}

public sealed class IndicatorRuntime(IIndicatorHud hud)
{
    private WorldIdentity _world;
    private bool _releasing, _wasEnabled, _restartRequested;
    public long Generation { get; private set; }
    public bool Releasing => _releasing;
    public void RequestRestart() => _restartRequested = true;

    public void Tick(WorldIdentity world, bool ready, PartSnapshot? snapshot, bool enabled, PartSnapshot? second = null)
    {
        bool changed = world != _world || enabled && !_wasEnabled || _restartRequested;
        _restartRequested = false;
        _wasEnabled = enabled;
        if (changed) { _world = world; Generation++; }
        if (!_releasing && hud.HasObjects && (changed || !enabled || !world.Valid))
        {
            hud.BeginRelease(); _releasing = true; return;
        }
        if (_releasing)
        {
            if (hud.PollRelease()) _releasing = false;
            return; // A frame that completed deletion cannot publish its old snapshot again.
        }
        var display = ready && enabled && world.Valid && snapshot?.Generation == Generation ? PartStatePolicy.Evaluate(snapshot) : default;
        PartDisplay? secondDisplay = null;
        if (second is not null)
        {
            var other = PartStatePolicy.Evaluate(second);
            if (snapshot is null || snapshot.ClassId != 0x7500 || snapshot.SelectedId != 5 || second.ClassId != snapshot.ClassId ||
                second.SelectedId != snapshot.SelectedId || second.SceneToken != snapshot.SceneToken || second.ActorToken != snapshot.ActorToken ||
                second.Generation != Generation || !other.Visible) display = default;
            else secondDisplay = other;
        }
        if (!display.Visible) { if (hud.HasObjects) hud.Hide(); return; }
        if (!hud.HasObjects && !hud.TryCreate(Generation)) return;
        // Native row1 is the boss's right horn, shown on the frontal picture's left.
        if (secondDisplay is { } frontalLeft) hud.Apply(frontalLeft, display);
        else hud.Apply(display);
    }
}
