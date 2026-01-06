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

    // AI Settings
    public int MovementDelayMs { get; set; } = 300;
    public int KeyPressDurationMs { get; set; } = 50;

    // State tracking
    private int _moveCount = 0;
    private int _exitFoundCount = 0;
    private int _stuckCount = 0;

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
        _logAction("AI Controller", "Stopped", ActionLogType.Info);
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

                    // Track statistics
                    if (_lastAnalysis.DetectedExits.Count > 0)
                        _exitFoundCount++;
                    if (!_lastAnalysis.ScreenChanged && _moveCount > 0)
                        _stuckCount++;

                    // Decide and execute action
                    var action = DecideSmartAction(screenshot);

                    // Update UI with analysis results
                    UpdateStatusDisplay(screenshot, action);

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
            if (screenshot != null)
            {
                // Send a COPY to UI to avoid "object is currently in use" errors
                // The UI and AI loop would otherwise fight over the same bitmap
                var uiCopy = new Bitmap(screenshot);
                ScreenCaptured?.Invoke(uiCopy);
                return screenshot;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Screen capture error: {ex.Message}");
        }

        return null;
    }

    private AIAction DecideSmartAction(Bitmap screenshot)
    {
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

    private void UpdateStatusDisplay(Bitmap screenshot, AIAction nextAction)
    {
        var sb = new StringBuilder();

        sb.AppendLine("=== SMART AI STATUS ===");
        sb.AppendLine();

        // Statistics
        sb.AppendLine($"Moves: {_moveCount}");
        sb.AppendLine($"Times stuck: {_stuckCount}");
        sb.AppendLine($"Exits found: {_exitFoundCount}");
        sb.AppendLine();

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
