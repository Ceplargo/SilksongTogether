namespace SSMP.Game.Client.Save;

/// <summary>
/// How a save value is shared between the players of a hosted save. Set per value as "Kind" in save-data.json.
/// </summary>
internal enum SaveKind {
    /// <summary>
    /// One value for the group: world and story progress, keys and quest items.
    /// </summary>
    Shared,

    /// <summary>
    /// Items and upgrades: shared while item sharing is on, kept per player while it is off.
    /// </summary>
    Item,

    /// <summary>
    /// Kept per player: health, silk, position, rosaries and the like.
    /// </summary>
    Personal
}
