namespace PokemonRedAI.Core.ScreenReader;

public class UIIndicatorDetector
{
    private static readonly ScreenPixel Black = new(0, 0, 0);
    private static readonly ScreenPixel White = new(248, 248, 248);

    // Down arrow template (simplified 7x7 pattern)
    // The continue arrow in Pokemon Red is a small downward triangle
    private static readonly bool[,] DownArrowTemplate = new bool[,]
    {
        { false, false, true, true, true, false, false },
        { false, false, true, true, true, false, false },
        { false, true, true, true, true, true, false },
        { false, true, true, true, true, true, false },
        { true, true, true, true, true, true, true },
        { false, true, true, true, true, true, false },
        { false, false, true, true, true, false, false }
    };

    // Right arrow template (simplified 7x7 pattern)
    private static readonly bool[,] RightArrowTemplate = new bool[,]
    {
        { false, false, true, false, false, false, false },
        { false, false, true, true, false, false, false },
        { true, true, true, true, true, false, false },
        { true, true, true, true, true, true, true },
        { true, true, true, true, true, false, false },
        { false, false, true, true, false, false, false },
        { false, false, true, false, false, false, false }
    };

    private ScreenPixel[,]? _previousFrame;
    private bool _lastArrowState;
    private int _blinkCounter;

    public UIIndicatorResult Detect(ScreenPixel[,] screen)
    {
        var result = new UIIndicatorResult();

        // Detect continue arrow (blinking down arrow)
        result.ContinueArrow = DetectContinueArrow(screen);
        result.ContinueArrowBlinking = DetectArrowBlinking(result.ContinueArrow);

        // Detect selection arrows in various menu positions
        result.SelectionArrows = DetectSelectionArrows(screen);

        // Detect text box presence
        result.HasTextBox = DetectTextBox(screen);

        // Detect Yes/No prompt
        result.HasYesNoPrompt = DetectYesNoPrompt(screen);

        _previousFrame = screen;
        return result;
    }

    private ArrowIndicator? DetectContinueArrow(ScreenPixel[,] screen)
    {
        // Common positions for continue arrow in Pokemon Red
        var positions = new (int x, int y)[]
        {
            (152, 136), // Standard text box
            (152, 128), // Battle text
            (144, 136), // Alternative position
        };

        foreach (var (x, y) in positions)
        {
            if (MatchesArrowTemplate(screen, x, y, DownArrowTemplate))
            {
                return new ArrowIndicator
                {
                    Type = ArrowType.Down,
                    X = x,
                    Y = y,
                    IsBlinking = false
                };
            }
        }

        return null;
    }

    private bool DetectArrowBlinking(ArrowIndicator? currentArrow)
    {
        if (_previousFrame == null)
            return false;

        bool currentVisible = currentArrow != null;

        if (currentVisible != _lastArrowState)
        {
            _blinkCounter++;
            _lastArrowState = currentVisible;
        }

        // If we've seen it toggle a few times, it's blinking
        return _blinkCounter >= 2;
    }

    private List<ArrowIndicator> DetectSelectionArrows(ScreenPixel[,] screen)
    {
        var arrows = new List<ArrowIndicator>();

        // Menu column positions where selection arrows appear
        int[] xPositions = { 8, 16, 88, 96, 104, 112 };

        // Row positions in 8-pixel increments (menu items)
        for (int menuY = 8; menuY < 136; menuY += 16)
        {
            foreach (int menuX in xPositions)
            {
                if (MatchesArrowTemplate(screen, menuX, menuY, RightArrowTemplate))
                {
                    arrows.Add(new ArrowIndicator
                    {
                        Type = ArrowType.Right,
                        X = menuX,
                        Y = menuY,
                        MenuIndex = (menuY - 8) / 16
                    });
                }
            }
        }

        return arrows;
    }

    private bool MatchesArrowTemplate(ScreenPixel[,] screen, int centerX, int centerY, bool[,] template)
    {
        int templateWidth = template.GetLength(1);
        int templateHeight = template.GetLength(0);
        int halfWidth = templateWidth / 2;
        int halfHeight = templateHeight / 2;

        int matchCount = 0;
        int expectedDark = 0;

        for (int ty = 0; ty < templateHeight; ty++)
        {
            for (int tx = 0; tx < templateWidth; tx++)
            {
                int screenX = centerX - halfWidth + tx;
                int screenY = centerY - halfHeight + ty;

                if (screenX < 0 || screenX >= 160 || screenY < 0 || screenY >= 144)
                    continue;

                if (template[ty, tx])
                {
                    expectedDark++;
                    if (screen[screenX, screenY].Matches(Black, 30))
                        matchCount++;
                }
            }
        }

        // Require at least 60% match for arrow detection
        return expectedDark > 0 && (float)matchCount / expectedDark >= 0.6f;
    }

    private bool DetectTextBox(ScreenPixel[,] screen)
    {
        // Text boxes have a characteristic border at y=96 (tile row 12)
        // and fill the width of the screen

        int borderY = 96;
        int darkPixelCount = 0;

        // Check for horizontal border line
        for (int x = 4; x < 156; x++)
        {
            if (screen[x, borderY].Matches(Black, 20))
                darkPixelCount++;
        }

        // Should have most of the width as dark pixels for border
        if (darkPixelCount < 100)
            return false;

        // Check for white interior below border
        int whiteCount = 0;
        for (int x = 8; x < 152; x += 8)
        {
            for (int y = 104; y < 136; y += 8)
            {
                if (screen[x, y].Matches(White, 30))
                    whiteCount++;
            }
        }

        return whiteCount > 10;
    }

    private bool DetectYesNoPrompt(ScreenPixel[,] screen)
    {
        // Yes/No box appears in the right portion of the screen
        // Typically around x=104-152, y=64-104

        int boxX = 104;
        int boxY = 64;

        // Check for small menu box border
        int borderCount = 0;
        for (int x = boxX; x < 152; x++)
        {
            if (screen[x, boxY].Matches(Black, 20))
                borderCount++;
            if (screen[x, boxY + 38].Matches(Black, 20))
                borderCount++;
        }

        return borderCount > 60;
    }
}

public class UIIndicatorResult
{
    public ArrowIndicator? ContinueArrow { get; set; }
    public bool ContinueArrowBlinking { get; set; }
    public List<ArrowIndicator> SelectionArrows { get; set; } = new();
    public bool HasTextBox { get; set; }
    public bool HasYesNoPrompt { get; set; }

    public bool HasContinuePrompt => ContinueArrow != null;
    public bool HasSelection => SelectionArrows.Count > 0;
    public int CurrentSelectionIndex => SelectionArrows.FirstOrDefault()?.MenuIndex ?? -1;
}

public class ArrowIndicator
{
    public ArrowType Type { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int MenuIndex { get; set; }
    public bool IsBlinking { get; set; }
}

public enum ArrowType
{
    Down,   // Continue/scroll indicator
    Right,  // Selection cursor
    Up      // Scroll up indicator
}
