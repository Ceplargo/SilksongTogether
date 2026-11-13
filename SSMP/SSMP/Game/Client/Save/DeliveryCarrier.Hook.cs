using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using MonoMod.RuntimeDetour;
using QuestPlaymakerActions;
using SSMP.Game.Client.Save.Complex;
using Logger = SSMP.Logging.Logger;

namespace SSMP.Game.Client.Save;

internal static partial class DeliveryCarrier {
    /// <summary>The delivery items by name, read from the game on the main thread; null until then.</summary>
    private static HashSet<string>? _items;

    /// <summary>The other players' own entries forwarded by the server, "item@player" to its amount.</summary>
    private static readonly Dictionary<string, int> Others = new();

    private static Hook? _activeItemsHook;
    private static Hook? _endQuestHook;
    private static Hook? _checkHook;
    private static Hook? _checkV2Hook;

    /// <summary>This game's player, as SSMP names it (set when the client connects).</summary>
    public static string? LocalName;

    /// <summary>
    /// (coop, 0.3.22) Whether a delivery's item stays with the player who took it. Not inlined: quest_carrier's twin
    /// skips it in both games, which gives false: the item is one shared collectable again, as in 0.3.21.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool KeepsItemWithCarrier() => true;

    /// <summary>
    /// The delivery items (by their class, DeliveryQuestItem, never by name): each carried by one player. Empty with the
    /// switch off or before <see cref="ReadItems"/>. Safe off the main thread (the server's network updates read it).
    /// </summary>
    public static ICollection<string> CarriedItems() =>
        KeepsItemWithCarrier() && _items is { } items ? items : Array.Empty<string>();

    /// <summary>
    /// The entries of the Collectables container that are each player's own: the delivery items and the rosary sets
    /// (<see cref="OwnNames"/>). Safe off the main thread.
    /// </summary>
    public static ICollection<string> OwnEntries() => OwnNames(CarriedItems());

    /// <summary>Read the delivery items from the game; on the main thread, once the game's items are loaded.</summary>
    public static void ReadItems() {
        if (_items != null) {
            return;
        }

        try {
            _items = new HashSet<string>(
                ManagerSingleton<CollectableItemManager>.Instance.GetAllCollectables().OfType<DeliveryQuestItem>()
                    .Select(item => item.name)
            );
            Logger.Info($"(coop) Delivery items, each carrier's own: {string.Join(", ", _items)}");
        } catch (Exception e) {
            Logger.Error($"Could not read the delivery items, a delivery's item is shared as before:\n{e}");
        }
    }

    /// <summary>
    /// Hook the game's delivery breaks, its hand-in and the NPCs' quest state tests, so a game that holds none of a
    /// taken delivery's item (another player carries it) neither breaks it, nor hands it in, nor is offered the hand-in.
    /// </summary>
    public static void Register() {
        ReadItems();
        try {
            _activeItemsHook ??= new Hook(
                typeof(DeliveryQuestItem).GetMethod(nameof(DeliveryQuestItem.GetActiveItems))!, GetActiveItems
            );
            _endQuestHook ??= new Hook(typeof(FullQuestBase).GetMethod(nameof(FullQuestBase.TryEndQuest))!, TryEndQuest);
            _checkHook ??= new Hook(
                typeof(CheckQuestState).GetMethod("DoQuestAction", System.Reflection.BindingFlags.NonPublic |
                                                                    System.Reflection.BindingFlags.Instance)!,
                CheckQuestStateDo
            );
            _checkV2Hook ??= new Hook(
                typeof(CheckQuestStateV2).GetMethod("DoQuestAction", System.Reflection.BindingFlags.NonPublic |
                                                                      System.Reflection.BindingFlags.Instance)!,
                CheckQuestStateV2Do
            );
        } catch (Exception e) {
            Logger.Error($"Could not hook the deliveries, a hit on any player can break one as before:\n{e}");
        }
    }

    /// <summary>Take the hooks off and forget the other players' entries.</summary>
    public static void Deregister() {
        _activeItemsHook?.Dispose();
        _endQuestHook?.Dispose();
        _checkHook?.Dispose();
        _checkV2Hook?.Dispose();
        _activeItemsHook = _endQuestHook = _checkHook = _checkV2Hook = null;
        lock (Others) {
            Others.Clear();
        }
    }

    /// <summary>A quest's delivery item: its first target's, as the game takes it (GetActiveItems), or null.</summary>
    private static DeliveryQuestItem? ItemOf(FullQuestBase quest) =>
        quest.Targets.Count > 0 ? quest.Targets[0].Counter as DeliveryQuestItem : null;

    /// <summary>Whether a taken, unfinished delivery is carried by another player: this game holds none of its item.</summary>
    public static bool HeldElsewhere(FullQuestBase quest) =>
        KeepsItemWithCarrier() && quest.IsAccepted && !quest.IsCompleted && ItemOf(quest) is { } item &&
        item.CollectedAmount <= 0;

    /// <summary>
    /// Who carries a delivery: this game's player when it holds its item, else the other player the server named, else
    /// null (no one in the game holds it).
    /// </summary>
    public static string? Carrier(string questName) {
        if (QuestManager.GetQuest(questName) is not { } quest || ItemOf(quest) is not { } item) {
            return null;
        }

        lock (Others) {
            return CarrierOf(item.CollectedAmount, LocalName, Others
                .Select(o => (Tag: Untag(o.Key), o.Value))
                .Where(o => o.Tag?.Entry == item.name)
                .Select(o => new KeyValuePair<string, int>(o.Tag!.Value.Player, o.Value)).ToList());
        }
    }

    /// <summary>
    /// An arriving Collectables update: the forwarded entries are kept aside (absolute amounts), never written into
    /// the game; an own entry arriving outside a join's snapshot is dropped (the server keeps them apart, so only a
    /// sender older than this one sends one). Returns the changes left to apply.
    /// </summary>
    public static IReadOnlyList<EntryChange> TakeArrivals(bool snapshot, IReadOnlyList<EntryChange> changes) {
        var own = OwnEntries();
        if (own.Count == 0) {
            return changes;
        }

        var left = new List<EntryChange>(changes.Count);
        foreach (var change in changes) {
            if (Untag(change.Entry) != null) {
                lock (Others) {
                    Others[change.Entry] = change.Value;
                }
            } else if (snapshot || !own.Contains(change.Entry)) {
                left.Add(change);
            }
        }

        return left;
    }

    /// <summary>The game's active deliveries, less those another player carries.</summary>
    private static IEnumerable<DeliveryQuestItem.ActiveItem> GetActiveItems(Func<IEnumerable<DeliveryQuestItem.ActiveItem>> orig) {
        var items = orig();
        if (!KeepsItemWithCarrier() || !items.Any(Elsewhere)) {
            return items;
        }

        return items.Where(item => !Elsewhere(item)).ToArray();

        static bool Elsewhere(DeliveryQuestItem.ActiveItem item) =>
            item.Quest != null && item.Item != null && item.Item.CollectedAmount <= 0;
    }

    /// <summary>A hand-in of a delivery another player carries ends nothing, as a quest that can't complete.</summary>
    private static bool TryEndQuest(
        Func<FullQuestBase, Action, bool, bool, bool, bool> orig,
        FullQuestBase self,
        Action afterPrompt,
        bool consumeCurrency,
        bool forceEnd,
        bool showPrompt
    ) {
        if (HeldElsewhere(self)) {
            Logger.Info($"(coop) Not handing in {self.name}: another player carries it");
            afterPrompt?.Invoke();
            return false;
        }

        return orig(self, afterPrompt, consumeCurrency, forceEnd, showPrompt);
    }

    /// <summary>An NPC's test of a delivery another player carries reads it as not taken here.</summary>
    private static void CheckQuestStateDo(Action<CheckQuestState, FullQuestBase> orig, CheckQuestState self, FullQuestBase quest) {
        if (HeldElsewhere(quest)) {
            self.Fsm.Event(self.NotTrackedEvent);
            return;
        }

        orig(self, quest);
    }

    /// <summary>The same for the second version of the test.</summary>
    private static void CheckQuestStateV2Do(Action<CheckQuestStateV2, FullQuestBase> orig, CheckQuestStateV2 self, FullQuestBase quest) {
        if (HeldElsewhere(quest)) {
            self.Fsm.Event(self.NotTrackedEvent);
            return;
        }

        orig(self, quest);
    }

    /// <summary>
    /// At host start, before the save is shared: reset the deliveries <see cref="ResetsAtStart"/> names in the loaded
    /// save, as a broken item resets one (DeliveryQuestItem.CancelQuest), and take their items from the host. Returns
    /// a chat line for each, for every player who joins.
    /// </summary>
    /// <param name="hasModSave">Whether the slot has a mod save.</param>
    /// <param name="carriersKept">Whether that mod save was written with carriers kept.</param>
    /// <param name="uncarried">The items the mod save names as left uncarried at its save.</param>
    public static List<string> ResetLoaded(bool hasModSave, bool carriersKept, ICollection<string> uncarried) {
        if (!KeepsItemWithCarrier()) {
            return [];
        }

        ReadItems();
        var underWay = QuestManager.GetAllQuests().OfType<FullQuestBase>()
            .Where(q => q.IsAccepted && !q.IsCompleted && ItemOf(q) != null)
            .ToDictionary(q => q.name, q => q);
        var resets = ResetsAtStart(underWay.Values.Select(q => (q.name, ItemOf(q)!.name)), hasModSave, carriersKept,
            uncarried);
        var pd = PlayerData.instance;
        var lines = new List<string>();
        foreach (var name in resets) {
            var quest = underWay[name];
            if (quest.WasEverCompleted) {
                quest.SilentlyComplete();
            } else {
                pd.QuestCompletionData.SetData(quest.name, default);
            }

            var item = ItemOf(quest)!;
            if (item.CollectedAmount > 0) {
                // (0.3.22) at host start the hero is not out yet, and the game's DeliveryQuestItem.OnTaken needs
                // HeroController.instance (its NRE stopped every reset after it: 2026-11-12_210145-oldsave-pair5).
                // With no hero, the amount alone: the hero sets its delivery items up from the inventory when it
                // starts (HeroController.Start, SetupDeliveryItems).
                if (HeroController.SilentInstance != null) {
                    item.Take(item.CollectedAmount, showCounter: false);
                } else {
                    CollectableItemManager.RemoveItem(item, item.CollectedAmount);
                }
            }

            lines.Add(ResetLine((string) quest.DisplayName, oldSave: !carriersKept));
            Logger.Info($"(coop) Reset {quest.name} at host start ({(carriersKept ? "missed night" : "old save")})");
        }

        if (resets.Count > 0) {
            QuestManager.IncrementVersion();
            CollectableItemManager.IncrementVersion();
        }

        return lines;
    }
}
