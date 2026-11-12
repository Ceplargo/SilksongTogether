using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using TeamCherry.Localization;
using UnityEngine;

namespace BossRule;

/// <summary>
/// (coop) The talker's side of a shared talk (the owner, 2026-11-07): each page their game shows, as language keys, and
/// each quest they take or finish, sent to the other players. Only reads: the talker's game runs its talk as alone.
/// Hook points: NOTES 360, Task 2.
/// </summary>
internal static class DialogueHooks {
    /// <summary>Sends one message to the other players; null while not connected.</summary>
    public static Action<string>? Send;

    public static Func<string>? Username;

    private static (string Sheet, string Key)? _pending;
    private static NPCControlBase? _npc;
    private static (string Sheet, string Key) _text;
    private static int _talk, _page;

    public static void Apply(Harmony harmony) {
        var me = typeof(DialogueHooks);
        harmony.Patch(AccessTools.Method(typeof(DialogueBox), nameof(DialogueBox.StartConversation),
                [typeof(LocalisedString), typeof(NPCControlBase), typeof(bool), typeof(DialogueBox.DisplayOptions), typeof(Action)]),
            prefix: new HarmonyMethod(me, nameof(KeyedPrefix)));
        harmony.Patch(AccessTools.Method(typeof(RunDialogueBase), "StartDialogue"), prefix: new HarmonyMethod(me, nameof(FsmPrefix)));
        harmony.Patch(AccessTools.Method(typeof(DialogueBox), nameof(DialogueBox.StartConversation),
                [typeof(string), typeof(NPCControlBase), typeof(bool), typeof(DialogueBox.DisplayOptions), typeof(Action), typeof(Action)]),
            prefix: new HarmonyMethod(me, nameof(StartedPrefix)));
        harmony.Patch(AccessTools.Method(typeof(NPCControlBase), nameof(NPCControlBase.NewLineStarted)),
            postfix: new HarmonyMethod(me, nameof(LinePostfix)));
        harmony.Patch(AccessTools.Method(typeof(DialogueBox), nameof(DialogueBox.EndConversation)),
            postfix: new HarmonyMethod(me, nameof(EndPostfix)));
        harmony.Patch(AccessTools.Method(typeof(FullQuestBase), nameof(FullQuestBase.BeginQuest)),
            prefix: new HarmonyMethod(me, nameof(BeginPrefix)), postfix: new HarmonyMethod(me, nameof(BeginPostfix)));
        harmony.Patch(AccessTools.Method(typeof(FullQuestBase), nameof(FullQuestBase.TryEndQuest)),
            postfix: new HarmonyMethod(me, nameof(EndQuestPostfix)));
        harmony.Patch(AccessTools.Method(typeof(CollectableItem), nameof(CollectableItem.Collect)),
            prefix: new HarmonyMethod(me, nameof(CollectPrefix)), postfix: new HarmonyMethod(me, nameof(CollectPostfix)));
    }

    private static void CollectPrefix(CollectableItem __instance, out int __state) => __state = __instance.CollectedAmount;

    /// <summary>
    /// (coop, 0.3.21) This game's hero found an item a wish counts (CollectableItem.Collect: a pickup taken, the Rock
    /// Rollers wish's Rock Roller Item): the other players see it as the game shows it here when the finder is in their
    /// view, else as a chat line (FoundNote; the owner, 2026-11-10).
    /// </summary>
    private static void CollectPostfix(CollectableItem __instance, int __state) {
        if (Send == null || __instance.CollectedAmount <= __state || HeroController.SilentInstance is not { } hero ||
            GameManager.SilentInstance is not { } gm) return;
        foreach (var quest in QuestManager.GetActiveQuests()) {
            foreach (var (target, count) in quest.TargetsAndCounters) {
                if (target.Counter != __instance) continue;
                var at = hero.transform.position;
                var note = new FoundNote(Username?.Invoke() ?? "", __instance.name, quest.name, count, target.Count,
                    __instance.CollectedAmount, gm.sceneName, at.x, at.y);
                Debug.Log($"[BossRule] Find sent: {__instance.name} for {quest.name} {count}/{target.Count} in {gm.sceneName}");
                Share(DialogueShare.Encode(note));
                return;
            }
        }
    }

    /// <summary>The one send of a page or a quest note (a check's twin skips it).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Share(string body) => Send?.Invoke(body);

    private static void KeyedPrefix(LocalisedString text) => _pending = (text.Sheet, text.Key);

    private static void FsmPrefix(RunDialogueBase __instance) {
        var t = Traverse.Create(__instance);
        if (t.Field("CustomText").FieldExists() && t.Field("CustomText").GetValue<FsmString>() is { IsNone: false }) {
            _pending = null;  // the game's own words: StartedPostfix sends them as they are
            return;
        }

        _pending = (t.Field("Sheet").GetValue<FsmString>()?.Value ?? "", t.Field("Key").GetValue<FsmString>()?.Value ?? "");
    }

    // A prefix: StartConversation starts the box's coroutine at once, and with the box still open from the last talk its
    // first line starts inside the call (DialogueBox.RunDialogue), so the new talk is counted before it (run
    // 2026-11-07_214611-pair5: the next talk's first line went out as the last talk's page 1).
    private static void StartedPrefix(string text, NPCControlBase instigator) {
        var pending = _pending;
        _pending = null;
        // DialogueBox runs nothing without its box or an instigator (StartConversation, RunDialogue)
        if (instigator == null || Traverse.Create(typeof(DialogueBox)).Field("_instance").GetValue<DialogueBox>() == null) return;
        _talk++;
        _page = 0;
        _npc = instigator;
        _text = pending is { Key: not "" } keyed ? keyed : ("", text);
    }

    private static void LinePostfix(NPCControlBase __instance, DialogueBox.DialogueLine line) {
        if (__instance != _npc || Send == null) return;
        var pos = __instance.transform.position;
        var page = new DialoguePage(Username?.Invoke() ?? "", _talk, _page++, "Titles", line.IsPlayer ? "" : TitleKey(__instance, line.Event),
            _text.Sheet, _text.Key, GameManager.instance.sceneName, pos.x, pos.y);
        Debug.Log($"[BossRule] Dialogue sent: talk {page.Talk} page {page.Page} {page.TextSheet}/{page.TextKey} speaker {page.SpeakerKey}");
        DialogueOverlay.Sent($"{page.Talker}|{page.Talk}|{page.Page}|{page.TextSheet}/{page.TextKey}");
        Share(DialogueShare.Encode(page));
    }

    /// <summary>The talk's box closes (its end, an FSM's EndDialogue, or a room change cutting it short).</summary>
    private static void EndPostfix() {
        if (_npc == null) return;
        _npc = null;
        if (Send == null) return;
        Debug.Log($"[BossRule] Dialogue sent: talk {_talk} ended");
        DialogueOverlay.Sent($"{Username?.Invoke()}|{_talk}|end");
        Share(DialogueShare.Encode(new DialoguePage(Username?.Invoke() ?? "", _talk, DialogueShare.EndPage, "", "", "", "",
            GameManager.instance.sceneName, 0f, 0f)));
    }

    private static void BeginPrefix(FullQuestBase __instance, out bool __state) => __state = __instance.IsAccepted && !__instance.IsCompleted;

    private static void BeginPostfix(FullQuestBase __instance, bool __state) {
        if (!__state && __instance.IsAccepted && !__instance.IsCompleted) Quest(__instance, false);
    }

    private static void EndQuestPostfix(FullQuestBase __instance, bool __result) {
        if (__result) Quest(__instance, true);
    }

    private static void Quest(FullQuestBase quest, bool done) {
        if (Send == null) return;
        Debug.Log($"[BossRule] Quest sent: {quest.name} {(done ? "finished" : "taken")}");
        DialogueOverlay.Sent($"{Username?.Invoke()}|quest|{quest.name}|{(done ? "finished" : "taken")}");
        Share(DialogueShare.Encode(new QuestNote(Username?.Invoke() ?? "", quest.name, done)));
    }

    /// <summary>
    /// The speaker's name key in the Titles sheet: the NPC's NpcDialogueTitle title for this line's speaker event, picked
    /// as NpcDialogueTitle.Show picks it, then "_MAIN" as the Area Title looks it up; "" for an NPC with no title.
    /// </summary>
    private static string TitleKey(NPCControlBase npc, string speakerEvent) {
        var title = npc.GetComponent<NpcDialogueTitle>();
        if (title == null) {
            foreach (var t in UnityEngine.Object.FindObjectsByType<NpcDialogueTitle>(FindObjectsSortMode.None)) {
                if (Array.IndexOf(Traverse.Create(t).Field("npcControls").GetValue<NPCControlBase[]>() ?? [], npc) >= 0) {
                    title = t;
                    break;
                }
            }
        }

        if (title == null || Traverse.Create(title).Field("titles").GetValue() is not Array titles) return "";
        string? chosen = null;
        foreach (var entry in titles) {
            var t = Traverse.Create(entry);
            if (chosen == null || t.Field("SpeakerEvent").GetValue<string>() == speakerEvent) chosen = t.Field("Title").GetValue<string>();
        }

        return string.IsNullOrEmpty(chosen) ? "" : chosen + "_MAIN";
    }
}
