using System.Drawing;
using System.Text;

namespace PokemonRedAI.WinForms;

public class AIController
{
    private readonly InputSender _inputSender;
    private readonly Action<string, string, ActionLogType> _logAction;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _aiTask;
    private readonly Random _random = new();
    private ScreenCapture? _screenCapture;

    // Smart navigation components
    private readonly RoomAnalyzer _roomAnalyzer = new();
    private readonly ExplorationStrategy _explorationStrategy = new();
    private RoomAnalyzer.AnalysisResult? _lastAnalysis;

    // Tile database for learning
    private readonly TileDatabase _tileDatabase = new();
    private long[,]? _lastTileHashes;
    private long[,]? _currentTileHashes;

    // AI Settings
    public int MovementDelayMs { get; set; } = 300;
    public int KeyPressDurationMs { get; set; } = 50;

    /// <summary>
    /// When enabled, the AI will prioritize discovering walkability of unknown tiles
    /// instead of normal exploration logic.
    /// </summary>
    public bool WalkableDiscoveryMode { get; set; } = false;

    // State tracking
    private int _moveCount = 0;
    private int _exitFoundCount = 0;
    private int _stuckCount = 0;
    private int _tilesRecorded = 0;

    // Walkability learning state
    private GameButton? _lastMoveDirection;
    private long _lastTargetTileHash;
    private long _lastScreenHash;
    private int _walkabilityLearned = 0;

    // Discovery mode visualization state
    private (int x, int y)? _targetTilePosition;
    private int _targetTileDistance;

    // Stuck detection - track repeated failed move attempts
    private GameButton? _lastAttemptedDirection;
    private int _sameDirectionAttempts = 0;
    private long _lastPlayerTileHash = 0;
    private const int MAX_SAME_DIRECTION_ATTEMPTS = 3;

    // Text box tile hashes - these indicate a dialogue/menu is open
    // Add the hash values of text box border/background tiles here
    private readonly HashSet<long> _textBoxTileHashes = new();

    /// <summary>
    /// Adds a tile hash that indicates a text box is present.
    /// When these tiles are detected on screen, the AI will press B to dismiss.
    /// </summary>
    public void AddTextBoxTileHash(long hash)
    {
        _textBoxTileHashes.Add(hash);
        _logAction("TextBox", $"Added text box tile hash: {hash}", ActionLogType.Info);
    }

    /// <summary>
    /// Adds multiple tile hashes that indicate a text box is present.
    /// </summary>
    public void AddTextBoxTileHashes(IEnumerable<long> hashes)
    {
        foreach (var hash in hashes)
        {
            _textBoxTileHashes.Add(hash);
        }
        _logAction("TextBox", $"Added {hashes.Count()} text box tile hashes", ActionLogType.Info);
    }

    /// <summary>
    /// Checks if any text box tiles are present on the current screen.
    /// </summary>
    private bool IsTextBoxPresent()
    {
        if (_currentTileHashes == null || _textBoxTileHashes.Count == 0)
            return false;

        // Check if any tile on screen matches a text box tile hash
        for (int y = 0; y < ScreenCapture.TILES_Y; y++)
        {
            for (int x = 0; x < ScreenCapture.TILES_X; x++)
            {
                long hash = _currentTileHashes[x, y];
                if (hash != 0 && _textBoxTileHashes.Contains(hash))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // Events for UI updates
    public event Action<Bitmap>? ScreenCaptured;
    public event Action<string>? StatusUpdated;

    public bool IsRunning => _aiTask != null && !_aiTask.IsCompleted;

    public AIController(InputSender inputSender, Action<string, string, ActionLogType> logAction)
    {
        _inputSender = inputSender;
        _logAction = logAction;
    }

    public void SetScreenCapture(ScreenCapture capture)
    {
        _screenCapture = capture;
    }

    public void Start()
    {
        if (IsRunning)
            return;

        _moveCount = 0;
        _exitFoundCount = 0;
        _stuckCount = 0;
        _walkabilityLearned = 0;
        _lastMoveDirection = null;
        _lastTargetTileHash = 0;
        _lastScreenHash = 0;
        _explorationStrategy.Reset();

        _cancellationTokenSource = new CancellationTokenSource();
        _aiTask = Task.Run(() => AILoop(_cancellationTokenSource.Token));
        _logAction("AI Controller", "Started smart exploration mode", ActionLogType.Info);
    }

    public void Stop()
    {
        if (!IsRunning)
            return;

        _cancellationTokenSource?.Cancel();
        try
        {
            _aiTask?.Wait(1000);
        }
        catch (AggregateException)
        {
            // Expected when cancelled
        }
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        _aiTask = null;

        // Save tile database
        _tileDatabase.Save();
        var stats = _tileDatabase.GetStats();
        _logAction("AI Controller", $"Stopped. {stats}", ActionLogType.Info);
    }

    private async Task AILoop(CancellationToken cancellationToken)
    {
        _logAction("AI Loop", "Starting smart exploration...", ActionLogType.Info);
        UpdateStatus("AI Started\nAnalyzing room...");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Capture and analyze the screen
                var screenshot = CaptureScreen();

                if (screenshot != null)
                {
                    // Analyze the room
                    _lastAnalysis = _roomAnalyzer.Analyze(screenshot);

                    // Extract and record tiles
                    RecordTiles(screenshot);

                    // Learn walkability from the previous move attempt
                    LearnWalkability(screenshot);

                    // Track statistics
                    if (_lastAnalysis.DetectedExits.Count > 0)
                        _exitFoundCount++;
                    if (!_lastAnalysis.ScreenChanged && _moveCount > 0)
                        _stuckCount++;

                    // Decide and execute action
                    var action = DecideSmartAction(screenshot);

                    // Now send the screenshot to UI with overlay (after decision is made)
                    SendScreenshotToUI(screenshot);

                    // Update UI with analysis results
                    UpdateStatusDisplay(screenshot, action);

                    // Before executing a move, store the target tile hash for walkability learning
                    if (action.Type == AIActionType.Move)
                    {
                        _lastMoveDirection = action.Direction;
                        _lastTargetTileHash = GetTargetTileHash(action.Direction);
                        _lastScreenHash = CalculateScreenHash(screenshot);
                    }
                    else
                    {
                        // Non-move actions reset walkability tracking
                        _lastMoveDirection = null;
                        _lastTargetTileHash = 0;
                    }

                    await ExecuteAction(action, cancellationToken);
                    _moveCount++;
                }
                else
                {
                    // No screenshot, try a random move
                    var fallbackAction = new AIAction { Type = AIActionType.Move, Direction = GetRandomDirection() };
                    await ExecuteAction(fallbackAction, cancellationToken);
                }

                // Wait between actions
                await Task.Delay(MovementDelayMs, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logAction("AI Error", ex.Message, ActionLogType.Error);
                UpdateStatus($"Error: {ex.Message}");
                await Task.Delay(1000, cancellationToken);
            }
        }
    }

    private Bitmap? CaptureScreen()
    {
        if (_screenCapture == null)
            return null;

        try
        {
            // Use CaptureGameScreen to get only the game area (no emulator borders)
            var screenshot = _screenCapture.CaptureGameScreen();
            return screenshot;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Screen capture error: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Sends the screenshot to the UI with discovery mode overlay if enabled.
    /// Called after DecideSmartAction so _targetTilePosition is set correctly.
    /// </summary>
    private void SendScreenshotToUI(Bitmap screenshot)
    {
        try
        {
            // Send a COPY to UI to avoid "object is currently in use" errors
            // The UI and AI loop would otherwise fight over the same bitmap
            var uiCopy = new Bitmap(screenshot);

            // Draw discovery mode overlay if enabled
            if (WalkableDiscoveryMode)
            {
                DrawDiscoveryOverlay(uiCopy);
            }

            ScreenCaptured?.Invoke(uiCopy);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SendScreenshotToUI error: {ex.Message}");
        }
    }

    /// <summary>
    /// Draws the discovery mode overlay on the screenshot showing:
    /// - Player position (underline)
    /// - Line to target tile being investigated
    /// </summary>
    private void DrawDiscoveryOverlay(Bitmap screenshot)
    {
        const int TILE_SIZE = 16;
        const int playerX = 4;
        const int playerY = 4;

        using var g = Graphics.FromImage(screenshot);
        using var playerPen = new Pen(Color.Cyan, 2);
        using var targetPen = new Pen(Color.Yellow, 2);
        using var linePen = new Pen(Color.Lime, 1);
        linePen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;

        // Calculate player tile pixel position
        int playerPixelX = playerX * TILE_SIZE;
        int playerPixelY = playerY * TILE_SIZE;

        // Draw underline under player tile
        g.DrawLine(playerPen,
            playerPixelX, playerPixelY + TILE_SIZE - 1,
            playerPixelX + TILE_SIZE, playerPixelY + TILE_SIZE - 1);

        // Draw box around player tile
        g.DrawRectangle(playerPen, playerPixelX, playerPixelY, TILE_SIZE, TILE_SIZE);

        // If we have a target tile, draw line to it and highlight it
        if (_targetTilePosition.HasValue)
        {
            int targetPixelX = _targetTilePosition.Value.x * TILE_SIZE;
            int targetPixelY = _targetTilePosition.Value.y * TILE_SIZE;

            // Draw dashed line from player center to target center
            g.DrawLine(linePen,
                playerPixelX + TILE_SIZE / 2, playerPixelY + TILE_SIZE / 2,
                targetPixelX + TILE_SIZE / 2, targetPixelY + TILE_SIZE / 2);

            // Draw box around target tile
            g.DrawRectangle(targetPen, targetPixelX, targetPixelY, TILE_SIZE, TILE_SIZE);

            // Draw X in target tile to mark it
            g.DrawLine(targetPen, targetPixelX + 2, targetPixelY + 2, targetPixelX + TILE_SIZE - 2, targetPixelY + TILE_SIZE - 2);
            g.DrawLine(targetPen, targetPixelX + TILE_SIZE - 2, targetPixelY + 2, targetPixelX + 2, targetPixelY + TILE_SIZE - 2);
        }
    }

    // Folder for saving tile images
    private static readonly string TilesFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tiles");

    private void RecordTiles(Bitmap screenshot)
    {
        if (_screenCapture == null)
            return;

        try
        {
            // Extract tiles from the screenshot
            var tiles = _screenCapture.ExtractTiles(screenshot);
            if (tiles == null)
                return;

            // Ensure tiles folder exists
            if (!Directory.Exists(TilesFolder))
            {
                Directory.CreateDirectory(TilesFolder);
            }

            // Store previous hashes
            _lastTileHashes = _currentTileHashes;
            _currentTileHashes = new long[ScreenCapture.TILES_X, ScreenCapture.TILES_Y];

            // Record each tile - only add if it doesn't already exist
            for (int y = 0; y < ScreenCapture.TILES_Y; y++)
            {
                for (int x = 0; x < ScreenCapture.TILES_X; x++)
                {
                    var tile = tiles[x, y];
                    if (tile != null)
                    {
                        long hash = TileDatabase.ComputeTileHash(tile);
                        _currentTileHashes[x, y] = hash;

                        // Only create new entry if tile doesn't exist - don't update existing tiles
                        if (_tileDatabase.GetTile(hash) == null)
                        {
                            _tileDatabase.GetOrCreateTile(hash);

                            // Save tile image as BMP with hash as filename
                            string tilePath = Path.Combine(TilesFolder, $"{hash}.bmp");
                            tile.Save(tilePath, System.Drawing.Imaging.ImageFormat.Bmp);
                        }

                        tile.Dispose();
                    }
                }
            }

            _tilesRecorded = _tileDatabase.GetStats().TotalTiles;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Tile recording error: {ex.Message}");
        }
    }

    private AIAction DecideSmartAction(Bitmap screenshot)
    {
        // First check if a text box is present - if so, press B to dismiss it
        if (IsTextBoxPresent())
        {
            _logAction("Discovery", "Text box detected - pressing B to dismiss", ActionLogType.Info);
            return new AIAction { Type = AIActionType.PressB };
        }

        // If walkable discovery mode is enabled, prioritize finding unknown tiles
        if (WalkableDiscoveryMode)
        {
            return DecideWalkableDiscoveryAction();
        }

        // Occasionally press A to interact (5% chance)
        if (_random.Next(100) < 5)
        {
            _explorationStrategy.OnActionButton(GameButton.A);
            return new AIAction { Type = AIActionType.PressA };
        }

        // Occasionally press B to cancel/speed up text (3% chance)
        if (_random.Next(100) < 3)
        {
            _explorationStrategy.OnActionButton(GameButton.B);
            return new AIAction { Type = AIActionType.PressB };
        }

        // Use the exploration strategy to decide movement
        var direction = _explorationStrategy.DecideNextMove(_lastAnalysis!, screenshot);
        return new AIAction { Type = AIActionType.Move, Direction = direction };
    }

    /// <summary>
    /// Decides the next action when in walkable discovery mode.
    /// Looks at the current screen and tries to find tiles with unknown walkability status.
    /// Walkability status: null = unknown, true = walkable, false = blocking.
    /// </summary>
    private AIAction DecideWalkableDiscoveryAction()
    {
        if (_currentTileHashes == null)
        {
            // No tile data yet, do a random move
            return new AIAction { Type = AIActionType.Move, Direction = GetRandomDirection() };
        }

        // Player position in tile coordinates (center of screen)
        const int playerX = 4;
        const int playerY = 4;

        // Check if player tile hash changed (indicates actual movement)
        long currentPlayerTileHash = _currentTileHashes[playerX, playerY];
        bool playerMoved = currentPlayerTileHash != _lastPlayerTileHash && _lastPlayerTileHash != 0;

        // Update stuck detection
        if (playerMoved)
        {
            // Player actually moved, reset stuck counter
            _sameDirectionAttempts = 0;
            _lastAttemptedDirection = null;
        }

        _lastPlayerTileHash = currentPlayerTileHash;

        // Check adjacent tiles for unknown walkability
        var directionsToCheck = new (GameButton direction, int dx, int dy)[]
        {
            (GameButton.Up, 0, -1),
            (GameButton.Down, 0, 1),
            (GameButton.Left, -1, 0),
            (GameButton.Right, 1, 0)
        };

        // Categorize adjacent tiles
        var unknownAdjacentDirections = new List<GameButton>();
        var walkableAdjacentDirections = new List<GameButton>();

        foreach (var (direction, dx, dy) in directionsToCheck)
        {
            int targetX = playerX + dx;
            int targetY = playerY + dy;

            // Check bounds
            if (targetX < 0 || targetX >= ScreenCapture.TILES_X ||
                targetY < 0 || targetY >= ScreenCapture.TILES_Y)
                continue;

            long tileHash = _currentTileHashes[targetX, targetY];
            if (tileHash == 0)
                continue;

            var tile = _tileDatabase.GetTile(tileHash);
            bool? walkability = GetTileWalkability(tile);

            if (walkability == null)
            {
                unknownAdjacentDirections.Add(direction);
                _logAction("Discovery", $"{direction}: Unknown adjacent tile (hash: {tileHash})", ActionLogType.Info);
            }
            else if (walkability == true)
            {
                walkableAdjacentDirections.Add(direction);
            }
        }

        // Priority 1: Test unknown adjacent tiles
        if (unknownAdjacentDirections.Count > 0)
        {
            var chosenDirection = unknownAdjacentDirections[_random.Next(unknownAdjacentDirections.Count)];
            // Set target tile position for visualization (adjacent tile = distance 1)
            var (dx, dy) = GetDirectionOffset(chosenDirection);
            _targetTilePosition = (playerX + dx, playerY + dy);
            _targetTileDistance = 1;
            _logAction("Discovery", $"Testing unknown tile: {chosenDirection}", ActionLogType.Movement);
            return TrackAndReturnMove(chosenDirection);
        }

        // Priority 2: Search further away (2, 3, 4... spaces) for unknown tiles with walkable path
        // But skip directions we've been stuck on
        var (directionToUnknown, targetPos, distance) = FindDirectionToUnknownTile(playerX, playerY, directionsToCheck, _lastAttemptedDirection, _sameDirectionAttempts >= MAX_SAME_DIRECTION_ATTEMPTS);
        if (directionToUnknown != null)
        {
            _targetTilePosition = targetPos;
            _targetTileDistance = distance;
            return TrackAndReturnMove(directionToUnknown.Value);
        }

        // Priority 3: No reachable unknown tiles - move to a walkable tile to explore new areas
        // This happens when unknown tiles exist but are blocked by non-walkable tiles
        _targetTilePosition = null;
        _targetTileDistance = 0;

        // Filter out the stuck direction from walkable options
        var availableWalkable = walkableAdjacentDirections;
        if (_sameDirectionAttempts >= MAX_SAME_DIRECTION_ATTEMPTS && _lastAttemptedDirection.HasValue)
        {
            availableWalkable = walkableAdjacentDirections.Where(d => d != _lastAttemptedDirection.Value).ToList();
            if (availableWalkable.Count == 0)
            {
                // All directions blocked or stuck, force try a different one
                availableWalkable = walkableAdjacentDirections;
                _logAction("Discovery", $"Stuck on {_lastAttemptedDirection.Value}, but no alternatives - trying anyway", ActionLogType.Info);
            }
            else
            {
                _logAction("Discovery", $"Avoiding stuck direction: {_lastAttemptedDirection.Value}", ActionLogType.Info);
            }
        }

        if (availableWalkable.Count > 0)
        {
            var chosenDirection = availableWalkable[_random.Next(availableWalkable.Count)];
            _logAction("Discovery", $"No reachable unknown tiles, exploring via: {chosenDirection}", ActionLogType.Movement);
            return TrackAndReturnMove(chosenDirection);
        }

        // Priority 4: All adjacent tiles are blocking - try random direction (might discover new walkable)
        var randomDirection = GetRandomDirection();
        // Avoid stuck direction even for random
        if (_sameDirectionAttempts >= MAX_SAME_DIRECTION_ATTEMPTS && _lastAttemptedDirection.HasValue && randomDirection == _lastAttemptedDirection.Value)
        {
            randomDirection = GetRandomDirectionExcept(_lastAttemptedDirection.Value);
        }
        _logAction("Discovery", $"All adjacent tiles blocked, trying random: {randomDirection}", ActionLogType.Movement);
        return TrackAndReturnMove(randomDirection);
    }

    /// <summary>
    /// Tracks the attempted direction for stuck detection and returns the move action.
    /// </summary>
    private AIAction TrackAndReturnMove(GameButton direction)
    {
        if (_lastAttemptedDirection == direction)
        {
            _sameDirectionAttempts++;
            if (_sameDirectionAttempts >= MAX_SAME_DIRECTION_ATTEMPTS)
            {
                _logAction("Discovery", $"Stuck! Tried {direction} {_sameDirectionAttempts} times without moving", ActionLogType.Info);

                // Mark the adjacent tile in this direction as blocking since we can't actually walk there
                var (dx, dy) = GetDirectionOffset(direction);
                int targetX = 4 + dx;
                int targetY = 4 + dy;
                if (_currentTileHashes != null &&
                    targetX >= 0 && targetX < ScreenCapture.TILES_X &&
                    targetY >= 0 && targetY < ScreenCapture.TILES_Y)
                {
                    long tileHash = _currentTileHashes[targetX, targetY];
                    if (tileHash != 0)
                    {
                        // Record multiple failures to override the false "walkable" data
                        for (int i = 0; i < 5; i++)
                        {
                            _tileDatabase.RecordWalkAttempt(tileHash, false);
                        }
                        _logAction("Discovery", $"Marked tile {tileHash} as blocking due to stuck detection", ActionLogType.Info);
                    }
                }
            }
        }
        else
        {
            _lastAttemptedDirection = direction;
            _sameDirectionAttempts = 1;
        }

        return new AIAction { Type = AIActionType.Move, Direction = direction };
    }

    /// <summary>
    /// Gets a random direction excluding the specified one.
    /// </summary>
    private GameButton GetRandomDirectionExcept(GameButton exclude)
    {
        var directions = new[] { GameButton.Up, GameButton.Down, GameButton.Left, GameButton.Right }
            .Where(d => d != exclude)
            .ToArray();
        return directions[_random.Next(directions.Length)];
    }

    /// <summary>
    /// Gets the (dx, dy) offset for a direction.
    /// </summary>
    private (int dx, int dy) GetDirectionOffset(GameButton direction)
    {
        return direction switch
        {
            GameButton.Up => (0, -1),
            GameButton.Down => (0, 1),
            GameButton.Left => (-1, 0),
            GameButton.Right => (1, 0),
            _ => (0, 0)
        };
    }

    /// <summary>
    /// Searches progressively further away (2, 3, 4... spaces) to find unknown tiles.
    /// Returns the direction to move, target tile position, and distance, or null if none found.
    /// Only considers paths where all intermediate tiles are walkable.
    /// </summary>
    private (GameButton? direction, (int x, int y)? targetPos, int distance) FindDirectionToUnknownTile(
        int playerX, int playerY,
        (GameButton direction, int dx, int dy)[] directions,
        GameButton? stuckDirection = null,
        bool avoidStuckDirection = false)
    {
        if (_currentTileHashes == null)
            return (null, null, 0);

        // Maximum distance to search (limited by screen size)
        int maxDistance = Math.Max(ScreenCapture.TILES_X, ScreenCapture.TILES_Y);

        // Search at increasing distances
        for (int distance = 2; distance <= maxDistance; distance++)
        {
            var candidates = new List<(GameButton direction, int targetX, int targetY)>();

            foreach (var (direction, dx, dy) in directions)
            {
                // Skip the stuck direction if we've been stuck
                if (avoidStuckDirection && stuckDirection.HasValue && direction == stuckDirection.Value)
                {
                    continue;
                }
                // Check if there's a walkable path to reach this distance
                bool pathWalkable = true;
                for (int step = 1; step < distance; step++)
                {
                    int checkX = playerX + dx * step;
                    int checkY = playerY + dy * step;

                    if (checkX < 0 || checkX >= ScreenCapture.TILES_X ||
                        checkY < 0 || checkY >= ScreenCapture.TILES_Y)
                    {
                        pathWalkable = false;
                        break;
                    }

                    long pathHash = _currentTileHashes[checkX, checkY];
                    if (pathHash == 0)
                    {
                        pathWalkable = false;
                        break;
                    }

                    var pathTile = _tileDatabase.GetTile(pathHash);
                    bool? pathWalkability = GetTileWalkability(pathTile);

                    // Path tile must be known walkable
                    if (pathWalkability != true)
                    {
                        pathWalkable = false;
                        break;
                    }
                }

                if (!pathWalkable)
                    continue;

                // Check the target tile at this distance
                int targetX = playerX + dx * distance;
                int targetY = playerY + dy * distance;

                if (targetX < 0 || targetX >= ScreenCapture.TILES_X ||
                    targetY < 0 || targetY >= ScreenCapture.TILES_Y)
                    continue;

                long targetHash = _currentTileHashes[targetX, targetY];
                if (targetHash == 0)
                    continue;

                var targetTile = _tileDatabase.GetTile(targetHash);
                bool? targetWalkability = GetTileWalkability(targetTile);

                // Found an unknown tile at this distance with a walkable path!
                if (targetWalkability == null)
                {
                    candidates.Add((direction, targetX, targetY));
                    _logAction("Discovery", $"Found unknown tile {distance} spaces {direction} (hash: {targetHash})", ActionLogType.Info);
                }
            }

            // If we found unknown tiles at this distance, pick one
            if (candidates.Count > 0)
            {
                var chosen = candidates[_random.Next(candidates.Count)];
                _logAction("Discovery", $"Moving {chosen.direction} toward unknown tile {distance} spaces away", ActionLogType.Movement);
                return (chosen.direction, (chosen.targetX, chosen.targetY), distance);
            }
        }

        return (null, null, 0);
    }

    /// <summary>
    /// Gets the walkability status of a tile.
    /// Returns: null = unknown (no walk attempts), true = walkable, false = blocking.
    /// </summary>
    private bool? GetTileWalkability(TileData? tile)
    {
        if (tile == null)
            return null;

        int totalAttempts = tile.WalkSuccessCount + tile.WalkFailCount;

        // No walk attempts yet - unknown
        if (totalAttempts == 0)
            return null;

        // Has walk data - determine walkability
        // Consider walkable if success rate > 50%
        return tile.WalkabilityConfidence > 0.5f;
    }

    private void UpdateStatusDisplay(Bitmap screenshot, AIAction nextAction)
    {
        var sb = new StringBuilder();

        if (WalkableDiscoveryMode)
            sb.AppendLine("=== WALKABLE DISCOVERY MODE ===");
        else
            sb.AppendLine("=== SMART AI STATUS ===");
        sb.AppendLine();

        // Statistics
        sb.AppendLine($"Moves: {_moveCount}");
        sb.AppendLine($"Times stuck: {_stuckCount}");
        sb.AppendLine($"Exits found: {_exitFoundCount}");
        sb.AppendLine();

        // Tile database stats
        var tileStats = _tileDatabase.GetStats();
        sb.AppendLine("=== Tile Database ===");
        sb.AppendLine($"Unique tiles: {tileStats.TotalTiles}");
        sb.AppendLine($"Walkable: {tileStats.WalkableTiles}");
        sb.AppendLine($"Blocking: {tileStats.BlockingTiles}");
        sb.AppendLine($"High confidence: {tileStats.HighConfidenceTiles}");
        sb.AppendLine($"Walk tests: {_walkabilityLearned}");
        sb.AppendLine();

        // Show adjacent tile walkability status in discovery mode
        if (WalkableDiscoveryMode && _currentTileHashes != null)
        {
            sb.AppendLine("=== Adjacent Tiles ===");
            const int playerX = 4;
            const int playerY = 4;

            var directions = new (string name, int dx, int dy)[]
            {
                ("Up", 0, -1), ("Down", 0, 1), ("Left", -1, 0), ("Right", 1, 0)
            };

            foreach (var (name, dx, dy) in directions)
            {
                int tx = playerX + dx;
                int ty = playerY + dy;
                if (tx >= 0 && tx < ScreenCapture.TILES_X && ty >= 0 && ty < ScreenCapture.TILES_Y)
                {
                    long hash = _currentTileHashes[tx, ty];
                    var tile = hash != 0 ? _tileDatabase.GetTile(hash) : null;
                    bool? walkability = GetTileWalkability(tile);
                    string status = walkability == null ? "?" : (walkability.Value ? "✓" : "✗");
                    sb.AppendLine($"  {name}: {status}");
                }
            }
            sb.AppendLine();
        }

        // Room analysis
        if (_lastAnalysis != null)
        {
            sb.AppendLine(_lastAnalysis.AnalysisSummary);
            sb.AppendLine();
        }

        // Exploration strategy
        sb.AppendLine("=== Strategy ===");
        sb.AppendLine(_explorationStrategy.StatusMessage);
        sb.AppendLine();

        // Next action
        sb.AppendLine("=== Next Action ===");
        switch (nextAction.Type)
        {
            case AIActionType.Move:
                sb.AppendLine($"MOVE: {nextAction.Direction}");
                break;
            case AIActionType.PressA:
                sb.AppendLine("INTERACT: Press A");
                break;
            case AIActionType.PressB:
                sb.AppendLine("CANCEL: Press B");
                break;
            case AIActionType.PressStart:
                sb.AppendLine("MENU: Press Start");
                break;
        }

        UpdateStatus(sb.ToString());
    }

    private void UpdateStatus(string status)
    {
        StatusUpdated?.Invoke(status);
    }

    private GameButton GetRandomDirection()
    {
        return _random.Next(4) switch
        {
            0 => GameButton.Up,
            1 => GameButton.Down,
            2 => GameButton.Left,
            _ => GameButton.Right
        };
    }

    /// <summary>
    /// Gets the tile hash of the tile in the specified direction from the player.
    /// Player is at center position (4, 4) in the 10x9 tile grid.
    /// </summary>
    private long GetTargetTileHash(GameButton direction)
    {
        if (_currentTileHashes == null)
            return 0;

        // Player position in tile coordinates (center of screen)
        const int playerX = 4;
        const int playerY = 4;

        int targetX = playerX;
        int targetY = playerY;

        switch (direction)
        {
            case GameButton.Up:
                targetY = playerY - 1;
                break;
            case GameButton.Down:
                targetY = playerY + 1;
                break;
            case GameButton.Left:
                targetX = playerX - 1;
                break;
            case GameButton.Right:
                targetX = playerX + 1;
                break;
        }

        // Check bounds
        if (targetX < 0 || targetX >= ScreenCapture.TILES_X ||
            targetY < 0 || targetY >= ScreenCapture.TILES_Y)
            return 0;

        return _currentTileHashes[targetX, targetY];
    }

    /// <summary>
    /// Calculates a hash of the screen for movement detection.
    /// </summary>
    private long CalculateScreenHash(Bitmap screenshot)
    {
        long hash = 0;
        int sampleStep = 10;

        for (int y = 0; y < screenshot.Height; y += sampleStep)
        {
            for (int x = 0; x < screenshot.Width; x += sampleStep)
            {
                var pixel = screenshot.GetPixel(x, y);
                hash = hash * 31 + pixel.R + pixel.G * 256 + pixel.B * 65536;
            }
        }

        return hash;
    }

    /// <summary>
    /// Learns walkability by comparing screens before and after a move attempt.
    /// </summary>
    private void LearnWalkability(Bitmap screenshot)
    {
        if (!_lastMoveDirection.HasValue || _lastTargetTileHash == 0)
            return;

        long currentScreenHash = CalculateScreenHash(screenshot);
        bool screenChanged = currentScreenHash != _lastScreenHash;

        // If screen changed, movement was successful (tile is walkable)
        // If screen didn't change, movement was blocked (tile is blocking)
        _tileDatabase.RecordWalkAttempt(_lastTargetTileHash, screenChanged);
        _walkabilityLearned++;

        var tile = _tileDatabase.GetTile(_lastTargetTileHash);
        if (tile != null)
        {
            string result = screenChanged ? "WALKABLE" : "BLOCKED";
            _logAction("Walkability", $"{_lastMoveDirection.Value} -> {result} (hash: {_lastTargetTileHash}, success: {tile.WalkSuccessCount}, fail: {tile.WalkFailCount})",
                screenChanged ? ActionLogType.Movement : ActionLogType.Info);
        }

        // Clear for next attempt
        _lastMoveDirection = null;
        _lastTargetTileHash = 0;
        _lastScreenHash = currentScreenHash;
    }

    private async Task ExecuteAction(AIAction action, CancellationToken cancellationToken)
    {
        switch (action.Type)
        {
            case AIActionType.Move:
                _logAction("AI Move", action.Direction.ToString(), ActionLogType.Movement);
                _inputSender.SendButton(action.Direction, KeyPressDurationMs);
                break;

            case AIActionType.PressA:
                _logAction("AI Input", "A (Interact)", ActionLogType.Input);
                _inputSender.SendButton(GameButton.A, KeyPressDurationMs);
                await Task.Delay(200, cancellationToken);
                break;

            case AIActionType.PressB:
                _logAction("AI Input", "B (Cancel/Speed)", ActionLogType.Input);
                _inputSender.SendButton(GameButton.B, KeyPressDurationMs);
                break;

            case AIActionType.PressStart:
                _logAction("AI Input", "Start (Menu)", ActionLogType.Input);
                _inputSender.SendButton(GameButton.Start, KeyPressDurationMs);
                await Task.Delay(500, cancellationToken);
                _inputSender.SendButton(GameButton.B, KeyPressDurationMs);
                break;
        }
    }

    private class AIAction
    {
        public AIActionType Type { get; set; }
        public GameButton Direction { get; set; }
    }

    private enum AIActionType
    {
        Move,
        PressA,
        PressB,
        PressStart
    }
}
