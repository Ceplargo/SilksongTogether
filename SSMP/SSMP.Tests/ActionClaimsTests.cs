using SSMP.Game.Server.Save;
using Xunit;

namespace SSMP.Tests;

public class ActionClaimsTests {
    [Fact]
    public void A_second_claim_of_one_marker_is_refused() {
        var claims = new ActionClaims();
        Assert.True(claims.Claim("quest:Pilgrim Rags", "host", 0));
        Assert.False(claims.Claim("quest:Pilgrim Rags", "friend", 0.02));
    }

    [Fact]
    public void A_second_claim_by_the_same_player_is_not_refused() {
        var claims = new ActionClaims();
        Assert.True(claims.Claim("pb:Bone_East_20:Heart Piece", "friend", 0));
        // The same action's next update, one resent, and one held over a dropped connection and sent with a rejoin
        Assert.True(claims.Claim("pb:Bone_East_20:Heart Piece", "friend", 0.5));
        Assert.True(claims.Claim("pb:Bone_East_20:Heart Piece", "friend", 30));
    }

    [Fact]
    public void Claims_of_two_markers_both_pass() {
        var claims = new ActionClaims();
        Assert.True(claims.Claim("item:Magnetite Brooch", "host", 0));
        Assert.True(claims.Claim("item:Curveclaw", "friend", 0));
    }

    [Fact]
    public void Shared_values_and_money_are_put_back() {
        Assert.True(ActionClaims.PutsBack(true, "heartPieces"));
        Assert.True(ActionClaims.PutsBack(true, null)); // a world object's saved value
        Assert.True(ActionClaims.PutsBack(false, "geo"));
        Assert.True(ActionClaims.PutsBack(false, "ShellShards"));
    }

    [Fact]
    public void A_players_own_value_other_than_money_is_never_put_back() {
        // The game works maxHealth out from the shared maxHealthBase: putting the loser's old one back took the mask
        // the winner's shard gave everyone
        Assert.False(ActionClaims.PutsBack(false, "maxHealth"));
        Assert.False(ActionClaims.PutsBack(false, "playTime"));
    }

    [Fact]
    public void A_quest_end_that_cannot_end_opens_no_claim() {
        // An NPC's talk tries to end its quest to see whether it is ready (TryEndQuestV2's fail event): that must not
        // make the talker the quest's owner, or the other player's real hand-in a minute later would be refused
        Assert.False(ActionClaims.EndOpens(forceEnd: false, canComplete: false));
        Assert.True(ActionClaims.EndOpens(forceEnd: false, canComplete: true));
        Assert.True(ActionClaims.EndOpens(forceEnd: true, canComplete: false));
    }

    [Fact]
    public void An_actions_own_changes_carry_its_marker() {
        // what the action pays or gives, and the one value it sets
        Assert.True(ActionClaims.Owns("QuestCompletionData", "QuestCompletionData", false, null));
        Assert.True(ActionClaims.Owns("QuestCompletionData", "geo", false, null));
        Assert.True(ActionClaims.Owns("QuestCompletionData", "ShellShards", false, null));
        Assert.True(ActionClaims.Owns("QuestCompletionData", "Collectables", true, null));
        Assert.True(ActionClaims.Owns("Bone_East_20:Heart Piece", null, false, "Bone_East_20:Heart Piece"));
        Assert.True(ActionClaims.Owns("PurchasedBonebottomBrooch", "PurchasedBonebottomBrooch", false, null));
    }

    [Fact]
    public void Another_change_while_an_action_is_open_carries_no_marker() {
        // 0.3.14's gate: the host's game ended The Threadspun Town after the friend's, and these four, made while its
        // marker was open, were put back (hit_merge's defeatedMossMother among them, so Moss Mother's fight never started)
        foreach (var name in new[] { "spinnerDefeated", "encounteredSpinner", "bellShrineBellhart", "defeatedMossMother" }) {
            Assert.False(ActionClaims.Owns("QuestCompletionData", name, false, null));
        }

        Assert.False(ActionClaims.Owns("Bone_East_20:Heart Piece", null, false, "Bone_East_20:Breakable Wall"));
        Assert.False(ActionClaims.Owns(null, "defeatedMossMother", false, null)); // a shop item with no purchase flag
    }

    [Fact]
    public void A_marker_closes_when_its_room_is_left() {
        // held by a sequence, the host's hero kept its marker open across the tests' room changes (0.3.14's gate)
        Assert.True(ActionClaims.Closes(now: 3, opened: 0, busyUntil: 10, roomLeft: true, min: 2, max: 120));
        Assert.False(ActionClaims.Closes(now: 3, opened: 0, busyUntil: 10, roomLeft: false, min: 2, max: 120));
    }

    [Fact]
    public void A_marker_stays_open_its_shortest_time_and_while_the_hero_is_held() {
        Assert.False(ActionClaims.Closes(now: 1, opened: 0, busyUntil: 0, roomLeft: false, min: 2, max: 120));
        Assert.True(ActionClaims.Closes(now: 2.5, opened: 0, busyUntil: 0, roomLeft: false, min: 2, max: 120));
        Assert.True(ActionClaims.Closes(now: 121, opened: 0, busyUntil: 200, roomLeft: false, min: 2, max: 120));
    }

    [Fact]
    public void A_claim_ends_a_minute_after_its_players_last_update() {
        var claims = new ActionClaims();
        claims.Claim("quest:Pilgrim Rags", "host", 0);
        claims.Claim("quest:Pilgrim Rags", "host", 10);
        Assert.False(claims.Claim("quest:Pilgrim Rags", "friend", 69));
        Assert.True(claims.Claim("quest:Pilgrim Rags", "friend", 70));
    }
}
