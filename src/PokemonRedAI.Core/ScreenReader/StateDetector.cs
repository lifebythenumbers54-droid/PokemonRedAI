using PokemonRedAI.Core.State;

namespace PokemonRedAI.Core.ScreenReader;

public class StateDetector
{
    // Pokemon Red color palette (approximate)
    private static readonly ScreenPixel White = new(248, 248, 248);
    private static readonly ScreenPixel LightGray = new(168, 168, 168);
    private static readonly ScreenPixel DarkGray = new(88, 88, 88);
    private static readonly ScreenPixel Black = new(0, 0, 0);

    // Screen regions (in 8x8 tile coordinates)
    private const int TileSize = 8;
    private const int ScreenWidthTiles = 20;
    private const int ScreenHeightTiles = 18;

    // Battle UI detection regions
    private const int BattleHpBarY = 2;
    private const int BattleMenuY = 12;

    // Text box region
    private const int TextBoxStartY = 12;
    private const int TextBoxHeight = 6;

    private GameState? _previousState;

    public GameState DetectState(ScreenPixel[,] screen)
    {
        var state = new GameState();

        // Check for black screen first
        if (IsBlackScreen(screen))
        {
            state.Type = GameStateType.BlackScreen;
            return state;
        }

        // Check for battle
        if (IsBattleScreen(screen))
        {
            state.Type = GameStateType.Battle;
            state.BattlePhase = DetectBattlePhase(screen);
        }
        // Check for menu
        else if (IsMenuScreen(screen, out var menuType))
        {
            state.Type = GameStateType.Menu;
            state.MenuType = menuType;
        }
        // Check for dialogue
        else if (HasTextBox(screen))
        {
            state.Type = GameStateType.Dialogue;
            state.HasDialogue = true;
        }
        // Default to overworld
        else
        {
            state.Type = GameStateType.Overworld;
        }

        // Detect UI indicators
        state.HasContinueArrow = DetectContinueArrow(screen);
        state.HasSelectionArrow = DetectSelectionArrow(screen, out int selectionIndex);
        state.SelectionIndex = selectionIndex;

        // Detect walking (compare with previous frame)
        if (_previousState != null && state.Type == GameStateType.Overworld)
        {
            state.IsWalking = DetectWalking(screen);
        }

        _previousState = state;
        return state;
    }

    private bool IsBlackScreen(ScreenPixel[,] screen)
    {
        int blackCount = 0;
        int totalPixels = screen.GetLength(0) * screen.GetLength(1);
        int sampleSize = 100;

        var random = new Random(42); // Fixed seed for consistency
        for (int i = 0; i < sampleSize; i++)
        {
            int x = random.Next(screen.GetLength(0));
            int y = random.Next(screen.GetLength(1));
            if (screen[x, y].IsBlack)
                blackCount++;
        }

        return blackCount > sampleSize * 0.9;
    }

    private bool IsBattleScreen(ScreenPixel[,] screen)
    {
        // Check for HP bar region characteristics
        // Battle screens have HP bars in specific locations

        // Check top-left region for enemy HP bar area
        int whiteCount = 0;
        for (int x = 0; x < 80; x++)
        {
            for (int y = 16; y < 32; y++)
            {
                if (screen[x, y].Matches(White, 20))
                    whiteCount++;
            }
        }

        // Check for battle menu box at bottom
        bool hasBottomMenu = HasMenuBox(screen, 0, 96, 160, 48);

        // Battle screens typically have white regions for HP bars and text boxes
        return whiteCount > 200 && hasBottomMenu;
    }

    private BattlePhase DetectBattlePhase(ScreenPixel[,] screen)
    {
        // Check for "What will X do?" text area
        if (HasTextBox(screen))
        {
            // Check if there's a selection menu visible
            if (DetectSelectionArrow(screen, out _))
            {
                // Check position of menu to determine type
                if (HasMenuBox(screen, 80, 96, 80, 48))
                    return BattlePhase.ActionSelection;
                else if (HasMenuBox(screen, 0, 48, 160, 96))
                    return BattlePhase.MoveSelection;
            }
            return BattlePhase.Text;
        }

        return BattlePhase.Animation;
    }

    private bool IsMenuScreen(ScreenPixel[,] screen, out MenuType menuType)
    {
        menuType = MenuType.None;

        // Start menu appears on the right side of the screen
        if (HasMenuBox(screen, 104, 0, 56, 120))
        {
            menuType = MenuType.StartMenu;
            return true;
        }

        // Full screen menu (bag, pokemon, etc.)
        if (HasMenuBox(screen, 0, 0, 160, 144))
        {
            // Would need more specific detection for each menu type
            menuType = MenuType.Bag;
            return true;
        }

        // Yes/No dialog box
        if (HasMenuBox(screen, 104, 64, 48, 40))
        {
            menuType = MenuType.YesNo;
            return true;
        }

        return false;
    }

    private bool HasTextBox(ScreenPixel[,] screen)
    {
        // Text boxes in Pokemon Red have a specific border pattern
        // Check bottom portion of screen for text box characteristics
        int y = TextBoxStartY * TileSize;
        int borderPixels = 0;

        // Check for border pattern at text box location
        for (int x = 0; x < 160; x++)
        {
            if (screen[x, y].Matches(Black, 10) || screen[x, y].Matches(DarkGray, 20))
                borderPixels++;
        }

        // If we have a horizontal line of dark pixels, likely a text box border
        return borderPixels > 100;
    }

    private bool HasMenuBox(ScreenPixel[,] screen, int startX, int startY, int width, int height)
    {
        // Check for menu box border pattern
        int borderCount = 0;
        int whiteCount = 0;

        // Sample the border region
        for (int x = startX; x < Math.Min(startX + width, 160); x += 4)
        {
            if (startY < 144 && screen[x, startY].Matches(Black, 20))
                borderCount++;
            int bottomY = Math.Min(startY + height - 1, 143);
            if (screen[x, bottomY].Matches(Black, 20))
                borderCount++;
        }

        // Check interior for white background
        for (int x = startX + 8; x < Math.Min(startX + width - 8, 160); x += 8)
        {
            for (int y = startY + 8; y < Math.Min(startY + height - 8, 144); y += 8)
            {
                if (screen[x, y].Matches(White, 30))
                    whiteCount++;
            }
        }

        return borderCount > 5 && whiteCount > 3;
    }

    private bool DetectContinueArrow(ScreenPixel[,] screen)
    {
        // Continue arrow (down arrow) appears at bottom right of text box
        // Located around pixel (152, 136) in standard text boxes

        int arrowX = 152;
        int arrowY = 136;

        if (arrowX >= 160 || arrowY >= 144)
            return false;

        // Check for arrow-shaped dark pixels
        // The arrow is typically a small triangular shape pointing down
        int darkPixels = 0;
        for (int dx = -4; dx <= 4; dx++)
        {
            for (int dy = -4; dy <= 4; dy++)
            {
                int x = arrowX + dx;
                int y = arrowY + dy;
                if (x >= 0 && x < 160 && y >= 0 && y < 144)
                {
                    if (screen[x, y].Matches(Black, 20))
                        darkPixels++;
                }
            }
        }

        // Arrow shape should have a moderate number of dark pixels
        return darkPixels >= 8 && darkPixels <= 30;
    }

    private bool DetectSelectionArrow(ScreenPixel[,] screen, out int selectionIndex)
    {
        selectionIndex = 0;

        // Selection arrow (right-pointing) appears on the left side of menu options
        // Check common menu locations

        int[] menuYPositions = { 16, 32, 48, 64, 80, 96, 112, 104, 120 };
        int arrowX = 8;

        foreach (int menuY in menuYPositions)
        {
            if (menuY >= 144) continue;

            // Check for right-pointing arrow pattern
            int darkPixels = 0;
            for (int dx = 0; dx < 8; dx++)
            {
                for (int dy = -3; dy <= 3; dy++)
                {
                    int x = arrowX + dx;
                    int y = menuY + dy;
                    if (x >= 0 && x < 160 && y >= 0 && y < 144)
                    {
                        if (screen[x, y].Matches(Black, 20))
                            darkPixels++;
                    }
                }
            }

            if (darkPixels >= 5 && darkPixels <= 20)
            {
                selectionIndex = Array.IndexOf(menuYPositions, menuY);
                return true;
            }
        }

        return false;
    }

    private bool DetectWalking(ScreenPixel[,] screen)
    {
        // Walking detection would compare current frame to previous
        // Look for player sprite animation changes
        // This is a simplified implementation
        return false;
    }
}
