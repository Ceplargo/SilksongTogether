using System.Collections.Generic;

namespace BossRule;

/// <summary>
/// (coop, 0.3.22) Rule A2, the owner's: everyone in the game gets a quest's full reward. When a player hands in a quest
/// (its game's TryEndQuest ends it), the other games get the quest note DialogueHooks already sends, and each pays this
/// player the quest's reward through the game's own Get: money (rosaries, shell shards: a CurrencyCollectable) and the
/// rosary sets, which SSMP keeps as each player's own (DeliveryCarrier.RosarySets). A one-of-a-kind reward is never paid
/// again: shared items already reach every game. A player not in the game gets nothing (the owner's pick). The Unity
/// side is in QuestRewards.Game.cs.
/// </summary>
internal static partial class QuestRewards {
    /// <summary>The money class of the game's rewards.</summary>
    public const string Money = "CurrencyCollectable";

    /// <summary>The rosary sets quests pay, each player's own in SSMP (keep in step with DeliveryCarrier.RosarySets).</summary>
    public static readonly HashSet<string> RosarySets = ["Rosary_Set_Medium", "Rosary_Set_Large"];

    /// <summary>How long the lock keeps a hand-in's quest for its player (SSMP's ActionClaims.KeepSeconds).</summary>
    public const double KeepSeconds = 60;

    /// <summary>How long after its own hand-in a game waits for the lock's answer before it counts as the first.</summary>
    public const double SettleSeconds = 3;

    /// <summary>What a game does with another player's hand-in note.</summary>
    public enum Payout { Pay, Wait, Skip }

    /// <summary>
    /// Another player's hand-in of a quest: paid, unless this game handed in the same quest within the lock's time
    /// (both at once). Then the lock keeps the quest for one of them and puts the other's changes back: a game whose
    /// hand-in was put back pays the first player's reward (its own went back); a game whose hand-in stood is the first
    /// and skips the note (it has its own). Until the lock's answer could have come, it waits.
    /// </summary>
    /// <param name="now">This game's time.</param>
    /// <param name="ownHandIn">When this game last handed in the same quest, or null.</param>
    /// <param name="putBack">When the server last put back a change of that hand-in, or null.</param>
    /// <param name="ownOpen">Whether this game's own action on the same quest (its deposit or hand-in) is open: a
    /// payment now would carry its marker and go back with it, so it waits until the action closes.</param>
    public static Payout OnNote(double now, double? ownHandIn, double? putBack, bool ownOpen = false) {
        if (ownOpen) return Payout.Wait;
        if (ownHandIn is not { } own || now - own > KeepSeconds) return Payout.Pay;
        if (putBack is { } back && back >= own) return Payout.Pay;
        return now - own < SettleSeconds ? Payout.Wait : Payout.Skip;
    }

    /// <summary>Whether a quest's reward is paid again to each other player in the game.</summary>
    /// <param name="rewardClass">The reward item's class name.</param>
    /// <param name="rewardName">The reward item's asset name.</param>
    /// <param name="unique">Whether the reward is one of a kind.</param>
    public static bool PaysOthers(string? rewardClass, string? rewardName, bool unique) =>
        !unique && (rewardClass == Money || rewardName != null && RosarySets.Contains(rewardName));

    /// <summary>
    /// (0.4 World B) Whether a world pickup's item, taken in one game, is paid to each other player in the game too: a
    /// rosary set (each player's own in SSMP) from a saved pickup, which is taken for everyone (shared). A pickup with no
    /// saved flag (an enemy's drop) stays on each player's own screen for them to take (the money decision, 2026-11-10).
    /// Ant_04_left's string paid only its taker (NOTES 575; the project, 2026-11-19).
    /// </summary>
    /// <param name="itemName">The pickup's item's asset name.</param>
    /// <param name="saved">Whether the pickup saves its taken flag (a PersistentBoolItem).</param>
    public static bool PaysOthersPickup(string? itemName, bool saved) =>
        saved && itemName != null && RosarySets.Contains(itemName);
}
