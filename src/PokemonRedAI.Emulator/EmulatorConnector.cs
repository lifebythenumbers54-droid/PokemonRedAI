using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace PokemonRedAI.Emulator;

public class EmulatorConnector : IEmulatorConnector
{
    // Window title patterns to look for
    private readonly string[] _supportedEmulators = { "EmuHawk", "mGBA", "VisualBoyAdvance", "VBA", "BizHawk", "NO$GBA", "VisualBoy" };
    private readonly string[] _searchTerms = { "Pokemon", "POKEMON", "Red", "Blue", "Yellow", ".gba", ".gbc", ".gb" };

    // Process names to look for (without .exe)
    private readonly string[] _emulatorProcessNames = { "mgba", "mGBA", "mgba-qt", "EmuHawk", "visualboyadvance", "vba", "VisualBoyAdvance-M", "no$gba" };

    private IntPtr _windowHandle;
    private string? _windowTitle;
    private bool _disposed;

    public string EmulatorName { get; private set; } = "Unknown";
    public bool IsConnected => _windowHandle != IntPtr.Zero;
    public IntPtr WindowHandle => _windowHandle;
    public string? WindowTitle => _windowTitle;

    public event EventHandler? Connected;
    public event EventHandler? Disconnected;
    public event EventHandler<string>? Error;

    public bool Connect()
    {
        // Try to find any emulator window automatically
        var windows = FindEmulatorWindows().ToList();

        if (windows.Count == 0)
        {
            Error?.Invoke(this, "No emulator windows found");
            return false;
        }

        // Prefer windows with Pokemon in the title
        var pokemonWindow = windows.FirstOrDefault(w =>
            _searchTerms.Any(term => w.Title.Contains(term, StringComparison.OrdinalIgnoreCase)));

        var targetWindow = pokemonWindow ?? windows.First();

        return Connect(targetWindow.Handle);
    }

    public bool Connect(string windowTitle)
    {
        var windows = FindEmulatorWindows().ToList();
        var matchingWindow = windows.FirstOrDefault(w =>
            w.Title.Contains(windowTitle, StringComparison.OrdinalIgnoreCase));

        if (matchingWindow == null)
        {
            Error?.Invoke(this, $"No window found matching '{windowTitle}'");
            return false;
        }

        return Connect(matchingWindow.Handle);
    }

    public bool Connect(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
        {
            Error?.Invoke(this, "Invalid window handle");
            return false;
        }

        if (!IsWindow(windowHandle))
        {
            Error?.Invoke(this, "Window handle is not valid");
            return false;
        }

        _windowHandle = windowHandle;
        _windowTitle = GetWindowTitle(windowHandle);

        // Determine emulator type from window title
        foreach (var emulator in _supportedEmulators)
        {
            if (_windowTitle?.Contains(emulator, StringComparison.OrdinalIgnoreCase) == true)
            {
                EmulatorName = emulator;
                break;
            }
        }

        Connected?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void Disconnect()
    {
        if (_windowHandle != IntPtr.Zero)
        {
            _windowHandle = IntPtr.Zero;
            _windowTitle = null;
            EmulatorName = "Unknown";
            Disconnected?.Invoke(this, EventArgs.Empty);
        }
    }

    public IEnumerable<EmulatorWindow> FindEmulatorWindows()
    {
        var windows = new List<EmulatorWindow>();

        EnumWindows((hWnd, lParam) =>
        {
            if (!IsWindowVisible(hWnd))
                return true;

            var title = GetWindowTitle(hWnd);
            if (string.IsNullOrEmpty(title))
                return true;

            GetWindowThreadProcessId(hWnd, out uint processId);
            string processName = "";

            try
            {
                var process = Process.GetProcessById((int)processId);
                processName = process.ProcessName;
            }
            catch { }

            // Check if this looks like an emulator window by title
            bool isEmulatorByTitle = _supportedEmulators.Any(e =>
                title.Contains(e, StringComparison.OrdinalIgnoreCase));

            // Check if title contains game-related terms
            bool hasPokemon = _searchTerms.Any(term =>
                title.Contains(term, StringComparison.OrdinalIgnoreCase));

            // Check if process name matches known emulators
            bool isEmulatorByProcess = _emulatorProcessNames.Any(p =>
                processName.Equals(p, StringComparison.OrdinalIgnoreCase));

            if (isEmulatorByTitle || hasPokemon || isEmulatorByProcess)
            {
                windows.Add(new EmulatorWindow
                {
                    Handle = hWnd,
                    Title = title,
                    ProcessId = (int)processId,
                    ProcessName = processName
                });
            }

            return true;
        }, IntPtr.Zero);

        return windows;
    }

    /// <summary>
    /// Lists all visible windows for debugging purposes
    /// </summary>
    public IEnumerable<EmulatorWindow> FindAllWindows()
    {
        var windows = new List<EmulatorWindow>();

        EnumWindows((hWnd, lParam) =>
        {
            if (!IsWindowVisible(hWnd))
                return true;

            var title = GetWindowTitle(hWnd);
            if (string.IsNullOrEmpty(title))
                return true;

            GetWindowThreadProcessId(hWnd, out uint processId);
            string processName = "";

            try
            {
                var process = Process.GetProcessById((int)processId);
                processName = process.ProcessName;
            }
            catch { }

            windows.Add(new EmulatorWindow
            {
                Handle = hWnd,
                Title = title,
                ProcessId = (int)processId,
                ProcessName = processName
            });

            return true;
        }, IntPtr.Zero);

        return windows;
    }

    public bool BringToFront()
    {
        if (!IsConnected)
            return false;

        return SetForegroundWindow(_windowHandle);
    }

    public bool IsEmulatorRunning()
    {
        if (!IsConnected)
            return false;

        return IsWindow(_windowHandle);
    }

    private static string GetWindowTitle(IntPtr hWnd)
    {
        int length = GetWindowTextLength(hWnd);
        if (length == 0)
            return string.Empty;

        var sb = new StringBuilder(length + 1);
        GetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Disconnect();
    }

    #region Win32 Imports

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    #endregion
}
