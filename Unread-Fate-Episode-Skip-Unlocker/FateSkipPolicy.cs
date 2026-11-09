namespace FateSkip;

public static class FateSkipPolicy
{
    public static bool IsTarget(uint conditionId) => conditionId is 0x4EA41E80 or 0x635542C2;
    public static bool IsValidFate(uint id) => id is not (0 or uint.MaxValue or 0x887AE0B0);

    public static bool ShouldOverride(bool enabled, int original, uint conditionId,
        uint state, byte invert, uint fateId, byte? flags) =>
        enabled && original == 3 && IsTarget(conditionId) && state == 0 && invert == 0 &&
        IsValidFate(fateId) && flags.HasValue && (flags.Value & 2) == 0;
}
