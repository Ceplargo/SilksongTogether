namespace SSMP.Game.Client.Entity;

/// <summary>
/// (coop, 0.4 enemies) A replayed activation that switches on a hurt box carries the scene host's body place and facing
/// at that moment, and the copy is put exactly there first. The copy eases toward each place sent
/// (PredictiveInterpolation), and the place and facing it gets come in the unreliable update, as the host's game had
/// them at the end of its frame, while the activation comes in the reliable data: the Dock Foremen's Thrower turned its
/// stomp blast (a child it leaves where it came on) on 1.1 units above the host's landing, or with the facing its own
/// FaceObjectV2 gave it later in the same state (1.16 units to the side; Dock_09, dock_blast). An activation that only
/// shows an effect carries nothing (the project). The flag byte goes with every ActivateGameObject replay; the
/// body's x and y follow it when it holds a facing. The body is posed so only for the switch-on (<see cref="PosedFor"/>):
/// the Explosion's DeactivateAfterDelay keeps the world place it had at OnEnable, and turns with the body after.
/// Unity-free, so it is tested without the game.
/// </summary>
internal static class BlastSettle {
    /// <summary>No place follows.</summary>
    public const byte NoPlace = 0;

    /// <summary>The body's x and y follow; its scale x is positive.</summary>
    public const byte FacesRight = 1;

    /// <summary>The body's x and y follow; its scale x is negative.</summary>
    public const byte FacesLeft = 2;

    /// <summary>The scene host's flag for a replayed activation: a facing when it switches on an object with a hurt box
    /// in it (the body's place then follows), else <see cref="NoPlace"/>.</summary>
    public static byte Flag(bool turnsOn, bool damaging, float scaleX) =>
        turnsOn && damaging && SettlesBlasts() ? scaleX < 0 ? FacesLeft : FacesRight : NoPlace;

    /// <summary>Whether the body's place follows the flag.</summary>
    public static bool HasPlace(byte flag) => flag is FacesRight or FacesLeft;

    /// <summary>The copy's body posed at the scene host's place and facing only while the object is switched on, then put
    /// back as its own updates left it: an update that came before the activation (the Thrower's turn by FaceObjectV2
    /// later in the same state) is kept, as no later update repeats it until the body moves or turns again.</summary>
    public static void PosedFor<TPose>(TPose own, TPose sent, System.Action<TPose> pose, System.Action switchOn) {
        pose(sent);
        try {
            switchOn();
        } finally {
            pose(own);
        }
    }

    /// <summary>The sign of the body's scale x the flag gives.</summary>
    public static float Sign(byte flag) => flag == FacesLeft ? -1f : 1f;

    /// <summary>Not inlined: dock_blast's twin skips it, which gives false (no place sent; the copy eases on, as
    /// before).</summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static bool SettlesBlasts() => true;
}
