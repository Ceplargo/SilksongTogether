using System.Runtime.CompilerServices;

namespace SSMP.Game.Client.Entity.Component;

/// <summary>
/// (coop, 0.4 enemies) Where the other games show a thrown copy past the scene host's last update, and where the scene
/// host's game reckons they show it (its resend check), Unity-free so SSMP.Tests compiles it. On its last speed alone,
/// a copy flew on 0.25 s after the scene host's game froze: the Widow's bell level from its apex while hers fell, 3.5
/// units above hers as the host's game came back; the Last Judge's censer 14 units past where her tween had stopped
/// it, its hurt box on (t3-show-p1-a, both orders). One with a dynamic body now falls as its body does, and one moved
/// by its FSM goes no further than the scene host's resend distance past its last update: a freeze or a stop shows
/// at most that far off, and the scene host's game resends once it has gone that far on.
/// </summary>
internal static class ThrownPrediction {
    /// <summary>The place <paramref name="dt"/> s past an update at (<paramref name="x"/>, <paramref name="y"/>) going
    /// (<paramref name="vx"/>, <paramref name="vy"/>), for a copy with a dynamic body.</summary>
    public static (float X, float Y) Predict(float x, float y, float vx, float vy, float gravityScale, float gravity,
        float dt) => (x + vx * dt, y + vy * dt + 0.5f * gravity * gravityScale * dt * dt);

    /// <summary>The speed of a copy with a dynamic body <paramref name="dt"/> s past an update.</summary>
    public static (float X, float Y) Speed(float vx, float vy, float gravityScale, float gravity, float dt) =>
        (vx, vy + gravity * gravityScale * dt);

    /// <summary>The place of a copy with no dynamic body (moved by its FSM): on its last speed, but no further than
    /// <paramref name="maxLead"/> from its last update.</summary>
    public static (float X, float Y) Held(float x, float y, float vx, float vy, float dt, float maxLead) {
        var (dx, dy) = (vx * dt, vy * dt);
        var lead = (float) System.Math.Sqrt(dx * dx + dy * dy);
        if (!float.IsFinite(lead)) {
            return (x, y); // a non-finite speed or time: none is sent today, but a copy shown at NaN is lost
        }

        return lead <= maxLead ? (x + dx, y + dy) : (x + dx * maxLead / lead, y + dy * maxLead / lead);
    }

    /// <summary>How long a copy waits at its prediction's end for the next update: the forced freeze of throw_freeze
    /// (1 s). Past it the copy flies on by its own body, as before the wait: with no limit, a Widow's bell copy whose
    /// updates had stopped (hers fell on below the room) waited at its prediction's end for good, its hurt box on,
    /// through four later checks (the FollowsFlight side by side, 2026-11-17, t4-sbs-on-p1-2257).</summary>
    public const float MaxWait = 1f;

    /// <summary>Whether a copy <paramref name="past"/> s past its prediction's end, still with no update, flies on by
    /// its own body. The check throw_freeze's twin skips it with a patch (a skipped one returns false): then the copy
    /// waits for good, as with no limit.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool Releases(float past) => past > MaxWait;

    /// <summary>(coop, 0.4 enemies) The longest a copy let go flies on by itself (left out of an update, or released
    /// past <see cref="MaxWait"/>): longer than the longest flight (9.45 s, the Pilgrim Fly's Spine Floater Spine) and
    /// the longest life (9.5 s, a Roachfeeder's Caltrop, resting at the end) of any thrown object in its room in the
    /// recorded hit logs, 3688 of the host's (NOTES).</summary>
    public const float LetGoLife = 12f;

    /// <summary>(coop, 0.4 enemies) Whether a copy let go <paramref name="since"/> s ago ends: once out of its room
    /// (where the scene host prunes its own) or past <see cref="LetGoLife"/>. A Widow's bell copy let go rose on for
    /// good, its hurt box on, through the next rooms (plan2, 2026-11-17, t3s5-merged-plan2-p1-0910).</summary>
    public static bool EndsLetGo(float since, bool inRoom) => EndsLetGoCopies() && (!inRoom || since > LetGoLife);

    /// <summary>The check's twin switches it off with a patch (a skipped one returns false): a copy let go flies on by
    /// itself for good, as before.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool EndsLetGoCopies() => true;

    /// <summary>The check throw_freeze's twin switches it off with a patch: then a copy goes on straight at its last
    /// speed, as far as the time allows.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool FollowsFlight() => true;

    /// <summary>(coop, 0.4 enemies) How far the scene host's object may be from where a landed copy rests, in an update
    /// newer than its landing, before the copy follows it again: the scene host's resend distance (MaxError).</summary>
    public const float HoldAway = 0.5f;

    /// <summary>(coop, 0.4 enemies) Below this speed the scene host's object has stopped too.</summary>
    public const float Stopped = 0.1f;

    /// <summary>(coop, 0.4 enemies) Whether a landed copy's rest ends on the scene host's update, (<paramref name="dx"/>,
    /// <paramref name="dy"/>) from where the copy rests, going (<paramref name="vx"/>, <paramref name="vy"/>). Only an
    /// update newer than the landing counts: the one in hand then still carries the fall. It ends once one has stopped
    /// too (the copy goes to the scene host's resting place), or is more than <see cref="HoldAway"/> from it going away
    /// (the scene host's did not land there, or bounced on): a copy never rests where the scene host's is not. One still
    /// coming toward it does not end it: under lag the scene host's last sends before its own landing arrive after the
    /// copy's, up to a unit back along the fall, and ending the rest on them carried the copy into the floor again
    /// (tar_rest --lag 50).</summary>
    public static bool HoldEnds(bool newer, float dx, float dy, float vx, float vy) =>
        newer && (vx * vx + vy * vy < Stopped * Stopped ||
                  dx * dx + dy * dy > HoldAway * HoldAway && dx * vx + dy * vy > 0f);

    /// <summary>(coop, 0.4 enemies) Whether a copy's own surface check (PlayMaker's BoolNoneTrue) is left to the scene
    /// host: once the copy has flown (<paramref name="flew"/>) and its own FSM has landed it (its body made kinematic,
    /// <paramref name="landed"/>), whether there is a surface under it is the scene host's to say, its Break coming as
    /// its state. Each game's copy lands a little apart from the scene host's (the follow's prediction), and in about 1
    /// of 50 landings one game's own rays found no surface and that game alone broke it at once: the Beastfly's tar
    /// shots, whose fire then ended about half a second early on the friend's screen (the boss sweep's forced
    /// attacks).</summary>
    public static bool RaysHeld(bool landed, bool flew) => landed && flew;
}
