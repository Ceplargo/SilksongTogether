using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace BossRule.Tests;

public class MarkRecordTests {
    // As written: Newtonsoft would read the time back as a DateTime in this PC's zone
    private static JObject Parse(string json) =>
        JObject.Load(new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None });

    [Fact]
    public void A_budget_takes_50_from_each_source_a_session() {
        var budget = new MarkBudget();
        for (var i = 0; i < MarkBudget.Max; i++) Assert.True(budget.Take(7));
        Assert.False(budget.Take(7));
        Assert.True(budget.Take(8));
        budget.Forget(7);
        Assert.True(budget.Take(7));
    }

    [Fact]
    public void RecentActions_KeepsOnlyTheLast10Seconds() {
        var recent = new RecentActions();
        foreach (var t in new[] { 0f, 1.5f, 5f, 9.5f, 12f }) recent.Add(t, "sent", $"Crawler {t}", "Control", "Walk", "Tk2dPlayAnimation");

        Assert.Equal(["Crawler 5", "Crawler 9.5", "Crawler 12"], recent.Since(12f).Select(a => a.Object));
    }

    [Fact]
    public void RecentActions_HoldsAtMostItsSize() {
        var recent = new RecentActions(10, 4);
        for (var i = 0; i < 10; i++) recent.Add(i * 0.1f, "applied", $"Crawler {i}", "Control", "Walk", "Tk2dPlayAnimation");

        Assert.Equal(["Crawler 6", "Crawler 7", "Crawler 8", "Crawler 9"], recent.Since(1f).Select(a => a.Object));
    }

    [Fact]
    public void MarkRecord_HasTheRoomPlayersEnemiesAndCopiedActions() {
        var recent = new RecentActions();
        recent.Add(97.5f, "applied", "Crawler (Clone)", "Crawler", "Walk", "Tk2dPlayAnimation");
        var json = MarkRecord.Json("2026-10-29_21-05-07-Host", "Host", "Client", new DateTimeOffset(2026, 10, 29, 21, 5, 7, TimeSpan.FromHours(1)),
            "Bone_East_02",
            [new MarkPlayer("Client", "Bone_East_02", 10.5f, 4f), new MarkPlayer("Host", "Bone_East_02", 12f, 4f)],
            [new MarkEnemy("Crawler (Clone)", 30, 20f, 3.5f, new Dictionary<string, string?> { ["Crawler"] = "Walk" })],
            recent.Since(100f), 100f);

        var o = Parse(json);
        Assert.Equal("2026-10-29_21-05-07-Host", (string?) o["mark"]);
        Assert.Equal("Host", (string?) o["by"]);
        Assert.Equal("Client", (string?) o["player"]);
        Assert.Equal("2026-10-29T21:05:07.000+01:00", (string?) o["time"]);
        Assert.Equal("Bone_East_02", (string?) o["scene"]);
        Assert.Equal(["Client", "Host"], o["players"]!.Select(p => (string?) p["name"]));
        Assert.Equal(12f, (float) o["players"]![1]!["x"]!);
        var enemy = o["enemies"]![0]!;
        Assert.Equal("Crawler (Clone)", (string?) enemy["path"]);
        Assert.Equal(30, (int) enemy["hp"]!);
        Assert.Equal("Walk", (string?) enemy["fsms"]!["Crawler"]);
        var copied = o["copied"]![0]!;
        Assert.Equal(2.5f, (float) copied["ago"]!);
        Assert.Equal(["applied", "Crawler (Clone)", "Crawler", "Walk", "Tk2dPlayAnimation"],
            new[] { "way", "object", "fsm", "state", "action" }.Select(k => (string?) copied[k]));
    }

    [Fact]
    public void MarkRecord_WithNothingLoaded_IsStillWritten() {
        var o = Parse(MarkRecord.Json("2026-10-29_21-05-07-Host", "Host", "Host", DateTimeOffset.Now, null, [], [],
            new RecentActions().Since(5f), 5f));

        Assert.Equal(JTokenType.Null, o["scene"]!.Type);
        Assert.Empty(o["players"]!);
        Assert.Empty(o["enemies"]!);
        Assert.Empty(o["copied"]!);
    }

    [Fact]
    public void MarkId_IsSafeAsAFolderName() {
        Assert.Equal("2026-10-29_21-05-07-Bo_b-2", MarkRecord.Id(new DateTime(2026, 10, 29, 21, 5, 7), "Bo_b-2"));
        Assert.Equal("2026-10-29_21-05-07-evilxy", MarkRecord.Id(new DateTime(2026, 10, 29, 21, 5, 7), "../ev\\il:x\ny*é"));
        Assert.Equal("2026-10-29_21-05-07-player", MarkRecord.Id(new DateTime(2026, 10, 29, 21, 5, 7), "..."));
        Assert.Equal("2026-10-29_21-05-07-evil", MarkRecord.Safe("2026-10-29_21-05-07-../evil"));
        Assert.Equal(64, MarkRecord.Safe(new string('a', 500)).Length);
    }
}
