using System.Drawing;

namespace PokemonRedAI.WinForms;

/// <summary>
/// Analyzes Pokemon Red game screens to detect room layout, exits, and obstacles
/// </summary>
public class RoomAnalyzer
{
    // Game Boy screen is 160x144 pixels, with 16x16 pixel tiles
    public const int GAMEBOY_WIDTH = 160;
    public const int GAMEBOY_HEIGHT = 144;
    public const int TILE_SIZE = 16;
    public const int TILES_X = 10;  // 160 / 16
    public const int TILES_Y = 9;   // 144 / 16

    // Player is typically at center of screen (tile 4-5, 4)
    public const int PLAYER_TILE_X = 4;
    public const int PLAYER_TILE_Y = 4;

    // Threshold for considering a tile "dark" (likely a doorway)
    private const int DARK_THRESHOLD = 40;

    // Threshold for considering two frames "same" (no movement)
    private const float SIMILARITY_THRESHOLD = 0.95f;

    private Bitmap? _previousFrame;
    private TileType[,] _currentTileMap = new TileType[TILES_X, TILES_Y];

    public enum TileType
    {
        Unknown,
        Walkable,
        Wall,
        Exit,
        NPC,
        Player
    }

    public class AnalysisResult
    {
        public TileType[,] TileMap { get; set; } = new TileType[TILES_X, TILES_Y];
        public List<Point> DetectedExits { get; set; } = new();
        public bool ScreenChanged { get; set; }
        public float SimilarityToPrevious { get; set; }
        public string AnalysisSummary { get; set; } = "";
    }

    /// <summary>
    /// Analyzes the game screen to detect room layout and exits
    /// </summary>
    public AnalysisResult Analyze(Bitmap screenshot)
    {
        var result = new AnalysisResult();

        // Scale the screenshot to Game Boy resolution for analysis
        using var scaledFrame = ScaleToGameBoy(screenshot);

        // Check if screen changed from previous frame
        if (_previousFrame != null)
        {
            result.SimilarityToPrevious = CompareFrames(_previousFrame, scaledFrame);
            result.ScreenChanged = result.SimilarityToPrevious < SIMILARITY_THRESHOLD;
        }
        else
        {
            result.ScreenChanged = true;
            result.SimilarityToPrevious = 0;
        }

        // Analyze each tile
        for (int ty = 0; ty < TILES_Y; ty++)
        {
            for (int tx = 0; tx < TILES_X; tx++)
            {
                result.TileMap[tx, ty] = AnalyzeTile(scaledFrame, tx, ty);
            }
        }

        // Mark player position
        result.TileMap[PLAYER_TILE_X, PLAYER_TILE_Y] = TileType.Player;

        // Detect exits (dark tiles at edges)
        result.DetectedExits = DetectExits(result.TileMap, scaledFrame);

        // Store current frame for next comparison
        _previousFrame?.Dispose();
        _previousFrame = new Bitmap(scaledFrame);

        _currentTileMap = result.TileMap;

        // Build summary
        result.AnalysisSummary = BuildSummary(result);

        return result;
    }

    private Bitmap ScaleToGameBoy(Bitmap source)
    {
        var scaled = new Bitmap(GAMEBOY_WIDTH, GAMEBOY_HEIGHT);
        using (var g = Graphics.FromImage(scaled))
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.DrawImage(source, 0, 0, GAMEBOY_WIDTH, GAMEBOY_HEIGHT);
        }
        return scaled;
    }

    private float CompareFrames(Bitmap frame1, Bitmap frame2)
    {
        int matchingPixels = 0;
        int totalPixels = 0;

        // Sample pixels instead of checking every pixel for performance
        int sampleStep = 4;

        for (int y = 0; y < GAMEBOY_HEIGHT; y += sampleStep)
        {
            for (int x = 0; x < GAMEBOY_WIDTH; x += sampleStep)
            {
                var pixel1 = frame1.GetPixel(x, y);
                var pixel2 = frame2.GetPixel(x, y);

                // Compare RGB values with some tolerance
                int diff = Math.Abs(pixel1.R - pixel2.R) +
                           Math.Abs(pixel1.G - pixel2.G) +
                           Math.Abs(pixel1.B - pixel2.B);

                if (diff < 30) // Allow small differences
                    matchingPixels++;

                totalPixels++;
            }
        }

        return (float)matchingPixels / totalPixels;
    }

    private TileType AnalyzeTile(Bitmap frame, int tileX, int tileY)
    {
        int startX = tileX * TILE_SIZE;
        int startY = tileY * TILE_SIZE;

        int darkPixels = 0;
        int totalBrightness = 0;
        int pixelCount = 0;

        // Sample pixels in the tile
        for (int y = startY; y < startY + TILE_SIZE && y < GAMEBOY_HEIGHT; y++)
        {
            for (int x = startX; x < startX + TILE_SIZE && x < GAMEBOY_WIDTH; x++)
            {
                var pixel = frame.GetPixel(x, y);
                int brightness = (pixel.R + pixel.G + pixel.B) / 3;
                totalBrightness += brightness;
                pixelCount++;

                if (brightness < DARK_THRESHOLD)
                    darkPixels++;
            }
        }

        float avgBrightness = (float)totalBrightness / pixelCount;
        float darkRatio = (float)darkPixels / pixelCount;

        // If mostly dark, likely an exit/doorway
        if (darkRatio > 0.7f)
            return TileType.Exit;

        // If very bright with uniform color, likely walkable floor
        if (avgBrightness > 150 && darkRatio < 0.1f)
            return TileType.Walkable;

        // Default to unknown (could be wall, object, etc.)
        return TileType.Unknown;
    }

    private List<Point> DetectExits(TileType[,] tileMap, Bitmap frame)
    {
        var exits = new List<Point>();

        // Check top row for exits
        for (int x = 0; x < TILES_X; x++)
        {
            if (IsPotentialExit(frame, x, 0))
                exits.Add(new Point(x, 0));
        }

        // Check bottom row for exits
        for (int x = 0; x < TILES_X; x++)
        {
            if (IsPotentialExit(frame, x, TILES_Y - 1))
                exits.Add(new Point(x, TILES_Y - 1));
        }

        // Check left column for exits
        for (int y = 0; y < TILES_Y; y++)
        {
            if (IsPotentialExit(frame, 0, y))
                exits.Add(new Point(0, y));
        }

        // Check right column for exits
        for (int y = 0; y < TILES_Y; y++)
        {
            if (IsPotentialExit(frame, TILES_X - 1, y))
                exits.Add(new Point(TILES_X - 1, y));
        }

        return exits;
    }

    private bool IsPotentialExit(Bitmap frame, int tileX, int tileY)
    {
        int startX = tileX * TILE_SIZE;
        int startY = tileY * TILE_SIZE;

        int darkPixels = 0;
        int blackPixels = 0;
        int totalPixels = TILE_SIZE * TILE_SIZE;

        for (int y = startY; y < startY + TILE_SIZE && y < GAMEBOY_HEIGHT; y++)
        {
            for (int x = startX; x < startX + TILE_SIZE && x < GAMEBOY_WIDTH; x++)
            {
                var pixel = frame.GetPixel(x, y);
                int brightness = (pixel.R + pixel.G + pixel.B) / 3;

                if (brightness < DARK_THRESHOLD)
                    darkPixels++;
                if (brightness < 10)
                    blackPixels++;
            }
        }

        // An exit is typically a dark rectangle (doorway)
        // At least 50% dark pixels and some fully black pixels
        float darkRatio = (float)darkPixels / totalPixels;
        float blackRatio = (float)blackPixels / totalPixels;

        return darkRatio > 0.5f || blackRatio > 0.3f;
    }

    private string BuildSummary(AnalysisResult result)
    {
        var lines = new List<string>();

        lines.Add("=== Room Analysis ===");
        lines.Add($"Screen changed: {result.ScreenChanged} ({result.SimilarityToPrevious:P1} similar)");
        lines.Add($"Exits detected: {result.DetectedExits.Count}");

        if (result.DetectedExits.Count > 0)
        {
            foreach (var exit in result.DetectedExits)
            {
                string direction = GetExitDirection(exit);
                lines.Add($"  - Exit at ({exit.X}, {exit.Y}) - {direction}");
            }
        }

        // Count tile types
        int walkable = 0, walls = 0, exits = 0, unknown = 0;
        for (int y = 0; y < TILES_Y; y++)
        {
            for (int x = 0; x < TILES_X; x++)
            {
                switch (result.TileMap[x, y])
                {
                    case TileType.Walkable: walkable++; break;
                    case TileType.Wall: walls++; break;
                    case TileType.Exit: exits++; break;
                    default: unknown++; break;
                }
            }
        }

        lines.Add($"Tiles: {walkable} walkable, {exits} exit, {unknown} unknown");

        return string.Join(Environment.NewLine, lines);
    }

    private string GetExitDirection(Point exit)
    {
        if (exit.Y == 0) return "NORTH";
        if (exit.Y == TILES_Y - 1) return "SOUTH";
        if (exit.X == 0) return "WEST";
        if (exit.X == TILES_X - 1) return "EAST";
        return "UNKNOWN";
    }

    /// <summary>
    /// Gets the direction to move toward the nearest exit
    /// </summary>
    public GameButton? GetDirectionToNearestExit(List<Point> exits)
    {
        if (exits.Count == 0)
            return null;

        // Player is at center (4, 4)
        var playerPos = new Point(PLAYER_TILE_X, PLAYER_TILE_Y);

        // Find closest exit
        Point? nearestExit = null;
        float minDistance = float.MaxValue;

        foreach (var exit in exits)
        {
            float dist = (float)Math.Sqrt(
                Math.Pow(exit.X - playerPos.X, 2) +
                Math.Pow(exit.Y - playerPos.Y, 2));

            if (dist < minDistance)
            {
                minDistance = dist;
                nearestExit = exit;
            }
        }

        if (nearestExit == null)
            return null;

        // Determine direction to move
        int dx = nearestExit.Value.X - playerPos.X;
        int dy = nearestExit.Value.Y - playerPos.Y;

        // Prioritize the larger difference
        if (Math.Abs(dy) >= Math.Abs(dx))
        {
            return dy < 0 ? GameButton.Up : GameButton.Down;
        }
        else
        {
            return dx < 0 ? GameButton.Left : GameButton.Right;
        }
    }

    public void Dispose()
    {
        _previousFrame?.Dispose();
    }
}
