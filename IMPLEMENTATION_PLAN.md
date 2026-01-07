# Pokemon Red AI - Implementation Plan

This document outlines the implementation plan to complete all features specified in `spec.md`. The plan is organized into phases, with each phase containing specific steps and technical details.

---

## Current State Assessment

### Already Implemented
- Emulator detection and connection (mGBA, BizHawk, VBA, No$GBA)
- Screen capture with tile extraction (160x144 → 10x9 grid of 16x16 tiles)
- Basic walkability learning system with tile hashing
- Persistent tile database (JSON-based with auto-save)
- Input sending via PostMessage + SendInput
- Basic exploration strategy with exit detection
- Text box detection and dismissal
- WinForms UI with manual controls and action logging
- Basic room analysis (tile classification, exit detection)

### Missing/Incomplete Features
1. **State Detection System** - No battle/menu/dialogue state detection
2. **Battle State Handling** - No battle AI
3. **Menu State Handling** - No menu navigation
4. **Indicator Detection** - No arrow detection (▼ and ►)
5. **Map-based Walkability** - Current system uses global tile hashes, not map-specific coordinates
6. **Player Position Tracking** - No coordinate-based position tracking

---

## Phase 1: Game State Detection System

**Goal**: Accurately detect which state the game is in (Overworld, Battle, Menu, Dialogue)

### Step 1.1: Create State Detection Infrastructure

**File**: `src/PokemonRedAI.Core/State/GameState.cs`

Create an enum and base classes for game states:
```csharp
public enum GameStateType
{
    Unknown,
    Overworld,
    OverworldWalking,
    Battle,
    BattleMenu,
    BattleAnimation,
    BattleText,
    Menu,
    Dialogue
}
```

### Step 1.2: Implement StateDetector Class

**File**: `src/PokemonRedAI.Core/ScreenReader/StateDetector.cs`

Detection logic based on screen analysis:

| State | Detection Method |
|-------|------------------|
| **Battle** | Look for HP bar patterns (horizontal bars with specific colors), battle menu box at bottom |
| **Menu** | Detect dark overlay with white text boxes, Start menu has specific layout |
| **Dialogue** | Text box at bottom of screen (≈40px height), look for text patterns |
| **Overworld** | Default state when none of above detected |

**Key Detection Patterns**:
- **HP Bars**: Green/Yellow/Red horizontal bars in specific screen regions
- **Battle UI**: HP bar at top-right (enemy), HP bar at bottom-right (player)
- **Menu Box**: White rectangle with black border in center/right of screen
- **Text Box**: White rectangle spanning bottom of screen (160px wide, ~40px tall)

### Step 1.3: Implement Indicator Detection

**File**: `src/PokemonRedAI.Core/ScreenReader/IndicatorDetector.cs`

Detect action prompts:

| Indicator | Location | Meaning |
|-----------|----------|---------|
| **▼ (Down Arrow)** | Bottom-right of text box | "Press A to continue" |
| **► (Right Arrow)** | Left of menu items | Selection cursor |

**Detection approach**:
- Scan specific pixel regions for arrow patterns
- Use template matching or pixel pattern recognition
- Track blinking state (arrow blinks on/off)

### Step 1.4: Integrate State Detection into AI Loop

**File**: `src/PokemonRedAI.WinForms/AIController.cs`

Modify `AILoop` to:
1. Capture screen
2. **Detect game state first**
3. Route to appropriate handler based on state
4. Execute state-specific action

---

## Phase 2: Battle State Handling

**Goal**: Handle battles by advancing through them (basic implementation)

### Step 2.1: Create Battle State Handler

**File**: `src/PokemonRedAI.Core/State/BattleStateHandler.cs`

Basic battle handling (v1 - just advance through):
```
1. If text displayed → Press A to advance
2. If menu displayed → Select first option (Fight)
3. If move selection → Select first move
4. Repeat until battle ends
```

### Step 2.2: Battle Detection Specifics

**Screen regions to analyze**:
- **Enemy HP Bar**: Top area, ~Y: 16-32, X: 96-152
- **Player HP Bar**: Bottom area, ~Y: 72-88, X: 96-152
- **Battle Menu**: Bottom box with "FIGHT/PKMN/ITEM/RUN"
- **Move Menu**: 4 move options in selection box

**Detection heuristics**:
- HP bars have specific green (#00FF00), yellow (#FFFF00), red (#FF0000) colors
- Battle background differs from overworld (usually solid color or pattern)
- Enemy Pokemon sprite in top-left area

### Step 2.3: Battle Action Logic

```
BattlePhase detection:
├── TEXT_DISPLAY → Press A
├── MAIN_MENU (Fight/PKMN/Item/Run visible) → Navigate to FIGHT, press A
├── MOVE_SELECTION (4 moves visible) → Press A on first move
├── ANIMATION → Wait
└── UNKNOWN → Press B (try to exit/cancel)
```

---

## Phase 3: Menu State Handling

**Goal**: Navigate and exit menus appropriately

### Step 3.1: Create Menu State Handler

**File**: `src/PokemonRedAI.Core/State/MenuStateHandler.cs`

Menu types to detect:
- **Start Menu**: POKEDEX, POKEMON, ITEM, etc.
- **Pokemon Menu**: Party Pokemon list
- **Item/Bag Menu**: Item listing
- **Options Menu**: Game settings

### Step 3.2: Menu Navigation Logic

For v1, simple approach - exit menus:
```
If in menu:
    Press B repeatedly to exit
    Maximum 5 B presses
    Check if returned to overworld
```

Future enhancement: Purposeful menu navigation (heal Pokemon, use items, etc.)

### Step 3.3: Selection Cursor Tracking

Detect ► cursor position:
- Scan left edge of menu options
- Track which option is highlighted
- Calculate cursor Y position to determine selection index

---

## Phase 4: Dialogue State Handling

**Goal**: Advance through dialogue and respond to prompts

### Step 4.1: Create Dialogue Handler

**File**: `src/PokemonRedAI.Core/State/DialogueStateHandler.cs`

### Step 4.2: Dialogue Detection

Detect text box:
- White rectangle at bottom of screen
- Contains text (non-uniform pixel pattern)
- May contain continue arrow (▼) or selection arrow (►)

### Step 4.3: Dialogue Actions

```
If dialogue detected:
├── If ▼ (continue arrow) visible → Press A
├── If ► (selection) visible →
│   ├── YES/NO prompt → Default to YES (first option), press A
│   └── Other selection → Press A on current selection
└── If text still rendering → Wait 100ms, then check again
```

### Step 4.4: Special Dialogue Handling

- **Saving dialogue**: Detect "Would you like to save?" → Handle appropriately
- **Healing dialogue**: Detect Pokemon Center nurse dialogue
- **Shop dialogue**: Detect buy/sell prompts

---

## Phase 5: Enhanced Walkability System

**Goal**: Implement map-aware walkability tracking with coordinates

### Step 5.1: Map Identification

**File**: `src/PokemonRedAI.Core/Learning/MapIdentifier.cs`

Identify current map using:
- Screen hash of static elements (borders, decorations)
- Tile patterns unique to each area
- Track map transitions (screen fade/scroll)

### Step 5.2: Coordinate-Based Walkability

**File**: `src/PokemonRedAI.Core/Learning/WalkabilityMap.cs`

Data structure:
```csharp
public class MapWalkability
{
    public string MapId { get; set; }
    public Dictionary<(int X, int Y), TileStatus> Tiles { get; set; }
}

public enum TileStatus
{
    Unknown,
    Walkable,
    Blocked,
    Conditional  // NPC blocking, etc.
}
```

### Step 5.3: Position Tracking

Track player position:
1. Detect player sprite on screen (center tile in most cases)
2. Track relative movement (+1/-1 in X or Y)
3. Maintain absolute coordinates per map
4. Reset coordinates on map transition

### Step 5.4: Update Persistence Format

**File**: `learned_data.json` structure:
```json
{
    "version": "2.0",
    "lastSaved": "2026-01-06T12:00:00Z",
    "maps": {
        "pallet_town_1": {
            "tiles": {
                "5,3": "WALKABLE",
                "5,4": "BLOCKED"
            },
            "exits": [
                {"x": 5, "y": 0, "destination": "route_1"}
            ]
        }
    },
    "tileHashes": {
        "12345678": {"isWalkable": true, "confidence": 0.95}
    }
}
```

---

## Phase 6: Integration & Polish

**Goal**: Connect all components and ensure smooth operation

### Step 6.1: Unified AI Controller

Refactor `AIController.cs` to:
```
MainLoop:
├── CaptureScreen()
├── DetectState() → GameStateType
├── switch (state):
│   ├── Overworld → OverworldHandler.Handle()
│   ├── Battle → BattleHandler.Handle()
│   ├── Menu → MenuHandler.Handle()
│   ├── Dialogue → DialogueHandler.Handle()
│   └── Unknown → DefaultHandler.Handle()
├── UpdateUI()
└── Delay()
```

### Step 6.2: Event System

Create event system for UI updates:
```csharp
public class AIEvents
{
    public event Action<GameStateType> OnStateChanged;
    public event Action<(int X, int Y)> OnPositionChanged;
    public event Action<string> OnActionPerformed;
    public event Action<Bitmap> OnScreenCaptured;
    public event Action<TileStatus> OnTileLearned;
}
```

### Step 6.3: Error Handling & Recovery

Implement robust error handling:
- Emulator disconnect detection
- Auto-reconnect logic
- Screen capture failure recovery
- Input timeout handling
- Stuck detection and recovery

### Step 6.4: Logging System

Enhance logging:
- File-based logging (Serilog to file)
- Log levels (Debug, Info, Warning, Error)
- Session logs with timestamps
- Performance metrics logging

### Step 6.5: WinForms UI Enhancements

Update `MainForm.cs` to display:
- Current game state indicator (Overworld/Battle/Menu/Dialogue)
- Player position coordinates
- Map name/identifier
- Enhanced statistics panel
- State transition history

---

## Phase 7: Testing & Validation

### Step 7.1: Unit Tests

**File**: `tests/PokemonRedAI.Tests/`

Test cases:
- State detection accuracy
- Walkability learning logic
- Input sending reliability
- Tile hash consistency
- Data persistence integrity

### Step 7.2: Integration Tests

- Full AI loop execution
- Emulator interaction
- State handler routing

### Step 7.3: Manual Testing Scenarios

| Scenario | Expected Behavior |
|----------|-------------------|
| Start in Pallet Town | AI explores, learns tiles, finds exits |
| Encounter wild Pokemon | AI detects battle, advances through it |
| Open Start Menu | AI detects menu, exits with B |
| Talk to NPC | AI advances dialogue, returns to overworld |
| Walk into wall | AI marks tile as blocked, tries different direction |
| Screen transition | AI handles map change, continues exploration |

---

## Implementation Priority Order

### Priority 1 (Critical Path)
1. Game State Detection (Phase 1.1-1.4)
2. Dialogue Handling (Phase 4) - Most common interruption
3. Menu Handling (Phase 3) - Accidental menu opens

### Priority 2 (Core Functionality)
4. Battle Handling (Phase 2) - Required for any gameplay
5. Enhanced Walkability (Phase 5.1-5.3) - Better navigation

### Priority 3 (Polish)
6. Integration (Phase 6) - Polish and reliability

### Priority 4 (Quality)
7. Testing (Phase 7) - Ensure stability

---

## Technical Notes

### Screen Analysis Performance

For real-time performance:
- Cache tile hashes (compute once per unique tile)
- Use unsafe bitmap access for pixel operations
- Limit screen capture to 4-10 FPS (sufficient for Game Boy speed)
- Use background threads for non-critical processing

### Memory Management

- Dispose Bitmap objects after use
- Use object pooling for frequently allocated objects
- Monitor memory usage in long sessions
- Implement periodic garbage collection hints

### Thread Safety

- Use locks for shared state access
- Use ConcurrentDictionary for tile database
- Use async/await for non-blocking operations
- Handle CancellationToken properly

---

## File Summary

### New Files to Create

| File | Purpose |
|------|---------|
| `Core/State/GameState.cs` | Game state enum and base classes |
| `Core/State/BattleStateHandler.cs` | Battle handling logic |
| `Core/State/MenuStateHandler.cs` | Menu handling logic |
| `Core/State/DialogueStateHandler.cs` | Dialogue handling logic |
| `Core/ScreenReader/StateDetector.cs` | State detection from screen |
| `Core/ScreenReader/IndicatorDetector.cs` | Arrow/prompt detection |
| `Core/Learning/MapIdentifier.cs` | Map identification |
| `Core/Learning/WalkabilityMap.cs` | Coordinate-based walkability |
| `Core/Events/AIEvents.cs` | Event system for UI updates |
| `Core/Configuration/AISettings.cs` | Centralized configuration |

### Files to Modify

| File | Changes |
|------|---------|
| `WinForms/AIController.cs` | Integrate state detection, add event system |
| `WinForms/MainForm.cs` | Add state display, improve UI |
| `Core/TileDatabase.cs` | Add map-aware storage |
| `Core/RoomAnalyzer.cs` | Enhance for state detection |

---

## Estimated Complexity

| Phase | Complexity | Key Challenge |
|-------|------------|---------------|
| Phase 1 | Medium | Accurate state detection patterns |
| Phase 2 | Medium | Battle menu navigation |
| Phase 3 | Low | Simple B-button exit |
| Phase 4 | Medium | Text rendering detection |
| Phase 5 | High | Map identification without memory reading |
| Phase 6 | Medium | Component integration |
| Phase 7 | Low | Standard testing |

---

## Success Metrics

After implementation, the AI should:

1. **State Detection**: >95% accuracy in identifying game state
2. **Battle Completion**: Successfully complete wild Pokemon battles
3. **Menu Escape**: Exit menus within 5 button presses
4. **Dialogue Advancement**: Never get stuck in dialogue
5. **Navigation**: Explore continuously without getting permanently stuck
6. **Persistence**: No data loss between sessions

---

## Next Steps

To begin implementation:

1. Start with Phase 1.1 - Create the `GameState` enum and infrastructure
2. Implement basic state detection in Phase 1.2
3. Test with live emulator to tune detection patterns
4. Proceed through phases in priority order

The WinForms UI will be used as the primary interface for monitoring and controlling the AI.
