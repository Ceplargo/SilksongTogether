using System;
using SSMP.Util;
using Xunit;

namespace SSMP.Tests;

public class HandlerListTests {
    private event Action? Update;

    [Fact]
    public void It_gives_the_handlers_as_they_are_now() {
        var list = new HandlerList();
        var ran = "";
        Action a = () => ran += "a", b = () => ran += "b";
        Assert.Empty(list.Of(Update));
        Update += a;
        Update += b;
        foreach (var handler in list.Of(Update)) ((Action) handler)();
        Update -= a;
        foreach (var handler in list.Of(Update)) ((Action) handler)();
        Update -= b;
        Assert.Empty(list.Of(Update));
        Assert.Equal("abb", ran);
    }

    [Fact]
    public void Reading_each_frame_allocates_nothing() {
        var list = new HandlerList();
        Update += () => { };
        Update += () => { };
        list.Of(Update);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var frame = 0; frame < 100; frame++) list.Of(Update);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
