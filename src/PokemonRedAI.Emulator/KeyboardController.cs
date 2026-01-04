using System.Runtime.InteropServices;
using PokemonRedAI.Core.Input;
using PokemonRedAI.Core.ScreenReader;

namespace PokemonRedAI.Emulator;

public class KeyboardController : IInputController
{
    private readonly Dictionary<GameButton, VirtualKey> _keyMappings;
    private bool _disposed;

    public int KeyPressDurationMs { get; set; } = 50;
    public int DelayBetweenInputsMs { get; set; } = 100;

    public event EventHandler<GameButton>? ButtonPressed;
    public event EventHandler<Direction>? DirectionPressed;

    public KeyboardController()
    {
        _keyMappings = new Dictionary<GameButton, VirtualKey>
        {
            { GameButton.A, VirtualKey.X },
            { GameButton.B, VirtualKey.Z },
            { GameButton.Start, VirtualKey.Return },
            { GameButton.Select, VirtualKey.Back },
            { GameButton.Up, VirtualKey.Up },
            { GameButton.Down, VirtualKey.Down },
            { GameButton.Left, VirtualKey.Left },
            { GameButton.Right, VirtualKey.Right }
        };
    }

    public void SetKeyMapping(GameButton button, VirtualKey key)
    {
        _keyMappings[button] = key;
    }

    public async Task PressKeyAsync(GameButton button)
    {
        if (!_keyMappings.TryGetValue(button, out var key))
            return;

        SendKeyDown(key);
        await Task.Delay(KeyPressDurationMs);
        SendKeyUp(key);
        await Task.Delay(DelayBetweenInputsMs);

        ButtonPressed?.Invoke(this, button);
    }

    public async Task PressDirectionAsync(Direction direction)
    {
        var button = direction switch
        {
            Direction.Up => GameButton.Up,
            Direction.Down => GameButton.Down,
            Direction.Left => GameButton.Left,
            Direction.Right => GameButton.Right,
            _ => throw new ArgumentOutOfRangeException(nameof(direction))
        };

        await PressKeyAsync(button);
        DirectionPressed?.Invoke(this, direction);
    }

    public async Task PressConfirmAsync()
    {
        await PressKeyAsync(GameButton.A);
    }

    public async Task PressCancelAsync()
    {
        await PressKeyAsync(GameButton.B);
    }

    public async Task MoveAsync(Direction direction)
    {
        await PressDirectionAsync(direction);
        // Wait for movement animation to complete (approximately 250ms in Pokemon Red)
        await Task.Delay(250);
    }

    public async Task<bool> TryMoveAsync(Direction direction, Func<bool> positionChangedCheck)
    {
        await PressDirectionAsync(direction);

        // Wait for movement to potentially complete
        await Task.Delay(300);

        return positionChangedCheck();
    }

    private void SendKeyDown(VirtualKey key)
    {
        var input = new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = (ushort)key,
                    wScan = 0,
                    dwFlags = 0,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };

        SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }

    private void SendKeyUp(VirtualKey key)
    {
        var input = new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = (ushort)key,
                    wScan = 0,
                    dwFlags = KEYEVENTF_KEYUP,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };

        SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }

    #region Win32 Imports

    private const int INPUT_KEYBOARD = 1;
    private const int KEYEVENTF_KEYUP = 0x0002;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public int type;
        public InputUnion u;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    #endregion
}

public enum VirtualKey : ushort
{
    Back = 0x08,
    Return = 0x0D,
    Shift = 0x10,
    Control = 0x11,
    Escape = 0x1B,
    Space = 0x20,
    Left = 0x25,
    Up = 0x26,
    Right = 0x27,
    Down = 0x28,
    A = 0x41,
    B = 0x42,
    X = 0x58,
    Z = 0x5A
}
