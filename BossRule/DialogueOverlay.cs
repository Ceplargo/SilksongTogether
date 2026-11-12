using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HutongGames.PlayMaker;
using TMProOld;
using UnityEngine;

namespace BossRule;

/// <summary>
/// (coop) A friend's talk on this player's screen, in the game's own dialogue box (the owner, 2026-11-07: "It should use
/// the same UI that's already in the game when talking to the NPC"), with the NPC's own name title. It only shows: the
/// box is opened as DialogueBox.ShowInstant opens it, with no NPC behind it, so nothing takes this player's controls
/// (InteractableBase and the NPC's talk do that, not the box) and no press advances it (DialogueBox.Update reads presses
/// only in a talk of its own); the HUD stays and the box's advance prompt stays hidden. It closes when the talk ends,
/// when this player leaves the room, or HOLD_S after its last line (a lost end), and gives way to this player's own talk.
/// </summary>
internal class DialogueOverlay : MonoBehaviour {
    private const float HoldS = 20f;
    private static DialogueOverlay? _instance;

    /// <summary>
    /// For checks (CoopQA component): each friend's page this game took, as "where|talker|talk|page|sheet/key|speaker|words"
    /// (where: Screen or Chat), and each page, end and quest this game sent, as "talker|talk|page|sheet/key",
    /// "talker|talk|end" and "talker|quest|name|taken or finished".
    /// </summary>
    public List<string> Log = new();

    public List<string> SentLog = new();

    /// <summary>Log and SentLog one entry a line, as CoopQA's component reads them.</summary>
    public string TookText => string.Join("\n", Log);

    public string SentText => string.Join("\n", SentLog);

    /// <summary>How many friends' talks are on screen now, for checks (0 or 1: the game has one box).</summary>
    public int Showing;

    /// <summary>
    /// For checks: what the game's own box shows now, read from the box itself: "" when it is closed, else its text
    /// (all of it, as the box holds it).
    /// </summary>
    public string GameBox => Box() is { } box && Traverse.Create(box).Field("isBoxOpen").GetValue<bool>()
        ? Traverse.Create(box).Field("textMesh").GetValue<TMP_Text>()?.text ?? "" : "";

    private Shown? _shown;

    private sealed class Shown {
        public string Talker = "";
        public int Talk;
        public string Scene = "";
        public float Until, Visible;
        public NpcDialogueTitle? Title;
    }

    /// <summary>Made once, at the plugin's start, so checks find its record before any talk.</summary>
    public static void Make() {
        if (_instance != null) return;
        var go = new GameObject("BossRule Dialogue");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<DialogueOverlay>();
    }

    /// <summary>One line of a friend's talk in the game's box (the caller checked this player is free to see it).</summary>
    public static void Show(DialoguePage page, DialogueBox.DialogueLine line) {
        Make();
        if (Box() is not { } box || OwnTalk()) return;
        var self = _instance!;
        var t = Traverse.Create(box);
        var wasOpen = self._shown != null && t.Field("isBoxOpen").GetValue<bool>();
        // ShowInstant's own steps without its "OUT INSTANT" to the HUD: this player keeps playing
        AccessTools.Method(typeof(DialogueBox), "ShowShared").Invoke(box, [line.Text, DialogueBox.DisplayOptions.Default, null]);
        t.Field("instigator").SetValue(null);
        var sameSpeaker = wasOpen && !DialogueBox.IsSpeakerDifferent(t.Field("currentLine").GetValue<DialogueBox.DialogueLine>(), line);
        AccessTools.Method(typeof(DialogueBox), "UpdateAppearance").Invoke(box, [line]);
        t.Field("stopAnimator").GetValue<Animator>()?.gameObject.SetActive(false);
        var animator = t.Field("animator").GetValue<Animator>();
        if (!sameSpeaker) animator.Play("Open", 0, wasOpen ? 1f : 0f);  // a new speaker's look at once, a first open as the game opens it
        t.Field("isBoxOpen").SetValue(true);
        var text = t.Field("textMesh").GetValue<TMP_Text>();
        text.text = line.Text;
        text.pageToDisplay = 1;
        text.maxVisibleCharacters = 0;

        if (self._shown is not { } shown || shown.Talker != page.Talker || shown.Talk != page.Talk) {
            if (self._shown?.Title is { } old) old.Hide();
            shown = new Shown { Talker = page.Talker, Talk = page.Talk, Title = TitleNear(page.NpcX, page.NpcY) };
            if (shown.Title != null) AccessTools.Method(typeof(NpcDialogueTitle), "Show").Invoke(shown.Title, [line]);
            self._shown = shown;
        }

        shown.Scene = page.Scene;
        shown.Until = Time.unscaledTime + HoldS;
        shown.Visible = 0f;
        self.Showing = 1;
    }

    public static void Took(Where where, DialoguePage page, string speaker, string words) {
        Make();
        _instance!.Log.Add($"{where}|{page.Talker}|{page.Talk}|{page.Page}|{page.TextSheet}/{page.TextKey}|{speaker}|{words}");
    }

    public static void Sent(string entry) {
        Make();
        _instance!.SentLog.Add(entry);
    }

    /// <summary>That talk's box closes, if it is the one showing.</summary>
    public static void End(string talker, int talk) {
        if (_instance?._shown is { } shown && shown.Talker == talker && shown.Talk == talk) _instance.Close();
    }

    public static void Clear() => _instance?.Close();

    /// <summary>Whether this player's own talk runs (DialogueBox).</summary>
    public static bool OwnTalk() => Box() is { } box && Traverse.Create(box).Field("isDialogueRunning").GetValue<bool>();

    private static DialogueBox? Box() => Traverse.Create(typeof(DialogueBox)).Field("_instance").GetValue<DialogueBox>();

    /// <summary>The NPC's own name title in this player's game: the NpcDialogueTitle nearest where the talker's NPC stands.</summary>
    private static NpcDialogueTitle? TitleNear(float x, float y) =>
        FindObjectsByType<NpcDialogueTitle>(FindObjectsSortMode.None)
            .Where(t => t.isActiveAndEnabled && Vector2.Distance(t.transform.position, new Vector2(x, y)) < 3f)
            .OrderBy(t => Vector2.Distance(t.transform.position, new Vector2(x, y))).FirstOrDefault();

    private void Close() {
        if (_shown == null) return;
        if (_shown.Title != null) _shown.Title.Hide();
        _shown = null;
        Showing = 0;
        if (Box() is not { } box || OwnTalk()) return;  // this player's own talk has the box now: leave it be
        var t = Traverse.Create(box);
        t.Field("isBoxOpen").SetValue(false);
        t.Field("animator").GetValue<Animator>().Play("Close");
    }

    private void Update() {
        if (_shown == null) return;
        if (OwnTalk()) {  // this player's own talk took the box over
            if (_shown.Title != null) _shown.Title.Hide();
            _shown = null;
            Showing = 0;
            return;
        }

        if (_shown.Scene != GameManager.SilentInstance?.sceneName || _shown.Until < Time.unscaledTime) {
            Close();
            return;
        }

        // the line printed as the box prints it (PrintText at its regular speed); a line longer than the box turns its
        // pages by itself, a page every PageS once printed. shortcut: the talker's own page turns within a line are not
        // sent; send them (DialogueBox.PrintText's pageIndex) if a watcher's pages run ahead of or behind the talker's.
        if (Box() is not { } box) return;
        var t = Traverse.Create(box);
        var text = t.Field("textMesh").GetValue<TMP_Text>();
        var info = text.textInfo;
        if (info == null || info.pageCount == 0) return;
        _shown.Visible += t.Field("regularRevealSpeed").GetValue<float>() * Time.deltaTime;
        var page = Mathf.Clamp(text.pageToDisplay - 1, 0, info.pageCount - 1);
        var last = info.pageInfo[page].lastCharacterIndex;
        var first = info.pageInfo[page].firstCharacterIndex;
        text.maxVisibleCharacters = Mathf.Min(first + Mathf.RoundToInt(_shown.Visible), last + 1);
        if (first + _shown.Visible > last + PageS * 65f && page < info.pageCount - 1) {
            text.pageToDisplay = page + 2;
            _shown.Visible = 0f;
        }
    }

    private const float PageS = 2.5f;
}
