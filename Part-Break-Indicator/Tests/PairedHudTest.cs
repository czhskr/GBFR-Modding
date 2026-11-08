using PartIndicator;

static class PairedHudTest
{
    public static void Run()
    {
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        var left = HudCoordinates.AngraRegion(0); var right = HudCoordinates.AngraRegion(1); var face = HudCoordinates.AngraRegion(2);
        Check(Math.Abs(left.X +25.872f)<.001 && Math.Abs(right.X-25.872f)<.001 && face.X == 0 &&
            Math.Abs(left.Width-51.744f)<.001 && Math.Abs(face.Width-103.488f)<.001,
            "head must enlarge10% from the previous ring-fit calibration");
        Check(Math.Abs(left.Y-left.Height/2-(face.Y+face.Height/2)) < .001 &&
            left.U1 == right.U0 && left.V0 == face.V1 && left.V1 == 0 && face.V0 == .6123046875f,
            "head crop regions must meet without gaps or upside-down UVs");
        Check(Math.Abs(left.Y + left.Height / 2 - 51.744f) < .001 && Math.Abs(face.Y - face.Height / 2 + 51.744f) < .001,
            "head picture bounds must remain centered around their common anchor");
        Check(left.U0 == .6123046875f && left.U1 == .30615234375f && right.U0 == .30615234375f && right.U1 == 0 &&
            face.U0 == .6123046875f && face.U1 == 0,
            "mirror the assembled source picture only, keeping display slots and independent state/number order");
        Check(Math.Abs(HudCoordinates.AngraText(0).Y+62.304f)<.001 && HudCoordinates.AngraText(0).Y == HudCoordinates.AngraText(1).Y &&
            Math.Abs(face.Y-face.Height/2-HudCoordinates.AngraText(0).Y-10.56f)<.001 &&
            Math.Abs(HudCoordinates.AngraText(0).X+39.6f)<.001 && Math.Abs(HudCoordinates.AngraText(1).X-39.6f)<.001,
            "numeric origin and gap must enlarge proportionally around the common anchor");
        foreach (bool a in new[] {false,true}) foreach (bool b in new[] {false,true})
        {
            Check(HudCoordinates.AngraTint(0,a).Packed == (a ? 0x595959ffu : 0xffffffffu) &&
                HudCoordinates.AngraTint(1,b).Packed == (b ? 0x595959ffu : 0xffffffffu) &&
                HudCoordinates.AngraTint(2,a).Packed == 0xffffffffu,
                "break states must gray only their own horn while the face remains white");
        }
        for (int value = 0; value <= 100; value++)
        {
            var text = PercentageText.Layout(value);
            float leftRight = HudCoordinates.AngraText(0).X + text.Max(q => (q.X + q.Width / 2) * 1.32f);
            float rightLeft = HudCoordinates.AngraText(1).X + text.Min(q => (q.X - q.Width / 2) * 1.32f);
            Check(leftRight < rightLeft, "independent percentages overlap at " + value);
        }
        var single = HudCoordinates.Icon(false, false, 0);
        Check(single.X == 0 && single.Y == 0 && Math.Abs(single.Width - 126.98734f) < .001 &&
            Math.Abs(single.Height - 138.53165f) < .001 && Math.Abs(HudCoordinates.TextTop(false, false) - 59.26582f) < .001,
            "single-part calibration regressed");

        var hud = new RecordingHud(); var runtime = new IndicatorRuntime(hud); var world = new WorldIdentity(1, 2, 3);
        var first = new PartSnapshot(4, 5, 0x7500, 5, true, 800, 800, [], true, 1, false);
        var second = first with { Current = 0, CurrentBroken = true };
        runtime.Tick(world, true, first, true, second);
        Check(hud.Applied == 1 && hud.First.Broken && PercentageText.Percentage(hud.First) == null && hud.Second is { Broken: false } &&
            PercentageText.Percentage(hud.Second.Value) == 100,
            "observed native row1/boss-right break must disable picture-left and hide its number while picture-right stays100%");
        runtime.Tick(world, true, new(4, 5, 0x501, 9, true, 1200, 1200, [], true, 1), true);
        Check(hud.Applied == 2 && hud.Second == null && PercentageText.Percentage(hud.First) == 100,
            "paired-to-single transition retained another horn");
        foreach (var invalid in new[] { second with { Generation = 2 }, second with { ActorToken = 6 },
            second with { SceneToken = 8 }, second with { SelectedId = 526 }, second with { Current = -1 }, second with { LockVisible = false } })
            runtime.Tick(world, true, first, true, invalid);
        Check(hud.Applied == 2 && hud.Hidden == 6, "invalid second horn allowed a partial/foreign pair");
        runtime.Tick(world, true, first with { Current = 601 }, true, second with { Current = 800, CurrentBroken = false });
        Check(hud.Applied == 3 && hud.First is { Broken: false, WholePercentage: 100 } && hud.Second is { WholePercentage: 75 } && hud.Created == 1,
            "frontal ordering must preserve each horn's own percentage after a single-part or hidden frame");
        runtime.Tick(world, true, first with { Current = 0, CurrentBroken = true }, true, second with { Current = 800, CurrentBroken = false });
        Check(hud.Applied == 4 && PercentageText.Percentage(hud.First)==100 && hud.Second is { Broken:true } &&
            PercentageText.Percentage(hud.Second.Value)==null,"native row0/boss-left break must disable picture-right only");
        runtime.Tick(world, true, first with { Current = 600, Initial = null, InitialVerified = false }, true, second with { Current = 800, CurrentBroken = false });
        Check(hud.Applied == 5 && PercentageText.Percentage(hud.First)==100 && hud.Second is { Broken:false,Fraction:null },
            "unknown initial proof must move with its own frontal slot, without borrowing the other horn's percentage");
        runtime.Tick(world with { Stamp = 1 }, true, first, true, second);
        Check(hud.Releases == 1 && hud.Applied == 5, "world transition reused paired UI before retirement");
        Console.WriteLine("PASS: calibrated head/UV/tint, independent0..100% slots, observed frontal horn mapping and single/pair retirement guards");
    }
    private sealed class RecordingHud : IIndicatorHud
    {
        public int Created, Applied, Hidden, Releases;
        public PartDisplay First; public PartDisplay? Second;
        public bool HasObjects { get; private set; }
        public bool TryCreate(long generation) { Created++; return HasObjects = true; }
        public void Apply(PartDisplay display, PartDisplay? second = null) { Applied++; First = display; Second = second; }
        public void Hide() { Hidden++; }
        public void BeginRelease() { Releases++; }
        public bool PollRelease() => false;
    }
}
