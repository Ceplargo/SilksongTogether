using System.Runtime.CompilerServices;

namespace BossRule;

/// <summary>
/// (0.3.22) Shakra's first greeting (the Mapper NPC's Ring Shout, then her Dialogue's meeting) shows in every game, as a
/// copy in the others (<see cref="GreetingFollow"/>). She reacts to each game's own hero only (SSMP's
/// NpcRangeSeesLocalHeroOnly), so her ring shout, its sounds and her talk ran in the walker's game only (probe_shakra,
/// 0.3.22 save checks Task 10). The walker's game sends each state her greeting enters, from her Ring Shout's Take Control
/// until her Rest Animation sings again, and the voice lines she speaks; another game plays only her own animation,
/// sounds and ring flash of each state, never what holds or moves its hero, opens a dialogue or a shop, or saves. A game
/// whose own greeting runs plays no copy, so two players walking up together see no doubles. Unity-free, so it is
/// tested without the game.
/// </summary>
internal static class GreetingRule {
    public const string Npc = "Mapper NPC";
    public const string Voice = "voice";

    /// <summary>What a game does with one action of a state another game's greeting entered.</summary>
    public enum Play {
        None,
        Clip,      // her animator plays the action's clip
        Sound,     // the sound action runs as its state's entry runs it
        Activate,  // her ring flash comes on
        Event      // her END ANIM, which stops her singing
    }

    public static bool Follows(string objectName, string fsmName) =>
        objectName == Npc && fsmName is "Ring Shout" or "Dialogue" or "Rest Animation";

    public static bool Starts(string fsm, string state) => fsm == "Ring Shout" && state == "Take Control";

    public static bool Ends(string fsm, string state) => fsm == "Rest Animation" && state is "Turn Right" or "Start Singing";

    /// <summary>Whether this game sends a state its own greeting entered (its end included).</summary>
    public static bool Sends(bool ownGreeting, string fsm, string state) =>
        ownGreeting && (fsm is "Ring Shout" or "Dialogue" || Ends(fsm, state));

    /// <summary>Whether this game plays another game's greeting: not while its own runs.</summary>
    public static bool Plays(bool ownGreeting) => !ownGreeting && PlaysGreetings();

    /// <summary>What this game plays of one action: type, the action's type name; atOwner, whether it targets her (an
    /// animation's or a sound's object); target, the object it turns on; sendEvent, the event it sends.</summary>
    public static Play Of(string type, bool atOwner, string? target, string? sendEvent) => type switch {
        "Tk2dPlayAnimation" or "Tk2dPlayAnimationWithEvents" or "Tk2dPlayAnimationWithEventsV2" when atOwner => Play.Clip,
        "PlayAudioEvent" or "PlayRandomAudioClipTable" when atOwner => Play.Sound,
        "ActivateGameObject" when target == "Ring Flash" => Play.Activate,
        "SendEventByName" when sendEvent == "END ANIM" => Play.Event,
        _ => Play.None
    };

    /// <summary>The shakra_follows check's twin skips it: no greeting plays here.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool PlaysGreetings() => true;
}
