using System.Collections.Generic;
using SSMP.Game.Client.Entity;
using Xunit;

namespace SSMP.Tests;

public class TakeoverCrawlersTests {
    // The Blade Spider's Behaviour FSM (Coral_03's dump): StartCrawler in Start Idle, Return Control and Alert End;
    // StopCrawler in Init, Sing, Attack Antic, Face Hero?, Alert? and Fall
    private static TakeoverCrawlers.State S(bool starts, bool stops, params string[] to) => new(starts, stops, to);

    // with its SetCrawlerSpeed: Start Idle sets 4, Alert End 9
    private static TakeoverCrawlers.State S(float speed, bool starts, bool stops, params string[] to) =>
        new(starts, stops, to, speed);

    private static readonly Dictionary<string, TakeoverCrawlers.State> Spider = new() {
        ["Init"] = S(false, true, "Start Idle", "Ambush Ready"),
        ["Crawl Idle"] = S(false, false, "Face Hero?", "Sing", "Attack Antic", "Fall"),
        ["Sing"] = S(false, true, "Sing End"),
        ["Sing End"] = S(false, false, "Return Control"),
        ["Return Control"] = S(true, false, "Crawl Alert"),
        ["Attack Antic"] = S(false, true, "Cancel Frame", "Ground Dash", "Fall"),
        ["Attack"] = S(false, false, "Attack Recover", "Multihit"),
        ["Cancel Frame"] = S(false, false, "Crawl Alert"),
        ["Crawl Alert"] = S(false, false, "Turn Wait", "Sing", "Attack Antic", "Start Idle", "Fall"),
        ["Face Hero?"] = S(false, true, "Alert"),
        ["Alert"] = S(false, false, "Alert End", "Fall"),
        ["Alert End"] = S(9f, true, false, "Crawl Alert"),
        ["Start Idle"] = S(4f, true, false, "Crawl Idle"),
        ["Alert?"] = S(false, true, "Start Idle", "Face Hero?"),
        ["Turn Wait"] = S(false, false, "Alert?"),
        ["Ambush Ready"] = S(false, false, "Ambush 1"),
        ["Ambush 1"] = S(false, false, "Ambush 2"),
        ["Ambush 2"] = S(false, false, "Start Idle"),
        ["Fall"] = S(false, true, "Land"),
        ["Land"] = S(false, false, "Start Idle"),
        ["Ground Dash"] = S(false, false, "Attack"),
        ["Multihit"] = S(false, false, "Attack Recover"),
        ["Attack Recover"] = S(false, false, "Return Control"),
    };

    // Coral_03's wall spiders and Coral_02's floor spider at the takeover (the takeover pass, 2026-11-16)
    [Fact]
    public void Crawl_idle_after_start_idle_walks() => Assert.True(TakeoverCrawlers.WalksOn("Crawl Idle", Spider));

    [Fact]
    public void Crawl_alert_walks_after_alert_end() => Assert.True(TakeoverCrawlers.WalksOn("Crawl Alert", Spider));

    [Fact]
    public void A_state_after_a_stop_does_not_walk() => Assert.False(TakeoverCrawlers.WalksOn("Alert", Spider));

    // its own StartCrawler starts it on entry (entered again once active: TakeoverStates)
    [Fact]
    public void A_state_that_starts_it_itself_is_left_to_itself() =>
        Assert.False(TakeoverCrawlers.WalksOn("Start Idle", Spider));

    [Fact]
    public void An_ambush_before_any_start_does_not_walk() =>
        Assert.False(TakeoverCrawlers.WalksOn("Ambush 1", Spider));

    // a spider in Fall at the takeover: StopCrawler left its body as Start Idle set it (gravity 0 on a wall)
    [Fact]
    public void A_state_after_a_start_and_a_stop_is_set_up() => Assert.True(TakeoverCrawlers.SetUp("Fall", Spider));

    [Fact]
    public void Crawl_idle_is_set_up() => Assert.True(TakeoverCrawlers.SetUp("Crawl Idle", Spider));

    // Coral_02's hanging spider waits in Ambush Ready, held kinematic, never set up as a crawler (NOTES 466)
    [Fact]
    public void An_ambush_before_any_start_is_not_set_up() =>
        Assert.False(TakeoverCrawlers.SetUp("Ambush Ready", Spider));

    [Fact]
    public void The_start_state_is_not_set_up() => Assert.False(TakeoverCrawlers.SetUp("Init", Spider));

    // Coral_03's spiders, taken over in Crawl Idle, walked at their prefab's speed 0 (the takeover pass, 2026-11-16)
    [Fact]
    public void Crawl_idle_walks_at_start_idles_speed() =>
        Assert.Equal(4f, TakeoverCrawlers.SpeedAt("Crawl Idle", Spider));

    // the nearest way in: Alert End's 9, one state back, before Return Control's way (Start Idle's 4, further)
    [Fact]
    public void Crawl_alert_walks_at_the_nearest_speed() =>
        Assert.Equal(9f, TakeoverCrawlers.SpeedAt("Crawl Alert", Spider));

    [Fact]
    public void An_ambush_before_any_speed_has_none() => Assert.Null(TakeoverCrawlers.SpeedAt("Ambush 1", Spider));

    [Fact]
    public void An_unknown_state_does_not_walk() => Assert.False(TakeoverCrawlers.WalksOn("Nowhere", Spider));

    // The Dock Worker's Behaviour FSM (Bone_09's dump), the states around its walk: StartWalkerV2 in Start Walker,
    // StopWalkerV2 in Attack Antic, Pray, Bonk and Turn?
    private static readonly Dictionary<string, TakeoverCrawlers.State> Worker = new() {
        ["Init"] = S(false, false, "Start Walker", "Start Dig"),
        ["Start Walker"] = S(true, false, "Walk"),
        ["Walk"] = S(false, false, "Attack Antic", "Pray", "Turn?"),
        ["Attack Antic"] = S(false, true, "Attack"),
        ["Attack"] = S(false, false, "Attack End"),
        ["Attack End"] = S(false, false, "Next Move"),
        ["Next Move"] = S(false, false, "Idle Pause", "Run", "Pray"),
        ["Run"] = S(false, false, "Run End"),
        ["Run End"] = S(false, false, "Face Hero?"),
        ["Idle Pause"] = S(false, false, "Face Hero?"),
        ["Face Hero?"] = S(false, false, "Start Walker"),
        ["Pray"] = S(false, true, "End Pray"),
        ["End Pray"] = S(false, false, "Start Walker"),
        ["Turn?"] = S(false, true, "Aggro Run", "Flip2"),
        ["Aggro Run"] = S(false, false, "Attack Antic", "Start Walker"),
        ["Flip2"] = S(false, false, "Aggro Run"),
        ["Start Dig"] = S(false, false, "Digging"),
        ["Digging"] = S(false, false),
    };

    // Bone_09's two Dock Workers, taken over in Walk, stood still (the takeover pass, 2026-11-16)
    [Fact]
    public void A_dock_worker_walks_on_in_walk() => Assert.True(TakeoverCrawlers.WalksOn("Walk", Worker));

    // its run is WalkLeftRight's, after an attack's StopWalkerV2
    [Fact]
    public void A_dock_worker_does_not_walk_in_its_run() => Assert.False(TakeoverCrawlers.WalksOn("Run", Worker));

    [Fact]
    public void A_digging_dock_worker_does_not_walk() => Assert.False(TakeoverCrawlers.WalksOn("Digging", Worker));

    // a Pilgrim's pilgrim_behaviour (Greymoor_13's dump): StartWalker in Start Walk and Attack Recover, StopWalker in
    // Attack Antic; the Farmer Scissors' Control has StartWalker in its Walker state itself
    private static readonly Dictionary<string, TakeoverCrawlers.State> Pilgrim = new() {
        ["Init"] = S(false, false, "Start Walk", "Ambush"),
        ["Start Walk"] = S(true, false, "Walk"),
        ["Walk"] = S(false, false, "Attack Antic"),
        ["Attack Antic"] = S(false, true, "Attack"),
        ["Attack"] = S(false, false, "Attack Recover"),
        ["Attack Recover"] = S(true, false, "Walk"),
        ["Ambush"] = S(false, false, "Start Walk"),
        ["Walker"] = S(true, false, "Attack Antic"),
    };

    // Greymoor_12's and Greymoor_13's Pilgrims, taken over in Walk (the takeover pass, 2026-11-16)
    [Fact]
    public void An_old_walker_walks_in_walk() => Assert.True(TakeoverCrawlers.WalksIn("Walk", Pilgrim));

    // the Farmer Scissors, taken over in their Walker state, which starts it itself
    [Fact]
    public void An_old_walker_walks_in_a_state_that_starts_it() => Assert.True(TakeoverCrawlers.WalksIn("Walker", Pilgrim));

    [Fact]
    public void An_old_walker_waits_in_an_ambush() => Assert.False(TakeoverCrawlers.WalksIn("Ambush", Pilgrim));

    [Fact]
    public void An_old_walker_stops_for_an_attack() => Assert.False(TakeoverCrawlers.WalksIn("Attack", Pilgrim));
}
