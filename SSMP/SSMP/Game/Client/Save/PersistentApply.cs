namespace SSMP.Game.Client.Save;

/// <summary>
/// What an arriving saved value does to a live lever (Lever_tk2d) in this game.
/// </summary>
internal enum LeverArrival {
    /// <summary>The game's own load of the value (PersistentBoolItem.PreSetup): a lever loaded on sends its gates
    /// ACTIVATE, which opens them at once with no sequence, as when a room loads with the lever pulled.</summary>
    Load,
    /// <summary>Nothing: this game's lever is on already (its own hit, or the copy of the other player's swing).</summary>
    Nothing,
    /// <summary>The lever's own hit (LeverHit.Play): its sound, and after its delay its gates' opening sequence.</summary>
    Hit
}

/// <summary>
/// What an arriving saved value does to a live StateChangeSequence (a bell shrine's) in this game.
/// </summary>
internal enum SequenceArrival {
    /// <summary>The game's own load of the value (PersistentIntItem.PreSetup): its sequence returns to that state, and
    /// with its complete bool set jumps to its last, as when a room loads after the shrine was rung.</summary>
    Load,
    /// <summary>Nothing: this game's sequence is there already (its own lever hit, or the copy's).</summary>
    Nothing,
    /// <summary>Its states up to the value entered as the doer's game entered them (SetState, not returning).</summary>
    Play
}

/// <summary>
/// A lever another player turned on plays its own hit here, once, as the game alone plays it: loading the value
/// jumped Bone_01's Great Bone Gate open in both games with none of its opening sequence (0.3.21 Part C).
/// </summary>
internal static class PersistentApply {
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static LeverArrival ForLever(bool arriving, bool activated) =>
        !arriving ? LeverArrival.Load : activated ? LeverArrival.Nothing : LeverArrival.Hit;

    /// <summary>
    /// Whether an arriving value for a gate is left to its own lever here: one already on opens it with its sequence,
    /// which saves the gate open as it starts (Bone_04's Bone Gate jumped open mid-way in the game whose lever's delay
    /// ran later).
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static bool LeftToItsLever(bool arriving, bool leverOn) => arriving && leverOn;

    /// <summary>
    /// Whether an arriving win for a wave arena is left to the arena here: one that started (its own count or the
    /// room's follow ends it) or already ending (its own end opens its gates after its pause). Loading it completes it
    /// at once with a quick gate open: Bone_18's gates opened 4 s early in the host's game, which the game alone never
    /// does. One that never started here (its player came in after) loads it.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static bool LeftToItsArena(bool arriving, bool started, bool completed) => arriving && (started || completed);

    /// <summary>
    /// What an arriving value does to a StateChangeSequence: a live one behind it plays its states as the doer's game
    /// did. Loading it set the Bellshrine's sequence returning: its lever's hit had set the complete bool already, so the
    /// other game jumped to the last state with none of the sequence (the sweep read 2 in the doer's game, 3 in the
    /// other, both doers; probe_bell_sequence). One not live here loads it.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static SequenceArrival ForSequence(int arriving, int current, bool live) =>
        !live ? SequenceArrival.Load : arriving > current ? SequenceArrival.Play : SequenceArrival.Nothing;

    /// <summary>
    /// Whether an arriving value for a BreakableHolder (a shell shard fossil: it saves its hits left) plays its hits
    /// here: a live one with more hits left. Loading it broke Bone_04's Tiny Front (2) in the host's game before the
    /// copy of the friend's swing reached it, with no hit, sound or shake (the shake rows' sweep, friend the doer).
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static bool ForHolder(int arriving, int hitsLeft, bool live) => live && arriving < hitsLeft;

    /// <summary>
    /// Whether an arriving value for a RosaryCache (a rosary string: it saves its hits) is owed here as hits: a live one
    /// with fewer. Loading it hid the beads with no fling, so this game never spawned them: Bone_01c's
    /// rosary_string_small_half (1), the friend's hit arriving 10 to 20 ms before the copy of the friend's swing
    /// (Task 12, run 2026-11-10_023548: the host's game 0 pieces, the friend's 2).
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static bool ForCache(int arriving, int state, bool live) => live && arriving > state;

    /// <summary>
    /// (0.3.23) Whether this game skips a hit on a struck thing (a BreakableHolder or a RosaryCache: each saves its hit
    /// count): a world object's (world: BlockedHits.World, a Dock Flyer's lava rock) in a following game (not sceneHost,
    /// not alone); the game that runs the room takes it and sends it. A player's hit counts in every game (this game's
    /// hero's, or the copy of another's swing, whose arriving value then plays nothing more). False is the old rule, so
    /// CoopQA skip switches it off (struck_world_hit's twin). Each game's copy of a rock falls its own way: the
    /// following game's DF Lava Rock took Dock_02's Song Shard Barrel 2 to 1 in all three of its orders, sent it, and the
    /// save kept it (Task 12, runs 2026-11-14_013330-world-pair3 to 5).
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static bool SkipsHit(bool world, bool sceneHost, bool alone) => world && !sceneHost && !alone;

    /// <summary>
    /// (0.4) Whether an arriving break for a Breakable (a plank wall, a spool) breaks the live object here, with the
    /// game's own Breakable.BreakSelf: a live, unbroken one. Setting its isBroken flag alone left Greymoor_16's plank
    /// wall standing with its Hero Solid in the host's game when the friend broke it and the copy of its swings missed
    /// (NOTES 515, run 2026-11-15_231644-pair3). False is the old rule (the flag alone), so CoopQA skip switches it off
    /// (breakable_arrives' twin). One not live here takes the flag, which the room's load reads.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static bool ForBreakable(bool arriving, bool broken, bool live) => arriving && !broken && live;
}
