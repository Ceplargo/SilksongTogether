namespace BossRule;

/// <summary>What a following game does with one of its wave arenas for the scene host's (ArenaWaves).</summary>
internal enum WaveStep { None, Start, Wave }

/// <summary>
/// (coop) The step an ordinary wave arena takes in a game that does not run its room, for the wave the scene host's
/// arena reached. Unity-free, so it is tested without the game.
/// </summary>
internal static class ArenaWave {
    /// <summary>
    /// Start (the arena's own start: its gates shut) once the scene host's started and this one has not; then Wave (the
    /// next wave) while the waves this game started are behind the host's. Nothing once it ended here, or with no host
    /// wave yet (-1). kicked: the last wave this game started, or its own current wave, whichever is later.
    /// </summary>
    public static WaveStep For(bool started, bool completed, int kicked, int hostWave) =>
        completed || hostWave < 0 ? WaveStep.None
        : !started ? kicked < 0 ? WaveStep.Start : WaveStep.None
        : kicked < hostWave ? WaveStep.Wave : WaveStep.None;

    /// <summary>A wave message's text: the arena's path and the wave.</summary>
    public static string Message(string path, int wave) => $"{path}|{wave}";

    /// <summary>A wave message's arena path and wave, or null if it is not one.</summary>
    public static (string Path, int Wave)? Parse(string text) {
        var bar = text.LastIndexOf('|');
        return bar > 0 && int.TryParse(text.Substring(bar + 1), out var wave) ? (text.Substring(0, bar), wave) : null;
    }
}
