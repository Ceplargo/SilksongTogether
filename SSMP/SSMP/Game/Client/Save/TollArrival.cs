namespace SSMP.Game.Client.Save;

/// <summary>
/// (coop, 0.4.1) A Bellway toll another player paid opens this game's station too, as the payer's game plays it. The
/// toll's PlayerData bool (the toll machine's "Pickup PlayerData Bool", UnlockedBoneforestEastStation in Bellway_03) is
/// shared, but the machine's Unlock Behaviour reads it only as the room loads (Inert's test, then Map Up). In the other
/// game the machine stayed in Inert and its bellway_floor_gate shut until that player left the room (Task 14's main
/// pass, Bellway_03's bell row in four orders; probe_bellway_gate, NOTES 619). This part is kept free of Unity types so
/// it can be tested without the game; the hook is in TollArrival.Hook.cs.
/// </summary>
internal static partial class TollArrival {
    /// <summary>The toll machine's FSM, its value's variable and the state its paid sequence starts at.</summary>
    public const string Fsm = "Unlock Behaviour", Variable = "Pickup PlayerData Bool", Start = "Sequence Animation";

    /// <summary>
    /// Whether a value arriving from another player plays this machine's paid sequence here: it arrived set, it is the
    /// machine's own value, and the machine still waits in Inert. A room loaded with the toll paid (Map Up), a machine
    /// already playing it, or this player's own prompt (Prompt) are left alone.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static bool Plays(bool value, string name, string? machineValue, string? state) =>
        value && name == machineValue && state == "Inert";
}
