using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Serilog;

namespace PokemonRedAI.Emulator;

public class EmulatorConnector : IEmulatorConnector
{
    // Window title patterns to look for (case-insensitive)
    private readonly string[] _supportedEmulators = { "emuhawk", "mgba", "visualboyadvance", "vba", "bizhawk", "no$gba", "visualboy" };
    private readonly string[] _searchTerms = { "pokemon", "red version", "blue version", "yellow", ".gba", ".gbc", ".gb" };

    // Process names to look for (without .exe, case-insensitive)
    private readonly string[] _emulatorProcessNames = { "mgba", "mgba-qt", "emuhawk", "visualboyadvance", "vba", "visualboyadvance-m", "no$gba" };

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
        Log.Debug("FindEmulatorWindows: Starting - using Process-based approach");
        var windows = new List<EmulatorWindow>();

        try
        {
            // Use Process.GetProcesses() instead of EnumWindows to avoid P/Invoke callback issues
            var processes = Process.GetProcesses();
            Log.Debug("FindEmulatorWindows: Got {Count} processes", processes.Length);

            foreach (var process in processes)
            {
                try
                {
                    // Skip processes without a main window
                    if (process.MainWindowHandle == IntPtr.Zero)
                        continue;

                    var title = process.MainWindowTitle;
                    if (string.IsNullOrEmpty(title))
                        continue;

                    var processName = process.ProcessName;
                    var titleLower = title.ToLowerInvariant();
                    var processLower = processName.ToLowerInvariant();

                    // Check if this looks like an emulator window by title
                    bool isEmulatorByTitle = _supportedEmulators.Any(e => titleLower.Contains(e));

                    // Check if title contains game-related terms
                    bool hasPokemon = _searchTerms.Any(term => titleLower.Contains(term));

                    // Check if process name matches known emulators
                    bool isEmulatorByProcess = _emulatorProcessNames.Any(p => processLower.Contains(p));

                    if (isEmulatorByTitle || hasPokemon || isEmulatorByProcess)
                    {
                        Log.Debug("FindEmulatorWindows: Found match: {Process} - {Title}", processName, title);
                        windows.Add(new EmulatorWindow
                        {
                            Handle = process.MainWindowHandle,
                            Title = title,
                            ProcessId = process.Id,
                            ProcessName = processName
                        });
                    }
                }
                catch (Exception ex)
                {
                    // Process may have exited, ignore
                    Log.Debug("FindEmulatorWindows: Skipping process due to error: {Error}", ex.Message);
                }
            }

            Log.Debug("FindEmulatorWindows: Completed, found {Count} windows", windows.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "FindEmulatorWindows: Exception");
        }

        return windows;
    }

    /// <summary>
    /// Lists all visible windows for debugging purposes
    /// </summary>
    public IEnumerable<EmulatorWindow> FindAllWindows()
    {
        Log.Debug("FindAllWindows: Starting - using Process-based approach");
        var windows = new List<EmulatorWindow>();

        try
        {
            // Use Process.GetProcesses() instead of EnumWindows to avoid P/Invoke callback issues
            var processes = Process.GetProcesses();
            Log.Debug("FindAllWindows: Got {Count} processes", processes.Length);

            foreach (var process in processes)
            {
                try
                {
                    // Skip processes without a main window
                    if (process.MainWindowHandle == IntPtr.Zero)
                        continue;

                    var title = process.MainWindowTitle;
                    if (string.IsNullOrEmpty(title))
                        continue;

                    // Skip very short titles (likely system windows)
                    if (title.Length < 3)
                        continue;

                    windows.Add(new EmulatorWindow
                    {
                        Handle = process.MainWindowHandle,
                        Title = title,
                        ProcessId = process.Id,
                        ProcessName = process.ProcessName
                    });
                }
                catch (Exception ex)
                {
                    // Process may have exited, ignore
                    Log.Debug("FindAllWindows: Skipping process due to error: {Error}", ex.Message);
                }
            }

            Log.Debug("FindAllWindows: Completed, found {Count} windows", windows.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "FindAllWindows: Exception");
        }

        return windows;
    }

    /// <summary>
    /// Gets diagnostic info about what windows were checked and why they matched/didn't match
    /// </summary>
    public string GetDiagnosticInfo()
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Emulator Detection Diagnostics ===");
            sb.AppendLine($"Looking for emulators: {string.Join(", ", _supportedEmulators)}");
            sb.AppendLine($"Looking for terms: {string.Join(", ", _searchTerms)}");
            sb.AppendLine($"Looking for processes: {string.Join(", ", _emulatorProcessNames)}");
            sb.AppendLine();

            var allWindows = FindAllWindows().ToList();
            sb.AppendLine($"Found {allWindows.Count} visible windows total");
            sb.AppendLine();

            foreach (var w in allWindows)
            {
                var titleLower = w.Title?.ToLowerInvariant() ?? "";
                var processLower = w.ProcessName?.ToLowerInvariant() ?? "";

                bool isEmulator = _supportedEmulators.Any(e => titleLower.Contains(e));
                bool hasPokemon = _searchTerms.Any(t => titleLower.Contains(t));
                bool isEmulatorProcess = _emulatorProcessNames.Any(p => processLower.Contains(p));

                if (isEmulator || hasPokemon || isEmulatorProcess)
                {
                    sb.AppendLine($"MATCH: [{w.ProcessName}] \"{w.Title}\"");
                    sb.AppendLine($"  - Emulator title match: {isEmulator}");
                    sb.AppendLine($"  - Pokemon term match: {hasPokemon}");
                    sb.AppendLine($"  - Process name match: {isEmulatorProcess}");
                }
            }

            var emulatorWindows = FindEmulatorWindows().ToList();
            sb.AppendLine();
            sb.AppendLine($"FindEmulatorWindows() returned {emulatorWindows.Count} windows");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error running diagnostics: {ex.Message}";
        }
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

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Disconnect();
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

    #region Win32 Imports

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    #endregion
}
