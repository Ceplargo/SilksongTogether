using SSMP.Game.Client.Entity.Component;
using Xunit;

namespace SSMP.Tests;

public class BodyBoxTests {
    [Fact]
    public void The_first_box_is_sent() => Assert.True(BodyBox.Changed(null, [1f, 4.2f, 1.5f, -1.8f, -2.2f]));

    [Fact]
    public void A_new_frames_size_is_sent() =>
        Assert.True(BodyBox.Changed([1f, 4.19f, 1.5f, -1.81f, -2.18f], [1f, 3.84f, 3.14f, -1.81f, -2.18f]));

    [Fact]
    public void Switched_off_is_sent() =>
        Assert.True(BodyBox.Changed([1f, 9.7f, 3.3f, 0f, 0f], [0f, 9.7f, 3.3f, 0f, 0f]));

    [Fact]
    public void A_turn_is_sent() =>
        // her copy kept a replayed turn (335.8 degrees) for seconds under lag while the host's was at 0 (NOTES 339)
        Assert.True(BodyBox.Changed([1f, 9.7f, 3.3f, 0f, 0f, 0f], [1f, 9.7f, 3.3f, 0f, 0f, 335.8f]));

    [Fact]
    public void The_same_box_is_not_sent() =>
        Assert.False(BodyBox.Changed([1f, 9.7f, 3.3f, 0f, 0f], [1f, 9.705f, 3.3f, 0f, 0f]));
}
