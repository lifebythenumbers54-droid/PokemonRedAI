using System.Security.Cryptography;
using System.Text.Json;

namespace PokemonRedAI.WinForms;

/// <summary>
/// Database for storing and retrieving tile data.
/// Persists tile information to disk for use across sessions.
/// </summary>
public class TileDatabase
{
    private readonly Dictionary<long, TileData> _tiles = new();
    private readonly string _databasePath;
    private readonly object _lock = new();
    private bool _isDirty = false;

    // Auto-save interval
    private readonly System.Timers.Timer _autoSaveTimer;
    private const int AUTO_SAVE_INTERVAL_MS = 10000; // 10 seconds

    public TileDatabase(string? databasePath = null)
    {
        _databasePath = databasePath ?? GetDefaultDatabasePath();

        // Ensure directory exists
        var dir = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        // Load existing data
        Load();

        // Setup auto-save
        _autoSaveTimer = new System.Timers.Timer(AUTO_SAVE_INTERVAL_MS);
        _autoSaveTimer.Elapsed += (s, e) => SaveIfDirty();
        _autoSaveTimer.AutoReset = true;
        _autoSaveTimer.Start();
    }

    private static string GetDefaultDatabasePath()
    {
        // Save in the application's directory (bin\Debug\net8.0-windows)
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        return Path.Combine(appDir, "tiles.json");
    }

    /// <summary>
    /// Gets tile data by hash. Returns null if not found.
    /// </summary>
    public TileData? GetTile(long tileHash)
    {
        lock (_lock)
        {
            return _tiles.TryGetValue(tileHash, out var tile) ? tile : null;
        }
    }

    /// <summary>
    /// Gets or creates tile data for a given hash.
    /// </summary>
    public TileData GetOrCreateTile(long tileHash)
    {
        lock (_lock)
        {
            if (!_tiles.TryGetValue(tileHash, out var tile))
            {
                tile = new TileData { TileHash = tileHash };
                _tiles[tileHash] = tile;
                _isDirty = true;
            }
            return tile;
        }
    }

    /// <summary>
    /// Updates tile data.
    /// </summary>
    public void UpdateTile(TileData tile)
    {
        lock (_lock)
        {
            _tiles[tile.TileHash] = tile;
            _isDirty = true;
        }
    }

    /// <summary>
    /// Records a walk attempt on a tile.
    /// </summary>
    public void RecordWalkAttempt(long tileHash, bool success)
    {
        lock (_lock)
        {
            var tile = GetOrCreateTile(tileHash);
            tile.RecordWalkAttempt(success);
            _isDirty = true;
        }
    }

    /// <summary>
    /// Marks a tile as a specific type.
    /// </summary>
    public void SetTileType(long tileHash, TileType type)
    {
        lock (_lock)
        {
            var tile = GetOrCreateTile(tileHash);

            tile.IsWalkable = type.HasFlag(TileType.Walkable);
            tile.IsBlocking = type.HasFlag(TileType.Blocking);
            tile.IsDoor = type.HasFlag(TileType.Door);
            tile.IsWarp = type.HasFlag(TileType.Warp);
            tile.IsWater = type.HasFlag(TileType.Water);
            tile.IsLedge = type.HasFlag(TileType.Ledge);
            tile.IsGrass = type.HasFlag(TileType.Grass);
            tile.IsInteractable = type.HasFlag(TileType.Interactable) || type.HasFlag(TileType.NPC);

            // If explicitly marked as blocking, it's not walkable
            if (tile.IsBlocking)
                tile.IsWalkable = false;

            _isDirty = true;
        }
    }

    /// <summary>
    /// Gets all known tiles.
    /// </summary>
    public IReadOnlyCollection<TileData> GetAllTiles()
    {
        lock (_lock)
        {
            return _tiles.Values.ToList().AsReadOnly();
        }
    }

    /// <summary>
    /// Gets statistics about the tile database.
    /// </summary>
    public TileDatabaseStats GetStats()
    {
        lock (_lock)
        {
            return new TileDatabaseStats
            {
                TotalTiles = _tiles.Count,
                WalkableTiles = _tiles.Values.Count(t => t.IsWalkable),
                BlockingTiles = _tiles.Values.Count(t => t.IsBlocking),
                DoorTiles = _tiles.Values.Count(t => t.IsDoor),
                WarpTiles = _tiles.Values.Count(t => t.IsWarp),
                GrassTiles = _tiles.Values.Count(t => t.IsGrass),
                WaterTiles = _tiles.Values.Count(t => t.IsWater),
                InteractableTiles = _tiles.Values.Count(t => t.IsInteractable),
                HighConfidenceTiles = _tiles.Values.Count(t => t.WalkSuccessCount + t.WalkFailCount >= 5)
            };
        }
    }

    /// <summary>
    /// Saves the database to disk.
    /// </summary>
    public void Save()
    {
        lock (_lock)
        {
            try
            {
                var json = JsonSerializer.Serialize(_tiles.Values.ToList(), new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(_databasePath, json);
                _isDirty = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save tile database: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Saves only if there are unsaved changes.
    /// </summary>
    public void SaveIfDirty()
    {
        if (_isDirty)
        {
            Save();
        }
    }

    /// <summary>
    /// Loads the database from disk.
    /// </summary>
    public void Load()
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(_databasePath))
                {
                    var json = File.ReadAllText(_databasePath);
                    var tiles = JsonSerializer.Deserialize<List<TileData>>(json);

                    _tiles.Clear();
                    if (tiles != null)
                    {
                        foreach (var tile in tiles)
                        {
                            _tiles[tile.TileHash] = tile;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load tile database: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Clears all tile data.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _tiles.Clear();
            _isDirty = true;
        }
    }

    /// <summary>
    /// Computes a hash for a tile bitmap that's consistent across sessions.
    /// Uses pixel data to create a unique identifier.
    /// </summary>
    public static long ComputeTileHash(Bitmap tile)
    {
        // Use a more robust hash that samples the entire tile
        long hash = 17;

        // Sample pixels in a grid pattern for speed
        int stepX = Math.Max(1, tile.Width / 8);
        int stepY = Math.Max(1, tile.Height / 8);

        for (int y = 0; y < tile.Height; y += stepY)
        {
            for (int x = 0; x < tile.Width; x += stepX)
            {
                var pixel = tile.GetPixel(x, y);
                // Quantize colors slightly to handle minor rendering differences
                int r = pixel.R / 8;
                int g = pixel.G / 8;
                int b = pixel.B / 8;
                hash = hash * 31 + (r << 10) + (g << 5) + b;
            }
        }

        return hash;
    }

    /// <summary>
    /// Computes a perceptual hash that's more tolerant of minor variations.
    /// Good for matching similar tiles.
    /// </summary>
    public static long ComputePerceptualHash(Bitmap tile)
    {
        // Resize to 8x8 and compute average
        using var small = new Bitmap(8, 8);
        using (var g = Graphics.FromImage(small))
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
            g.DrawImage(tile, 0, 0, 8, 8);
        }

        // Convert to grayscale and compute average
        long sum = 0;
        var grays = new int[64];

        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                var pixel = small.GetPixel(x, y);
                int gray = (pixel.R + pixel.G + pixel.B) / 3;
                grays[y * 8 + x] = gray;
                sum += gray;
            }
        }

        int avg = (int)(sum / 64);

        // Build hash based on whether each pixel is above or below average
        long hash = 0;
        for (int i = 0; i < 64; i++)
        {
            if (grays[i] >= avg)
                hash |= (1L << i);
        }

        return hash;
    }

    public void Dispose()
    {
        _autoSaveTimer.Stop();
        _autoSaveTimer.Dispose();
        SaveIfDirty();
    }
}

/// <summary>
/// Statistics about the tile database.
/// </summary>
public class TileDatabaseStats
{
    public int TotalTiles { get; set; }
    public int WalkableTiles { get; set; }
    public int BlockingTiles { get; set; }
    public int DoorTiles { get; set; }
    public int WarpTiles { get; set; }
    public int GrassTiles { get; set; }
    public int WaterTiles { get; set; }
    public int InteractableTiles { get; set; }
    public int HighConfidenceTiles { get; set; }

    public override string ToString()
    {
        return $"Tiles: {TotalTiles} (Walkable: {WalkableTiles}, Blocking: {BlockingTiles}, Doors: {DoorTiles}, Warps: {WarpTiles}, Grass: {GrassTiles}, Water: {WaterTiles})";
    }
}
