using Microsoft.AspNetCore.SignalR;
using PokemonRedAI.Core.State;

namespace PokemonRedAI.Web.Hubs;

public class GameHub : Hub
{
    public async Task SendGameState(GameStateDto state)
    {
        await Clients.All.SendAsync("ReceiveGameState", state);
    }

    public async Task SendActionLog(ActionLogEntry entry)
    {
        await Clients.All.SendAsync("ReceiveActionLog", entry);
    }

    public async Task SendScreenUpdate(byte[] screenData)
    {
        await Clients.All.SendAsync("ReceiveScreenUpdate", screenData);
    }

    public async Task SendWalkabilityUpdate(WalkabilityTileDto tile)
    {
        await Clients.All.SendAsync("ReceiveWalkabilityUpdate", tile);
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        await Clients.Caller.SendAsync("Connected", Context.ConnectionId);
    }
}

public class GameStateDto
{
    public string StateType { get; set; } = "Unknown";
    public string? BattlePhase { get; set; }
    public string? MenuType { get; set; }
    public bool IsWalking { get; set; }
    public bool HasContinueArrow { get; set; }
    public bool HasSelectionArrow { get; set; }
    public int PlayerX { get; set; }
    public int PlayerY { get; set; }
    public string MapId { get; set; } = "unknown";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class ActionLogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Action { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public ActionLogType Type { get; set; } = ActionLogType.Info;
}

public enum ActionLogType
{
    Info,
    Movement,
    Input,
    StateChange,
    Learning,
    Error
}

public class WalkabilityTileDto
{
    public int X { get; set; }
    public int Y { get; set; }
    public string State { get; set; } = "Unknown";
    public string MapId { get; set; } = string.Empty;
}
