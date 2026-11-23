using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SSMP.Game.Client.Entity;

/// <summary>
/// (coop, 0.4) A crawler's walk (Crawler.Crawl) is a coroutine its FSM starts with StartCrawler and ends with
/// StopCrawler, and StartCrawler also sets its body up: a wall or roof crawler gets gravity 0 and is frozen against its
/// wall. A takeover enters the room runner's state on the hidden original, which never ran either: in Coral_03 the wall
/// Blade Spiders, entered in Crawl Idle with gravity 1, fell to the floor (one through it), and in Coral_02 the floor
/// spider stood still (the takeover pass, 0.4 enemies Task 4). Every taken-over crawler past its start state gets its
/// body set up first, and one whose state comes after a StartCrawler with no StopCrawler between starts crawling once
/// it is active. Unity-free, so it is tested without the game.
/// shortcut: a crawler the runner woke from its ambient idle (a hit) idles again on the original; read the runner's
/// Crawler if an enemy ever shows it.
/// </summary>
internal static class TakeoverCrawlers {
    /// <summary>
    /// One FSM state: whether it has a StartCrawler, a StopCrawler, the states it goes to, and the speed its
    /// SetCrawlerSpeed sets (if it has one).
    /// </summary>
    public readonly record struct State(bool Starts, bool Stops, IReadOnlyList<string> To, float? Speed = null);

    /// <summary>
    /// Whether the runner's crawler walks in this state without the state starting or stopping it itself: some way in
    /// comes from a state where it walks. A state with its own StartCrawler or StopCrawler sets that on entry.
    /// </summary>
    public static bool WalksOn(string current, IReadOnlyDictionary<string, State> states) =>
        states.TryGetValue(current, out var here) && !here.Starts && !here.Stops &&
        After(states, next => !next.Stops).Contains(current);

    /// <summary>
    /// (coop, 0.4) Whether the runner's old Walker (Update-driven, no coroutine) walks in this state: its own StartWalker
    /// started it, or it walks on from one. Entered while hidden, StartWalker's StartMoving does nothing to a Walker that
    /// has not started (its state NotReady), and its Start then waits for a StartWalker that never comes (startInactive):
    /// Greymoor's Farmer Scissors (StartWalker in their Walker state) and Pilgrims (in Start Walk, before Walk) stood
    /// still (the takeover pass, 2026-11-16).
    /// </summary>
    public static bool WalksIn(string current, IReadOnlyDictionary<string, State> states) =>
        states.TryGetValue(current, out var here) && here.Starts || WalksOn(current, states);

    /// <summary>
    /// Whether the runner's crawler had its body set up by a StartCrawler before this state: some way in passes one. A
    /// StopCrawler ends the walk but leaves the body (its gravity) as it was.
    /// </summary>
    public static bool SetUp(string current, IReadOnlyDictionary<string, State> states) {
        return After(states, _ => true).Contains(current);
    }

    /// <summary>
    /// The crawl speed the runner's crawler had in this state: the one the nearest SetCrawlerSpeed on a way in set
    /// (the state's own first; a stop keeps it). The original's own is its prefab's (the Blade Spider's is 0: taken
    /// over walking, it walked at 0). Null when no way in sets one.
    /// </summary>
    public static float? SpeedAt(string current, IReadOnlyDictionary<string, State> states) {
        if (!states.TryGetValue(current, out var here)) return null;
        if (here.Speed is { } own) return own;

        var seen = new HashSet<string> { current };
        var ring = new List<string> { current };
        while (ring.Count > 0) {
            var next = new List<string>();
            foreach (var (name, state) in states) {
                if (seen.Contains(name) || !state.To.Any(ring.Contains)) continue;
                if (state.Speed is { } speed) return speed;
                seen.Add(name);
                next.Add(name);
            }

            ring = next;
        }

        return null;
    }

    /// <summary>The states with a StartCrawler, and every state reached from them through states that pass.</summary>
    private static HashSet<string> After(IReadOnlyDictionary<string, State> states, System.Func<State, bool> passes) {
        var reached = states.Keys.Where(name => states[name].Starts).ToHashSet();
        for (var grew = true; grew;) {
            grew = false;
            foreach (var (name, state) in states) {
                if (!reached.Contains(name)) continue;
                foreach (var to in state.To) {
                    if (states.TryGetValue(to, out var next) && passes(next) && reached.Add(to)) grew = true;
                }
            }
        }

        return reached;
    }

    /// <summary>Not inlined: the takeover pass's twin skips it, which gives false (crawlers taken over as before).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool StartsCrawlers() => true;

    /// <summary>
    /// (coop, 0.4) The same for a WalkerV2, whose walk is a coroutine its FSM starts with StartWalkerV2 and ends with
    /// StopWalkerV2 (WalksOn on that graph): Bone_09's Dock Workers, taken over in Walk, stood still (the takeover
    /// pass, 2026-11-16). Not inlined: takeover_rooms' twin skips it, which gives false.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool StartsWalkers() => true;
}
