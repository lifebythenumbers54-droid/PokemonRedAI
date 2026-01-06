using System.Text.Json.Serialization;

namespace PokemonRedAI.WinForms;

/// <summary>
/// Represents the properties of a tile in the game world.
/// </summary>
public class TileData
{
    /// <summary>
    /// Unique hash identifying this tile's visual appearance.
    /// </summary>
    public long TileHash { get; set; }

    /// <summary>
    /// Can the player walk on this tile?
    /// </summary>
    public bool IsWalkable { get; set; } = true;

    /// <summary>
    /// Is this tile blocking movement? (walls, fences, furniture, trees, etc.)
    /// Opposite of walkable - a solid obstacle that cannot be passed.
    /// </summary>
    public bool IsBlocking { get; set; } = false;

    /// <summary>
    /// Can the player interact with this tile (talk to NPC, read sign, etc.)?
    /// </summary>
    public bool IsInteractable { get; set; } = false;

    /// <summary>
    /// Is this tile a door/entrance that leads to another area?
    /// </summary>
    public bool IsDoor { get; set; } = false;

    /// <summary>
    /// Is this tile a warp point (stairs, cave entrance, etc.)?
    /// </summary>
    public bool IsWarp { get; set; } = false;

    /// <summary>
    /// Is this tile water (requires Surf)?
    /// </summary>
    public bool IsWater { get; set; } = false;

    /// <summary>
    /// Is this tile a ledge (can only jump down)?
    /// </summary>
    public bool IsLedge { get; set; } = false;

    /// <summary>
    /// Is this tile tall grass (can trigger wild encounters)?
    /// </summary>
    public bool IsGrass { get; set; } = false;

    /// <summary>
    /// Custom notes about this tile.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Number of times this tile has been observed.
    /// Higher counts indicate more confidence in the classification.
    /// </summary>
    public int ObservationCount { get; set; } = 1;

    /// <summary>
    /// Number of times we successfully walked on this tile.
    /// </summary>
    public int WalkSuccessCount { get; set; } = 0;

    /// <summary>
    /// Number of times we failed to walk on this tile (collision).
    /// </summary>
    public int WalkFailCount { get; set; } = 0;

    /// <summary>
    /// Last time this tile was observed.
    /// </summary>
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Confidence score for walkability (0.0 to 1.0).
    /// Based on walk success/fail counts.
    /// </summary>
    [JsonIgnore]
    public float WalkabilityConfidence
    {
        get
        {
            int total = WalkSuccessCount + WalkFailCount;
            if (total == 0) return 0.5f; // Unknown
            return (float)WalkSuccessCount / total;
        }
    }

    /// <summary>
    /// Updates walkability based on observed behavior.
    /// </summary>
    public void RecordWalkAttempt(bool success)
    {
        if (success)
            WalkSuccessCount++;
        else
            WalkFailCount++;

        // Auto-update IsWalkable and IsBlocking based on observations
        if (WalkSuccessCount + WalkFailCount >= 3)
        {
            IsWalkable = WalkabilityConfidence > 0.5f;
            IsBlocking = !IsWalkable;
        }

        LastSeen = DateTime.UtcNow;
        ObservationCount++;
    }

    /// <summary>
    /// Returns true if this tile should be avoided for pathfinding.
    /// A tile is impassable if it's blocking, water (without Surf), or a ledge facing the wrong way.
    /// </summary>
    [JsonIgnore]
    public bool IsImpassable => IsBlocking || IsWater;
}

/// <summary>
/// Types of tiles for quick classification.
/// </summary>
[Flags]
public enum TileType
{
    Unknown = 0,
    Walkable = 1,
    Blocking = 2,       // Walls, fences, furniture, trees - cannot pass
    Door = 4,
    Warp = 8,
    Water = 16,
    Ledge = 32,
    Grass = 64,
    NPC = 128,
    Interactable = 256
}
