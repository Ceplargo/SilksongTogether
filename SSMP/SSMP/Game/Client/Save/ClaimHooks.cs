using System;
using System.Reflection;
using HutongGames.PlayMaker.Actions;
using MonoMod.RuntimeDetour;
using SSMP.Game.Server.Save;
using UnityEngine;
using Logger = SSMP.Logging.Logger;

namespace SSMP.Game.Client.Save;

/// <summary>
/// (coop) The one-of-a-kind action this player's game is doing, by its marker (decision "The lock"): a mask shard's
/// pickup (pb:scene:object), a wish handed in or its items deposited (quest:name), or a one-of-a-kind shop item bought
/// (shop:item). Each save
/// update of the action's own changes (ActionClaims.Owns) the game sends while it is open carries the marker, so the
/// server keeps the action for the first player whose changes for it arrive and puts a second player's game back. It
/// stays open while the hero is held (the shard's animation, the board's prompts, the shop's menu, however long a player
/// reads) and a moment after: the board gives its reward only after the wish's prompt, and an item container's change
/// goes out up to 0.2 s after it is made. It closes when the hero leaves the action's room.
/// </summary>
internal static class ClaimHooks {
    /// <summary>The shortest time a marker stays open, in seconds.</summary>
    private const float MinSeconds = 2;

    /// <summary>How long a marker stays open after the hero takes input again, in seconds.</summary>
    private const float GraceSeconds = 1;

    /// <summary>The longest time a marker stays open, in seconds.</summary>
    private const float MaxSeconds = 120;

    /// <summary>The value a quest's end or deposit sets: the quests' item container.</summary>
    private const string QuestCompletion = "QuestCompletionData";

    /// <summary>The event a mask shard's pickup FSM sends to the event registers as its pickup starts.</summary>
    private const string ShardPickupEvent = "SPECIAL ITEM COLLECT";

    private static readonly FieldInfo? PurchaseBool =
        typeof(ShopItem).GetField("playerDataBoolName", BindingFlags.NonPublic | BindingFlags.Instance);

    private static Hook? _endQuestHook;
    private static Hook? _consumeHook;
    private static Hook? _purchaseHook;
    private static Hook? _pickupHook;

    private static (string Marker, string? Own, string Room, float Opened, float BusyUntil)? _open;

    /// <summary>The marker of the action open now, or null.</summary>
    public static string? Current => _open?.Marker;

    /// <summary>
    /// The marker a save change carries: the open action's, for that action's own changes only (ActionClaims.Owns);
    /// with the switch off, for every change, as before the ruling of 2026-10-31.
    /// </summary>
    /// <param name="name">The changed PlayerData name, or null for a world object's saved value.</param>
    /// <param name="item">Whether the changed value is an item or upgrade (save-data.json's Kind Item).</param>
    /// <param name="objectKey">The changed world object as "scene:object", or null for a PlayerData value.</param>
    public static string MarkerFor(string? name, bool item, string? objectKey) =>
        _open is not { } open ? ""
        : !MarksOnlyItsAction() || ActionClaims.Owns(open.Own, name, item, objectKey) ? open.Marker : "";

    /// <summary>
    /// (coop) Whether a marker goes only on its action's own changes and closes when the hero leaves the action's room:
    /// marked, the host's game had four unrelated changes put back in 0.3.14's gate, hit_merge's defeatedMossMother among
    /// them. Not inlined: claim_changes' twin skips it, which gives false (every change marked, the marker kept across
    /// rooms).
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static bool MarksOnlyItsAction() => true;

    /// <summary>The room the hero is in.</summary>
    private static string Room() => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

    /// <summary>
    /// Hook the three actions' starts. A hook that fails (a game update renamed the method) is logged, and that action
    /// counts again for each player who does it at the same moment, as before.
    /// </summary>
    public static void Register() {
        try {
            _endQuestHook ??= new Hook(
                typeof(FullQuestBase).GetMethod(nameof(FullQuestBase.TryEndQuest))!, TryEndQuest
            );
            _consumeHook ??= new Hook(
                typeof(FullQuestBase).GetMethod(nameof(FullQuestBase.ConsumeTarget))!, ConsumeTarget
            );
            _purchaseHook ??= new Hook(typeof(ShopItem).GetMethod(nameof(ShopItem.SetPurchased))!, SetPurchased);
            _pickupHook ??= new Hook(
                typeof(SendEventToRegister).GetMethod(nameof(SendEventToRegister.OnEnter))!, SendEventToRegisterOnEnter
            );
        } catch (Exception e) {
            Logger.Error($"Could not hook the one-of-a-kind actions, each player's counts as before:\n{e}");
        }
    }

    /// <summary>Take the hooks off and close the open marker.</summary>
    public static void Deregister() {
        _endQuestHook?.Dispose();
        _consumeHook?.Dispose();
        _purchaseHook?.Dispose();
        _pickupHook?.Dispose();
        _endQuestHook = _consumeHook = _purchaseHook = _pickupHook = null;
        _open = null;
    }

    /// <summary>
    /// Each frame, before the frame's save changes are compared: close the open marker once its action is over.
    /// </summary>
    public static void OnUpdate() {
        if (_open is not { } open) {
            return;
        }

        var now = Time.unscaledTime;
        var hero = HeroController.SilentInstance;
        var busyUntil = hero != null && !hero.acceptingInput ? now + GraceSeconds : open.BusyUntil;
        if (ActionClaims.Closes(now, open.Opened, busyUntil, MarksOnlyItsAction() && Room() != open.Room, MinSeconds,
                MaxSeconds)) {
            Logger.Info($"Closed one-of-a-kind action {open.Marker}");
            _open = null;
            return;
        }

        _open = (open.Marker, open.Own, open.Room, open.Opened, busyUntil);
    }

    /// <param name="marker">The action's marker.</param>
    /// <param name="own">The one value the action sets: a PlayerData name, or a world object as "scene:object".</param>
    private static void Open(string marker, string? own) {
        Logger.Info($"Opened one-of-a-kind action {marker}");
        _open = (marker, own, Room(), Time.unscaledTime, Time.unscaledTime);
    }

    /// <summary>
    /// A wish or a quest handed in (a wish board's hand-in, an NPC's), marked before its changes are made; not an NPC's
    /// try that ends nothing (its talk asking whether the quest is ready).
    /// </summary>
    private static bool TryEndQuest(
        Func<FullQuestBase, Action, bool, bool, bool, bool> orig,
        FullQuestBase self,
        Action afterPrompt,
        bool consumeCurrency,
        bool forceEnd,
        bool showPrompt
    ) {
        if (ActionClaims.EndOpens(forceEnd, self.CanComplete)) {
            Open($"quest:{self.name}", QuestCompletion);
        }

        return orig(self, afterPrompt, consumeCurrency, forceEnd, showPrompt);
    }

    /// <summary>
    /// A quest's items taken (a wish board's deposit, which comes before its hand-in, or a donation), marked once they
    /// are: the changes go out at the next compare.
    /// </summary>
    private static bool ConsumeTarget(Func<FullQuestBase, bool> orig, FullQuestBase self) {
        var consumed = orig(self);
        if (consumed) {
            Open($"quest:{self.name}", QuestCompletion);
        }

        return consumed;
    }

    /// <summary>
    /// A shop item bought: one the shop sells once (it sets a purchase flag, or its item is unique, as a tool) is marked
    /// before the price is taken.
    /// </summary>
    private static void SetPurchased(Action<ShopItem, Action, int> orig, ShopItem self, Action onComplete, int subItemIndex) {
        var purchased = PurchaseBool?.GetValue(self) as string;
        if (!string.IsNullOrEmpty(purchased) || self.Item != null && self.Item.IsUnique) {
            Open($"shop:{self.name}", string.IsNullOrEmpty(purchased) ? null : purchased);
        }

        orig(self, onComplete, subItemIndex);
    }

    /// <summary>A mask shard's pickup starting (its FSM's Get state), marked by the shard's room and object.</summary>
    private static void SendEventToRegisterOnEnter(Action<SendEventToRegister> orig, SendEventToRegister self) {
        if (self.eventName?.Value == ShardPickupEvent && self.Owner is { } owner) {
            Open($"pb:{owner.scene.name}:{owner.name}", $"{owner.scene.name}:{owner.name}");
        }

        orig(self);
    }
}
