using SSMP.Game.Client.Entity.Component;
using Xunit;

namespace SSMP.Tests;

public class KillSoundsTests {
    private const int Damage = 5, Crack = 6, DeathSword = 7, Goomba = 1;

    [Fact]
    public void A_death_on_the_hosts_word_repeats_no_sound_its_copys_killing_hit_played() {
        // the friend's game at Bone_04's Bone Goomba: its killing hit played damage and crack, and the host's death came
        // 56 ms later with damage, crack and the death's own sword sound
        var sounds = new KillSounds();
        sounds.Played(Damage, 10.0);
        sounds.Played(Crack, 10.0);
        sounds.Killed(Goomba, 10.0);
        sounds.BeginDeath(Goomba, 10.056);
        Assert.True(sounds.Repeats(Damage));
        Assert.True(sounds.Repeats(Crack));
        Assert.False(sounds.Repeats(DeathSword));
    }

    [Fact]
    public void A_death_whose_killing_hit_was_not_this_games_plays_every_sound() {
        // the host killed it: this game's copy took no killing hit, so the death plays all, as the game alone does
        var sounds = new KillSounds();
        sounds.Played(Damage, 10.0);
        sounds.BeginDeath(Goomba, 10.056);
        Assert.False(sounds.Repeats(Damage));
    }

    [Fact]
    public void A_sound_from_before_the_killing_hit_plays_again() {
        var sounds = new KillSounds();
        sounds.Played(Damage, 9.0);
        sounds.Killed(Goomba, 10.0);
        sounds.BeginDeath(Goomba, 10.056);
        Assert.False(sounds.Repeats(Damage));
    }

    [Fact]
    public void A_killing_hit_older_than_Keep_is_forgotten() {
        var sounds = new KillSounds();
        sounds.Played(Damage, 10.0);
        sounds.Killed(Goomba, 10.0);
        sounds.BeginDeath(Goomba, 10.0 + KillSounds.Keep + 0.1);
        Assert.False(sounds.Repeats(Damage));
    }

    [Fact]
    public void Only_during_the_death_and_only_once() {
        var sounds = new KillSounds();
        sounds.Played(Damage, 10.0);
        sounds.Killed(Goomba, 10.0);
        Assert.False(sounds.Repeats(Damage));
        sounds.BeginDeath(Goomba, 10.056);
        Assert.True(sounds.Repeats(Damage));
        sounds.EndDeath();
        Assert.False(sounds.Repeats(Damage));
        sounds.BeginDeath(Goomba, 10.1);
        Assert.False(sounds.Repeats(Damage));
    }
}
