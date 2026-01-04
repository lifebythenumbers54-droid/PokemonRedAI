namespace PokemonRedAI.Core.ScreenReader;

public class TileReader
{
    public const int TileSize = 8;
    public const int ScreenWidthTiles = 20;  // 160 / 8
    public const int ScreenHeightTiles = 18; // 144 / 8

    // Player is typically centered on screen (with some offset)
    public const int PlayerTileX = 10; // Center X
    public const int PlayerTileY = 9;  // Center Y (slightly above true center)

    public Tile[,] ParseTiles(ScreenPixel[,] screen)
    {
        var tiles = new Tile[ScreenWidthTiles, ScreenHeightTiles];

        for (int tileY = 0; tileY < ScreenHeightTiles; tileY++)
        {
            for (int tileX = 0; tileX < ScreenWidthTiles; tileX++)
            {
                tiles[tileX, tileY] = ExtractTile(screen, tileX, tileY);
            }
        }

        return tiles;
    }

    public Tile ExtractTile(ScreenPixel[,] screen, int tileX, int tileY)
    {
        var tile = new Tile
        {
            TileX = tileX,
            TileY = tileY,
            PixelData = new ScreenPixel[TileSize, TileSize]
        };

        int startX = tileX * TileSize;
        int startY = tileY * TileSize;

        for (int y = 0; y < TileSize; y++)
        {
            for (int x = 0; x < TileSize; x++)
            {
                int screenX = startX + x;
                int screenY = startY + y;

                if (screenX < screen.GetLength(0) && screenY < screen.GetLength(1))
                {
                    tile.PixelData[x, y] = screen[screenX, screenY];
                }
            }
        }

        tile.Hash = ComputeTileHash(tile.PixelData);
        tile.IsBlack = IsTileBlack(tile.PixelData);

        return tile;
    }

    public Tile? GetPlayerTile(Tile[,] tiles)
    {
        if (PlayerTileX < tiles.GetLength(0) && PlayerTileY < tiles.GetLength(1))
        {
            return tiles[PlayerTileX, PlayerTileY];
        }
        return null;
    }

    public Tile? GetAdjacentTile(Tile[,] tiles, Direction direction)
    {
        int targetX = PlayerTileX;
        int targetY = PlayerTileY;

        switch (direction)
        {
            case Direction.Up:
                targetY--;
                break;
            case Direction.Down:
                targetY++;
                break;
            case Direction.Left:
                targetX--;
                break;
            case Direction.Right:
                targetX++;
                break;
        }

        if (targetX >= 0 && targetX < tiles.GetLength(0) &&
            targetY >= 0 && targetY < tiles.GetLength(1))
        {
            return tiles[targetX, targetY];
        }

        return null;
    }

    public bool AreTilesSimilar(Tile tile1, Tile tile2, int tolerance = 10)
    {
        if (tile1.Hash == tile2.Hash)
            return true;

        int differences = 0;
        int totalPixels = TileSize * TileSize;

        for (int y = 0; y < TileSize; y++)
        {
            for (int x = 0; x < TileSize; x++)
            {
                if (!tile1.PixelData[x, y].Matches(tile2.PixelData[x, y], tolerance))
                {
                    differences++;
                }
            }
        }

        return differences < totalPixels * 0.1; // Allow 10% difference
    }

    private long ComputeTileHash(ScreenPixel[,] pixels)
    {
        long hash = 17;
        for (int y = 0; y < TileSize; y += 2)
        {
            for (int x = 0; x < TileSize; x += 2)
            {
                var p = pixels[x, y];
                hash = hash * 31 + p.R;
                hash = hash * 31 + p.G;
                hash = hash * 31 + p.B;
            }
        }
        return hash;
    }

    private bool IsTileBlack(ScreenPixel[,] pixels)
    {
        int blackCount = 0;
        for (int y = 0; y < TileSize; y++)
        {
            for (int x = 0; x < TileSize; x++)
            {
                if (pixels[x, y].IsBlack)
                    blackCount++;
            }
        }
        return blackCount > TileSize * TileSize * 0.9;
    }
}

public class Tile
{
    public int TileX { get; set; }
    public int TileY { get; set; }
    public ScreenPixel[,] PixelData { get; set; } = new ScreenPixel[TileReader.TileSize, TileReader.TileSize];
    public long Hash { get; set; }
    public bool IsBlack { get; set; }

    public override string ToString() => $"Tile({TileX}, {TileY})";
}

public enum Direction
{
    Up,
    Down,
    Left,
    Right
}
