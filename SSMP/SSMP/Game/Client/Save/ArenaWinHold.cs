using System.Runtime.CompilerServices;

namespace SSMP.Game.Client.Save;

/// <summary>
/// (0.3.21) A wave arena's win from the other game, left to this game's running arena (PersistentApply.LeftToItsArena),
/// is loaded after all if that arena has not begun its own end by <see cref="GraceS"/>. Its end is the room's win
/// message (BossRule's ArenaWins) or its own count; with the message missing, the arena ran on with its battle music
/// and camera lock (arena_after on 0.3.21's build: Shellwood_01b and Room_Forge, 6 s after both arenas were over),
/// where loading the win had ended it before the arena fix.
/// </summary>
internal sealed class ArenaWinHold {
    /// <summary>The room's win message comes with the win's saved value (14 ms apart on pair 3); kept
    /// short so a loaded win's end and fanfare still come soon (at 2 s, 5.5 s after the win).</summary>
    public const float GraceS = 1f;

    private float _due = -1f;

    /// <summary>A win arrived and was left to the running arena.</summary>
    public void Left(float now) {
        if (_due < 0f) _due = now + GraceS;
    }

    /// <summary>Whether the win is loaded now: once, after the grace, if the arena has not begun its end.</summary>
    public bool Due(float now, bool ending) {
        if (_due < 0f || now < _due) return false;
        _due = -1f;
        return !ending;
    }

    /// <summary>The fallback's switch (its twin skips it: a win left to the arena is never loaded, as before).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool Loads() => true;

    /// <summary>The held load stores the win again first (a run's stored win had gone back to false). Not inlined: a
    /// check's twin skips it.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool StoresAgain() => true;
}
