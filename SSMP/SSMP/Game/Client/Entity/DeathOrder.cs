namespace SSMP.Game.Client.Entity;

internal static class DeathOrder {
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static bool FinishesDeathBeforeHide(bool active, bool dying) => !active && dying;

    /// <summary>
    /// (coop) Whether a copy's update stops at that index for the next frame: it is after the update's last hurt-box data
    /// before its death and not after the death, and the scene host's data turned a box of the copy on in this frame. The
    /// scene host showed that box for under one send tick before the enemy died, so box and death came in one frame and
    /// the box never showed (Bone_16's Bone Crawler, its Spike Hit 3). The killing hit and the death's own actions wait
    /// with the death. One frame at most: what waits holds no box data before its death.
    /// </summary>
    /// <param name="index">The data's index in the update.</param>
    /// <param name="lastBox">The index of the update's last hurt-box data before its death, or -1.</param>
    /// <param name="death">The index of the update's death, or -1.</param>
    /// <param name="boxOnFrame">The frame the scene host's data last turned a box of the copy on.</param>
    /// <param name="frame">This frame.</param>
    public static bool WaitsForBox(int index, int lastBox, int death, int boxOnFrame, int frame) =>
        index > lastBox && index <= death && boxOnFrame == frame && ShowsLastBox();

    /// <summary>
    /// (coop) Whether an enemy's last hurt box before its death shows in the other games: the scene host polls the enemy's
    /// boxes as it dies, before the death is sent, and a copy's death waits a frame for a box that came on in its frame
    /// (WaitsForBox). Not inlined: death_box_order's twin skips it, which gives false: then neither, as before.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static bool ShowsLastBox() => true;
}
