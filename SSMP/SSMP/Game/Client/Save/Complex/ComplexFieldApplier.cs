using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using Logger = SSMP.Logging.Logger;
using Object = UnityEngine.Object;

namespace SSMP.Game.Client.Save.Complex;

/// <summary>
/// Writes changes to the game's item containers that arrived from other players, through the game's own code where
/// there is one.
/// </summary>
internal static class ComplexFieldApplier {
    /// <summary>
    /// Merge the changes into the container by each member's rule, write the members that changed and refresh the
    /// game's caches for that container.
    /// </summary>
    /// <param name="fieldName">A field name from <see cref="ComplexFieldSpecs.All"/>.</param>
    /// <param name="snapshot">Whether the changes are a full snapshot (a player joining).</param>
    /// <param name="changes">The changes.</param>
    public static void Apply(string fieldName, bool snapshot, IReadOnlyList<EntryChange> changes) {
        var container = ComplexFieldReader.Container(fieldName);
        if (container == null) {
            return;
        }

        var before = ComplexFieldReader.Read(fieldName);
        var after = new Projection();
        foreach (var (entry, members) in before) {
            after[entry] = new Dictionary<string, int>(members);
        }

        ProjectionOps.Apply(after, changes, ComplexFieldSpecs.All[fieldName], snapshot);

        foreach (var (entry, members) in after) {
            before.TryGetValue(entry, out var old);
            foreach (var (member, value) in members.ToList()) {
                var oldValue = old != null && old.TryGetValue(member, out var known) ? known : 0;
                if (oldValue != value) {
                    Write(container, fieldName, entry, member, value);
                }
            }
        }

        Refresh(fieldName);
        HidePickupsOfOwnedItems(fieldName);
        // (coop, 0.3.21) another player's take of a followed drop's item (a Pilgrim Rag): this game's goes too
        if (fieldName == "Collectables" && !snapshot) {
            foreach (var (entry, members) in after) {
                var was = before.TryGetValue(entry, out var old) && old.TryGetValue("Amount", out var known) ? known : 0;
                if (members.TryGetValue("Amount", out var now) && now > was) {
                    Entity.Component.ThrownComponent.HidesTakenDrops(entry, now - was);
                }
            }
        }
    }

    /// <summary>
    /// A unique item that arrived from another player hides its pickups in this game's loaded rooms, as the game's own
    /// check does when a room loads (CollectableItemPickup.CheckActivation: a pickup whose item cannot be got more is
    /// picked up). A unique item's pickup keeps no saved flag, so without this the friend saw a relic the host had just
    /// taken still lying in the room until it loaded again. Only after the containers of unique items (tools, crests,
    /// relics), which merge by Max; and nothing from the game's pickup code may stop the item update around it (the
    /// update's last-read copy would stay old, and the next check would send the arrived change again as this game's).
    /// </summary>
    private static void HidePickupsOfOwnedItems(string fieldName) {
        if (!HidesOwnedPickups() || fieldName is not ("Tools" or "ToolEquips" or "Relics") ||
            ActivatedReadField == null || CheckActivationMethod == null) {
            return;
        }

        try {
            foreach (var pickup in Object.FindObjectsByType<CollectableItemPickup>(FindObjectsSortMode.None)) {
                if (pickup.Item is { IsUnique: true } item && !item.CanGetMore() &&
                    !(bool) ActivatedReadField.GetValue(pickup)) {
                    CheckActivationMethod.Invoke(pickup, null);
                }
            }
        } catch (Exception e) {
            Logger.Warn($"Could not hide the pickups of an owned unique item: {e.GetBaseException().Message}");
        }
    }

    /// <summary>
    /// Whether an arriving unique item hides its pickups at once.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)] // a check's twin switches it off with a patch
    private static bool HidesOwnedPickups() => true;

    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    // Null after a game update that renames them: then pickups are left as they are, as before the hide
    private static readonly FieldInfo? ActivatedReadField = typeof(CollectableItemPickup).GetField("activatedRead", Private);
    private static readonly MethodInfo? CheckActivationMethod =
        typeof(CollectableItemPickup).GetMethod("CheckActivation", Private);

    private static void Write(object container, string fieldName, string entry, string member, int value) {
        // The game's own unlock also fills a new tool's uses and sets up a crest's slots
        if (member == "IsUnlocked" && value == 1) {
            if (fieldName == "Tools" && ToolItemManager.GetToolByName(entry) is { } tool) {
                tool.Unlock(null, ToolItem.PopupFlags.None);
                return;
            }

            if (fieldName == "ToolEquips" && ToolItemManager.GetCrestByName(entry) is { } crest) {
                crest.Unlock();
                return;
            }
        }

        if (container is EnemyJournalKillData journal) {
            var kill = journal.GetKillData(entry);
            if (member == nameof(EnemyJournalKillData.KillData.Kills)) {
                kill.Kills = value;
            }

            journal.RecordKillData(entry, kill);
            return;
        }

        var type = container.GetType();
        var data = type.GetMethod("GetData")?.Invoke(container, [entry]);
        var field = data?.GetType().GetField(member);
        if (data == null || field == null) {
            return;
        }

        // The data is a boxed struct: set the member on the box, then write the box back
        field.SetValue(data, field.FieldType == typeof(bool) ? value != 0 : value);
        type.GetMethod("SetData")?.Invoke(container, [entry, data]);
    }

    /// <summary>
    /// The game caches tool, collectable and quest lists by a version number; a new version rebuilds them.
    /// </summary>
    private static void Refresh(string fieldName) {
        switch (fieldName) {
            case "Tools":
            case "ToolEquips":
                ToolItemManager.IncrementVersion();
                break;
            case "Collectables":
                CollectableItemManager.IncrementVersion();
                break;
            case "QuestCompletionData":
            case "QuestRumourData":
                QuestManager.IncrementVersion();
                break;
        }
    }
}
