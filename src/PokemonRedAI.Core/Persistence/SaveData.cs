namespace PokemonRedAI.Core.Persistence;

public class SaveData
{
    public string Version { get; set; } = "1.0";
    public DateTime LastSaved { get; set; } = DateTime.UtcNow;
    public Dictionary<string, MapWalkabilityData> WalkabilityMaps { get; set; } = new();
    public Dictionary<string, string> KnownObjects { get; set; } = new();
    public GameProgressData GameProgress { get; set; } = new();
}

public class MapWalkabilityData
{
    public string MapId { get; set; } = string.Empty;
    public Dictionary<string, TileState> Tiles { get; set; } = new();
}

public enum TileState
{
    Unknown,
    Walkable,
    Blocked
}

public class GameProgressData
{
    public int TotalSteps { get; set; }
    public int TilesDiscovered { get; set; }
    public TimeSpan TotalPlayTime { get; set; }
    public string LastMapId { get; set; } = string.Empty;
    public int LastPositionX { get; set; }
    public int LastPositionY { get; set; }
}
