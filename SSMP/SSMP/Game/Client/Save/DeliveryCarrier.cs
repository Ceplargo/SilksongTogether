using System;
using System.Collections.Generic;
using System.Linq;
using SSMP.Game.Client.Save.Complex;

namespace SSMP.Game.Client.Save;

/// <summary>
/// (coop, 0.3.22) A delivery is carried by the player who took it (the owner's rule A1). A delivery's item (a
/// DeliveryQuestItem collectable: its Amount is the hits it has left) is each player's own entry of the shared
/// Collectables container: it is left out of the shared container both ways, the server keeps it in that player's own
/// store under the same index (a friend's in the host's mod save, the host's in the host's own save), and a joining
/// player gets the shared container plus its own entries. The other games learn who carries: the server forwards a
/// player's own entries renamed "item@player", which a game keeps aside and never writes into its save. The quest log
/// stays one for the group. Kept free of Unity types so it can be tested without the game; the game's side is in
/// DeliveryCarrier.Hook.cs.
/// </summary>
internal static partial class DeliveryCarrier {
    /// <summary>The mark between an own entry's item and its player in a forwarded entry.</summary>
    public const char TagMark = '@';

    /// <summary>The member of a collectable entry that holds how many a player has.</summary>
    public const string Amount = "Amount";

    /// <summary>
    /// (0.3.22, rule A2) The rosary sets quests pay: each player's own entries too, since every player in the game is
    /// paid its own (BossRule's QuestRewards); shared, each payment would add a set for everyone. An old save's count
    /// stays the host's, a friend's starts at 0 (the project's ruling (d), 2026-11-11).
    /// </summary>
    public static readonly string[] RosarySets = ["Rosary_Set_Medium", "Rosary_Set_Large"];

    /// <summary>A player's own entries of the Collectables: the delivery items it carries, and the rosary sets.</summary>
    public static HashSet<string> OwnNames(IEnumerable<string> carried) => [.. carried, .. RosarySets];

    /// <summary>An own entry of a player, as forwarded to the other games.</summary>
    public static string Tag(string entry, string player) => $"{entry}{TagMark}{player}";

    /// <summary>The item and the player of a forwarded entry, or null for a plain entry.</summary>
    public static (string Entry, string Player)? Untag(string entry) {
        var mark = entry.IndexOf(TagMark);
        return mark <= 0 ? null : (entry.Substring(0, mark), entry.Substring(mark + 1));
    }

    /// <summary>The changes split into the shared ones and the player's own (the entries in <paramref name="own"/>).</summary>
    public static (List<EntryChange> Shared, List<EntryChange> Own) Split(
        IEnumerable<EntryChange> changes,
        ICollection<string> own
    ) {
        var shared = new List<EntryChange>();
        var owned = new List<EntryChange>();
        foreach (var change in changes) {
            (own.Contains(change.Entry) ? owned : shared).Add(change);
        }

        return (shared, owned);
    }

    /// <summary>
    /// A stored snapshot (encoded) split into the entries that are shared and those that are a player's own, each
    /// encoded as a snapshot. The host's loaded save holds the host's own entries in its Collectables.
    /// </summary>
    public static (byte[] Shared, byte[] Own) Separate(byte[] snapshot, ICollection<string> own, ComplexFieldSpec spec) {
        var (shared, owned) = Split(ProjectionOps.Decode(snapshot).Changes, own);
        return (ProjectionOps.MergeIntoSnapshot(null, ProjectionOps.Encode(true, shared), spec),
            ProjectionOps.MergeIntoSnapshot(null, ProjectionOps.Encode(true, owned), spec));
    }

    /// <summary>
    /// The container a joining player gets: the shared snapshot without any own entry, then the player's own entries,
    /// and every other own entry at 0, so an item left in that game from its own earlier play is not kept.
    /// </summary>
    public static byte[] JoinSnapshot(byte[]? shared, byte[]? own, ICollection<string> ownNames, ComplexFieldSpec spec) {
        var projection = new Projection();
        if (shared is { Length: > 0 }) {
            ProjectionOps.Apply(projection, ProjectionOps.Decode(shared).Changes.Where(c => !ownNames.Contains(c.Entry)),
                spec, snapshot: true);
        }

        ProjectionOps.Apply(projection, ownNames.Select(name => new EntryChange(name, Amount, 0)), spec, snapshot: true);
        if (own is { Length: > 0 }) {
            ProjectionOps.Apply(projection, ProjectionOps.Decode(own).Changes, spec, snapshot: true);
        }

        return ProjectionOps.Encode(true, ProjectionOps.Snapshot(projection));
    }

    /// <summary>A player's own store forwarded to the other games: each entry's amount, as a forwarded entry.</summary>
    public static List<EntryChange> Tags(byte[]? ownStore, string player) =>
        ownStore is not { Length: > 0 }
            ? []
            : ProjectionOps.Decode(ownStore).Changes.Where(c => c.Member == Amount)
                .Select(c => new EntryChange(Tag(c.Entry, player), Amount, c.Value)).ToList();

    /// <summary>
    /// Who carries an item: this game's player when it holds some, else the first other player that holds some (by
    /// name), else null.
    /// </summary>
    /// <param name="localHeld">How many this game holds.</param>
    /// <param name="localName">This game's player.</param>
    /// <param name="others">The other players' amounts of the item, by player.</param>
    public static string? CarrierOf(int localHeld, string? localName, IEnumerable<KeyValuePair<string, int>> others) =>
        localHeld > 0 ? localName : others.Where(o => o.Value > 0).Select(o => o.Key).OrderBy(n => n, StringComparer.Ordinal)
            .FirstOrDefault();

    /// <summary>
    /// The items whose delivery is left uncarried at a save: held by a player who is not in the game. At the next
    /// host start their deliveries reset (a missed night).
    /// </summary>
    /// <param name="stores">Each player's own store (encoded snapshot), by player key.</param>
    /// <param name="inGame">Whether a player key is in the game.</param>
    public static List<string> Uncarried(IEnumerable<KeyValuePair<string, byte[]>> stores, Func<string, bool> inGame) =>
        stores.Where(s => !inGame(s.Key) && s.Value is { Length: > 0 })
            .SelectMany(s => ProjectionOps.Decode(s.Value).Changes)
            .Where(c => c.Member == Amount && c.Value > 0).Select(c => c.Entry).Distinct()
            .OrderBy(n => n, StringComparer.Ordinal).ToList();

    /// <summary>
    /// The deliveries a host start resets: in a save whose mod save was written before carriers were kept (0.3.21 and
    /// earlier), every delivery under way, since its carrier is not on record; else those whose item was left
    /// uncarried at the last save. A save with no mod save was only ever played alone: its deliveries are the host's.
    /// </summary>
    /// <param name="underWay">The deliveries under way (accepted, not completed), each with its item.</param>
    /// <param name="hasModSave">Whether the slot has a mod save.</param>
    /// <param name="carriersKept">Whether that mod save was written with carriers kept.</param>
    /// <param name="uncarried">The items left uncarried at the last save.</param>
    public static List<string> ResetsAtStart(
        IEnumerable<(string Quest, string Item)> underWay,
        bool hasModSave,
        bool carriersKept,
        ICollection<string> uncarried
    ) =>
        underWay.Where(d => hasModSave && (!carriersKept || uncarried.Contains(d.Item))).Select(d => d.Quest).ToList();

    /// <summary>A store (encoded snapshot) with the given items at 0.</summary>
    public static byte[] Cleared(byte[]? store, IEnumerable<string> items, ComplexFieldSpec spec) =>
        ProjectionOps.MergeIntoSnapshot(store is { Length: > 0 } ? store : null,
            ProjectionOps.Encode(true, items.Select(item => new EntryChange(item, Amount, 0)).ToList()), spec);

    /// <summary>The chat line for a delivery a host start reset.</summary>
    /// <param name="quest">The quest's name as the game shows it.</param>
    /// <param name="oldSave">Whether it reset because its save is from before carriers were kept.</param>
    public static string ResetLine(string quest, bool oldSave) => oldSave
        ? $"{quest} was reset: a delivery under way in a save from before 0.3.22 starts over."
        : $"{quest} was reset: its carrier was away when the host last saved.";
}
