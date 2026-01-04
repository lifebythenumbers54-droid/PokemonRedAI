namespace PokemonRedAI.Core.State;

public enum GameStateType
{
    Unknown,
    Overworld,
    Battle,
    Menu,
    Dialogue,
    BlackScreen,
    TitleScreen
}

public enum BattlePhase
{
    None,
    ActionSelection,
    MoveSelection,
    ItemSelection,
    PokemonSelection,
    Animation,
    Text
}

public enum MenuType
{
    None,
    StartMenu,
    Bag,
    Pokemon,
    Save,
    Options,
    Pokedex,
    Shop,
    PC,
    YesNo
}

public class GameState
{
    public GameStateType Type { get; set; } = GameStateType.Unknown;
    public BattlePhase BattlePhase { get; set; } = BattlePhase.None;
    public MenuType MenuType { get; set; } = MenuType.None;
    public bool IsWalking { get; set; }
    public bool HasDialogue { get; set; }
    public bool HasContinueArrow { get; set; }
    public bool HasSelectionArrow { get; set; }
    public int SelectionIndex { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public bool RequiresInput => HasContinueArrow || HasSelectionArrow ||
                                  Type == GameStateType.Menu ||
                                  BattlePhase != BattlePhase.None;
}
