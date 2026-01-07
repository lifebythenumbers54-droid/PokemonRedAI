# Pokémon Red AI Player - Project Specification

## Overview

A C# application that plays an emulated version of Pokémon Red autonomously. The program reads the game screen, interprets the game state, and sends inputs to navigate and play the game. A WinForms UI displays the current state and actions to the user.

---

## Architecture

### Components

1. **Emulator Interface** - Connects to a Game Boy emulator (e.g., BizHawk, mGBA)
2. **Screen Reader** - Captures and analyzes the game screen
3. **State Machine** - Determines current game state and decides actions
4. **Input Controller** - Sends keyboard inputs to the emulator
5. **Data Persistence** - Saves/loads learned data (walkable tiles, etc.)
6. **WinForms UI** - Displays game state and AI decisions to the user

---

## Startup Sequence

### Step 1: Load Saved Data
- Check for existing save file (e.g., `learned_data.json`)
- If exists: Load walkability map, known objects, and other learned data
- If not exists: Initialize empty data structures, proceed with fresh state

### Step 2: Initialize Screen Reader
- Connect to emulator window/process
- Begin screen capture loop
- Start state detection

---

## Screen Reader Module

### Game State Detection

The screen reader must identify which of the following states the game is in:

#### 1. Battle State
- Detect battle UI elements (HP bars, Pokémon sprites, menu boxes)
- Identify battle phase (selection, animation, text)

#### 2. Overworld State
- Game world navigation mode
- **Tile System**: 8x8 pixel tiles
- Must track:
  - Player position (tile coordinates)
  - Surrounding tile data
  - NPCs and objects

#### 3. Walking State (Sub-state of Overworld)
- Detect player movement animation
- Track player's current tile position
- Monitor movement direction

#### 4. Menu State
- Detect menu overlays (Start menu, Bag, Pokémon, etc.)
- Identify menu type and current selection

### Indicator Detection

#### Blinking Down Arrow (▼)
- Indicates: "Press button to continue"
- Location: Typically bottom-right of text box
- Action: Press X (A button) to advance

#### Sideways Arrow (►)
- Indicates: Selection cursor in menu
- Location: Left side of menu options
- Action: Navigate with arrows, confirm with X

---

## Walkability Learning System

### Tile Classification

Each tile in the game world is classified as:
- **WALKABLE** - Player can move onto this tile
- **BLOCKED** - Player cannot move onto this tile
- **UNKNOWN** - Not yet tested

### Learning Algorithm

```
When player attempts to move in a direction:
    1. Record current position (x, y)
    2. Record target position based on direction
    3. Send movement input
    4. Wait for movement to complete or timeout
    5. Check new position:
        - If position changed → Mark target tile as WALKABLE
        - If position unchanged → Mark target tile as BLOCKED
    6. Save updated walkability data
```

### Special Rules

- **Black screen tiles**: Assume BLOCKED (not walkable)
- **Screen transitions**: Handle map changes, update tile context
- **Dynamic objects**: NPCs may block tiles temporarily

### Data Structure

```json
{
  "maps": {
    "map_id": {
      "tiles": {
        "x,y": "WALKABLE" | "BLOCKED"
      }
    }
  }
}
```

---

## Input Controller

### Key Mappings

| Action | Key | Game Button |
|--------|-----|-------------|
| Move Up | ↑ (Up Arrow) | D-Pad Up |
| Move Down | ↓ (Down Arrow) | D-Pad Down |
| Move Left | ← (Left Arrow) | D-Pad Left |
| Move Right | → (Right Arrow) | D-Pad Right |
| Confirm/Continue | X | A Button |
| Cancel/Back | Z | B Button |

### Input Timing

- Minimum key press duration: ~50ms
- Delay between inputs: ~100ms (configurable)
- Movement completion check: Wait for player sprite to settle

---

## Data Persistence

### Save File: `learned_data.json`

```json
{
  "version": "1.0",
  "lastSaved": "2026-01-04T12:00:00Z",
  "walkabilityMaps": { },
  "knownObjects": { },
  "gameProgress": { }
}
```

### Auto-Save Triggers

- After learning new tile walkability
- On game state changes
- Periodic interval (every 60 seconds)
- On application exit

---

## WinForms UI

### Display Elements

1. **Game Screen Mirror** - Shows current emulator screen
2. **State Indicator** - Current detected state (Battle/Overworld/Menu)
3. **Position Display** - Player coordinates and current map
4. **Action Log** - Recent inputs and decisions
5. **Walkability Map** - Visual grid of known tiles
6. **Statistics** - Steps taken, tiles learned, etc.

### Controls

- Start/Stop AI
- Manual override (send specific inputs)
- Save/Load data manually
- Adjust timing parameters

---

## Project Structure

```
PokemonRedAI/
├── PokemonRedAI.sln
├── src/
│   ├── PokemonRedAI.Core/           # Core logic
│   │   ├── ScreenReader/
│   │   │   ├── IScreenCapture.cs
│   │   │   ├── ScreenAnalyzer.cs
│   │   │   ├── StateDetector.cs
│   │   │   └── TileReader.cs
│   │   ├── Input/
│   │   │   ├── IInputController.cs
│   │   │   └── KeyboardController.cs
│   │   ├── State/
│   │   │   ├── GameState.cs
│   │   │   ├── BattleState.cs
│   │   │   ├── OverworldState.cs
│   │   │   └── MenuState.cs
│   │   ├── Learning/
│   │   │   ├── WalkabilityMap.cs
│   │   │   └── TileClassifier.cs
│   │   └── Persistence/
│   │       ├── SaveData.cs
│   │       └── DataManager.cs
│   ├── PokemonRedAI.WinForms/       # WinForms UI
│   │   ├── MainForm.cs
│   │   ├── AIController.cs
│   │   └── ScreenCapture.cs
│   └── PokemonRedAI.Emulator/       # Emulator integration
│       ├── IEmulatorConnector.cs
│       └── BizHawkConnector.cs
└── tests/
    └── PokemonRedAI.Tests/
```

---

## Dependencies

- **.NET 8** or later
- **System.Drawing** - Image processing (Windows Forms)
- **Win32 API** - Keyboard input and window capture
- **System.Text.Json** - Data serialization

---

## Emulator Requirements

Recommended: **BizHawk** or **mGBA**

- Must support external window capture
- Must accept keyboard input when not focused (or use input injection)
- Consistent window size for screen reading

---

## Future Enhancements (Out of Scope for v1)

- Battle AI decision making
- Pathfinding with A* algorithm
- Goal-based navigation (go to specific locations)
- Pokemon team management
- Item usage optimization
- Memory reading for precise game state (bypasses screen reading)

---

## Success Criteria

1. Successfully loads and saves learned data
2. Accurately detects game state (Battle/Overworld/Menu)
3. Identifies text continuation and menu selection prompts
4. Learns and remembers tile walkability
5. Navigates the overworld without getting stuck
6. WinForms UI displays real-time game state and AI actions
