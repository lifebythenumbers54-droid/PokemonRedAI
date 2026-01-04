using PokemonRedAI.Core.Persistence;
using PokemonRedAI.Core.ScreenReader;

namespace PokemonRedAI.Core.Learning;

public class WalkabilityMap
{
    private readonly DataManager _dataManager;
    private string _currentMapId = "unknown";

    public event EventHandler<TileLearnedEventArgs>? TileLearned;

    public string CurrentMapId
    {
        get => _currentMapId;
        set
        {
            if (_currentMapId != value)
            {
                _currentMapId = value;
                MapChanged?.Invoke(this, value);
            }
        }
    }

    public event EventHandler<string>? MapChanged;

    public WalkabilityMap(DataManager dataManager)
    {
        _dataManager = dataManager;
    }

    public TileState GetTileState(int x, int y)
    {
        return _dataManager.GetTileState(_currentMapId, x, y);
    }

    public TileState GetTileState(int x, int y, Direction direction)
    {
        var (targetX, targetY) = GetTargetPosition(x, y, direction);
        return GetTileState(targetX, targetY);
    }

    public void SetTileState(int x, int y, TileState state)
    {
        var previousState = GetTileState(x, y);
        _dataManager.SetTileState(_currentMapId, x, y, state);

        if (previousState != state)
        {
            TileLearned?.Invoke(this, new TileLearnedEventArgs
            {
                MapId = _currentMapId,
                X = x,
                Y = y,
                PreviousState = previousState,
                NewState = state
            });
        }
    }

    public void MarkWalkable(int x, int y)
    {
        SetTileState(x, y, TileState.Walkable);
    }

    public void MarkBlocked(int x, int y)
    {
        SetTileState(x, y, TileState.Blocked);
    }

    public bool IsWalkable(int x, int y)
    {
        return GetTileState(x, y) == TileState.Walkable;
    }

    public bool IsBlocked(int x, int y)
    {
        return GetTileState(x, y) == TileState.Blocked;
    }

    public bool IsUnknown(int x, int y)
    {
        return GetTileState(x, y) == TileState.Unknown;
    }

    public (int x, int y) GetTargetPosition(int x, int y, Direction direction)
    {
        return direction switch
        {
            Direction.Up => (x, y - 1),
            Direction.Down => (x, y + 1),
            Direction.Left => (x - 1, y),
            Direction.Right => (x + 1, y),
            _ => (x, y)
        };
    }

    public List<Direction> GetWalkableDirections(int x, int y)
    {
        var directions = new List<Direction>();

        foreach (Direction dir in Enum.GetValues<Direction>())
        {
            var (targetX, targetY) = GetTargetPosition(x, y, dir);
            if (IsWalkable(targetX, targetY))
            {
                directions.Add(dir);
            }
        }

        return directions;
    }

    public List<Direction> GetUnexploredDirections(int x, int y)
    {
        var directions = new List<Direction>();

        foreach (Direction dir in Enum.GetValues<Direction>())
        {
            var (targetX, targetY) = GetTargetPosition(x, y, dir);
            if (IsUnknown(targetX, targetY))
            {
                directions.Add(dir);
            }
        }

        return directions;
    }

    public MapStatistics GetStatistics()
    {
        var stats = new MapStatistics { MapId = _currentMapId };

        if (_dataManager.CurrentData.WalkabilityMaps.TryGetValue(_currentMapId, out var mapData))
        {
            foreach (var tile in mapData.Tiles)
            {
                switch (tile.Value)
                {
                    case TileState.Walkable:
                        stats.WalkableTiles++;
                        break;
                    case TileState.Blocked:
                        stats.BlockedTiles++;
                        break;
                    case TileState.Unknown:
                        stats.UnknownTiles++;
                        break;
                }
            }
        }

        return stats;
    }
}

public class TileLearnedEventArgs : EventArgs
{
    public string MapId { get; set; } = string.Empty;
    public int X { get; set; }
    public int Y { get; set; }
    public TileState PreviousState { get; set; }
    public TileState NewState { get; set; }
}

public class MapStatistics
{
    public string MapId { get; set; } = string.Empty;
    public int WalkableTiles { get; set; }
    public int BlockedTiles { get; set; }
    public int UnknownTiles { get; set; }
    public int TotalKnownTiles => WalkableTiles + BlockedTiles;
}
