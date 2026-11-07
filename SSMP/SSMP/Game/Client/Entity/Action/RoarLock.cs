using System.Runtime.CompilerServices;

namespace SSMP.Game.Client.Entity.Action;

/// <summary>
/// (coop) A roar lock this game's hero takes from a copy's stunning roar, Unity-free so SSMP.Tests compiles it. The game's
/// StartRoarEmitter locks only the local hero (ROAR WOUND CANCEL, then ROAR ENTER; ROAR EXIT at the roar state's exit or
/// a StopRoarEmitter), and it runs only in the game that runs the boss: SSMP's replay of the copy's roar gave the other
/// player the rings and no lock, so its Hornet could move through every boss's intro roar while the first player's
/// could not, and never struck the intro pose (0.3.20's intro_pose: the Skull King's Wake Roar, both builds). The copy's
/// roar now sends the game's own events to the local hero, and the lock ends with the roar's own end, as the scene
/// host's state exit or StopRoarEmitter reaches this game, or the copy going; if none comes, at TimeoutS.
/// </summary>
internal sealed class RoarLock {
    /// <summary>
    /// The longest a copy's roar can hold this game's hero. The longest stunning roar lock of Act 1's copied boss roars
    /// is the Last Judge's Intro Roar, 3.2 s (its state's own wait; the others 1.9 to 3.0 s); 0.8 s more covers the
    /// network delay of its end and a long frame, so a lost end frees the hero 4 s after the roar at most (the launching
    /// chat's ruling: 15 s left a missed end stunning a player mid-fight).
    /// </summary>
    public const float TimeoutS = 4f;

    private static RoarLock? _latest;
    private readonly float _started;

    public RoarLock(float started) {
        _started = started;
        _latest = this;
    }

    /// <summary>Whether ending this lock frees the hero: no later roar's lock holds it (the game's one ROAR EXIT ends
    /// whatever lock the hero is in).</summary>
    public bool Frees => _latest == this;

    public bool Ended { get; private set; }

    /// <summary>The roar's own end: true the first time (send ROAR EXIT), false after.</summary>
    public bool End() {
        if (Ended) return false;
        Ended = true;
        return true;
    }

    /// <summary>Every frame: true once, when TimeoutS has passed with no end (send ROAR EXIT).</summary>
    public bool Tick(float now) => !Ended && TimesOut() && now - _started >= TimeoutS && End();

    /// <summary>The event the game's own StartRoarEmitter sends the local hero for this roar.</summary>
    public static string EnterEvent(bool burst, bool forced) =>
        (burst ? "ROAR BURST ENTER" : "ROAR ENTER") + (forced ? " FORCED" : "");

    /// <summary>Whether the scene host sends the roar: for its rings, or for the lock alone (a roar with no rings).</summary>
    public static bool Sends(bool noVisualEffect, bool stunHero) => !noVisualEffect || stunHero && Follows();

    /// <summary>Whether the scene host's own hero took the roar's lock, as the copy's roar carries it: a roar that locks at
    /// once has locked it already (the game left it free when its own conditions said so: the Crawfather's Roar from his
    /// background idle); a delayed one locks later.</summary>
    public static bool HostLocked(bool stunHero, bool delayed, bool heroLocked) => stunHero && (delayed || heroLocked);

    /// <summary>Whether a copy's stunning roar locks this game's hero.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)] // a check's twin switches it off with a patch
    public static bool Follows() => true;

    /// <summary>Whether the roar's own end (state exit, StopRoarEmitter) ends the lock; roar_timeout holds it back.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool EndsWithRoar() => true;

    /// <summary>Whether the timeout ends a lock whose end never came; roar_timeout's twin switches it off.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool TimesOut() => true;
}
