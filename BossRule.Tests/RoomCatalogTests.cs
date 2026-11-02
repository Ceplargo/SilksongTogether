using Xunit;

namespace BossRule.Tests;

public class RoomCatalogTests {
    [Fact]
    public void A_room_is_looked_up_once_while_this_game_stays_in_it() {
        // ArenaWins.Tick in a catalog room asked the catalog every frame: its return came before its next look moved on
        var asked = 0;
        var catalog = new RoomCatalog();
        bool Lists(string room) {
            asked++;
            return room == "Greymoor_05";
        }

        Assert.True(catalog.Skipped("Greymoor_05", Lists));
        Assert.True(catalog.Skipped("Greymoor_05", Lists));
        Assert.Equal(1, asked);
        Assert.False(catalog.Skipped("Greymoor_04", Lists));
        Assert.Equal(2, asked);
    }

    [Fact]
    public void A_room_is_skipped_while_no_catalog_is_set() {
        Assert.True(new RoomCatalog().Skipped("Greymoor_04", null));
    }
}
