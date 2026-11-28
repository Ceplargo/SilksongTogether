using SSMP.Game.Client.Save;
using Xunit;

// 0.4.1 gate, arena_after (Room_Forge, friend first): after the host's autosave in the room, the stored entry was the
// arena's own item data, and an arriving win written there was read back to false
public class StoredApartTests {
    private sealed class Data {
        public bool Value;
    }

    private static Data Copy(Data d) => new() { Value = d.Value };

    [Fact]
    public void An_entry_that_is_a_live_items_own_data_is_replaced_by_a_copy() {
        var own = new Data { Value = false };
        var entry = StoredApart.Entry(own, new object?[] { new Data(), own }, Copy);
        Assert.NotSame(own, entry);
        entry.Value = true;
        Assert.False(own.Value);
    }

    [Fact]
    public void An_entry_of_its_own_is_written_in_place() {
        var stored = new Data();
        Assert.Same(stored, StoredApart.Entry(stored, new object?[] { new Data(), null }, Copy));
    }
}
