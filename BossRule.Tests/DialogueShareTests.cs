using Xunit;

namespace BossRule.Tests;

public class DialogueShareTests {
    private static DialoguePage Page(int page, string scene = "Bone_04", float x = 10f, float y = 5f, int talk = 1,
        string talker = "Ann") =>
        new(talker, talk, page, "Titles", "MAPPER", "Mapper", "MAPPER_TALK_1", scene, x, y);

    private static readonly View Near = new(0f, 0f, 20f, 12f);
    private static readonly View Far = new(100f, 0f, 120f, 12f);

    [Fact]
    public void A_page_round_trips_exactly() {
        var page = new DialoguePage("Ann|x", 7, 3, "Titles", "MAPPER", "Mapper", "MAPPER_TALK_1", "Bone_04", -12.375f, 4.1f);
        Assert.Equal(page, DialogueShare.Decode(DialogueShare.Encode(page)));
    }

    private static FoundNote Found(string scene = "Bone_04", float x = 10f, float y = 5f) =>
        new("Ann", "Rock Roller Item", "Rock Rollers", 3, 3, 3, scene, x, y);

    [Fact]
    public void A_found_note_round_trips_exactly() {
        var note = new FoundNote("Ann|x", "Rock Roller Item", "Rock Rollers", 2, 3, 2, "Bone_04", -12.375f, 4.1f);
        Assert.Equal(note, DialogueShare.Decode(DialogueShare.Encode(note)));
    }

    // the owner, 2026-11-10: in view of the find, the game's own message; out of view or in another room, a chat line
    [Fact]
    public void A_find_in_view_in_the_same_room_shows_on_screen() =>
        Assert.Equal(Where.Screen, DialogueShare.Route(Found(), "Bone_04", Near, busy: false));

    [Fact]
    public void A_find_out_of_view_goes_to_the_chat() =>
        Assert.Equal(Where.Chat, DialogueShare.Route(Found(), "Bone_04", Far, busy: false));

    [Fact]
    public void A_find_in_another_room_goes_to_the_chat() =>
        Assert.Equal(Where.Chat, DialogueShare.Route(Found("Bone_01"), "Bone_04", Near, busy: false));

    [Fact]
    public void A_find_while_busy_goes_to_the_chat() =>
        Assert.Equal(Where.Chat, DialogueShare.Route(Found(), "Bone_04", Near, busy: true));

    [Fact]
    public void A_find_line_names_the_finder_the_item_and_the_wish_count() =>
        Assert.Equal("Ann found Rock Roller Item (Rock Rollers 3/3)",
            DialogueShare.FoundLine("Ann", "Rock Roller Item", "Rock Rollers", 3, 3));

    [Fact]
    public void A_quest_note_round_trips_exactly() {
        var note = new QuestNote("Bob|x", "Shiny Bell Goomba", true, "Bone_04", -12.375f, 4.1f);
        Assert.Equal(note, DialogueShare.Decode(DialogueShare.Encode(note)));
    }

    private static QuestNote Quest(string scene = "Bone_04", float x = 10f, float y = 5f) =>
        new("Ann", "Shiny Bell Goomba", false, scene, x, y);

    // 0.3.22 quest rules Task 6 (rule A4): a quest note shows where a find shows
    [Fact]
    public void A_quest_note_shows_on_screen_in_the_watchers_room_and_view_when_not_busy() =>
        Assert.Equal(Where.Screen, DialogueShare.Route(Quest(), "Bone_04", Near, busy: false));

    [Fact]
    public void A_quest_note_goes_to_the_chat_out_of_view_in_another_room_or_when_busy() {
        Assert.Equal(Where.Chat, DialogueShare.Route(Quest(), "Bone_04", Far, busy: false));
        Assert.Equal(Where.Chat, DialogueShare.Route(Quest(scene: "Bone_05"), "Bone_04", Near, busy: false));
        Assert.Equal(Where.Chat, DialogueShare.Route(Quest(), "Bone_04", Near, busy: true));
    }

    [Fact]
    public void A_delivery_line_names_its_carrier() =>
        Assert.Equal("Ann carries the delivery for Bonebottom Delivery",
            DialogueShare.CarrierLine("Ann", "Bonebottom Delivery"));

    [Fact]
    public void A_quest_book_entry_names_the_carrier_after_its_text() =>
        Assert.Equal("Take the supplies.\n\nCarried by Ann.", DialogueShare.CarriedBy("Take the supplies.", "Ann"));

    [Fact]
    public void A_page_carries_keys_not_looked_up_text() {
        var text = DialogueShare.Encode(Page(0));
        Assert.Contains("MAPPER_TALK_1", text);
        Assert.Contains("Titles", text);
    }

    [Fact]
    public void A_malformed_message_decodes_to_nothing() {
        Assert.Null(DialogueShare.Decode(""));
        Assert.Null(DialogueShare.Decode("P\u001fAnn"));
        Assert.Null(DialogueShare.Decode("P\u001fAnn\u001fx\u001f0\u001fa\u001fb\u001fc\u001fd\u001fe\u001f1\u001f2"));
    }

    [Fact]
    public void Same_scene_with_the_npc_in_view_shows_on_screen() =>
        Assert.Equal(Where.Screen, DialogueShare.Route(Page(0), "Bone_04", Near, busy: false));

    [Fact]
    public void Same_scene_with_the_npc_out_of_view_goes_to_chat() =>
        Assert.Equal(Where.Chat, DialogueShare.Route(Page(0), "Bone_04", Far, busy: false));

    [Fact]
    public void Another_scene_goes_to_chat() =>
        Assert.Equal(Where.Chat, DialogueShare.Route(Page(0), "Bone_05", Near, busy: false));

    [Fact]
    public void A_busy_watcher_gets_chat() =>
        Assert.Equal(Where.Chat, DialogueShare.Route(Page(0), "Bone_04", Near, busy: true));

    [Fact]
    public void After_leaving_the_view_the_rest_of_the_talk_goes_to_chat() {
        var watch = new DialogueWatch();
        Assert.Equal(Where.Screen, watch.Take(Page(0), "Bone_04", Near, false));
        Assert.Equal(Where.Chat, watch.Take(Page(1), "Bone_04", Far, false));
        Assert.Equal(Where.Chat, watch.Take(Page(2), "Bone_04", Near, false));
    }

    [Fact]
    public void A_repeat_of_a_page_is_dropped() {
        var watch = new DialogueWatch();
        Assert.Equal(Where.Chat, watch.Take(Page(0), "Bone_05", Near, false));
        Assert.Null(watch.Take(Page(0), "Bone_05", Near, false));
        Assert.Equal(Where.Chat, watch.Take(Page(1), "Bone_05", Near, false));
    }

    [Fact]
    public void A_talk_opened_again_shows_again_from_its_first_page() {
        var watch = new DialogueWatch();
        watch.Take(Page(0), "Bone_04", Far, false);
        watch.Take(Page(1), "Bone_04", Far, false);
        Assert.Equal(Where.Screen, watch.Take(Page(0, talk: 2), "Bone_04", Near, false));
    }

    [Fact]
    public void Two_talkers_are_routed_apart() {
        var watch = new DialogueWatch();
        Assert.Equal(Where.Chat, watch.Take(Page(0, talker: "Ann"), "Bone_04", Far, false));
        Assert.Equal(Where.Screen, watch.Take(Page(0, talker: "Bob", x: 110f), "Bone_04", Far, false));
    }

    [Fact]
    public void A_chat_line_names_the_speaker() {
        Assert.Equal("Shakra: Hello there.", DialogueShare.ChatLine("Shakra", "Hello <b>there</b>.\n"));
        Assert.Equal("A sign says this.", DialogueShare.ChatLine("", "A sign says this."));
    }

    [Fact]
    public void A_quest_line_names_the_player_and_the_quest() {
        Assert.Equal("Ann took Shiny Bell Goomba", DialogueShare.QuestLine("Ann", "Shiny Bell Goomba", false));
        Assert.Equal("Bob finished Shiny Bell Goomba", DialogueShare.QuestLine("Bob", "Shiny Bell Goomba", true));
    }
}
