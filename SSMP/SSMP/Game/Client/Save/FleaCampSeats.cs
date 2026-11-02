using System;

namespace SSMP.Game.Client.Save;

/// <summary>
/// (coop) The rescued fleas' camp seats them in the same places in every game. The game picks which seats show its
/// rescued fleas at random when the room loads (SavedFleaActivator.Start), and whether the camp's giant flea is awake
/// or asleep (an ActivateRandomChild in the camp); the count is shared (each flea's SavedFlea bool), so seeding those
/// picks by the camp gives every game the same camp. This part is kept free of Unity types so it
/// can be tested without the game; the hook is in FleaCampSeats.Hook.cs.
/// </summary>
internal static partial class FleaCampSeats {
    /// <summary>
    /// The seed for a camp's pick: FNV-1a over "scene/camp", the same number in every game (string.GetHashCode may
    /// differ between runs).
    /// </summary>
    public static int Seed(string scene, string camp) {
        var hash = 2166136261u;
        foreach (var c in scene + "/" + camp) {
            hash = unchecked((hash ^ c) * 16777619u);
        }

        return unchecked((int) hash);
    }

    /// <summary>
    /// Run a pick on seeded random numbers, then put the random state back as it was (also if the pick throws), so the
    /// game's other random picks go on as they would have.
    /// </summary>
    public static void Seeded<TState>(int seed, Func<TState> state, Action<TState> setState, Action<int> init, Action run) {
        var kept = state();
        init(seed);
        try {
            run();
        } finally {
            setState(kept);
        }
    }
}
