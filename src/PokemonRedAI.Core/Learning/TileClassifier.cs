using PokemonRedAI.Core.Persistence;
using PokemonRedAI.Core.ScreenReader;

namespace PokemonRedAI.Core.Learning;

public class TileClassifier
{
    private readonly WalkabilityMap _walkabilityMap;
    private readonly TileReader _tileReader;

    private int _currentX;
    private int _currentY;
    private Tile[,]? _previousTiles;

    public int CurrentX => _currentX;
    public int CurrentY => _currentY;

    public event EventHandler<MovementResult>? MovementCompleted;

    public TileClassifier(WalkabilityMap walkabilityMap, TileReader tileReader)
    {
        _walkabilityMap = walkabilityMap;
        _tileReader = tileReader;
    }

    public void SetPosition(int x, int y)
    {
        _currentX = x;
        _currentY = y;
    }

    public MovementResult ProcessMovementAttempt(Direction direction, Tile[,] currentTiles)
    {
        var result = new MovementResult
        {
            Direction = direction,
            StartX = _currentX,
            StartY = _currentY
        };

        // Get target position
        var (targetX, targetY) = _walkabilityMap.GetTargetPosition(_currentX, _currentY, direction);
        result.TargetX = targetX;
        result.TargetY = targetY;

        // Check if tiles have changed (indicating movement)
        bool positionChanged = DetectPositionChange(currentTiles);

        if (positionChanged)
        {
            // Movement successful - mark target as walkable
            _walkabilityMap.MarkWalkable(targetX, targetY);
            _currentX = targetX;
            _currentY = targetY;
            result.Success = true;
            result.TileState = TileState.Walkable;
        }
        else
        {
            // Movement failed - mark target as blocked
            _walkabilityMap.MarkBlocked(targetX, targetY);
            result.Success = false;
            result.TileState = TileState.Blocked;
        }

        result.EndX = _currentX;
        result.EndY = _currentY;

        _previousTiles = currentTiles;
        MovementCompleted?.Invoke(this, result);

        return result;
    }

    public void UpdateTiles(Tile[,] tiles)
    {
        _previousTiles = tiles;
    }

    public bool DetectPositionChange(Tile[,] currentTiles)
    {
        if (_previousTiles == null)
            return false;

        // Compare tiles to detect if the screen has scrolled
        // In Pokemon Red, when you move, the entire screen scrolls

        int changedTiles = 0;
        int totalTiles = 0;

        for (int y = 0; y < currentTiles.GetLength(1); y++)
        {
            for (int x = 0; x < currentTiles.GetLength(0); x++)
            {
                totalTiles++;
                if (!_tileReader.AreTilesSimilar(currentTiles[x, y], _previousTiles[x, y]))
                {
                    changedTiles++;
                }
            }
        }

        // If a significant portion of tiles changed, assume movement occurred
        float changeRatio = (float)changedTiles / totalTiles;
        return changeRatio > 0.1f; // More than 10% of tiles changed
    }

    public TileState ClassifyTileFromPixels(Tile tile)
    {
        // Black tiles are assumed to be blocked (out of bounds, walls, etc.)
        if (tile.IsBlack)
        {
            return TileState.Blocked;
        }

        return TileState.Unknown;
    }

    public void PreClassifyVisibleTiles(Tile[,] tiles)
    {
        // Pre-classify obviously blocked tiles (like black tiles)
        for (int y = 0; y < tiles.GetLength(1); y++)
        {
            for (int x = 0; x < tiles.GetLength(0); x++)
            {
                var tile = tiles[x, y];
                if (tile.IsBlack)
                {
                    // Convert screen tile position to world position
                    int worldX = _currentX + (x - TileReader.PlayerTileX);
                    int worldY = _currentY + (y - TileReader.PlayerTileY);
                    _walkabilityMap.MarkBlocked(worldX, worldY);
                }
            }
        }
    }
}

public class MovementResult
{
    public Direction Direction { get; set; }
    public int StartX { get; set; }
    public int StartY { get; set; }
    public int TargetX { get; set; }
    public int TargetY { get; set; }
    public int EndX { get; set; }
    public int EndY { get; set; }
    public bool Success { get; set; }
    public TileState TileState { get; set; }

    public bool PositionChanged => StartX != EndX || StartY != EndY;
}
