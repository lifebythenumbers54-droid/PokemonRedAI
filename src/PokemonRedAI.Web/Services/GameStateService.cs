using Microsoft.AspNetCore.SignalR;
using PokemonRedAI.Core.State;
using PokemonRedAI.Web.Hubs;

namespace PokemonRedAI.Web.Services;

public class GameStateService
{
    private readonly IHubContext<GameHub> _hubContext;
    private readonly List<ActionLogEntry> _actionLog = new();
    private readonly object _logLock = new();
    private const int MaxLogEntries = 100;

    private GameStateDto _currentState = new();
    private bool _isRunning;
    private int _playerX;
    private int _playerY;
    private string _mapId = "unknown";

    public GameStateDto CurrentState => _currentState;
    public bool IsRunning => _isRunning;
    public int PlayerX => _playerX;
    public int PlayerY => _playerY;
    public string MapId => _mapId;
    public IReadOnlyList<ActionLogEntry> ActionLog
    {
        get
        {
            lock (_logLock)
            {
                return _actionLog.ToList();
            }
        }
    }

    public event EventHandler<GameStateDto>? StateChanged;
    public event EventHandler<ActionLogEntry>? ActionLogged;
    public event EventHandler<bool>? RunningStateChanged;

    public GameStateService(IHubContext<GameHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task UpdateGameStateAsync(GameState gameState)
    {
        _currentState = new GameStateDto
        {
            StateType = gameState.Type.ToString(),
            BattlePhase = gameState.BattlePhase != BattlePhase.None ? gameState.BattlePhase.ToString() : null,
            MenuType = gameState.MenuType != MenuType.None ? gameState.MenuType.ToString() : null,
            IsWalking = gameState.IsWalking,
            HasContinueArrow = gameState.HasContinueArrow,
            HasSelectionArrow = gameState.HasSelectionArrow,
            PlayerX = _playerX,
            PlayerY = _playerY,
            MapId = _mapId,
            Timestamp = DateTime.UtcNow
        };

        StateChanged?.Invoke(this, _currentState);
        await _hubContext.Clients.All.SendAsync("ReceiveGameState", _currentState);
    }

    public async Task UpdatePositionAsync(int x, int y, string? mapId = null)
    {
        _playerX = x;
        _playerY = y;
        if (mapId != null)
            _mapId = mapId;

        _currentState.PlayerX = x;
        _currentState.PlayerY = y;
        _currentState.MapId = _mapId;

        await _hubContext.Clients.All.SendAsync("ReceiveGameState", _currentState);
    }

    public async Task LogActionAsync(string action, string details = "", ActionLogType type = ActionLogType.Info)
    {
        var entry = new ActionLogEntry
        {
            Timestamp = DateTime.UtcNow,
            Action = action,
            Details = details,
            Type = type
        };

        lock (_logLock)
        {
            _actionLog.Add(entry);
            if (_actionLog.Count > MaxLogEntries)
            {
                _actionLog.RemoveAt(0);
            }
        }

        ActionLogged?.Invoke(this, entry);
        await _hubContext.Clients.All.SendAsync("ReceiveActionLog", entry);
    }

    public async Task SendScreenUpdateAsync(byte[] screenData)
    {
        await _hubContext.Clients.All.SendAsync("ReceiveScreenUpdate", Convert.ToBase64String(screenData));
    }

    public async Task SendWalkabilityUpdateAsync(int x, int y, string state, string mapId)
    {
        var tile = new WalkabilityTileDto
        {
            X = x,
            Y = y,
            State = state,
            MapId = mapId
        };

        await _hubContext.Clients.All.SendAsync("ReceiveWalkabilityUpdate", tile);
    }

    public void SetRunning(bool running)
    {
        _isRunning = running;
        RunningStateChanged?.Invoke(this, running);
    }

    public void ClearLog()
    {
        lock (_logLock)
        {
            _actionLog.Clear();
        }
    }
}
