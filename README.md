# Pokemon Red AI Player

An autonomous AI player for Pokemon Red that learns to navigate the game world by analyzing the screen and remembering walkable tiles.

## Features

- **Screen Reading**: Captures and analyzes the emulator screen in real-time
- **State Detection**: Identifies game states (Overworld, Battle, Menu, Dialogue)
- **Walkability Learning**: Learns which tiles are walkable through trial and error
- **Web Dashboard**: Real-time visualization of AI status and game state
- **Data Persistence**: Saves learned data between sessions

## Requirements

- **.NET 8 SDK** or later
- **Windows OS** (uses Win32 API for screen capture and input)
- **Supported Emulator**:
  - BizHawk (recommended)
  - mGBA
- **Pokemon Red ROM** (not included - you must provide your own legally obtained ROM)

## Installation

1. Clone the repository:
```bash
git clone https://github.com/lifebythenumbers54-droid/PokemonRedAI.git
cd PokemonRedAI
```

2. Build the solution:
```bash
dotnet build
```

3. Run the web application:
```bash
dotnet run --project src/PokemonRedAI.Web
```

4. Open your browser to `https://localhost:5001` or `http://localhost:5000`

## Emulator Setup

### BizHawk (Recommended)

1. Download BizHawk from the official website
2. Load your Pokemon Red ROM
3. Configure controls:
   - A Button → X key
   - B Button → Z key
   - D-Pad → Arrow keys
   - Start → Enter
   - Select → Backspace
4. Keep the emulator window visible (don't minimize)

### mGBA

1. Download mGBA from the official website
2. Load your Pokemon Red ROM
3. Configure controls in Options → Settings → Keyboard
4. Use the same key mappings as above

## Usage

### Starting the AI

1. Start your emulator with Pokemon Red loaded
2. Launch the web application
3. Go to the Dashboard page
4. Click "Start AI" on the Controls page

### Controls

- **Start/Stop AI**: Toggle autonomous play
- **Manual Input**: Use the D-pad and buttons for manual control
- **Save Data**: Manually save learned walkability data
- **Load Data**: Load previously saved data

### Web Pages

- **Dashboard**: Overview of game state, screen mirror, and statistics
- **Controls**: Start/Stop AI, manual input, action log
- **Walkability Map**: Visual representation of learned tiles
- **Settings**: Configure emulator connection and input timing

## How It Works

### Screen Capture

The AI captures the emulator window and converts it to pixel data for analysis. The Game Boy screen resolution is 160x144 pixels, divided into 8x8 pixel tiles (20x18 grid).

### State Detection

The AI detects the current game state by analyzing screen patterns:

- **Battle**: HP bars, battle menu UI elements
- **Overworld**: Normal gameplay view with player sprite
- **Menu**: Start menu, item bag, Pokemon menu overlays
- **Dialogue**: Text boxes with continue/selection arrows

### Walkability Learning

When the AI attempts to move:

1. Records current position
2. Sends movement input
3. Waits for movement to complete
4. Compares position:
   - If changed → Target tile is WALKABLE
   - If unchanged → Target tile is BLOCKED
5. Saves the learned information

Black screen areas are automatically marked as blocked.

### Input System

The AI sends keyboard inputs using the Windows SendInput API:

| Game Button | Keyboard Key |
|-------------|--------------|
| A (Confirm) | X |
| B (Cancel)  | Z |
| D-Pad       | Arrow Keys |
| Start       | Enter |
| Select      | Backspace |

## Project Structure

```
PokemonRedAI/
├── PokemonRedAI.sln
├── src/
│   ├── PokemonRedAI.Core/           # Core AI logic
│   │   ├── GameRunner.cs            # Main game loop
│   │   ├── ScreenReader/            # Screen analysis
│   │   ├── Input/                   # Input handling
│   │   ├── State/                   # Game state types
│   │   ├── Learning/                # Walkability learning
│   │   └── Persistence/             # Data save/load
│   ├── PokemonRedAI.Web/            # Blazor web UI
│   │   ├── Pages/                   # Dashboard, Controls, etc.
│   │   ├── Hubs/                    # SignalR hub
│   │   └── Services/                # Game state service
│   └── PokemonRedAI.Emulator/       # Emulator integration
│       ├── ScreenCapture.cs         # Window capture
│       ├── KeyboardController.cs    # Input sending
│       └── EmulatorConnector.cs     # Window detection
└── tests/
    └── PokemonRedAI.Tests/
```

## Configuration

### Input Timing (Settings page)

- **Key Press Duration**: How long to hold each key (default: 50ms)
- **Delay Between Inputs**: Wait time between key presses (default: 100ms)
- **Movement Wait Time**: Time to wait for movement animation (default: 250ms)

### Auto-Save

Learned data is automatically saved:
- Every 60 seconds
- When the AI is stopped
- When the application exits

Save location: `%APPDATA%/PokemonRedAI/learned_data.json`

## Troubleshooting

### Emulator not detected

1. Make sure the emulator is running and visible
2. Check that the window title contains "Pokemon" or the emulator name
3. Try entering the window title manually in Settings

### Input not working

1. Ensure the emulator is the focused window
2. Check that key mappings match your emulator settings
3. Try increasing the key press duration

### Screen capture issues

1. Don't minimize the emulator window
2. Avoid overlapping other windows on top
3. Use windowed mode (not fullscreen)

## Limitations

- Windows only (uses Win32 API)
- Requires emulator window to be visible
- No battle AI (currently just advances through battles)
- No pathfinding (random exploration only)

## Future Improvements

See the [spec.md](spec.md) file for planned future enhancements.

## License

This project is for educational purposes only. Pokemon is a trademark of Nintendo/Game Freak. You must own a legal copy of the game to use this software.
