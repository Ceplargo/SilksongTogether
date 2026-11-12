using System;
using TeamCherry.Localization;
using UnityEngine;

namespace BossRule;

/// <summary>
/// (coop) The watcher's side of a friend's talk (the owner, 2026-11-07): each page looked up in this game's own language
/// and shown in the box (DialogueOverlay) when the NPC is in this player's room and camera view and nothing keeps them
/// busy, else as a chat line "NPC: words"; a quest line "Ann took X" or "Ann finished X".
/// </summary>
internal static class DialogueChat {
    /// <summary>SSMP's chat box; set by the client add-on.</summary>
    public static Action<string>? AddMessage;

    private static DialogueWatch _watch = new();

    public static void Reset() {
        _watch = new DialogueWatch();
        DialogueOverlay.Clear();
    }

    public static void Received(string body) {
        switch (DialogueShare.Decode(body)) {
            case QuestNote note:
                Chat(DialogueShare.QuestLine(note.Talker, QuestName(note.QuestKey), note.Done));
                break;
            case FoundNote found:
                TakeFind(found);
                break;
            case DialoguePage { Page: DialogueShare.EndPage } end:
                DialogueOverlay.End(end.Talker, end.Talk);
                break;
            case DialoguePage page:
                Take(page);
                break;
            default:
                Debug.LogWarning($"[BossRule] A dialogue message it cannot read: {body.Length} characters");
                break;
        }
    }

    private static void Take(DialoguePage page) {
        if (GameManager.SilentInstance is not { } gm) return;
        var (view, busy) = (MyView(), BusyWhy(gm));
        var where = _watch.Take(page, gm.sceneName, view, busy != "");
        if (where == null) return;

        var text = page.TextSheet == "" ? page.TextKey : new LocalisedString(page.TextSheet, page.TextKey).ToString(false);
        var lines = DialogueBox.ParseTextForDialogueLines(text);
        if (page.Page >= lines.Count) {
            Debug.LogWarning($"[BossRule] {page.TextSheet}/{page.TextKey} has {lines.Count} pages here, not page {page.Page}");
            return;
        }

        var line = lines[page.Page];
        var speaker = line.IsPlayer ? page.Talker
            : page.SpeakerKey == "" ? "" : new LocalisedString(page.SpeakerSheet, page.SpeakerKey).ToString(false);
        Debug.Log($"[BossRule] Dialogue received: {page.Talker}'s talk {page.Talk} page {page.Page} " +
                  $"{page.TextSheet}/{page.TextKey} to the {where} (here {gm.sceneName}, view {view}, NPC at " +
                  $"{page.NpcX:0.#}, {page.NpcY:0.#} in {page.Scene}{(busy == "" ? "" : ", busy: " + busy)})");
        DialogueOverlay.Took(where.Value, page, speaker, DialogueShare.Plain(line.Text));
        if (where == Where.Screen) {
            DialogueOverlay.Show(page, line);
        } else {
            DialogueOverlay.End(page.Talker, page.Talk);
            Chat(DialogueShare.ChatLine(speaker, line.Text));
        }
    }

    /// <summary>
    /// (coop, 0.3.21) Another player's find of an item a wish counts: in this player's room and view (and nothing keeping
    /// them busy), the game's own message and the wish's update once the find's amount has arrived here (SSMP's
    /// Collectables sync), as CollectableItem.Collect shows them; else the chat line "Ann found Rock Roller Item (Rock
    /// Rollers 3/3)". Only shown: it stops no one.
    /// </summary>
    private static void TakeFind(FoundNote found) {
        if (!ShowsFinds() || GameManager.SilentInstance is not { } gm) return;
        var (view, busy) = (MyView(), BusyWhy(gm));
        var where = DialogueShare.Route(found, gm.sceneName, view, busy != "");
        var item = CollectableItemManager.GetItemByName(found.ItemKey);
        Debug.Log($"[BossRule] Find received: {found.Finder}'s {found.ItemKey} for {found.QuestKey} to the {where} (here " +
                  $"{gm.sceneName}, view {view}, finder at {found.X:0.#}, {found.Y:0.#} in {found.Scene}" +
                  $"{(busy == "" ? "" : ", busy: " + busy)})");
        if (where == Where.Screen && item != null) {
            gm.StartCoroutine(ShowFind(item, found.Amount));
        } else {
            var name = item != null ? item.GetDisplayName(CollectableItem.ReadSource.GetPopup) : found.ItemKey;
            Chat(DialogueShare.FoundLine(found.Finder, name, QuestName(found.QuestKey), found.Count, found.Needed));
        }
    }

    private static System.Collections.IEnumerator ShowFind(CollectableItem item, int amount) {
        var until = Time.unscaledTime + 3f;  // the amount comes by SSMP's save sync, a moment after
        while (item.CollectedAmount < amount && Time.unscaledTime < until) yield return null;
        var message = CollectableUIMsg.Spawn(item, item.IsAtMax() ? GlobalSettings.UI.MaxItemsTextColor : Color.white);
        QuestManager.MaybeShowQuestUpdated(item, message);
    }

    /// <summary>Whether another player's find shows here at all. Not inlined: wish_message's twin skips it.</summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static bool ShowsFinds() => true;

    private static void Chat(string line) {
        Debug.Log($"[BossRule] Dialogue chat line: {line}");
        AddMessage?.Invoke(line);
    }

    private static string QuestName(string key) {
        var quest = QuestManager.GetQuest(key);
        return quest != null ? quest.DisplayName.ToString(false) : key;
    }

    /// <summary>
    /// What keeps this player busy, or "": a menu, a cutscene, their own talk, a hold on their hero, or a boss fight in
    /// their room.
    /// </summary>
    private static string BusyWhy(GameManager gm) =>
        gm.isPaused ? "paused"
        : gm.GameState != GlobalEnums.GameState.PLAYING ? gm.GameState.ToString()
        : HeroController.SilentInstance is not { } hero ? "no hero"
        : DialogueOverlay.OwnTalk() ? "own talk"
        : hero.controlReqlinquished ? "hero held"
        : Spectate.Watching ? "watching a fight"
        : Spectate.FightOnIn(gm.sceneName) ? "boss fight" : "";

    /// <summary>The main camera's view on the plane the NPCs stand on (z 0).</summary>
    private static View MyView() {
        var cam = GameCameras.SilentInstance != null ? GameCameras.SilentInstance.mainCamera : null;
        if (cam == null) return new View(0f, 0f, -1f, -1f);
        var depth = Mathf.Abs(cam.transform.position.z);
        var low = cam.ViewportToWorldPoint(new Vector3(0f, 0f, depth));
        var high = cam.ViewportToWorldPoint(new Vector3(1f, 1f, depth));
        return new View(low.x, low.y, high.x, high.y);
    }
}
