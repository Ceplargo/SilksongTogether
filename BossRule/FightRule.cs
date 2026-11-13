using System.Runtime.CompilerServices;

namespace BossRule;

/// <summary>
/// (0.3.22) Shakra's fight with the room's enemies (the Mapper NPC's Attack Enemies) follows the scene host's game
/// (<see cref="FightFollow"/>). Her Idle's Enemy Range trigger set off a fight in each game on its own, from its own
/// enemy or copy, and the two went apart: her teleports are random (probe_shakra_fight, 0.3.22 save checks Task 10: the
/// friend's fight threw once more and landed 0.6 s later). The scene host's game sends each state of her fight and her
/// look as it changes (place, facing, shown, clip), and each sound, ring flash and song event as one of its actions plays
/// it (some sit after a test: Arriving Voice? speaks only while she enters); a following game starts no fight of its own
/// and shows hers: those frames and those actions. Each disc she throws goes as frames of its own (its place each frame
/// it moves; thrown alike, the scene host's turned back at her target enemy and a copy flew on), shown there with its
/// hits left out (the enemy copies' health is the scene host's game's). Unity-free, so it is tested without the game.
/// </summary>
internal static class FightRule {
    public const string Fsm = "Attack Enemies";
    public const float FrameEvery = 0.05f;  // s: a move alone goes at most this often (a clip, facing or shown at once)

    public enum Play {
        None,
        Sound,     // the sound action runs as its state's entry runs it
        Activate,  // her ring flash comes on
        Event,     // her END ANIM (her song stops) or MAPPER READY (back to rest)
        Throw      // her disc: the scene host's game numbers it and sends its flight, a following game shows it there
    }

    /// <summary>Whether this game sends her fight: it runs the room (sceneHost).</summary>
    public static bool Sends(bool sceneHost, string fsm) => sceneHost && fsm == Fsm;

    /// <summary>Whether this game follows her fight from the scene host's game.</summary>
    public static bool Follows(bool connected, bool sceneHost) => connected && !sceneHost && FollowsFights();

    /// <summary>Whether this game leaves out entering this state of hers: a following game's Idle, whose Enemy Range
    /// trigger would start a fight of its own.</summary>
    public static bool SkipsOwn(bool following, string fsm, string state) => following && fsm == Fsm && state == "Idle";

    /// <summary>What a following game plays of one action of a state her fight entered (type, the action's type name;
    /// atOwner, whether it targets her; target, the object it acts on; sendEvent, the event it sends).</summary>
    public static Play Of(string type, bool atOwner, string? target, string? sendEvent) => type switch {
        "PlayAudioEvent" or "PlayAudioEventRandom" or "PlayRandomAudioClipTable" when atOwner => Play.Sound,
        "AudioPlayRandomVoiceFromTableV2" when target == "Audio Loop Voice" => Play.Sound,  // her voice, on her child
        "ActivateGameObject" when target == "Ring Flash" => Play.Activate,
        "SendEventByName" when sendEvent is "END ANIM" or "MAPPER READY" => Play.Event,
        "SetFsmBool" when atOwner => Play.Event,  // her Dialogue's Attacking, which keeps her from startling while she fights
        "FlingObjectsFromGlobalPool" => Play.Throw,  // her disc at an enemy (Throw)
        _ => Play.None
    };

    /// <summary>The name a played action of hers goes by: its state and its place in that state's actions.</summary>
    public static string ActionTag(string state, int index) => $"{state}#{index}";

    /// <summary>The state and place a played action's name gives; null for a state's own name.</summary>
    public static (string State, int Index)? FromTag(string tag) {
        var at = tag.LastIndexOf('#');
        return at > 0 && int.TryParse(tag.Substring(at + 1), out var index) ? (tag.Substring(0, at), index) : null;
    }

    /// <summary>The name a thrown disc's frame goes by: its throw's number in this room and the frame's number in its
    /// flight.</summary>
    public static string DiscTag(int number, int frame) => $"{DiscName}{number}/{frame}";

    /// <summary>The throw's and the frame's numbers a disc's frame names; null for any other frame.</summary>
    public static (int Number, int Frame)? DiscOf(string tag) {
        if (!tag.StartsWith(DiscName)) return null;
        var parts = tag.Substring(DiscName.Length).Split('/');
        return parts.Length == 2 && int.TryParse(parts[0], out var number) && int.TryParse(parts[1], out var frame)
            ? (number, frame)
            : null;
    }

    /// <summary>Whether a disc's frame is newer than the last one shown of it (none yet: null). Under loss a resent
    /// older frame can come after a newer one (--lag 50, 173350: a copy held 3.2 units off once her disc stopped), and
    /// a "went" frame's disc must not be thrown again by an older one.</summary>
    public static bool Newer(int frame, int? last) => last is not { } shown || frame > shown;

    private const string DiscName = "@disc:";

    /// <summary>Whether the scene host's game sends her look now: it changed (clip, facing, shown), or she moved and
    /// the last frame went FrameEvery or more ago.</summary>
    public static bool SendsFrame(bool looks, bool moved, float since) => looks || moved && since >= FrameEvery;

    /// <summary>The shakra_fight check's twin skips it: no game follows her fight.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool FollowsFights() => true;

    /// <summary>The shakra_discs check's twin skips it: a following game throws no sent disc.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool CopiesDiscs() => true;
}
