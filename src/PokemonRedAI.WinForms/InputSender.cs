using System.Runtime.InteropServices;

namespace PokemonRedAI.WinForms;

public enum GameButton
{
    Up,
    Down,
    Left,
    Right,
    A,
    B,
    Start,
    Select
}

public class InputSender
{
    private readonly IntPtr _windowHandle;

    // Default key mappings (mGBA defaults)
    private readonly Dictionary<GameButton, Keys> _keyMappings = new()
    {
        { GameButton.Up, Keys.Up },
        { GameButton.Down, Keys.Down },
        { GameButton.Left, Keys.Left },
        { GameButton.Right, Keys.Right },
        { GameButton.A, Keys.X },
        { GameButton.B, Keys.Z },
        { GameButton.Start, Keys.Enter },
        { GameButton.Select, Keys.Back }
    };

    public InputSender(IntPtr windowHandle)
    {
        _windowHandle = windowHandle;
    }

    public void SetKeyMapping(GameButton button, Keys key)
    {
        _keyMappings[button] = key;
    }

    public bool FocusWindow()
    {
        if (_windowHandle == IntPtr.Zero)
            return false;

        if (!IsWindow(_windowHandle))
            return false;

        var currentForeground = GetForegroundWindow();
        if (currentForeground == _windowHandle)
            return true;

        // Use AttachThreadInput to allow SetForegroundWindow
        uint foregroundThread = GetWindowThreadProcessId(currentForeground, out _);
        uint currentThread = GetCurrentThreadId();

        if (foregroundThread != currentThread)
        {
            AttachThreadInput(currentThread, foregroundThread, true);
        }

        ShowWindow(_windowHandle, SW_RESTORE);
        SetForegroundWindow(_windowHandle);
        BringWindowToTop(_windowHandle);

        if (foregroundThread != currentThread)
        {
            AttachThreadInput(currentThread, foregroundThread, false);
        }

        Thread.Sleep(100);
        return GetForegroundWindow() == _windowHandle;
    }

    public void SendButton(GameButton button, int durationMs = 50)
    {
        if (!_keyMappings.TryGetValue(button, out var key))
            return;

        // Focus the window first
        if (!FocusWindow())
        {
            throw new InvalidOperationException("Could not focus emulator window");
        }

        Thread.Sleep(100);

        // Try multiple methods to send the key

        // Method 1: PostMessage (works for many apps)
        SendKeyViaPostMessage(key, true);
        Thread.Sleep(durationMs);
        SendKeyViaPostMessage(key, false);

        // Method 2: Also send via SendInput as backup
        SendKeyViaSendInput(key, true);
        Thread.Sleep(durationMs);
        SendKeyViaSendInput(key, false);

        Thread.Sleep(50);
    }

    private void SendKeyViaPostMessage(Keys key, bool keyDown)
    {
        uint vk = (uint)key;
        uint scanCode = MapVirtualKey(vk, MAPVK_VK_TO_VSC);

        // lParam format for WM_KEYDOWN/WM_KEYUP
        // Bits 0-15: repeat count (1)
        // Bits 16-23: scan code
        // Bit 24: extended key flag
        // Bit 29: context code (0 for WM_KEYDOWN)
        // Bit 30: previous key state (0 for down, 1 for up)
        // Bit 31: transition state (0 for down, 1 for up)

        uint lParam;
        if (keyDown)
        {
            lParam = 1 | (scanCode << 16);
        }
        else
        {
            lParam = 1 | (scanCode << 16) | (1u << 30) | (1u << 31);
        }

        // Check if it's an extended key (arrow keys, etc.)
        if (key == Keys.Up || key == Keys.Down || key == Keys.Left || key == Keys.Right ||
            key == Keys.Insert || key == Keys.Delete || key == Keys.Home || key == Keys.End ||
            key == Keys.PageUp || key == Keys.PageDown)
        {
            lParam |= (1u << 24); // Set extended key flag
        }

        uint msg = keyDown ? WM_KEYDOWN : WM_KEYUP;
        PostMessage(_windowHandle, msg, (IntPtr)vk, (IntPtr)lParam);

        System.Diagnostics.Debug.WriteLine($"PostMessage: {key} {(keyDown ? "DOWN" : "UP")} vk={vk} scan={scanCode} lParam={lParam:X8}");
    }

    private void SendKeyViaSendInput(Keys key, bool keyDown)
    {
        var inputs = new INPUT[1];
        inputs[0].type = INPUT_KEYBOARD;
        inputs[0].u.ki.wVk = (ushort)key;
        inputs[0].u.ki.wScan = (ushort)MapVirtualKey((uint)key, MAPVK_VK_TO_VSC);
        inputs[0].u.ki.dwFlags = keyDown ? 0 : KEYEVENTF_KEYUP;

        // For extended keys (arrow keys), set the extended flag
        if (key == Keys.Up || key == Keys.Down || key == Keys.Left || key == Keys.Right)
        {
            inputs[0].u.ki.dwFlags |= KEYEVENTF_EXTENDEDKEY;
        }

        inputs[0].u.ki.time = 0;
        inputs[0].u.ki.dwExtraInfo = IntPtr.Zero;

        var result = SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
        System.Diagnostics.Debug.WriteLine($"SendInput: {key} {(keyDown ? "DOWN" : "UP")} result={result}");
    }

    public string GetDiagnosticInfo()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Target Window Handle: {_windowHandle}");
        sb.AppendLine($"Target Window Valid: {IsWindow(_windowHandle)}");
        sb.AppendLine($"Current Foreground: {GetForegroundWindow()}");
        sb.AppendLine($"Is Target Foreground: {GetForegroundWindow() == _windowHandle}");
        sb.AppendLine();
        sb.AppendLine("Key Mappings:");
        foreach (var mapping in _keyMappings)
        {
            sb.AppendLine($"  {mapping.Key} -> {mapping.Value} (VK: {(int)mapping.Value})");
        }
        return sb.ToString();
    }

    #region Win32 Imports

    private const int INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    private const int SW_RESTORE = 9;
    private const uint WM_KEYDOWN = 0x0100;
    private const uint WM_KEYUP = 0x0101;
    private const uint MAPVK_VK_TO_VSC = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public int type;
        public INPUTUNION u;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct INPUTUNION
    {
        [FieldOffset(0)]
        public KEYBDINPUT ki;
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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    #endregion
}
