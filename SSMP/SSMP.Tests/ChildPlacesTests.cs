using SSMP.Game.Client.Entity.Component;
using Xunit;

namespace SSMP.Tests;

public class ChildPlacesTests {
    [Fact]
    public void The_first_places_are_sent() => Assert.True(ChildPlaces.Moved(null, [0f, 2f]));

    [Fact]
    public void A_child_moved_past_the_step_is_sent() => Assert.True(ChildPlaces.Moved([0f, 2f], [0f, 0.88f]));

    [Fact]
    public void A_child_still_is_not_sent() => Assert.False(ChildPlaces.Moved([1f, 0f], [1.005f, 0f]));

    [Fact]
    public void Another_number_of_children_is_sent() => Assert.True(ChildPlaces.Moved([1f, 0f], [1f, 0f, 2f, 2f]));
}
