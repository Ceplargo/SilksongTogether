using Xunit;

namespace BossRule.Tests;

// (0.3.22) A2: every player in the game gets a quest's full reward (Task 3's reads: the couriers pay a Money Reward,
// Pilgrim Rags and Shiny Bell Goomba a Rosary_Set_Medium, Skull King a Rosary_Set_Large, the rest one-of-a-kind items)
public class QuestRewardsTests {
    [Fact]
    public void A_money_reward_handed_in_in_one_game_is_paid_in_each_other_game() =>
        Assert.True(QuestRewards.PaysOthers("CurrencyCollectable", "Money Reward", unique: false));

    [Fact]
    public void A_rosary_set_reward_is_paid_in_each_other_game() {
        Assert.True(QuestRewards.PaysOthers("CollectableItemBasic", "Rosary_Set_Medium", unique: false));
        Assert.True(QuestRewards.PaysOthers("CollectableItemBasic", "Rosary_Set_Large", unique: false));
    }

    [Fact]
    public void A_one_of_a_kind_reward_is_never_paid_again() {
        Assert.False(QuestRewards.PaysOthers("CollectableItemBasic", "Heart Piece", unique: false));
        Assert.False(QuestRewards.PaysOthers("ToolItemBasic", "Longneedle", unique: true));
        Assert.False(QuestRewards.PaysOthers("CurrencyCollectable", "Money Reward", unique: true));
    }

    [Fact]
    public void A_quest_with_no_reward_pays_nothing() => Assert.False(QuestRewards.PaysOthers(null, null, unique: false));

    // both_at_once's Pilgrim Rags (2026-11-12): both handed in at once and each game paid the other's note too (2 sets)
    [Fact]
    public void A_game_that_handed_in_the_same_quest_first_skips_the_other_players_note() {
        Assert.Equal(QuestRewards.Payout.Wait, QuestRewards.OnNote(100.5, 100, null));
        Assert.Equal(QuestRewards.Payout.Skip, QuestRewards.OnNote(104, 100, null));
        Assert.Equal(QuestRewards.Payout.Skip, QuestRewards.OnNote(104, 100, 90)); // a put-back of an earlier hand-in
    }

    [Fact]
    public void A_game_whose_hand_in_the_lock_put_back_pays_the_first_players_note() =>
        Assert.Equal(QuestRewards.Payout.Pay, QuestRewards.OnNote(100.5, 100, 100.3));

    // both_at_once's Pilgrim Rags (2026-11-12, after the lock rule): the friend's deposit opened its marker, the
    // host's note paid it meanwhile, and the put-back of its refused deposit took the set again (host 1, friend 0)
    [Fact]
    public void A_note_waits_while_this_games_own_action_on_the_quest_is_open() {
        Assert.Equal(QuestRewards.Payout.Wait, QuestRewards.OnNote(100, null, null, ownOpen: true));
        Assert.Equal(QuestRewards.Payout.Wait, QuestRewards.OnNote(110, 100, 100.3, ownOpen: true));
        Assert.Equal(QuestRewards.Payout.Pay, QuestRewards.OnNote(110, 100, 100.3, ownOpen: false));
    }

    [Fact]
    public void A_note_for_a_quest_this_game_did_not_hand_in_lately_is_paid() {
        Assert.Equal(QuestRewards.Payout.Pay, QuestRewards.OnNote(100, null, null));
        Assert.Equal(QuestRewards.Payout.Pay, QuestRewards.OnNote(200, 100, null));
    }
}
