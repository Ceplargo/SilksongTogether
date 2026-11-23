namespace SSMP.Game.Client.Entity;

/// <summary>
/// (coop, 0.4 enemies) A pool object whose prefab went with its room ends when it recycles itself. The game's
/// ObjectPool.Recycle(obj, prefab) returns at once on a destroyed prefab, so such an object stays active for good in
/// DontDestroyOnLoad. The Widow's lone slash did this in the friend's game: her FSM spawned one more slash from her room's
/// pool in the frames between the leave's own sweep (AutoRecycleSelf.RecycleActiveRecyclers) and the room's unload. Its
/// own 2.5 s recycle then did nothing, and its hurt box stood in every room after (2 of 8 probe runs friend-side, 0 of 6
/// alone; NOTES). The hook (PoolLeftovers.Hook.cs) takes it out of the pool's spawned list and destroys it, as the game
/// does with an object it does not track. Unity-free, so it is tested without the game.
/// </summary>
internal static partial class PoolLeftovers {
    /// <summary>Whether a recycle ends the object itself: it is alive and the prefab it was spawned from is gone.</summary>
    public static bool Ends(bool objectAlive, bool prefabAlive) =>
        objectAlive && !prefabAlive && EndsLeftovers();

    /// <summary>Not inlined: slash_outlives's twin skips it, which gives false (the object stays, as in the game).</summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static bool EndsLeftovers() => true;
}
