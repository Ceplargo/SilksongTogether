using SSMP.Api.Client;
using SSMP.Internals;
using UnityEngine;

namespace SSMP.Game.Client;

/// <inheritdoc />
internal class ClientPlayerData : IClientPlayer {
    /// <inheritdoc />
    public required ushort Id { get; init; }

    /// <inheritdoc />
    public required string Username { get; init; }

    /// <inheritdoc />
    public bool IsInLocalScene { get; set; }

    /// <inheritdoc />
    public GameObject? PlayerContainer { get; set; }

    /// <summary>
    /// The player object of the player's last spawn in our scene; kept after they leave, for recycling.
    /// </summary>
    public GameObject? PlayerObject { get; set; }

    /// <inheritdoc />
    GameObject? IClientPlayer.PlayerObject => IsInLocalScene ? PlayerObject : null;

    /// <summary>
    /// Whether enemies may target this player. False while the player watches a boss fight.
    /// </summary>
    public bool Targetable { get; set; } = true;

    /// <inheritdoc />
    public Team Team { get; set; }

    /// <inheritdoc />
    public byte SkinId { get; set; }

    /// <inheritdoc />
    public CrestType CrestType { get; set; }
}
