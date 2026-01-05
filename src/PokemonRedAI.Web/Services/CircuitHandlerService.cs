using Microsoft.AspNetCore.Components.Server.Circuits;
using Serilog;

namespace PokemonRedAI.Web.Services;

public class CircuitHandlerService : CircuitHandler
{
    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        Log.Information("Circuit opened: {CircuitId}", circuit.Id);
        return Task.CompletedTask;
    }

    public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        Log.Information("Circuit closed: {CircuitId}", circuit.Id);
        return Task.CompletedTask;
    }

    public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        Log.Information("Connection up: {CircuitId}", circuit.Id);
        return Task.CompletedTask;
    }

    public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        Log.Warning("Connection down: {CircuitId}", circuit.Id);
        return Task.CompletedTask;
    }
}
