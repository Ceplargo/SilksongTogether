using System.Linq;

namespace BossRule;

/// <summary>
/// (0.3.20) The bosses a player's performance wakes (the Needolin played near them), and when a game tells the room or
/// wakes its boss on another's word. Unity-free, so BossRule.Tests compiles it.
/// </summary>
internal static class PerformWakes {
    /// <summary>Each: its room, the boss, the FSM and the state it waits in, the event that wakes it, the alert range
    /// its hero must be in and the radius the performance must reach (the boss's CheckHeroPerformanceRegionV2).</summary>
    public static readonly (string Scene, string Boss, string Fsm, string Waits, string Event, string Range, float Radius)[]
        Wakes = [("Bone_15", "Skull King", "Behaviour", "State Check", "WAKE", "Wake Range", 4f)];

    /// <summary>How often a game whose hero keeps performing tells the room again.</summary>
    public const float Every = 1f;

    /// <summary>The boss of that room a performance wakes, if any.</summary>
    public static (string Scene, string Boss, string Fsm, string Waits, string Event, string Range, float Radius)? In(string? scene) =>
        Wakes.Where(w => w.Scene == scene).Cast<(string, string, string, string, string, string, float)?>().FirstOrDefault();

    /// <summary>Whether this game tells the room: its own hero performs where the boss hears it (in its range, the
    /// performance reaching it), not within Every of the last time.</summary>
    public static bool Sends(bool performing, bool inRange, float now, float lastSent) =>
        performing && inRange && now - lastSent >= Every;

    /// <summary>Whether another game's word wakes this game's boss: this game runs it (its FSM on) and it still waits.</summary>
    public static bool Echoes(bool runs, string? state, string waits) => runs && state == waits;
}
