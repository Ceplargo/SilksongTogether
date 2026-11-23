using System;
using System.Globalization;

namespace BossRule;

/// <summary>
/// (0.4 world Task 16) Damaging world hazards each game moves on its own clock from the moment it loads the room, so
/// the same hazard stood in a different place in each game: Greymoor's windmill cogs (their root's Animator turns the
/// spikes; probe_own_clock read them 120 to 144 degrees apart at about 23 deg/s) and Dock_16's lava waterfall sets
/// (a Control FSM loop of Wait and ActivateGameObject states from room load; up to 1.2 s apart). The game that comes
/// in later takes the scene host's phase (<see cref="ClockFollow"/>), and a freeze of either game, which moves its own
/// clock alone, has the scene host's phase sent again. The rules here; the game side in ClockFollow.
/// </summary>
internal static class OwnClocks {
    /// <summary>A room word's prefix: "Clock:" then a phase word, or <see cref="Ask"/>.</summary>
    public const string Prefix = "Clock:";

    /// <summary>A game that came in, or froze, asks the scene host's game for its phases.</summary>
    public const string Ask = "?";

    /// <summary>A burst that is not on (its object inactive).</summary>
    public const string Off = "off";

    /// <summary>The scene host's answer to an ask, ahead of its phases: "!" and the ask's own stamp, so the asker
    /// learns the two games' clock offset from that round trip.</summary>
    public const string Pong = "!";

    // a frame that long moves this game's own clocks off the others' (a hitch; the probe's freeze was 1 s)
    private const float FreezeS = 0.25f;
    // apart: a cog more than this share of its cycle off (its cycle is about 15 s: 0.3 s), a loop more than this
    // many seconds into the same state (its states last 0.3 to 2 s)
    private const float CycleNear = 0.02f;
    private const float LoopNearS = 0.1f;
    // a lava fall moves about 40 units a second: a burst more than a frame off is taken (a needless take is harmless)
    private const float BurstNearS = 0.02f;
    // an answer to this game's own ask comes within a round trip; longer allows a lagged one
    private const float AskedS = 2f;
    // a longer round trip is a stalled answer, not the link's time
    private const float MaxLeadS = 0.5f;
    private const float AskAgainS = 0.5f;
    private const float AskForS = 5f;

    public enum Kind { None, Animator, Loop, Burst }

    // (object name prefix, kind, the FSM a loop runs on)
    private static readonly (string Name, Kind Kind, string Fsm)[] Kinds = [
        ("Greymoor_windmill_cog", Kind.Animator, ""),
        ("Lava_Waterfall Set", Kind.Loop, "Control"),
        // each set's ActivateGameObject turns one on; its Animator plays the fall and DeactivateAfterDelay turns it off
        // 2.4 or 2.5 s later, so one already falling when a loop's phase is taken keeps its own time (probe_own_clock)
        ("Lava_waterfall_hazard", Kind.Burst, ""),
        // Organ_01's steam (0.4 World B, 3c walk: the columns' Damagers up to 27 units apart in 90 of 100 reads): the
        // timer's Control loop of Waits sends STEAM UP/DOWN ANTIC, ON and OFF to the columns; each column's Control
        // only answers them, so a column takes the scene host's state too (a timer jumped mid-cycle sends only its
        // new state's event: a column left on would hurt a whole cycle long)
        ("Exhaust Column Timer", Kind.Loop, "Control"),
        ("Exhaust Column New", Kind.Loop, "Control"),
    ];

    /// <summary>Which kind of own-clock hazard an object is (a loop by its FSM's name), or None.</summary>
    public static Kind KindOf(string objectName, string fsmName) {
        foreach (var (name, kind, fsm) in Kinds) {
            if (objectName.StartsWith(name, StringComparison.Ordinal) && fsm == fsmName) return kind;
        }

        return Kind.None;
    }

    /// <summary>A phase word: the object's path, its state (an Animator state's hash, or an FSM state's name) and its
    /// place in it (an Animator's normalized time, or a loop's seconds in its state).</summary>
    public static string Word(string path, string state, float value) =>
        path + "|" + state + "|" + value.ToString("R", CultureInfo.InvariantCulture);

    public static bool TryParse(string word, out string path, out string state, out float value) {
        path = state = "";
        value = 0f;
        var last = word.LastIndexOf('|');
        var middle = last > 0 ? word.LastIndexOf('|', last - 1) : -1;
        if (middle <= 0) return false;
        if (!float.TryParse(word.Substring(last + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return false;
        path = word.Substring(0, middle);
        state = word.Substring(middle + 1, last - middle - 1);
        return true;
    }

    /// <summary>Whether this game's phase is far enough off the scene host's to take it: another state, or a cog
    /// more than CycleNear of its cycle off (whole cycles are the same place), or a loop more than LoopNearS off.</summary>
    public static bool Apart(Kind kind, string mineState, float mine, string theirState, float theirs) {
        if (kind == Kind.Burst) {
            if (mineState == Off || theirState == Off) return mineState != theirState;
            if (!TryBurst(mineState, out var mineHash, out var mineLeft, out var mineFlip, out _)
                || !TryBurst(theirState, out var theirHash, out var theirLeft, out var theirFlip, out var length)) return true;
            // its place in its fall as well as its off-timer: a freeze frame steps its Animator, not always its timer (run 6)
            return mineHash != theirHash || mineFlip != theirFlip || Math.Abs(mineLeft - theirLeft) > BurstNearS
                   || Math.Abs(mine - theirs) * length > BurstNearS;
        }

        if (mineState != theirState) return true;
        if (kind == Kind.Animator) {
            var off = Math.Abs((mine - (float)Math.Floor(mine)) - (theirs - (float)Math.Floor(theirs)));
            return Math.Min(off, 1f - off) > CycleNear;
        }

        return Math.Abs(mine - theirs) > LoopNearS;
    }

    /// <summary>Whether a loop's own place is apart from the scene host's: by its state and its Wait's time, or, for a
    /// loop with no Wait in its state (mine null: Organ_01's steam columns, which only the timer's events move), by its
    /// state alone (its time is nothing, so a lead added to it must not count).</summary>
    public static bool LoopApart(string mineState, float? mine, string theirState, float theirs) =>
        mine is null ? mineState != theirState : Apart(Kind.Loop, mineState, mine.Value, theirState, theirs);

    /// <summary>A burst's state: "off", or its Animator state's hash, its off-timer's seconds left, its flip (see
    /// <see cref="FlipOf"/>) and its Animator state's length in seconds.</summary>
    public static string BurstState(bool on, int hash, float timer, int flip, float length) =>
        on ? string.Join("/", hash.ToString(CultureInfo.InvariantCulture), timer.ToString("R", CultureInfo.InvariantCulture),
                         flip.ToString(CultureInfo.InvariantCulture), length.ToString("R", CultureInfo.InvariantCulture)) : Off;

    public static bool TryBurst(string state, out int hash, out float timer, out int flip, out float length) {
        hash = flip = 0;
        timer = length = 0f;
        var parts = state.Split('/');
        return parts.Length == 4
               && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out hash)
               && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out timer)
               && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out flip)
               && float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out length);
    }

    /// <summary>Whether a game takes a phase word: a game that is not the scene host, or one that asked a moment ago
    /// (a game that just came in can still count itself its room's scene host).</summary>
    public static bool Takes(bool sceneHost, float sinceAsk) => !sceneHost || sinceAsk < AskedS;

    /// <summary>A burst's mirroring (RandomlyFlipScale flips one at random as it comes on, in each game alone): 1 when
    /// its x scale is below 0, plus 2 when its y scale is.</summary>
    public static int FlipOf(float scaleX, float scaleY) => (scaleX < 0f ? 1 : 0) + (scaleY < 0f ? 2 : 0);

    /// <summary>The scene host's phase as it is now: its word moved ahead by the lead (a cog by lead/cycleLength of its
    /// cycle, a loop's wait by lead, a burst's fall by lead/its length and its off-timer back by lead). Compared and
    /// taken this way: compared as sent, a hazard one lead behind read as near (own_clock under 50 ms lag).</summary>
    public static (string State, float Value) Ahead(Kind kind, string state, float value, float lead, float cycleLength) {
        if (kind == Kind.Loop) return (state, value + lead);
        if (kind == Kind.Animator) return (state, value + (cycleLength > 0f ? lead / cycleLength : 0f));
        if (kind != Kind.Burst || !TryBurst(state, out var hash, out var left, out var flip, out var length)) return (state, value);
        return (BurstState(true, hash, left - lead, flip, length), value + (length > 0f ? lead / length : 0f));
    }

    /// <summary>A word with its sender's frame time in front: "12.5@word".</summary>
    public static string Stamp(float at, string word) => at.ToString("R", CultureInfo.InvariantCulture) + "@" + word;

    /// <summary>A word and its sender's frame time; an unstamped word's time is NaN.</summary>
    public static bool TryStamp(string text, out float at, out string word) {
        at = float.NaN;
        word = text;
        var mark = text.IndexOf('@');
        if (mark < 0) return true;
        if (!float.TryParse(text.Substring(0, mark), NumberStyles.Float, CultureInfo.InvariantCulture, out at)) return false;
        word = text.Substring(mark + 1);
        return true;
    }

    /// <summary>The other game's clock minus this game's, from an ask sent at askedAt, its pong landing at
    /// answeredAt and stamped theirAt (the pong sent halfway through the round trip).</summary>
    public static float Offset(float askedAt, float answeredAt, float theirAt) => theirAt - (askedAt + answeredAt) / 2f;

    /// <summary>How old a word stamped theirAt is as it lands at receivedAt, by the clock offset (at most
    /// MaxLeadS).</summary>
    public static float Age(float receivedAt, float theirAt, float offset) =>
        Math.Min(Math.Max(receivedAt - (theirAt - offset), 0f), MaxLeadS);

    /// <summary>How far ahead to set a phase taken: half the round trip of this game's last ask (a phase word is that
    /// old when it lands; a lava fall moves about 40 units a second), at most MaxLeadS.</summary>
    public static float Lead(float roundTrip) => Math.Min(Math.Max(roundTrip / 2f, 0f), MaxLeadS);

    /// <summary>Whether a game that came in asks again: no answer yet, its last ask AskAgainS ago, and still within
    /// AskForS of coming in (an answer in a room's first moments can be lost; run 3 of own_clock).</summary>
    public static bool AsksAgain(bool answered, float sinceEntry, float sinceAsk) =>
        !answered && sinceAsk >= AskAgainS && sinceEntry <= AskForS;

    /// <summary>Whether a frame this long moved this game's own clocks off the others'.</summary>
    public static bool Froze(float frameGap) => frameGap >= FreezeS;
}
