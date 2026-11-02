using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSMP.Util;

/// <summary>
/// MonoBehaviour that offers static utilities methods.
/// </summary>
internal class MonoBehaviourUtil : MonoBehaviour {
    /// <summary>
    /// The instance of this class.
    /// </summary>
    public static MonoBehaviourUtil Instance = null!;

    /// <summary>
    /// Event that is execute each Unity update tick.
    /// </summary>
    public event Action? OnUpdateEvent;

    /// <summary>
    /// Event that is executed each Unity late update tick, after every update (the game's FSMs included).
    /// </summary>
    public event Action? OnLateUpdateEvent;

    /// <summary>
    /// (coop) Event that is executed each physics step (a long frame runs several before that frame's updates).
    /// </summary>
    public event Action? OnFixedUpdateEvent;

    public void Awake() {
        if (Instance != null) {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    public void Update() {
        if (OnUpdateEvent == null) {
            return;
        }

        // Each handler on its own: a handler that throws would otherwise skip every handler after it that frame, and
        // entity sync shares this event with save sync (a save item that threw every frame froze enemy movement)
        foreach (var handler in OnUpdateEvent.GetInvocationList()) {
            try {
                ((Action) handler)();
            } catch (Exception e) {
                Debug.LogException(e);
            }
        }
    }

    public void FixedUpdate() {
        if (OnFixedUpdateEvent == null) {
            return;
        }

        foreach (var handler in OnFixedUpdateEvent.GetInvocationList()) {
            try {
                ((Action) handler)();
            } catch (Exception e) {
                Debug.LogException(e);
            }
        }
    }

    public void LateUpdate() {
        if (OnLateUpdateEvent == null) {
            return;
        }

        foreach (var handler in OnLateUpdateEvent.GetInvocationList()) {
            try {
                ((Action) handler)();
            } catch (Exception e) {
                Debug.LogException(e);
            }
        }
    }

    /// <summary>
    /// Destroys all children of a given GameObject.
    /// </summary>
    /// <param name="gameObject">The GameObject to destroy all children of.</param>
    public static void DestroyAllChildren(GameObject gameObject) {
        DestroyAllChildren(gameObject, new List<string>());
    }

    /// <summary>
    /// Destroys all children of the given GameObject, excluding game objects that match the names in the
    /// given list.
    /// </summary>
    /// <param name="gameObject">The GameObject to destroy all children of.</param>
    /// <param name="exclude">The list of names to exclude.</param>
    public static void DestroyAllChildren(
        GameObject gameObject,
        List<string> exclude
    ) {
        for (var i = 0; i < gameObject.transform.childCount; i++) {
            var child = gameObject.transform.GetChild(i);

            if (exclude.Contains(child.name)) {
                continue;
            }

            Destroy(child.gameObject);
        }
    }
}
