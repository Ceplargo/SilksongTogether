using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace BossRule;

/// <summary>
/// (coop) One page of a talk in the talker's game: the talker's name, their talk's number (a talk opened again is a new
/// one), the page (DialogueBox's line index; <see cref="DialogueShare.EndPage"/> once the talk ends), the speaker's name
/// and the text as language keys (each game looks them up in its own language; an empty TextSheet means TextKey is the
/// game's own words, from a talk that had no key), and where the NPC stands.
/// </summary>
internal record DialoguePage(string Talker, int Talk, int Page, string SpeakerSheet, string SpeakerKey, string TextSheet,
    string TextKey, string Scene, float NpcX, float NpcY);

/// <summary>(coop) A quest taken or finished in the talker's game; QuestKey is the quest's asset name.</summary>
internal record QuestNote(string Talker, string QuestKey, bool Done);

/// <summary>
/// (coop, 0.3.21) An item a wish counts, found in the finder's game (CollectableItem.Collect): the finder's name, the
/// item's and the wish's asset names, the wish's count of it and the count it needs, the finder's amount after, and
/// where the finder stood. The other players see the game's own message when the finder is in their room and view, else
/// a chat line (the owner, 2026-11-10: as the shared dialogue).
/// </summary>
internal record FoundNote(string Finder, string ItemKey, string QuestKey, int Count, int Needed, int Amount, string Scene,
    float X, float Y);

internal enum Where { Screen, Chat }

/// <summary>The watcher's camera view in world units.</summary>
internal readonly record struct View(float MinX, float MinY, float MaxX, float MaxY) {
    public bool Contains(float x, float y) => x >= MinX && x <= MaxX && y >= MinY && y <= MaxY;
}

/// <summary>
/// (coop) A friend's NPC talk on the other players' screens (the owner, 2026-11-07): the message as one string, and
/// where the watcher's game shows each page. Unity-free.
/// </summary>
internal static class DialogueShare {
    public const int EndPage = -1;
    private const char Sep = '\u001f';
    private static readonly Regex Tags = new("<[^>]*>");

    public static string Encode(DialoguePage p) => string.Join(Sep.ToString(), "P", p.Talker, Num(p.Talk), Num(p.Page),
        p.SpeakerSheet, p.SpeakerKey, p.TextSheet, p.TextKey, p.Scene, Num(p.NpcX), Num(p.NpcY));

    public static string Encode(QuestNote n) => string.Join(Sep.ToString(), "Q", n.Talker, n.QuestKey, n.Done ? "1" : "0");

    public static string Encode(FoundNote n) => string.Join(Sep.ToString(), "F", n.Finder, n.ItemKey, n.QuestKey,
        Num(n.Count), Num(n.Needed), Num(n.Amount), n.Scene, Num(n.X), Num(n.Y));

    /// <summary>A <see cref="DialoguePage"/>, <see cref="QuestNote"/> or <see cref="FoundNote"/>; null for anything else.</summary>
    public static object? Decode(string text) {
        var f = text.Split(Sep);
        if (f[0] == "Q" && f.Length == 4 && f[3] is "0" or "1") return new QuestNote(f[1], f[2], f[3] == "1");
        if (f[0] == "F" && f.Length == 10) {
            return int.TryParse(f[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var count)
                   && int.TryParse(f[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out var needed)
                   && int.TryParse(f[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount)
                   && float.TryParse(f[8], NumberStyles.Float, CultureInfo.InvariantCulture, out var fx)
                   && float.TryParse(f[9], NumberStyles.Float, CultureInfo.InvariantCulture, out var fy)
                ? new FoundNote(f[1], f[2], f[3], count, needed, amount, f[7], fx, fy)
                : null;
        }

        if (f[0] != "P" || f.Length != 11) return null;
        if (!int.TryParse(f[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var talk)
            || !int.TryParse(f[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var page)
            || !float.TryParse(f[9], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
            || !float.TryParse(f[10], NumberStyles.Float, CultureInfo.InvariantCulture, out var y)) return null;
        return new DialoguePage(f[1], talk, page, f[4], f[5], f[6], f[7], f[8], x, y);
    }

    /// <summary>On the watcher's screen only when nothing keeps them busy and the NPC is in their room and view.</summary>
    public static Where Route(DialoguePage page, string myScene, View myView, bool busy) =>
        !busy && page.Scene == myScene && myView.Contains(page.NpcX, page.NpcY) ? Where.Screen : Where.Chat;

    /// <summary>"Speaker: words" (just the words when the speaker has no name), text tags and line breaks out.</summary>
    public static string ChatLine(string speaker, string words) {
        var plain = Plain(words);
        return speaker == "" ? plain : $"{speaker}: {plain}";
    }

    /// <summary>On the watcher's screen only when nothing keeps them busy and the finder is in their room and view.</summary>
    public static Where Route(FoundNote note, string myScene, View myView, bool busy) =>
        !busy && note.Scene == myScene && myView.Contains(note.X, note.Y) ? Where.Screen : Where.Chat;

    /// <summary>"Ann found Rock Roller Item (Rock Rollers 3/3)".</summary>
    public static string FoundLine(string finder, string item, string quest, int count, int needed) =>
        $"{finder} found {Plain(item)} ({Plain(quest)} {count}/{needed})";

    public static string QuestLine(string talker, string quest, bool done) => $"{talker} {(done ? "finished" : "took")} {quest}";

    /// <summary>The text with its rich-text tags out and its lines joined.</summary>
    public static string Plain(string words) =>
        Regex.Replace(Tags.Replace(words, ""), @"\s*\n\s*", " ").Trim();

    private static string Num(int v) => v.ToString(CultureInfo.InvariantCulture);

    private static string Num(float v) => v.ToString("R", CultureInfo.InvariantCulture);
}

/// <summary>
/// (coop) The watcher's side of each talker's talk: drops a page already taken (a resend, or a page shown twice), and once
/// one of a talk's pages went to the chat, sends the rest of that talk there too (a watcher who walked out of view).
/// </summary>
internal class DialogueWatch {
    private readonly Dictionary<string, (int Talk, int Page, bool Chat)> _last = new();

    /// <summary>Where this page shows; null to drop it.</summary>
    public Where? Take(DialoguePage page, string myScene, View myView, bool busy) {
        var known = _last.TryGetValue(page.Talker, out var last) && last.Talk == page.Talk;
        if (known && page.Page <= last.Page) return null;
        var where = known && last.Chat ? Where.Chat : DialogueShare.Route(page, myScene, myView, busy);
        _last[page.Talker] = (page.Talk, page.Page, where == Where.Chat);
        return where;
    }
}
