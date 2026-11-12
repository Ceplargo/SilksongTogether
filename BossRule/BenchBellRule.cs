namespace BossRule;

/// <summary>
/// (0.3.21) A bench bell's strike, as <see cref="BenchBells"/> sends and follows it. Unity-free, so BossRule.Tests
/// compiles it. Its interactive_bell FSM waits in Idle; a nail's hit (NAIL HIT) runs Get Dir, which reads the hit's
/// direction into Atk Dir (0: struck from the left, it swings right; 180: from the right, it swings left; any other, an
/// up or down hit: a random side, with a smaller force) and the rest of the strike in the same frame (probe_bell_dir).
/// </summary>
internal static class BenchBellRule {
    public const string Idle = "Idle";

    /// <summary>Whether this game's bell was just struck: it left Idle.</summary>
    public static bool Struck(string? before, string? now) => before == Idle && now != null && now != Idle;

    /// <summary>The Atk Dir another game's bell takes to swing the striker's way: the striker's own for a side hit, else
    /// the side its force took (a negative force swings left, as Atk Dir 180 does).</summary>
    public static float Side(float atkDir, float hitForce) => atkDir is 0f or 180f ? atkDir : hitForce < 0 ? 180f : 0f;

    /// <summary>Whether another game's strike rings this game's bell: only one waiting in Idle.</summary>
    public static bool Echoes(string? now) => now == Idle;
}
