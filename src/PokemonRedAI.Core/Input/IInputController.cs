using PokemonRedAI.Core.ScreenReader;

namespace PokemonRedAI.Core.Input;

public interface IInputController : IDisposable
{
    int KeyPressDurationMs { get; set; }
    int DelayBetweenInputsMs { get; set; }

    Task PressKeyAsync(GameButton button);
    Task PressDirectionAsync(Direction direction);
    Task PressConfirmAsync();
    Task PressCancelAsync();

    Task MoveAsync(Direction direction);
    Task<bool> TryMoveAsync(Direction direction, Func<bool> positionChangedCheck);
}

public enum GameButton
{
    A,      // Confirm (X key)
    B,      // Cancel (Z key)
    Start,
    Select,
    Up,
    Down,
    Left,
    Right
}
