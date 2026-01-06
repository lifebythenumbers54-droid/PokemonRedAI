using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace PokemonRedAI.WinForms;

public class ScreenCapture
{
    private readonly IntPtr _windowHandle;

    // Game Boy native resolution
    private const int GB_WIDTH = 160;
    private const int GB_HEIGHT = 144;

    // Cached game area bounds (detected once and reused)
    private Rectangle? _cachedGameArea;

    public ScreenCapture(IntPtr windowHandle)
    {
        _windowHandle = windowHandle;
    }

    /// <summary>
    /// Captures only the game screen area (the actual game tiles), excluding emulator UI/borders.
    /// </summary>
    public Bitmap? CaptureGameScreen()
    {
        var fullWindow = CaptureClientArea();
        if (fullWindow == null)
            return null;

        try
        {
            // Detect or use cached game area
            var gameArea =  DetectGameArea(fullWindow);
            if (gameArea.Width <= 0 || gameArea.Height <= 0)
            {
                // Fallback: return full client area if detection fails
                return fullWindow;
            }

            // Crop to game area
            var cropped = new Bitmap(gameArea.Width, gameArea.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(cropped))
            {
                g.DrawImage(fullWindow,
                    new Rectangle(0, 0, gameArea.Width, gameArea.Height),
                    gameArea,
                    GraphicsUnit.Pixel);
            }

            fullWindow.Dispose();
            return cropped;
        }
        catch
        {
            return fullWindow;
        }
    }

    /// <summary>
    /// Detects the game area within the emulator window by finding the black border around the game.
    /// mGBA and similar emulators have a thin black border immediately surrounding the game screen.
    /// </summary>
    private Rectangle DetectGameArea(Bitmap fullWindow)
    {
        int width = fullWindow.Width;
        int height = fullWindow.Height;

        return EstimateGameArea(width, height);
    }

    /// <summary>
    /// Scans from center in the given direction to find the edge of the game content.
    /// Returns the coordinate where game content ends (black border or frame begins).
    /// </summary>
    private int FindBlackBorderInnerEdge(Bitmap bitmap, int startX, int startY, int dx, int dy)
    {
        int x = startX;
        int y = startY;
        int width = bitmap.Width;
        int height = bitmap.Height;

        // Move in the specified direction until we hit a dark/black pixel
        // (indicating the border around the game)
        while (x > 0 && x < width - 1 && y > 0 && y < height - 1)
        {
            var pixel = bitmap.GetPixel(x, y);

            // Check if this is a very dark pixel (black border)
            // The black border around the game screen is typically RGB close to (0,0,0)
            if (pixel.R < 30 && pixel.G < 30 && pixel.B < 30)
            {
                // Found the black border - return the previous position (inside the game)
                if (dx != 0) return x - dx;
                if (dy != 0) return y - dy;
            }

            x += dx;
            y += dy;
        }

        // Reached edge of image
        if (dx < 0) return 0;
        if (dx > 0) return width;
        if (dy < 0) return 0;
        return height;
    }

    private Rectangle EstimateGameArea(int windowWidth, int windowHeight)
    {
        // Calculate expected game area based on Game Boy aspect ratio (10:9)
        float gbAspect = (float)GB_WIDTH / GB_HEIGHT;
        float windowAspect = (float)windowWidth / windowHeight;

        int gameWidth, gameHeight, offsetX, offsetY;

        if (windowAspect > gbAspect)
        {
            // Window is wider than game - black bars on sides
            gameHeight = windowHeight;
            gameWidth = (int)(windowHeight * gbAspect);
            offsetX = (windowWidth - gameWidth) / 2;
            offsetY = 0;
        }
        else
        {
            // Window is taller than game - black bars on top/bottom
            gameWidth = windowWidth;
            gameHeight = (int)(windowWidth / gbAspect);
            offsetX = 0;
            offsetY = (windowHeight - gameHeight) / 2;
        }

        return new Rectangle(offsetX, offsetY, gameWidth, gameHeight);
    }

    /// <summary>
    /// Resets the cached game area detection (call if window is resized).
    /// </summary>
    public void ResetGameAreaCache()
    {
        _cachedGameArea = null;
    }

    // Pokemon Red overworld tile size (16x16 pixels = 2x2 GB tiles)
    private const int TILE_SIZE = 16;
    // Visible tiles: 160/16 = 10 wide, 144/16 = 9 tall
    public const int TILES_X = 10;
    public const int TILES_Y = 9;

    /// <summary>
    /// Extracts the game screen as a grid of tiles.
    /// Pokemon Red uses 16x16 pixel tiles in the overworld.
    /// Returns a 10x9 grid of tile bitmaps.
    /// </summary>
    public Bitmap[,]? ExtractTiles(Bitmap? gameScreen = null)
    {
        var screen = gameScreen ?? CaptureGameScreen();
        if (screen == null)
            return null;

        try
        {
            // Scale the captured screen to native GB resolution for consistent tile extraction
            using var scaledScreen = new Bitmap(GB_WIDTH, GB_HEIGHT, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(scaledScreen))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                g.DrawImage(screen, 0, 0, GB_WIDTH, GB_HEIGHT);
            }

            var tiles = new Bitmap[TILES_X, TILES_Y];

            for (int y = 0; y < TILES_Y; y++)
            {
                for (int x = 0; x < TILES_X; x++)
                {
                    var tile = new Bitmap(TILE_SIZE, TILE_SIZE, PixelFormat.Format32bppArgb);
                    using (var g = Graphics.FromImage(tile))
                    {
                        g.DrawImage(scaledScreen,
                            new Rectangle(0, 0, TILE_SIZE, TILE_SIZE),
                            new Rectangle(x * TILE_SIZE, y * TILE_SIZE, TILE_SIZE, TILE_SIZE),
                            GraphicsUnit.Pixel);
                    }
                    tiles[x, y] = tile;
                }
            }

            // Dispose the screen if we created it
            if (gameScreen == null)
                screen.Dispose();

            return tiles;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Extracts a single tile at the given grid position.
    /// </summary>
    public Bitmap? ExtractTile(int tileX, int tileY, Bitmap? gameScreen = null)
    {
        if (tileX < 0 || tileX >= TILES_X || tileY < 0 || tileY >= TILES_Y)
            return null;

        var screen = gameScreen ?? CaptureGameScreen();
        if (screen == null)
            return null;

        try
        {
            // Scale to native resolution
            using var scaledScreen = new Bitmap(GB_WIDTH, GB_HEIGHT, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(scaledScreen))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                g.DrawImage(screen, 0, 0, GB_WIDTH, GB_HEIGHT);
            }

            var tile = new Bitmap(TILE_SIZE, TILE_SIZE, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(tile))
            {
                g.DrawImage(scaledScreen,
                    new Rectangle(0, 0, TILE_SIZE, TILE_SIZE),
                    new Rectangle(tileX * TILE_SIZE, tileY * TILE_SIZE, TILE_SIZE, TILE_SIZE),
                    GraphicsUnit.Pixel);
            }

            if (gameScreen == null)
                screen.Dispose();

            return tile;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets a hash of a tile for comparison purposes.
    /// Can be used to identify unique tiles or detect tile changes.
    /// </summary>
    public static long GetTileHash(Bitmap tile)
    {
        long hash = 0;
        for (int y = 0; y < tile.Height && y < 16; y += 2)
        {
            for (int x = 0; x < tile.Width && x < 16; x += 2)
            {
                var pixel = tile.GetPixel(x, y);
                // Simple hash combining RGB values
                hash = hash * 31 + pixel.R + pixel.G * 256 + pixel.B * 65536;
            }
        }
        return hash;
    }

    public Bitmap? CaptureWindow()
    {
        if (_windowHandle == IntPtr.Zero || !IsWindow(_windowHandle))
            return null;

        try
        {
            // Get window rectangle
            if (!GetWindowRect(_windowHandle, out RECT rect))
                return null;

            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;

            if (width <= 0 || height <= 0)
                return null;

            // Create bitmap
            var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);

            using (var graphics = Graphics.FromImage(bitmap))
            {
                var hdcDest = graphics.GetHdc();
                var hdcSrc = GetWindowDC(_windowHandle);

                // Use BitBlt to capture the window
                BitBlt(hdcDest, 0, 0, width, height, hdcSrc, 0, 0, SRCCOPY);

                ReleaseDC(_windowHandle, hdcSrc);
                graphics.ReleaseHdc(hdcDest);
            }

            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    public Bitmap? CaptureClientArea()
    {
        if (_windowHandle == IntPtr.Zero || !IsWindow(_windowHandle))
            return null;

        try
        {
            // Get client rectangle
            if (!GetClientRect(_windowHandle, out RECT clientRect))
                return null;

            int width = clientRect.Right - clientRect.Left;
            int height = clientRect.Bottom - clientRect.Top;

            if (width <= 0 || height <= 0)
                return null;

            // Convert client coordinates to screen coordinates
            POINT topLeft = new POINT { X = 0, Y = 0 };
            //827,813
            ClientToScreen(_windowHandle, ref topLeft);

            // Create bitmap
            var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);

            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(topLeft.X, topLeft.Y, 0, 0, new Size(width, height));
            }

            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    #region Win32 Imports

    private const int SRCCOPY = 0x00CC0020;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int width, int height,
        IntPtr hdcSrc, int xSrc, int ySrc, int rop);

    #endregion
}
