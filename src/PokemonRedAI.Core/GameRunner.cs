using PokemonRedAI.Core.Input;
using PokemonRedAI.Core.Learning;
using PokemonRedAI.Core.Persistence;
using PokemonRedAI.Core.ScreenReader;
using PokemonRedAI.Core.State;

namespace PokemonRedAI.Core;

public enum AIState
{
    Idle,
    Initializing,
    Exploring,
    InBattle,
    InMenu,
    ProcessingDialogue,
    WaitingForAnimation,
    Paused,
    Error
}

public class GameRunner : IDisposable
{
    private readonly IScreenCapture _screenCapture;
    private readonly IInputController _inputController;
    private readonly DataManager _dataManager;
    private readonly StateDetector _stateDetector;
    private readonly TileReader _tileReader;
    private readonly UIIndicatorDetector _uiDetector;
    private readonly WalkabilityMap _walkabilityMap;
    private readonly TileClassifier _tileClassifier;

    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _gameLoopTask;
    private AIState _currentAIState = AIState.Idle;
    private GameState _currentGameState = new();
    private int _playerX;
    private int _playerY;
    private DateTime _startTime;
    private bool _disposed;

    public int TickDelayMs { get; set; } = 100;
    public bool IsRunning => _gameLoopTask != null && !_gameLoopTask.IsCompleted;
    public AIState CurrentAIState => _currentAIState;
    public GameState CurrentGameState => _currentGameState;
    public int PlayerX => _playerX;
    public int PlayerY => _playerY;

    public event EventHandler<GameState>? GameStateChanged;
    public event EventHandler<AIState>? AIStateChanged;
    public event EventHandler<(int x, int y)>? PositionChanged;
    public event EventHandler<string>? ActionPerformed;
    public event EventHandler<Exception>? ErrorOccurred;
    public event EventHandler<byte[]>? ScreenCaptured;

    public GameRunner(
        IScreenCapture screenCapture,
        IInputController inputController,
        DataManager dataManager)
    {
        _screenCapture = screenCapture;
        _inputController = inputController;
        _dataManager = dataManager;

        _stateDetector = new StateDetector();
        _tileReader = new TileReader();
        _uiDetector = new UIIndicatorDetector();
        _walkabilityMap = new WalkabilityMap(_dataManager);
        _tileClassifier = new TileClassifier(_walkabilityMap, _tileReader);
    }

    public async Task StartAsync()
    {
        if (IsRunning)
            return;

        _cancellationTokenSource = new CancellationTokenSource();
        _startTime = DateTime.UtcNow;

        SetAIState(AIState.Initializing);

        // Load saved data
        await _dataManager.LoadAsync();

        // Restore position from save
        _playerX = _dataManager.CurrentData.GameProgress.LastPositionX;
        _playerY = _dataManager.CurrentData.GameProgress.LastPositionY;
        _walkabilityMap.CurrentMapId = _dataManager.CurrentData.GameProgress.LastMapId;

        if (string.IsNullOrEmpty(_walkabilityMap.CurrentMapId))
        {
            _walkabilityMap.CurrentMapId = "unknown";
        }

        _tileClassifier.SetPosition(_playerX, _playerY);

        SetAIState(AIState.Exploring);

        _gameLoopTask = Task.Run(() => GameLoopAsync(_cancellationTokenSource.Token));
    }

    public async Task StopAsync()
    {
        if (!IsRunning)
            return;

        _cancellationTokenSource?.Cancel();

        if (_gameLoopTask != null)
        {
            try
            {
                await _gameLoopTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }

        // Save data on stop
        _dataManager.AddPlayTime(DateTime.UtcNow - _startTime);
        await _dataManager.SaveAsync();

        SetAIState(AIState.Idle);
    }

    public void Pause()
    {
        if (_currentAIState != AIState.Paused && IsRunning)
        {
            SetAIState(AIState.Paused);
        }
    }

    public void Resume()
    {
        if (_currentAIState == AIState.Paused)
        {
            SetAIState(AIState.Exploring);
        }
    }

    private async Task GameLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (_currentAIState == AIState.Paused)
                {
                    await Task.Delay(100, cancellationToken);
                    continue;
                }

                // 1. Capture screen
                var screen = _screenCapture.CaptureFrameAsPixels();
                if (screen == null)
                {
                    await Task.Delay(TickDelayMs, cancellationToken);
                    continue;
                }

                var rawFrame = _screenCapture.CaptureFrame();
                if (rawFrame != null)
                {
                    ScreenCaptured?.Invoke(this, rawFrame);
                }

                // 2. Detect game state
                var newGameState = _stateDetector.DetectState(screen);
                if (newGameState.Type != _currentGameState.Type)
                {
                    _currentGameState = newGameState;
                    GameStateChanged?.Invoke(this, _currentGameState);
                }

                // 3. Detect UI indicators
                var uiIndicators = _uiDetector.Detect(screen);

                // 4. Parse tiles
                var tiles = _tileReader.ParseTiles(screen);

                // 5. Process based on state
                await ProcessGameStateAsync(newGameState, uiIndicators, tiles, cancellationToken);

                // 6. Update walkability from visible tiles
                _tileClassifier.PreClassifyVisibleTiles(tiles);

                // 7. Wait for next tick
                await Task.Delay(TickDelayMs, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
                SetAIState(AIState.Error);
                await Task.Delay(1000, cancellationToken);
                SetAIState(AIState.Exploring);
            }
        }
    }

    private async Task ProcessGameStateAsync(
        GameState gameState,
        UIIndicatorResult uiIndicators,
        Tile[,] tiles,
        CancellationToken cancellationToken)
    {
        switch (gameState.Type)
        {
            case GameStateType.Overworld:
                SetAIState(AIState.Exploring);
                await ProcessOverworldAsync(tiles, cancellationToken);
                break;

            case GameStateType.Battle:
                SetAIState(AIState.InBattle);
                await ProcessBattleAsync(gameState, uiIndicators, cancellationToken);
                break;

            case GameStateType.Menu:
                SetAIState(AIState.InMenu);
                await ProcessMenuAsync(gameState, uiIndicators, cancellationToken);
                break;

            case GameStateType.Dialogue:
                SetAIState(AIState.ProcessingDialogue);
                await ProcessDialogueAsync(uiIndicators, cancellationToken);
                break;

            case GameStateType.BlackScreen:
                SetAIState(AIState.WaitingForAnimation);
                // Wait for screen transition
                break;
        }
    }

    private async Task ProcessOverworldAsync(Tile[,] tiles, CancellationToken cancellationToken)
    {
        // Get unexplored directions
        var unexplored = _walkabilityMap.GetUnexploredDirections(_playerX, _playerY);

        Direction targetDirection;

        if (unexplored.Count > 0)
        {
            // Prioritize unexplored tiles
            targetDirection = unexplored[Random.Shared.Next(unexplored.Count)];
        }
        else
        {
            // Choose random walkable direction
            var walkable = _walkabilityMap.GetWalkableDirections(_playerX, _playerY);
            if (walkable.Count > 0)
            {
                targetDirection = walkable[Random.Shared.Next(walkable.Count)];
            }
            else
            {
                // Try any direction
                var allDirections = Enum.GetValues<Direction>();
                targetDirection = allDirections[Random.Shared.Next(allDirections.Length)];
            }
        }

        // Attempt movement
        await _inputController.PressDirectionAsync(targetDirection);
        ActionPerformed?.Invoke(this, $"Moving {targetDirection}");

        // Wait for movement
        await Task.Delay(300, cancellationToken);

        // Capture new screen to check if moved
        var newScreen = _screenCapture.CaptureFrameAsPixels();
        if (newScreen != null)
        {
            var newTiles = _tileReader.ParseTiles(newScreen);
            var result = _tileClassifier.ProcessMovementAttempt(targetDirection, newTiles);

            if (result.Success)
            {
                _playerX = result.EndX;
                _playerY = result.EndY;
                _dataManager.IncrementSteps();
                _dataManager.UpdatePosition(_walkabilityMap.CurrentMapId, _playerX, _playerY);
                PositionChanged?.Invoke(this, (_playerX, _playerY));
                ActionPerformed?.Invoke(this, $"Moved to ({_playerX}, {_playerY})");
            }
            else
            {
                ActionPerformed?.Invoke(this, $"Blocked at ({result.TargetX}, {result.TargetY})");
            }
        }
    }

    private async Task ProcessBattleAsync(
        GameState gameState,
        UIIndicatorResult uiIndicators,
        CancellationToken cancellationToken)
    {
        // Simple battle handling: spam A to proceed through battle
        if (uiIndicators.HasContinuePrompt)
        {
            await _inputController.PressConfirmAsync();
            ActionPerformed?.Invoke(this, "Battle: Pressing A to continue");
        }
        else if (uiIndicators.HasSelection)
        {
            // Select first option (usually Fight or Run)
            await _inputController.PressConfirmAsync();
            ActionPerformed?.Invoke(this, "Battle: Selecting option");
        }
        else
        {
            // Wait for battle animation
            await Task.Delay(100, cancellationToken);
        }
    }

    private async Task ProcessMenuAsync(
        GameState gameState,
        UIIndicatorResult uiIndicators,
        CancellationToken cancellationToken)
    {
        // Close menu by pressing B
        await _inputController.PressCancelAsync();
        ActionPerformed?.Invoke(this, "Closing menu");
    }

    private async Task ProcessDialogueAsync(
        UIIndicatorResult uiIndicators,
        CancellationToken cancellationToken)
    {
        if (uiIndicators.HasContinuePrompt)
        {
            await _inputController.PressConfirmAsync();
            ActionPerformed?.Invoke(this, "Advancing dialogue");
        }
        else if (uiIndicators.HasYesNoPrompt)
        {
            // Default to No (press B)
            await _inputController.PressCancelAsync();
            ActionPerformed?.Invoke(this, "Declining prompt");
        }
        else
        {
            // Wait for text to finish
            await Task.Delay(50, cancellationToken);
        }
    }

    private void SetAIState(AIState newState)
    {
        if (_currentAIState != newState)
        {
            _currentAIState = newState;
            AIStateChanged?.Invoke(this, newState);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        StopAsync().GetAwaiter().GetResult();
        _cancellationTokenSource?.Dispose();
    }
}
