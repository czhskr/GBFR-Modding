namespace PartIndicator;

public sealed record PartSnapshot(nint SceneToken, nint ActorToken, uint ClassId, int SelectedId,
    bool LockVisible, double Current, double? Initial, int[] DestroyedIndices,
    bool InitialVerified = false, long Generation = 0, bool? CurrentBroken = null);
public readonly record struct PartDisplay(bool Visible, bool Broken, double? Fraction, int? WholePercentage = null);

public static class PartStatePolicy
{
    public static int CrocodilePartIndex(uint type, int selected) => type is 0x500 or 0x501 or 0x502
        ? selected switch { 2628 => 0, 9 => 1, 13 => 2, _ => -1 } : -1;

    // Selected lock IDs differ from both damage UIDs and state-array order.
    public static int PyetPartIndex(uint type, int selected) => type == 0x3100
        ? selected switch { 772 => 6, 800 => 1, 784 => 2, 2602 => 3, 2740 => 4, 934 => 5, 901 => 0, _ => -1 } : -1;

    public static int TayuitarLegIndex(uint type, int selected) => type == 0x1900
        ? selected switch { 787 => 2, 1043 => 3, 1296 => 4, 1552 => 5, _ => -1 } : -1;

    public static int FurycaneArmorIndex(uint type, int selected) => type is 0x7201 or 0x7211
        ? selected switch { 0x883 => 1, 0xc60 => 2, 0x613 => 3, 0xc0c => 4, 5 => 5, _ => -1 } : -1;

    public static PartDisplay Evaluate(PartSnapshot? s)
    {
        if (s is null || !s.LockVisible || s.SceneToken == 0 || s.ActorToken == 0 ||
            !double.IsFinite(s.Current) || s.Current < 0 || s.Current > 1e9 ||
            s.DestroyedIndices is null || s.DestroyedIndices.Length > 64 || s.DestroyedIndices.Any(x => x is < 0 or > 63)) return default;
        bool broken;
        int crocodileIndex = CrocodilePartIndex(s.ClassId, s.SelectedId);
        int pyetIndex = PyetPartIndex(s.ClassId, s.SelectedId);
        if (crocodileIndex >= 0 && s.Current == Math.Truncate(s.Current)) broken = s.DestroyedIndices.Contains(crocodileIndex);
        else if (s.ClassId is 0x1700 or >= 0x1800 and <= 0x1806 && s.SelectedId == 5) broken = s.DestroyedIndices.Contains(0);
        else if (((s.ClassId is 0x7200 or 0x7210 && s.SelectedId is 48 or 80 or 114 or 130 or 146 or 162) ||
                  FurycaneArmorIndex(s.ClassId, s.SelectedId) >= 0) &&
                 s.Current == Math.Truncate(s.Current) && s.CurrentBroken is bool currentBroken) broken = currentBroken;
        else if ((s.ClassId == 0x7100 && s.SelectedId is 9 or 13 || s.ClassId == 0x7101 && s.SelectedId is 3840 or 282) && s.CurrentBroken is bool armBroken) broken = armBroken;
        else if (s.ClassId == 0x7500 && s.SelectedId == 5 && s.Current == Math.Truncate(s.Current) && s.CurrentBroken is bool partBroken) broken = partBroken;
        else if (pyetIndex >= 0 && s.Current == Math.Truncate(s.Current) && s.CurrentBroken is bool pyetBroken) broken = pyetBroken;
        else if ((TayuitarLegIndex(s.ClassId, s.SelectedId) >= 0 || s.ClassId == 0x1900 && s.SelectedId == 7) &&
                 s.Current == Math.Truncate(s.Current) && s.CurrentBroken is bool tayuBroken) broken = tayuBroken;
        else if (s.ClassId == 0x7001 && s.SelectedId == 2048 && s.Current == Math.Truncate(s.Current) &&
                 s.CurrentBroken is bool protoBroken) broken = protoBroken;
        else return default;
        bool percentageClass = crocodileIndex >= 0 || pyetIndex >= 0 || (s.ClassId >= 0x1800 && s.ClassId <= 0x1806) ||
            s.ClassId == 0x1900 && s.SelectedId == 7 || s.ClassId == 0x7001 && s.SelectedId == 2048 ||
            s.ClassId is 0x1700 or 0x7200 or 0x7210 or 0x7201 or 0x7211 or 0x7100 or 0x7101 or 0x7500;
        double? fraction = !broken && percentageClass && s.InitialVerified && s.Initial is double initial &&
            double.IsFinite(initial) && initial > 0 && s.Current <= initial ? s.Current / initial : null;
        int? wholePercentage = fraction.HasValue && s.Initial is double denominator && denominator is >= 1 and <= 1e9
            ? (int)(100d * s.Current / denominator) : null;
        return new(true, broken, fraction, wholePercentage);
    }
}
