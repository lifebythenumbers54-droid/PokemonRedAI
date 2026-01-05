using System.Drawing;

namespace PokemonRedAI.WinForms;

/// <summary>
/// Smart exploration strategy that tracks visited locations and navigates toward exits
/// </summary>
public class ExplorationStrategy
{
    // Track which directions have been tried from each "location"
    // Location is identified by a hash of the current screen
    private readonly Dictionary<long, HashSet<GameButton>> _triedDirections = new();

    // Track recent movement history for backtracking
    private readonly Stack<GameButton> _movementHistory = new();

    // Track if we're stuck (same screen after movement)
    private int _stuckCounter = 0;
    private const int MAX_STUCK_COUNT = 2; // Reduced - get unstuck faster

    // Track last few screen hashes to detect loops
    private readonly Queue<long> _recentScreens = new();
    private const int SCREEN_HISTORY_SIZE = 10;

    private readonly Random _random = new();
    private GameButton? _lastDirection;
    private long _currentScreenHash;

    public string StatusMessage { get; private set; } = "";

    /// <summary>
    /// Decides the next move based on room analysis and exploration history
    /// </summary>
    public GameButton DecideNextMove(RoomAnalyzer.AnalysisResult analysis, Bitmap screenshot)
    {
        var lines = new List<string>();

        // Calculate screen hash for location tracking
        long newScreenHash = CalculateScreenHash(screenshot);
        bool screenChanged = newScreenHash != _currentScreenHash;

        lines.Add($"Screen hash: {newScreenHash:X8}");
        lines.Add($"Screen changed: {screenChanged}");

        // Update stuck counter
        if (!screenChanged && _lastDirection.HasValue)
        {
            _stuckCounter++;
            lines.Add($"STUCK! Count: {_stuckCounter} - {_lastDirection.Value} blocked");

            // Mark this direction as blocked from current location
            MarkDirectionTried(_currentScreenHash, _lastDirection.Value);

            // Immediately try a different direction if we just got stuck
            if (_stuckCounter == 1)
            {
                var altDirection = GetUntriedDirection(_currentScreenHash);
                if (altDirection.HasValue && altDirection.Value != _lastDirection.Value)
                {
                    _lastDirection = altDirection.Value;
                    lines.Add($"Immediately trying: {altDirection.Value}");
                    StatusMessage = string.Join(Environment.NewLine, lines);
                    return altDirection.Value;
                }
            }
        }
        else if (screenChanged)
        {
            _stuckCounter = 0;

            // Record successful movement for backtracking
            if (_lastDirection.HasValue)
            {
                _movementHistory.Push(_lastDirection.Value);
                if (_movementHistory.Count > 50) // Limit history size
                {
                    // Remove oldest entries
                    var temp = new Stack<GameButton>();
                    for (int i = 0; i < 25; i++)
                        temp.Push(_movementHistory.Pop());
                    _movementHistory.Clear();
                    while (temp.Count > 0)
                        _movementHistory.Push(temp.Pop());
                }
            }

            // Update screen history
            _recentScreens.Enqueue(newScreenHash);
            if (_recentScreens.Count > SCREEN_HISTORY_SIZE)
                _recentScreens.Dequeue();
        }

        _currentScreenHash = newScreenHash;

        // Check for loops (same screen appearing multiple times recently)
        int loopCount = _recentScreens.Count(h => h == newScreenHash);
        if (loopCount >= 3)
        {
            lines.Add("LOOP DETECTED! Trying random direction.");
            var randomDir = GetRandomUntriedDirection(newScreenHash);
            _lastDirection = randomDir;
            lines.Add($"Decision: {randomDir} (random escape)");
            StatusMessage = string.Join(Environment.NewLine, lines);
            return randomDir;
        }

        // If we're very stuck, try all directions systematically then backtrack
        if (_stuckCounter >= MAX_STUCK_COUNT)
        {
            lines.Add("Very stuck! Trying systematic escape.");

            // First try any untried direction
            var escapeDir = GetUntriedDirection(_currentScreenHash);
            if (escapeDir.HasValue)
            {
                _lastDirection = escapeDir.Value;
                lines.Add($"Decision: {escapeDir.Value} (untried escape)");
                StatusMessage = string.Join(Environment.NewLine, lines);
                return escapeDir.Value;
            }

            // All directions tried from here - clear and try backtracking
            lines.Add("All directions blocked! Attempting backtrack.");
            var backtrackDir = GetBacktrackDirection();
            if (backtrackDir.HasValue)
            {
                // Clear tried directions since we're in a new situation
                _triedDirections.Remove(_currentScreenHash);
                _stuckCounter = 0;
                _lastDirection = backtrackDir.Value;
                lines.Add($"Decision: {backtrackDir.Value} (backtrack)");
                StatusMessage = string.Join(Environment.NewLine, lines);
                return backtrackDir.Value;
            }

            // Can't backtrack either - reset and try random
            lines.Add("Can't backtrack - resetting exploration");
            _triedDirections.Clear();
            _stuckCounter = 0;
        }

        // Priority 1: If we see exits, move toward the nearest one
        if (analysis.DetectedExits.Count > 0)
        {
            lines.Add($"Found {analysis.DetectedExits.Count} exits!");

            var exitDirection = GetBestExitDirection(analysis.DetectedExits, newScreenHash);
            if (exitDirection.HasValue)
            {
                _lastDirection = exitDirection.Value;
                lines.Add($"Decision: {exitDirection.Value} (toward exit)");
                StatusMessage = string.Join(Environment.NewLine, lines);
                return exitDirection.Value;
            }
        }

        // Priority 2: Try an untried direction
        var untriedDir = GetUntriedDirection(newScreenHash);
        if (untriedDir.HasValue)
        {
            _lastDirection = untriedDir.Value;
            lines.Add($"Decision: {untriedDir.Value} (unexplored)");
            StatusMessage = string.Join(Environment.NewLine, lines);
            return untriedDir.Value;
        }

        // Priority 3: Random direction (all directions tried)
        lines.Add("All directions tried, going random.");
        var fallbackDir = GetRandomUntriedDirection(newScreenHash);
        _lastDirection = fallbackDir;
        lines.Add($"Decision: {fallbackDir} (fallback)");
        StatusMessage = string.Join(Environment.NewLine, lines);
        return fallbackDir;
    }

    private long CalculateScreenHash(Bitmap screenshot)
    {
        // Quick hash based on sampled pixels
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

    private void MarkDirectionTried(long screenHash, GameButton direction)
    {
        if (!_triedDirections.ContainsKey(screenHash))
            _triedDirections[screenHash] = new HashSet<GameButton>();

        _triedDirections[screenHash].Add(direction);
    }

    private GameButton? GetUntriedDirection(long screenHash)
    {
        var allDirections = new[] { GameButton.Up, GameButton.Down, GameButton.Left, GameButton.Right };

        if (!_triedDirections.ContainsKey(screenHash))
            return allDirections[_random.Next(allDirections.Length)];

        var tried = _triedDirections[screenHash];
        var untried = allDirections.Where(d => !tried.Contains(d)).ToList();

        if (untried.Count == 0)
            return null;

        return untried[_random.Next(untried.Count)];
    }

    private GameButton GetRandomUntriedDirection(long screenHash)
    {
        var allDirections = new[] { GameButton.Up, GameButton.Down, GameButton.Left, GameButton.Right };

        // Clear tried directions for this screen to allow retry
        if (_triedDirections.ContainsKey(screenHash))
            _triedDirections[screenHash].Clear();

        return allDirections[_random.Next(allDirections.Length)];
    }

    private GameButton? GetBacktrackDirection()
    {
        if (_movementHistory.Count == 0)
            return null;

        // Get last successful movement and reverse it
        var lastMove = _movementHistory.Pop();
        return GetOppositeDirection(lastMove);
    }

    private GameButton GetOppositeDirection(GameButton direction)
    {
        return direction switch
        {
            GameButton.Up => GameButton.Down,
            GameButton.Down => GameButton.Up,
            GameButton.Left => GameButton.Right,
            GameButton.Right => GameButton.Left,
            _ => GameButton.Down
        };
    }

    private GameButton? GetBestExitDirection(List<Point> exits, long screenHash)
    {
        // Find exits we haven't tried to reach yet
        var triedDirs = _triedDirections.GetValueOrDefault(screenHash) ?? new HashSet<GameButton>();

        // Player is at center (4, 4)
        var playerPos = new Point(RoomAnalyzer.PLAYER_TILE_X, RoomAnalyzer.PLAYER_TILE_Y);

        // Score each exit based on distance and whether we've tried that direction
        var exitScores = new List<(Point exit, GameButton direction, float score)>();

        foreach (var exit in exits)
        {
            float dist = (float)Math.Sqrt(
                Math.Pow(exit.X - playerPos.X, 2) +
                Math.Pow(exit.Y - playerPos.Y, 2));

            // Determine primary direction to exit
            int dx = exit.X - playerPos.X;
            int dy = exit.Y - playerPos.Y;

            GameButton primaryDir;
            if (Math.Abs(dy) >= Math.Abs(dx))
                primaryDir = dy < 0 ? GameButton.Up : GameButton.Down;
            else
                primaryDir = dx < 0 ? GameButton.Left : GameButton.Right;

            // Score: lower distance is better, untried direction is better
            float score = dist;
            if (triedDirs.Contains(primaryDir))
                score += 10; // Penalty for tried directions

            exitScores.Add((exit, primaryDir, score));
        }

        if (exitScores.Count == 0)
            return null;

        // Pick the best scoring exit
        var best = exitScores.OrderBy(e => e.score).First();
        return best.direction;
    }

    /// <summary>
    /// Call when the AI presses a non-movement button (A, B, Start)
    /// </summary>
    public void OnActionButton(GameButton button)
    {
        // A button might trigger a dialog or interaction
        // Reset stuck counter as the game state may have changed
        if (button == GameButton.A || button == GameButton.B || button == GameButton.Start)
        {
            _stuckCounter = 0;
        }
    }

    /// <summary>
    /// Resets the exploration state
    /// </summary>
    public void Reset()
    {
        _triedDirections.Clear();
        _movementHistory.Clear();
        _recentScreens.Clear();
        _stuckCounter = 0;
        _lastDirection = null;
        _currentScreenHash = 0;
        StatusMessage = "Exploration reset";
    }
}
