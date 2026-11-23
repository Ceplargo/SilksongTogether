using System;

namespace SSMP.Util;

/// <summary>
/// (coop, optimization fix D) An event's handlers, read again only when the event changed. A delegate never changes, so
/// a different one means a handler was added or removed. MonoBehaviourUtil called GetInvocationList for its update
/// events each frame, a new array every time (about 220 bytes a frame, mini PC job 38).
/// </summary>
internal sealed class HandlerList {
    private Delegate? _of;
    private Delegate[] _handlers = Array.Empty<Delegate>();

    public Delegate[] Of(Delegate? current) {
        if (current == null) return Array.Empty<Delegate>();
        if (!ReferenceEquals(current, _of)) {
            _handlers = current.GetInvocationList();
            _of = current;
        }

        return _handlers;
    }
}
