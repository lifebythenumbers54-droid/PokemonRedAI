namespace PokemonRedAI.Core.ScreenReader;

public class PlayerTracker
{
    private readonly TileReader _tileReader;

    // Player sprite characteristics in Pokemon Red
    // The player sprite is 16x16 pixels (2x2 tiles) and centered on screen
    private const int PlayerSpriteWidth = 16;
    private const int PlayerSpriteHeight = 16;

    // Screen position where player is typically located
    private const int PlayerScreenX = 72;  // Pixel X (center of 160px screen - 8px offset)
    private const int PlayerScreenY = 64;  // Pixel Y (slightly above center)

    private Tile[,]? _previousTiles;
    private ScreenPixel[,]? _previousScreen;
    private PlayerPosition _currentPosition = new();
    private int _animationFrame;

    public PlayerPosition CurrentPosition => _currentPosition;

    public event EventHandler<PlayerPosition>? PositionChanged;
    public event EventHandler<Direction>? MovementStarted;
    public event EventHandler? MovementCompleted;

    public PlayerTracker(TileReader tileReader)
    {
        _tileReader = tileReader;
    }

    public void Initialize(int worldX, int worldY, string mapId)
    {
        _currentPosition = new PlayerPosition
        {
            WorldX = worldX,
            WorldY = worldY,
            MapId = mapId
        };
    }

    public PlayerPosition Update(ScreenPixel[,] screen, Tile[,] tiles)
    {
        // Detect if player is currently in a movement animation
        bool isMoving = DetectMovementAnimation(screen);
        _currentPosition.IsMoving = isMoving;

        // Detect facing direction from player sprite
        _currentPosition.FacingDirection = DetectFacingDirection(screen);

        // Detect map transition (black screen fade)
        if (DetectMapTransition(screen))
        {
            _currentPosition.IsTransitioning = true;
        }
        else if (_currentPosition.IsTransitioning && !IsBlackScreen(screen))
        {
            // Transition completed
            _currentPosition.IsTransitioning = false;
            // Map ID would need to be detected/updated here
        }

        // Track screen scroll for position updates
        if (_previousTiles != null && !isMoving)
        {
            var scrollDirection = DetectScreenScroll(tiles, _previousTiles);
            if (scrollDirection.HasValue)
            {
                // Update world position based on scroll
                UpdateWorldPosition(scrollDirection.Value);
            }
        }

        _previousTiles = tiles;
        _previousScreen = screen;

        return _currentPosition;
    }

    public bool DetectMovementAnimation(ScreenPixel[,] screen)
    {
        // Check the player sprite area for animation changes
        // During walking, the player sprite alternates between frames

        if (_previousScreen == null)
            return false;

        int changes = 0;
        int playerCenterX = PlayerScreenX;
        int playerCenterY = PlayerScreenY;

        // Sample pixels in the player sprite region
        for (int y = playerCenterY - 8; y < playerCenterY + 8; y++)
        {
            for (int x = playerCenterX - 8; x < playerCenterX + 8; x++)
            {
                if (x < 0 || x >= 160 || y < 0 || y >= 144)
                    continue;

                if (!screen[x, y].Matches(_previousScreen[x, y], 5))
                {
                    changes++;
                }
            }
        }

        // If significant changes in player sprite area, likely animating
        return changes > 20 && changes < 200;
    }

    public Direction DetectFacingDirection(ScreenPixel[,] screen)
    {
        // Player sprite facing direction can be detected by analyzing the sprite pattern
        // This is a simplified detection based on common sprite characteristics

        int centerX = PlayerScreenX;
        int centerY = PlayerScreenY;

        // Sample points that differ between directional sprites
        // These would need to be calibrated for the actual Pokemon Red sprites

        // Check top of sprite for up-facing indicators
        int topDarkPixels = CountDarkPixels(screen, centerX - 4, centerY - 8, 8, 4);

        // Check bottom for down-facing
        int bottomDarkPixels = CountDarkPixels(screen, centerX - 4, centerY + 4, 8, 4);

        // Check left side
        int leftDarkPixels = CountDarkPixels(screen, centerX - 8, centerY - 4, 4, 8);

        // Check right side
        int rightDarkPixels = CountDarkPixels(screen, centerX + 4, centerY - 4, 4, 8);

        // Determine direction based on asymmetry
        int horizontal = rightDarkPixels - leftDarkPixels;
        int vertical = bottomDarkPixels - topDarkPixels;

        if (Math.Abs(horizontal) > Math.Abs(vertical))
        {
            return horizontal > 0 ? Direction.Right : Direction.Left;
        }
        else
        {
            return vertical > 0 ? Direction.Down : Direction.Up;
        }
    }

    private int CountDarkPixels(ScreenPixel[,] screen, int startX, int startY, int width, int height)
    {
        int count = 0;
        for (int y = startY; y < startY + height && y < 144; y++)
        {
            for (int x = startX; x < startX + width && x < 160; x++)
            {
                if (x >= 0 && y >= 0)
                {
                    var pixel = screen[x, y];
                    if (pixel.R < 100 && pixel.G < 100 && pixel.B < 100)
                        count++;
                }
            }
        }
        return count;
    }

    public bool DetectMapTransition(ScreenPixel[,] screen)
    {
        // Map transitions in Pokemon Red involve a black screen fade
        return IsBlackScreen(screen);
    }

    private bool IsBlackScreen(ScreenPixel[,] screen)
    {
        int blackCount = 0;
        int sampleSize = 50;
        var random = new Random(123);

        for (int i = 0; i < sampleSize; i++)
        {
            int x = random.Next(160);
            int y = random.Next(144);
            if (screen[x, y].IsBlack)
                blackCount++;
        }

        return blackCount > sampleSize * 0.9;
    }

    private Direction? DetectScreenScroll(Tile[,] currentTiles, Tile[,] previousTiles)
    {
        // Compare tiles to detect if the screen has scrolled
        // When player moves, the entire screen shifts by one tile

        // Check for upward scroll (tiles shifted down)
        if (TilesMatch(currentTiles, previousTiles, 0, 1))
            return Direction.Up;

        // Check for downward scroll (tiles shifted up)
        if (TilesMatch(currentTiles, previousTiles, 0, -1))
            return Direction.Down;

        // Check for leftward scroll (tiles shifted right)
        if (TilesMatch(currentTiles, previousTiles, 1, 0))
            return Direction.Left;

        // Check for rightward scroll (tiles shifted left)
        if (TilesMatch(currentTiles, previousTiles, -1, 0))
            return Direction.Right;

        return null;
    }

    private bool TilesMatch(Tile[,] current, Tile[,] previous, int offsetX, int offsetY)
    {
        int matchCount = 0;
        int compareCount = 0;

        for (int y = 2; y < TileReader.ScreenHeightTiles - 2; y++)
        {
            for (int x = 2; x < TileReader.ScreenWidthTiles - 2; x++)
            {
                int prevX = x + offsetX;
                int prevY = y + offsetY;

                if (prevX >= 0 && prevX < TileReader.ScreenWidthTiles &&
                    prevY >= 0 && prevY < TileReader.ScreenHeightTiles)
                {
                    compareCount++;
                    if (current[x, y].Hash == previous[prevX, prevY].Hash)
                    {
                        matchCount++;
                    }
                }
            }
        }

        return compareCount > 0 && (float)matchCount / compareCount > 0.8f;
    }

    private void UpdateWorldPosition(Direction direction)
    {
        int oldX = _currentPosition.WorldX;
        int oldY = _currentPosition.WorldY;

        switch (direction)
        {
            case Direction.Up:
                _currentPosition.WorldY--;
                break;
            case Direction.Down:
                _currentPosition.WorldY++;
                break;
            case Direction.Left:
                _currentPosition.WorldX--;
                break;
            case Direction.Right:
                _currentPosition.WorldX++;
                break;
        }

        if (oldX != _currentPosition.WorldX || oldY != _currentPosition.WorldY)
        {
            PositionChanged?.Invoke(this, _currentPosition);
        }
    }
}

public class PlayerPosition
{
    public int WorldX { get; set; }
    public int WorldY { get; set; }
    public string MapId { get; set; } = "unknown";
    public Direction FacingDirection { get; set; } = Direction.Down;
    public bool IsMoving { get; set; }
    public bool IsTransitioning { get; set; }

    public override string ToString() => $"({WorldX}, {WorldY}) on {MapId}, facing {FacingDirection}";
}
