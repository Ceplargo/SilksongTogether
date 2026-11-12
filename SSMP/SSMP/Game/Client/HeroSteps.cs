namespace SSMP.Game.Client;

/// <summary>
/// (coop) A hero's run start, which her game sends to her copies (<see cref="RemoteSteps"/>): the game alone plays it
/// when she starts to run or walk with no footsteps going (HeroAudioController.PlaySound, FOOTSTEPS_RUN or _WALK: the
/// first call sets playedRunStart or playedWalkStart, and the sound plays unless she is in a room change). Unity-free,
/// so it is tested without the game.
/// </summary>
internal static class HeroSteps {
    /// <summary>
    /// Whether one PlaySound call played the run start: a footsteps sound (run or walk) whose start flag it set, with
    /// the hero not in a room change.
    /// </summary>
    public static bool RunStarted(bool footstepSound, bool startedBefore, bool startedAfter, bool transitioning) =>
        footstepSound && !startedBefore && startedAfter && !transitioning;
}
