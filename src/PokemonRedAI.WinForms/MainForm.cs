using PokemonRedAI.Emulator;
using System.Runtime.InteropServices;

namespace PokemonRedAI.WinForms;

public partial class MainForm : Form
{
    private EmulatorConnector? _connector;
    private InputSender? _inputSender;
    private AIController? _aiController;
    private ScreenCapture? _screenCapture;
    private bool _isAiRunning = false;
    private readonly List<ActionLogEntry> _actionLog = new();
    private System.Windows.Forms.Timer? _updateTimer;

    // Settings
    private int _keyPressDuration = 50;
    private int _inputDelay = 100;
    private int _movementWait = 250;

    public MainForm()
    {
        InitializeComponent();
        _connector = new EmulatorConnector();

        // Setup update timer for checking connection status
        _updateTimer = new System.Windows.Forms.Timer();
        _updateTimer.Interval = 1000;
        _updateTimer.Tick += UpdateTimer_Tick;
        _updateTimer.Start();

        LogAction("Application started", "", ActionLogType.Info);
    }

    private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        // Update connection status
        if (_connector != null && _connector.IsConnected)
        {
            if (!_connector.IsEmulatorRunning())
            {
                _connector.Disconnect();
                UpdateConnectionStatus();
                LogAction("Emulator disconnected", "Window no longer exists", ActionLogType.Error);
            }
        }
    }

    private void UpdateConnectionStatus()
    {
        if (_connector != null && _connector.IsConnected)
        {
            lblConnectionStatus.Text = $"Connected: {_connector.WindowTitle}";
            lblConnectionStatus.ForeColor = Color.Green;
            btnStartAI.Enabled = true;
        }
        else
        {
            lblConnectionStatus.Text = "Not Connected";
            lblConnectionStatus.ForeColor = Color.Red;
            btnStartAI.Enabled = false;
            if (_isAiRunning)
            {
                StopAI();
            }
        }
    }

    #region Settings Tab - Emulator Detection

    private void btnDetectEmulator_Click(object sender, EventArgs e)
    {
        try
        {
            lblStatus.Text = "Searching for emulator...";
            lblStatus.ForeColor = Color.Blue;
            Application.DoEvents();

            if (_connector == null)
            {
                _connector = new EmulatorConnector();
            }

            var windows = _connector.FindEmulatorWindows().ToList();

            if (windows.Count > 0)
            {
                lstWindows.Items.Clear();
                foreach (var window in windows)
                {
                    lstWindows.Items.Add(new WindowListItem(window));
                }

                // Auto-select and connect to first one
                var firstWindow = windows.First();
                if (_connector.Connect(firstWindow.Handle))
                {
                    _inputSender = new InputSender(firstWindow.Handle);
                    lblStatus.Text = $"Connected: {firstWindow.Title}";
                    lblStatus.ForeColor = Color.Green;
                    UpdateConnectionStatus();
                    LogAction("Emulator connected", firstWindow.Title, ActionLogType.Info);
                }
            }
            else
            {
                lblStatus.Text = "No emulator windows found. Try 'Scan All Windows'.";
                lblStatus.ForeColor = Color.Orange;
            }
        }
        catch (Exception ex)
        {
            lblStatus.Text = $"Error: {ex.Message}";
            lblStatus.ForeColor = Color.Red;
            MessageBox.Show($"Error detecting emulator:\n\n{ex}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void btnScanAllWindows_Click(object sender, EventArgs e)
    {
        try
        {
            lblStatus.Text = "Scanning all windows...";
            lblStatus.ForeColor = Color.Blue;
            Application.DoEvents();

            if (_connector == null)
            {
                _connector = new EmulatorConnector();
            }

            var windows = _connector.FindAllWindows()
                .OrderBy(w => w.ProcessName)
                .ThenBy(w => w.Title)
                .ToList();

            lstWindows.Items.Clear();
            foreach (var window in windows)
            {
                lstWindows.Items.Add(new WindowListItem(window));
            }

            lblStatus.Text = $"Found {windows.Count} windows";
            lblStatus.ForeColor = Color.Black;
        }
        catch (Exception ex)
        {
            lblStatus.Text = $"Error: {ex.Message}";
            lblStatus.ForeColor = Color.Red;
            MessageBox.Show($"Error scanning windows:\n\n{ex}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void btnRunDiagnostics_Click(object sender, EventArgs e)
    {
        try
        {
            lblStatus.Text = "Running diagnostics...";
            lblStatus.ForeColor = Color.Blue;
            Application.DoEvents();

            if (_connector == null)
            {
                _connector = new EmulatorConnector();
            }

            var diagnostics = _connector.GetDiagnosticInfo();
            txtDiagnostics.Text = diagnostics;
            lblStatus.Text = "Diagnostics complete";
            lblStatus.ForeColor = Color.Black;
        }
        catch (Exception ex)
        {
            lblStatus.Text = $"Error: {ex.Message}";
            lblStatus.ForeColor = Color.Red;
            txtDiagnostics.Text = $"Error running diagnostics:\n\n{ex}";
        }
    }

    private void btnSelectWindow_Click(object sender, EventArgs e)
    {
        if (lstWindows.SelectedItem is WindowListItem item)
        {
            try
            {
                if (_connector == null)
                {
                    _connector = new EmulatorConnector();
                }

                if (_connector.Connect(item.Window.Handle))
                {
                    _inputSender = new InputSender(item.Window.Handle);
                    lblStatus.Text = $"Connected: {item.Window.Title}";
                    lblStatus.ForeColor = Color.Green;
                    UpdateConnectionStatus();
                    LogAction("Emulator connected", item.Window.Title, ActionLogType.Info);
                }
                else
                {
                    lblStatus.Text = "Failed to connect to window";
                    lblStatus.ForeColor = Color.Red;
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"Error: {ex.Message}";
                lblStatus.ForeColor = Color.Red;
            }
        }
        else
        {
            MessageBox.Show("Please select a window from the list first.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void lstWindows_DoubleClick(object sender, EventArgs e)
    {
        btnSelectWindow_Click(sender, e);
    }

    #endregion

    #region Controls Tab - Manual Input

    private void SendInput(GameButton button, string buttonName)
    {
        if (_inputSender == null || _connector == null || !_connector.IsConnected)
        {
            LogAction($"Cannot send {buttonName}", "Not connected to emulator", ActionLogType.Error);
            MessageBox.Show("Please connect to an emulator first!\n\nGo to Settings tab and click 'Detect Emulator'.",
                "Not Connected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            // InputSender will focus the window automatically
            _inputSender.SendButton(button, _keyPressDuration);
            LogAction($"Input: {buttonName}", "", ActionLogType.Input);
        }
        catch (Exception ex)
        {
            LogAction($"Input failed: {buttonName}", ex.Message, ActionLogType.Error);
            MessageBox.Show($"Failed to send input: {ex.Message}", "Input Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void btnUp_Click(object sender, EventArgs e) => SendInput(GameButton.Up, "Up");
    private void btnDown_Click(object sender, EventArgs e) => SendInput(GameButton.Down, "Down");
    private void btnLeft_Click(object sender, EventArgs e) => SendInput(GameButton.Left, "Left");
    private void btnRight_Click(object sender, EventArgs e) => SendInput(GameButton.Right, "Right");
    private void btnA_Click(object sender, EventArgs e) => SendInput(GameButton.A, "A");
    private void btnB_Click(object sender, EventArgs e) => SendInput(GameButton.B, "B");
    private void btnStart_Click(object sender, EventArgs e) => SendInput(GameButton.Start, "Start");
    private void btnSelect_Click(object sender, EventArgs e) => SendInput(GameButton.Select, "Select");

    private void btnStartAI_Click(object sender, EventArgs e)
    {
        if (_isAiRunning)
        {
            StopAI();
        }
        else
        {
            StartAI();
        }
    }

    private void StartAI()
    {
        if (_inputSender == null || _connector == null || !_connector.IsConnected)
        {
            MessageBox.Show("Please connect to an emulator first!", "Not Connected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Create screen capture
        _screenCapture = new ScreenCapture(_connector.WindowHandle);

        // Create AI controller if needed
        if (_aiController == null)
        {
            _aiController = new AIController(_inputSender, LogAction);
        }

        // Set up screen capture
        _aiController.SetScreenCapture(_screenCapture);

        // Subscribe to events
        _aiController.ScreenCaptured += OnScreenCaptured;
        _aiController.StatusUpdated += OnStatusUpdated;

        // Update AI settings
        _aiController.KeyPressDurationMs = _keyPressDuration;
        _aiController.MovementDelayMs = _movementWait;

        _aiController.Start();
        _isAiRunning = true;
        btnStartAI.Text = "Stop AI";
        btnStartAI.BackColor = Color.IndianRed;
        lblAIStatus.Text = "AI Running";
        lblAIStatus.ForeColor = Color.Green;
    }

    private void OnScreenCaptured(Bitmap screenshot)
    {
        // Update the PictureBox on the UI thread
        if (picGameScreen.InvokeRequired)
        {
            picGameScreen.Invoke(new Action(() => UpdateGameScreen(screenshot)));
        }
        else
        {
            UpdateGameScreen(screenshot);
        }
    }

    private void UpdateGameScreen(Bitmap screenshot)
    {
        // Dispose old image
        var oldImage = picGameScreen.Image;
        picGameScreen.Image = screenshot;
        oldImage?.Dispose();
    }

    private void OnStatusUpdated(string status)
    {
        // Update the TextBox on the UI thread
        if (txtAIStatus.InvokeRequired)
        {
            txtAIStatus.Invoke(new Action(() => txtAIStatus.Text = status));
        }
        else
        {
            txtAIStatus.Text = status;
        }
    }

    private void StopAI()
    {
        // Unsubscribe from events
        if (_aiController != null)
        {
            _aiController.ScreenCaptured -= OnScreenCaptured;
            _aiController.StatusUpdated -= OnStatusUpdated;
        }

        _aiController?.Stop();
        _isAiRunning = false;
        btnStartAI.Text = "Start AI";
        btnStartAI.BackColor = Color.LightGreen;
        lblAIStatus.Text = "AI Stopped";
        lblAIStatus.ForeColor = Color.Gray;
    }

    private void btnClearLog_Click(object sender, EventArgs e)
    {
        _actionLog.Clear();
        lstActionLog.Items.Clear();
        LogAction("Log cleared", "", ActionLogType.Info);
    }

    private void btnTestInput_Click(object sender, EventArgs e)
    {
        if (_inputSender == null || _connector == null || !_connector.IsConnected)
        {
            MessageBox.Show("Please connect to an emulator first!", "Not Connected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var info = _inputSender.GetDiagnosticInfo();
        info += "\n\nAttempting to focus window and send 'Up' key...";

        try
        {
            var focused = _inputSender.FocusWindow();
            info += $"\n\nWindow focused: {focused}";

            if (focused)
            {
                info += "\n\nSending Up key in 1 second...";
                MessageBox.Show(info, "Input Test", MessageBoxButtons.OK, MessageBoxIcon.Information);

                Thread.Sleep(1000);
                _inputSender.SendButton(GameButton.Up, _keyPressDuration);
                LogAction("Test Input: Up", "Test completed", ActionLogType.Input);
            }
            else
            {
                MessageBox.Show(info + "\n\nCould not focus window!", "Input Test Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"{info}\n\nError: {ex.Message}", "Input Test Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    #endregion

    #region Settings Tab - Input Timing

    private void numKeyPressDuration_ValueChanged(object sender, EventArgs e)
    {
        _keyPressDuration = (int)numKeyPressDuration.Value;
    }

    private void numInputDelay_ValueChanged(object sender, EventArgs e)
    {
        _inputDelay = (int)numInputDelay.Value;
    }

    private void numMovementWait_ValueChanged(object sender, EventArgs e)
    {
        _movementWait = (int)numMovementWait.Value;
    }

    #endregion

    #region Action Log

    private void LogAction(string action, string details, ActionLogType type)
    {
        var entry = new ActionLogEntry
        {
            Timestamp = DateTime.Now,
            Action = action,
            Details = details,
            Type = type
        };

        _actionLog.Add(entry);
        if (_actionLog.Count > 1000)
        {
            _actionLog.RemoveAt(0);
        }

        // Update the list on the UI thread
        if (lstActionLog.InvokeRequired)
        {
            lstActionLog.Invoke(new Action(() => AddLogEntry(entry)));
        }
        else
        {
            AddLogEntry(entry);
        }
    }

    private void AddLogEntry(ActionLogEntry entry)
    {
        var item = new ListViewItem(entry.Timestamp.ToString("HH:mm:ss.fff"));
        item.SubItems.Add(entry.Type.ToString());
        item.SubItems.Add(entry.Action);
        item.SubItems.Add(entry.Details);

        item.BackColor = entry.Type switch
        {
            ActionLogType.Error => Color.MistyRose,
            ActionLogType.Learning => Color.Honeydew,
            ActionLogType.StateChange => Color.AliceBlue,
            ActionLogType.Input => Color.LemonChiffon,
            _ => Color.White
        };

        lstActionLog.Items.Insert(0, item);

        // Keep only last 500 items in view
        while (lstActionLog.Items.Count > 500)
        {
            lstActionLog.Items.RemoveAt(lstActionLog.Items.Count - 1);
        }
    }

    #endregion

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _aiController?.Stop();
        _updateTimer?.Stop();
        _updateTimer?.Dispose();
        _connector?.Dispose();
        base.OnFormClosing(e);
    }

    private class WindowListItem
    {
        public EmulatorWindow Window { get; }

        public WindowListItem(EmulatorWindow window)
        {
            Window = window;
        }

        public override string ToString()
        {
            return $"[{Window.ProcessName}] {Window.Title}";
        }
    }
}

public class ActionLogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
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
