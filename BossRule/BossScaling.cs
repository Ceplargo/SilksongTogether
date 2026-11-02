using System;

namespace BossRule;

/// <summary>
/// Boss health from the host's boss settings: more for each extra player at the fight's start, or a fixed percent.
/// Phase switch points scale with the health, so phases come at the same share of it. Unity-free.
/// </summary>
internal static class BossScaling {
    /// <summary>
    /// <c>fixedPercent / 100</c> when it is above 0, otherwise <c>1 + perPlayerPercent / 100 * (players - 1)</c>;
    /// never below a tenth.
    /// </summary>
    public static float Multiplier(int playersAtStart, int perPlayerPercent, int fixedPercent) {
        var multiplier = fixedPercent > 0
            ? fixedPercent / 100f
            : 1f + perPlayerPercent / 100f * (playersAtStart - 1);
        return Math.Max(multiplier, 0.1f);
    }

    /// <summary>
    /// The base health times the multiplier, to the nearest whole number, at least 1.
    /// </summary>
    public static int ScaleHp(int baseHp, float multiplier) =>
        Math.Max(1, (int) Math.Round(baseHp * multiplier, MidpointRounding.AwayFromZero));

    public static float ScaleThreshold(float value, float multiplier) => value * multiplier;
}
