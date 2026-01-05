namespace PokemonRedAI.WinForms;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    private void InitializeComponent()
    {
        this.components = new System.ComponentModel.Container();

        // Main TabControl
        this.tabControl = new TabControl();
        this.tabSettings = new TabPage();
        this.tabControls = new TabPage();
        this.tabLog = new TabPage();

        // Status bar
        this.statusStrip = new StatusStrip();
        this.lblConnectionStatus = new ToolStripStatusLabel();
        this.lblAIStatus = new ToolStripStatusLabel();

        // Settings tab controls
        this.grpEmulator = new GroupBox();
        this.grpWindows = new GroupBox();
        this.grpDiagnostics = new GroupBox();
        this.grpInputTiming = new GroupBox();

        this.lblEmulatorType = new Label();
        this.cmbEmulatorType = new ComboBox();
        this.btnDetectEmulator = new Button();
        this.btnScanAllWindows = new Button();
        this.btnRunDiagnostics = new Button();
        this.lblStatus = new Label();
        this.lstWindows = new ListBox();
        this.btnSelectWindow = new Button();
        this.txtDiagnostics = new TextBox();

        // Input timing controls
        this.lblKeyPressDuration = new Label();
        this.numKeyPressDuration = new NumericUpDown();
        this.lblInputDelay = new Label();
        this.numInputDelay = new NumericUpDown();
        this.lblMovementWait = new Label();
        this.numMovementWait = new NumericUpDown();

        // Controls tab
        this.grpAIControl = new GroupBox();
        this.grpManualInput = new GroupBox();
        this.grpGameView = new GroupBox();
        this.grpAIInfo = new GroupBox();
        this.btnStartAI = new Button();
        this.btnTestInput = new Button();
        this.btnUp = new Button();
        this.btnDown = new Button();
        this.btnLeft = new Button();
        this.btnRight = new Button();
        this.btnA = new Button();
        this.btnB = new Button();
        this.btnStart = new Button();
        this.btnSelect = new Button();
        this.picGameScreen = new PictureBox();
        this.lblAIThinking = new Label();
        this.txtAIStatus = new TextBox();

        // Log tab
        this.lstActionLog = new ListView();
        this.btnClearLog = new Button();

        // Suspend layouts
        this.tabControl.SuspendLayout();
        this.tabSettings.SuspendLayout();
        this.tabControls.SuspendLayout();
        this.tabLog.SuspendLayout();
        this.grpEmulator.SuspendLayout();
        this.grpWindows.SuspendLayout();
        this.grpDiagnostics.SuspendLayout();
        this.grpInputTiming.SuspendLayout();
        this.grpAIControl.SuspendLayout();
        this.grpManualInput.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)this.numKeyPressDuration).BeginInit();
        ((System.ComponentModel.ISupportInitialize)this.numInputDelay).BeginInit();
        ((System.ComponentModel.ISupportInitialize)this.numMovementWait).BeginInit();
        this.statusStrip.SuspendLayout();
        this.SuspendLayout();

        // ===== TAB CONTROL =====
        this.tabControl.Controls.Add(this.tabSettings);
        this.tabControl.Controls.Add(this.tabControls);
        this.tabControl.Controls.Add(this.tabLog);
        this.tabControl.Dock = DockStyle.Fill;
        this.tabControl.Font = new Font("Segoe UI", 10F);
        this.tabControl.Padding = new Point(12, 6);

        // ===== SETTINGS TAB =====
        this.tabSettings.Text = "Settings";
        this.tabSettings.Padding = new Padding(10);
        this.tabSettings.Controls.Add(this.grpDiagnostics);
        this.tabSettings.Controls.Add(this.grpInputTiming);
        this.tabSettings.Controls.Add(this.grpWindows);
        this.tabSettings.Controls.Add(this.grpEmulator);

        // Emulator Group
        this.grpEmulator.Text = "Emulator Connection";
        this.grpEmulator.Location = new Point(10, 10);
        this.grpEmulator.Size = new Size(350, 180);

        this.lblEmulatorType.Text = "Emulator Type:";
        this.lblEmulatorType.Location = new Point(15, 30);
        this.lblEmulatorType.AutoSize = true;

        this.cmbEmulatorType.Location = new Point(15, 50);
        this.cmbEmulatorType.Size = new Size(150, 25);
        this.cmbEmulatorType.DropDownStyle = ComboBoxStyle.DropDownList;
        this.cmbEmulatorType.Items.AddRange(new object[] { "mGBA", "BizHawk", "VisualBoyAdvance" });
        this.cmbEmulatorType.SelectedIndex = 0;

        this.btnDetectEmulator.Text = "Detect Emulator";
        this.btnDetectEmulator.Location = new Point(15, 85);
        this.btnDetectEmulator.Size = new Size(130, 30);
        this.btnDetectEmulator.Click += new EventHandler(this.btnDetectEmulator_Click);

        this.btnScanAllWindows.Text = "Scan All Windows";
        this.btnScanAllWindows.Location = new Point(155, 85);
        this.btnScanAllWindows.Size = new Size(130, 30);
        this.btnScanAllWindows.Click += new EventHandler(this.btnScanAllWindows_Click);

        this.btnRunDiagnostics.Text = "Run Diagnostics";
        this.btnRunDiagnostics.Location = new Point(15, 125);
        this.btnRunDiagnostics.Size = new Size(130, 30);
        this.btnRunDiagnostics.Click += new EventHandler(this.btnRunDiagnostics_Click);

        this.lblStatus.Text = "Not Connected";
        this.lblStatus.Location = new Point(155, 133);
        this.lblStatus.AutoSize = true;
        this.lblStatus.ForeColor = Color.Red;
        this.lblStatus.Font = new Font("Segoe UI", 9, FontStyle.Bold);

        this.grpEmulator.Controls.Add(this.lblEmulatorType);
        this.grpEmulator.Controls.Add(this.cmbEmulatorType);
        this.grpEmulator.Controls.Add(this.btnDetectEmulator);
        this.grpEmulator.Controls.Add(this.btnScanAllWindows);
        this.grpEmulator.Controls.Add(this.btnRunDiagnostics);
        this.grpEmulator.Controls.Add(this.lblStatus);

        // Input Timing Group
        this.grpInputTiming.Text = "Input Timing";
        this.grpInputTiming.Location = new Point(10, 200);
        this.grpInputTiming.Size = new Size(350, 150);

        this.lblKeyPressDuration.Text = "Key Press Duration (ms):";
        this.lblKeyPressDuration.Location = new Point(15, 30);
        this.lblKeyPressDuration.AutoSize = true;

        this.numKeyPressDuration.Location = new Point(200, 28);
        this.numKeyPressDuration.Size = new Size(80, 25);
        this.numKeyPressDuration.Minimum = 20;
        this.numKeyPressDuration.Maximum = 200;
        this.numKeyPressDuration.Value = 50;
        this.numKeyPressDuration.ValueChanged += new EventHandler(this.numKeyPressDuration_ValueChanged);

        this.lblInputDelay.Text = "Delay Between Inputs (ms):";
        this.lblInputDelay.Location = new Point(15, 65);
        this.lblInputDelay.AutoSize = true;

        this.numInputDelay.Location = new Point(200, 63);
        this.numInputDelay.Size = new Size(80, 25);
        this.numInputDelay.Minimum = 50;
        this.numInputDelay.Maximum = 500;
        this.numInputDelay.Value = 100;
        this.numInputDelay.ValueChanged += new EventHandler(this.numInputDelay_ValueChanged);

        this.lblMovementWait.Text = "Movement Wait Time (ms):";
        this.lblMovementWait.Location = new Point(15, 100);
        this.lblMovementWait.AutoSize = true;

        this.numMovementWait.Location = new Point(200, 98);
        this.numMovementWait.Size = new Size(80, 25);
        this.numMovementWait.Minimum = 100;
        this.numMovementWait.Maximum = 1000;
        this.numMovementWait.Value = 250;
        this.numMovementWait.ValueChanged += new EventHandler(this.numMovementWait_ValueChanged);

        this.grpInputTiming.Controls.Add(this.lblKeyPressDuration);
        this.grpInputTiming.Controls.Add(this.numKeyPressDuration);
        this.grpInputTiming.Controls.Add(this.lblInputDelay);
        this.grpInputTiming.Controls.Add(this.numInputDelay);
        this.grpInputTiming.Controls.Add(this.lblMovementWait);
        this.grpInputTiming.Controls.Add(this.numMovementWait);

        // Windows Group
        this.grpWindows.Text = "Detected Windows";
        this.grpWindows.Location = new Point(370, 10);
        this.grpWindows.Size = new Size(350, 340);

        this.lstWindows.Location = new Point(10, 25);
        this.lstWindows.Size = new Size(330, 260);
        this.lstWindows.Font = new Font("Consolas", 9);
        this.lstWindows.DoubleClick += new EventHandler(this.lstWindows_DoubleClick);

        this.btnSelectWindow.Text = "Connect to Selected";
        this.btnSelectWindow.Location = new Point(10, 295);
        this.btnSelectWindow.Size = new Size(330, 35);
        this.btnSelectWindow.Click += new EventHandler(this.btnSelectWindow_Click);

        this.grpWindows.Controls.Add(this.lstWindows);
        this.grpWindows.Controls.Add(this.btnSelectWindow);

        // Diagnostics Group
        this.grpDiagnostics.Text = "Diagnostics Output";
        this.grpDiagnostics.Location = new Point(730, 10);
        this.grpDiagnostics.Size = new Size(350, 340);

        this.txtDiagnostics.Multiline = true;
        this.txtDiagnostics.ScrollBars = ScrollBars.Both;
        this.txtDiagnostics.Location = new Point(10, 25);
        this.txtDiagnostics.Size = new Size(330, 300);
        this.txtDiagnostics.Font = new Font("Consolas", 9);
        this.txtDiagnostics.ReadOnly = true;
        this.txtDiagnostics.BackColor = Color.FromArgb(30, 30, 30);
        this.txtDiagnostics.ForeColor = Color.LightGreen;
        this.txtDiagnostics.WordWrap = false;

        this.grpDiagnostics.Controls.Add(this.txtDiagnostics);

        // ===== CONTROLS TAB =====
        this.tabControls.Text = "Controls";
        this.tabControls.Padding = new Padding(10);
        this.tabControls.Controls.Add(this.grpAIControl);
        this.tabControls.Controls.Add(this.grpManualInput);
        this.tabControls.Controls.Add(this.grpGameView);
        this.tabControls.Controls.Add(this.grpAIInfo);

        // AI Control Group
        this.grpAIControl.Text = "AI Control";
        this.grpAIControl.Location = new Point(10, 10);
        this.grpAIControl.Size = new Size(300, 140);

        this.btnStartAI.Text = "Start AI";
        this.btnStartAI.Location = new Point(15, 30);
        this.btnStartAI.Size = new Size(270, 50);
        this.btnStartAI.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
        this.btnStartAI.BackColor = Color.LightGreen;
        this.btnStartAI.Enabled = false;
        this.btnStartAI.Click += new EventHandler(this.btnStartAI_Click);

        this.btnTestInput.Text = "Test Input (Debug)";
        this.btnTestInput.Location = new Point(15, 90);
        this.btnTestInput.Size = new Size(270, 35);
        this.btnTestInput.Click += new EventHandler(this.btnTestInput_Click);

        this.grpAIControl.Controls.Add(this.btnStartAI);
        this.grpAIControl.Controls.Add(this.btnTestInput);

        // Manual Input Group
        this.grpManualInput.Text = "Manual Input";
        this.grpManualInput.Location = new Point(10, 160);
        this.grpManualInput.Size = new Size(300, 250);

        // D-Pad
        this.btnUp.Text = "\u25B2";
        this.btnUp.Location = new Point(115, 30);
        this.btnUp.Size = new Size(60, 50);
        this.btnUp.Font = new Font("Segoe UI", 14F);
        this.btnUp.Click += new EventHandler(this.btnUp_Click);

        this.btnLeft.Text = "\u25C0";
        this.btnLeft.Location = new Point(50, 85);
        this.btnLeft.Size = new Size(60, 50);
        this.btnLeft.Font = new Font("Segoe UI", 14F);
        this.btnLeft.Click += new EventHandler(this.btnLeft_Click);

        this.btnRight.Text = "\u25B6";
        this.btnRight.Location = new Point(180, 85);
        this.btnRight.Size = new Size(60, 50);
        this.btnRight.Font = new Font("Segoe UI", 14F);
        this.btnRight.Click += new EventHandler(this.btnRight_Click);

        this.btnDown.Text = "\u25BC";
        this.btnDown.Location = new Point(115, 140);
        this.btnDown.Size = new Size(60, 50);
        this.btnDown.Font = new Font("Segoe UI", 14F);
        this.btnDown.Click += new EventHandler(this.btnDown_Click);

        // A/B Buttons
        this.btnA.Text = "A";
        this.btnA.Location = new Point(50, 200);
        this.btnA.Size = new Size(50, 40);
        this.btnA.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
        this.btnA.BackColor = Color.LightBlue;
        this.btnA.Click += new EventHandler(this.btnA_Click);

        this.btnB.Text = "B";
        this.btnB.Location = new Point(110, 200);
        this.btnB.Size = new Size(50, 40);
        this.btnB.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
        this.btnB.BackColor = Color.LightCoral;
        this.btnB.Click += new EventHandler(this.btnB_Click);

        // Start/Select
        this.btnStart.Text = "START";
        this.btnStart.Location = new Point(180, 200);
        this.btnStart.Size = new Size(60, 20);
        this.btnStart.Font = new Font("Segoe UI", 8F);
        this.btnStart.Click += new EventHandler(this.btnStart_Click);

        this.btnSelect.Text = "SELECT";
        this.btnSelect.Location = new Point(180, 222);
        this.btnSelect.Size = new Size(60, 20);
        this.btnSelect.Font = new Font("Segoe UI", 8F);
        this.btnSelect.Click += new EventHandler(this.btnSelect_Click);

        this.grpManualInput.Controls.Add(this.btnUp);
        this.grpManualInput.Controls.Add(this.btnDown);
        this.grpManualInput.Controls.Add(this.btnLeft);
        this.grpManualInput.Controls.Add(this.btnRight);
        this.grpManualInput.Controls.Add(this.btnA);
        this.grpManualInput.Controls.Add(this.btnB);
        this.grpManualInput.Controls.Add(this.btnStart);
        this.grpManualInput.Controls.Add(this.btnSelect);

        // Game View Group
        this.grpGameView.Text = "Game Screen";
        this.grpGameView.Location = new Point(320, 10);
        this.grpGameView.Size = new Size(340, 320);

        ((System.ComponentModel.ISupportInitialize)this.picGameScreen).BeginInit();
        this.picGameScreen.Location = new Point(10, 25);
        this.picGameScreen.Size = new Size(320, 288);
        this.picGameScreen.SizeMode = PictureBoxSizeMode.Zoom;
        this.picGameScreen.BackColor = Color.Black;
        this.picGameScreen.BorderStyle = BorderStyle.FixedSingle;
        ((System.ComponentModel.ISupportInitialize)this.picGameScreen).EndInit();

        this.grpGameView.Controls.Add(this.picGameScreen);

        // AI Info Group
        this.grpAIInfo.Text = "AI Status";
        this.grpAIInfo.Location = new Point(670, 10);
        this.grpAIInfo.Size = new Size(300, 320);

        this.lblAIThinking.Text = "AI Thinking:";
        this.lblAIThinking.Location = new Point(10, 25);
        this.lblAIThinking.AutoSize = true;
        this.lblAIThinking.Font = new Font("Segoe UI", 10F, FontStyle.Bold);

        this.txtAIStatus.Multiline = true;
        this.txtAIStatus.ScrollBars = ScrollBars.Vertical;
        this.txtAIStatus.Location = new Point(10, 50);
        this.txtAIStatus.Size = new Size(280, 255);
        this.txtAIStatus.Font = new Font("Consolas", 9);
        this.txtAIStatus.ReadOnly = true;
        this.txtAIStatus.BackColor = Color.FromArgb(40, 40, 40);
        this.txtAIStatus.ForeColor = Color.LightGreen;

        this.grpAIInfo.Controls.Add(this.lblAIThinking);
        this.grpAIInfo.Controls.Add(this.txtAIStatus);

        // ===== LOG TAB =====
        this.tabLog.Text = "Action Log";
        this.tabLog.Padding = new Padding(10);
        this.tabLog.Controls.Add(this.lstActionLog);
        this.tabLog.Controls.Add(this.btnClearLog);

        this.lstActionLog.View = View.Details;
        this.lstActionLog.FullRowSelect = true;
        this.lstActionLog.GridLines = true;
        this.lstActionLog.Dock = DockStyle.Fill;
        this.lstActionLog.Font = new Font("Consolas", 9);
        this.lstActionLog.Columns.Add("Time", 100);
        this.lstActionLog.Columns.Add("Type", 80);
        this.lstActionLog.Columns.Add("Action", 200);
        this.lstActionLog.Columns.Add("Details", 400);

        this.btnClearLog.Text = "Clear Log";
        this.btnClearLog.Dock = DockStyle.Bottom;
        this.btnClearLog.Height = 35;
        this.btnClearLog.Click += new EventHandler(this.btnClearLog_Click);

        // ===== STATUS STRIP =====
        this.statusStrip.Items.AddRange(new ToolStripItem[] { this.lblConnectionStatus, this.lblAIStatus });

        this.lblConnectionStatus.Text = "Not Connected";
        this.lblConnectionStatus.ForeColor = Color.Red;
        this.lblConnectionStatus.Spring = true;
        this.lblConnectionStatus.TextAlign = ContentAlignment.MiddleLeft;

        this.lblAIStatus.Text = "AI Stopped";
        this.lblAIStatus.ForeColor = Color.Gray;

        // ===== MAIN FORM =====
        this.AutoScaleDimensions = new SizeF(7F, 15F);
        this.AutoScaleMode = AutoScaleMode.Font;
        this.ClientSize = new Size(1000, 480);
        this.Controls.Add(this.tabControl);
        this.Controls.Add(this.statusStrip);
        this.MinimumSize = new Size(1000, 520);
        this.Text = "Pokemon Red AI Player";
        this.StartPosition = FormStartPosition.CenterScreen;

        // Resume layouts
        ((System.ComponentModel.ISupportInitialize)this.numKeyPressDuration).EndInit();
        ((System.ComponentModel.ISupportInitialize)this.numInputDelay).EndInit();
        ((System.ComponentModel.ISupportInitialize)this.numMovementWait).EndInit();
        this.grpEmulator.ResumeLayout(false);
        this.grpEmulator.PerformLayout();
        this.grpWindows.ResumeLayout(false);
        this.grpDiagnostics.ResumeLayout(false);
        this.grpDiagnostics.PerformLayout();
        this.grpInputTiming.ResumeLayout(false);
        this.grpInputTiming.PerformLayout();
        this.grpAIControl.ResumeLayout(false);
        this.grpManualInput.ResumeLayout(false);
        this.tabSettings.ResumeLayout(false);
        this.tabControls.ResumeLayout(false);
        this.tabLog.ResumeLayout(false);
        this.tabControl.ResumeLayout(false);
        this.statusStrip.ResumeLayout(false);
        this.statusStrip.PerformLayout();
        this.ResumeLayout(false);
        this.PerformLayout();
    }

    #endregion

    private TabControl tabControl;
    private TabPage tabSettings;
    private TabPage tabControls;
    private TabPage tabLog;

    private StatusStrip statusStrip;
    private ToolStripStatusLabel lblConnectionStatus;
    private ToolStripStatusLabel lblAIStatus;

    // Settings tab
    private GroupBox grpEmulator;
    private GroupBox grpWindows;
    private GroupBox grpDiagnostics;
    private GroupBox grpInputTiming;

    private Label lblEmulatorType;
    private ComboBox cmbEmulatorType;
    private Button btnDetectEmulator;
    private Button btnScanAllWindows;
    private Button btnRunDiagnostics;
    private Label lblStatus;
    private ListBox lstWindows;
    private Button btnSelectWindow;
    private TextBox txtDiagnostics;

    private Label lblKeyPressDuration;
    private NumericUpDown numKeyPressDuration;
    private Label lblInputDelay;
    private NumericUpDown numInputDelay;
    private Label lblMovementWait;
    private NumericUpDown numMovementWait;

    // Controls tab
    private GroupBox grpAIControl;
    private GroupBox grpManualInput;
    private GroupBox grpGameView;
    private GroupBox grpAIInfo;
    private Button btnStartAI;
    private Button btnTestInput;
    private Button btnUp;
    private Button btnDown;
    private Button btnLeft;
    private Button btnRight;
    private Button btnA;
    private Button btnB;
    private Button btnStart;
    private Button btnSelect;
    private PictureBox picGameScreen;
    private Label lblAIThinking;
    private TextBox txtAIStatus;

    // Log tab
    private ListView lstActionLog;
    private Button btnClearLog;
}
