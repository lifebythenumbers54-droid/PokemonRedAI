namespace PokemonRedAI.Emulator;

public interface IEmulatorConnector : IDisposable
{
    string EmulatorName { get; }
    bool IsConnected { get; }
    IntPtr WindowHandle { get; }
    string? WindowTitle { get; }

    bool Connect();
    bool Connect(string windowTitle);
    bool Connect(IntPtr windowHandle);
    void Disconnect();

    IEnumerable<EmulatorWindow> FindEmulatorWindows();
}

public class EmulatorWindow
{
    public IntPtr Handle { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    public int ProcessId { get; set; }
}
