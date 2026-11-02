using System.Collections.Generic;

namespace SSMP.Game.Server.Save;

/// <summary>
/// (coop) Which player a one-of-a-kind action belongs to (a mask shard picked up, a wish handed in, a one-of-a-kind shop
/// item bought; named by its marker): the first player whose save update carries the marker. The server refuses
/// another player's updates with it, so two players doing it at the same moment count it once (decision "The lock").
/// Kept free of Unity types so it can be tested without the game.
/// </summary>
internal class ActionClaims {
    /// <summary>
    /// How long a marker stays with its player after their last update with it, in seconds: longer than any action's
    /// changes take to arrive, and short enough that a later repeat of a repeatable wish is the next player's.
    /// </summary>
    public const double KeepSeconds = 60;

    private readonly Dictionary<string, (string Player, double Time)> _claims = new();

    /// <summary>
    /// Whether a second player's update with a claimed marker is refused and put back to the server's value: a shared
    /// value (a count, an item, a world object's saved value), or the player's money (a price paid, a reward). Not
    /// another of the player's own values: the game works some out from shared ones (maxHealth from maxHealthBase), so
    /// putting the old one back would take what the first player's action gave everyone.
    /// </summary>
    /// <param name="shared">Whether the value is shared.</param>
    /// <param name="name">The PlayerData name, or null for a world object's saved value.</param>
    public static bool PutsBack(bool shared, string? name) => shared || name is "geo" or "ShellShards";

    /// <summary>
    /// Whether a quest's end opens its marker: only one that ends it (forced, or its targets met). An NPC's talk tries to
    /// end its quest to see whether it is ready, and a try that ends nothing must not make the talker its owner.
    /// </summary>
    public static bool EndOpens(bool forceEnd, bool canComplete) => forceEnd || canComplete;

    /// <summary>
    /// Whether a change made while an action is open is that action's own, and so carries its marker: what the action
    /// pays or gives (rosaries, shell shards, an item or upgrade) or the one value it sets (a quest's completion, a shop
    /// item's purchase flag, a picked-up object's saved value). Any other change the game makes meanwhile goes unmarked,
    /// so the server never refuses it for another player's action: in 0.3.14's gate the host's game ended a quest the
    /// friend's had ended first, and four unrelated changes it made while its marker was open were put back.
    /// </summary>
    /// <param name="own">The action's one value: a PlayerData name, or a world object as "scene:object".</param>
    /// <param name="name">The changed PlayerData name, or null for a world object's saved value.</param>
    /// <param name="item">Whether the changed value is an item or upgrade (save-data.json's Kind Item).</param>
    /// <param name="objectKey">The changed world object as "scene:object", or null for a PlayerData value.</param>
    public static bool Owns(string? own, string? name, bool item, string? objectKey) =>
        name is "geo" or "ShellShards" || item || own != null && (name ?? objectKey) == own;

    /// <summary>
    /// Whether an open action's marker closes now: its room left, its longest time over, or its shortest time over and
    /// the hero's hold with the moment after it.
    /// </summary>
    public static bool Closes(double now, double opened, double busyUntil, bool roomLeft, double min, double max) =>
        roomLeft || now > opened + max || now > System.Math.Max(opened + min, busyUntil);

    /// <summary>
    /// Whether the player may send updates with this marker: it is theirs, or nobody's in the last KeepSeconds. A
    /// marker this returns true for is kept for the player from now.
    /// </summary>
    /// <param name="marker">The action's marker.</param>
    /// <param name="player">The player's auth key, which a rejoin keeps.</param>
    /// <param name="now">The time in seconds.</param>
    public bool Claim(string marker, string player, double now) {
        if (_claims.TryGetValue(marker, out var claim) && claim.Player != player && now - claim.Time < KeepSeconds) {
            return false;
        }

        _claims[marker] = (player, now);
        return true;
    }
}
