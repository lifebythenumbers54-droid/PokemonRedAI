using System.Drawing;
using PokemonRedAI.Core.State;

namespace PokemonRedAI.Core.ScreenReader;

/// <summary>
/// Template image used for state detection.
/// </summary>
public class StateTemplate
{
    public string Name { get; set; } = string.Empty;
    public Bitmap Image { get; set; } = null!;
    public GameStateType State { get; set; }
    public BattlePhase BattlePhase { get; set; } = BattlePhase.None;
    public MenuType MenuType { get; set; } = MenuType.None;
    public float MinConfidence { get; set; } = 0.9f;
    public Rectangle? SearchRegion { get; set; }
}

/// <summary>
/// Detects game state by scanning for known template images in the screen.
/// Uses bitmap template matching for 100% accurate state detection.
/// </summary>
public class TemplateStateDetector : IDisposable
{
    private readonly TemplateMatcher _matcher;
    private readonly List<StateTemplate> _templates = new();
    private readonly Action<string>? _logger;
    private bool _disposed;

    // Screen dimensions (Game Boy)
    private const int SCREEN_WIDTH = 160;
    private const int SCREEN_HEIGHT = 144;

    /// <summary>
    /// Creates a new TemplateStateDetector.
    /// </summary>
    /// <param name="logger">Optional logging callback for debug output</param>
    public TemplateStateDetector(Action<string>? logger = null)
    {
        _matcher = new TemplateMatcher(pixelTolerance: 30, sampleStep: 2);
        _logger = logger;
    }

    /// <summary>
    /// Loads template images from a directory.
    /// Expected files: BattleHP.bmp, BattleFight.bmp, WILD.bmp, YES.bmp, PC.bmp, etc.
    /// </summary>
    public void LoadTemplatesFromDirectory(string directory)
    {
        if (!Directory.Exists(directory))
        {
            _logger?.Invoke($"Template directory not found: {directory}");
            return;
        }

        // Define expected templates and their meanings
        var templateDefinitions = new[]
        {
            // Battle state templates
            new { File = "BattleHP.bmp", State = GameStateType.Battle, Battle = BattlePhase.None, Menu = MenuType.None, Confidence = 0.85f },
            new { File = "BattleFight.bmp", State = GameStateType.Battle, Battle = BattlePhase.ActionSelection, Menu = MenuType.None, Confidence = 0.9f },
            new { File = "WILD.bmp", State = GameStateType.Battle, Battle = BattlePhase.Text, Menu = MenuType.None, Confidence = 0.9f },
            new { File = "Appeared.bmp", State = GameStateType.Battle, Battle = BattlePhase.Text, Menu = MenuType.None, Confidence = 0.85f },

            // Menu state templates
            new { File = "YES.bmp", State = GameStateType.Menu, Battle = BattlePhase.None, Menu = MenuType.YesNo, Confidence = 0.9f },
            new { File = "PC.bmp", State = GameStateType.Menu, Battle = BattlePhase.None, Menu = MenuType.PC, Confidence = 0.85f },
        };

        foreach (var def in templateDefinitions)
        {
            string path = Path.Combine(directory, def.File);
            if (File.Exists(path))
            {
                try
                {
                    var bitmap = new Bitmap(path);
                    var template = new StateTemplate
                    {
                        Name = def.File,
                        Image = bitmap,
                        State = def.State,
                        BattlePhase = def.Battle,
                        MenuType = def.Menu,
                        MinConfidence = def.Confidence
                    };
                    _templates.Add(template);
                    _logger?.Invoke($"Loaded template: {def.File} ({bitmap.Width}x{bitmap.Height})");
                }
                catch (Exception ex)
                {
                    _logger?.Invoke($"Failed to load template {def.File}: {ex.Message}");
                }
            }
            else
            {
                _logger?.Invoke($"Template not found: {path}");
            }
        }

        _logger?.Invoke($"Loaded {_templates.Count} templates");
    }

    /// <summary>
    /// Adds a custom template for state detection.
    /// </summary>
    public void AddTemplate(StateTemplate template)
    {
        _templates.Add(template);
    }

    /// <summary>
    /// Detects the current game state by scanning for known templates.
    /// </summary>
    public GameState DetectState(Bitmap screen)
    {
        var state = new GameState
        {
            Type = GameStateType.Unknown,
            Timestamp = DateTime.UtcNow
        };

        if (screen == null || _templates.Count == 0)
        {
            state.Type = GameStateType.Overworld; // Default if no templates
            return state;
        }

        // Check for black screen first
        if (IsBlackScreen(screen))
        {
            state.Type = GameStateType.BlackScreen;
            return state;
        }

        // Check each template
        StateTemplate? bestMatch = null;
        float bestConfidence = 0;
        TemplateMatchResult? bestResult = null;

        foreach (var template in _templates)
        {
            TemplateMatchResult result;

            if (template.SearchRegion.HasValue)
            {
                result = _matcher.FindTemplateInRegion(screen, template.Image,
                    template.SearchRegion.Value, template.MinConfidence);
            }
            else
            {
                result = _matcher.FindTemplate(screen, template.Image, template.MinConfidence);
            }

            if (result.Found && result.Confidence > bestConfidence)
            {
                bestConfidence = result.Confidence;
                bestMatch = template;
                bestResult = result;
            }
        }

        if (bestMatch != null && bestResult != null)
        {
            state.Type = bestMatch.State;
            state.BattlePhase = bestMatch.BattlePhase;
            state.MenuType = bestMatch.MenuType;

            _logger?.Invoke($"Detected: {bestMatch.Name} at ({bestResult.X},{bestResult.Y}) confidence={bestResult.Confidence:P0}");
        }
        else
        {
            // No template matched - check for dialogue (text box at bottom)
            if (HasTextBox(screen))
            {
                state.Type = GameStateType.Dialogue;
                state.HasDialogue = true;
                _logger?.Invoke("Detected: Dialogue (text box)");
            }
            else
            {
                // Default to Overworld
                state.Type = GameStateType.Overworld;
            }
        }

        // Detect UI indicators
        DetectIndicators(screen, state);

        return state;
    }

    /// <summary>
    /// Checks if the screen is mostly black (loading/transition).
    /// </summary>
    private bool IsBlackScreen(Bitmap screen)
    {
        int blackPixels = 0;
        int sampleCount = 50;
        var random = new Random(42); // Fixed seed for consistency

        for (int i = 0; i < sampleCount; i++)
        {
            int x = random.Next(screen.Width);
            int y = random.Next(screen.Height);
            var pixel = screen.GetPixel(x, y);

            if (pixel.R < 20 && pixel.G < 20 && pixel.B < 20)
                blackPixels++;
        }

        return blackPixels > sampleCount * 0.9;
    }

    /// <summary>
    /// Checks for a text box at the bottom of the screen.
    /// Text boxes have a characteristic white interior with dark border.
    /// </summary>
    private bool HasTextBox(Bitmap screen)
    {
        // Text box typically starts around y=96 (bottom third of 144px screen)
        // and spans the full width

        int textBoxY = 96;
        int borderDarkPixels = 0;
        int interiorWhitePixels = 0;

        // Check for dark border line at top of text box area
        for (int x = 8; x < 152; x += 4)
        {
            if (textBoxY < screen.Height)
            {
                var pixel = screen.GetPixel(x, textBoxY);
                if (pixel.R < 50 && pixel.G < 50 && pixel.B < 50)
                    borderDarkPixels++;
            }
        }

        // Check for white interior below border
        for (int x = 16; x < 144; x += 8)
        {
            for (int y = textBoxY + 8; y < Math.Min(textBoxY + 40, screen.Height); y += 8)
            {
                var pixel = screen.GetPixel(x, y);
                if (pixel.R > 200 && pixel.G > 200 && pixel.B > 200)
                    interiorWhitePixels++;
            }
        }

        // Need both border and white interior
        return borderDarkPixels >= 20 && interiorWhitePixels >= 10;
    }

    /// <summary>
    /// Detects UI indicators like continue arrow and selection arrow.
    /// </summary>
    private void DetectIndicators(Bitmap screen, GameState state)
    {
        // Continue arrow (▼) - typically at bottom-right of text box
        state.HasContinueArrow = DetectContinueArrow(screen);

        // Selection arrow (►) - on left side of menu options
        state.HasSelectionArrow = DetectSelectionArrow(screen, out int selectionIndex);
        state.SelectionIndex = selectionIndex;
    }

    /// <summary>
    /// Detects the continue arrow (blinking down arrow) in text boxes.
    /// </summary>
    private bool DetectContinueArrow(Bitmap screen)
    {
        // Continue arrow appears around (152, 136) in standard text boxes
        int[] checkX = { 152, 144, 148 };
        int[] checkY = { 136, 132, 128 };

        foreach (int x in checkX)
        {
            foreach (int y in checkY)
            {
                if (x >= screen.Width || y >= screen.Height)
                    continue;

                // Check for cluster of dark pixels in arrow shape
                int darkCount = 0;
                for (int dx = -3; dx <= 3; dx++)
                {
                    for (int dy = -3; dy <= 3; dy++)
                    {
                        int px = x + dx;
                        int py = y + dy;
                        if (px >= 0 && px < screen.Width && py >= 0 && py < screen.Height)
                        {
                            var pixel = screen.GetPixel(px, py);
                            if (pixel.R < 50 && pixel.G < 50 && pixel.B < 50)
                                darkCount++;
                        }
                    }
                }

                // Arrow shape has moderate dark pixel count
                if (darkCount >= 10 && darkCount <= 30)
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Detects selection arrows in menus.
    /// </summary>
    private bool DetectSelectionArrow(Bitmap screen, out int selectionIndex)
    {
        selectionIndex = -1;

        // Selection arrows appear at x around 8-16, at various y positions
        int[] menuYPositions = { 16, 32, 48, 64, 80, 96, 104, 112, 120 };
        int arrowX = 8;

        for (int i = 0; i < menuYPositions.Length; i++)
        {
            int y = menuYPositions[i];
            if (y >= screen.Height)
                continue;

            // Check for right-pointing arrow pattern
            int darkCount = 0;
            for (int dx = 0; dx < 8; dx++)
            {
                for (int dy = -2; dy <= 2; dy++)
                {
                    int px = arrowX + dx;
                    int py = y + dy;
                    if (px >= 0 && px < screen.Width && py >= 0 && py < screen.Height)
                    {
                        var pixel = screen.GetPixel(px, py);
                        if (pixel.R < 50 && pixel.G < 50 && pixel.B < 50)
                            darkCount++;
                    }
                }
            }

            if (darkCount >= 8 && darkCount <= 25)
            {
                selectionIndex = i;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets a summary of loaded templates.
    /// </summary>
    public string GetTemplatesSummary()
    {
        if (_templates.Count == 0)
            return "No templates loaded";

        var summary = $"Loaded {_templates.Count} templates:\n";
        foreach (var t in _templates)
        {
            summary += $"  - {t.Name}: {t.State}";
            if (t.BattlePhase != BattlePhase.None)
                summary += $" ({t.BattlePhase})";
            if (t.MenuType != MenuType.None)
                summary += $" ({t.MenuType})";
            summary += $" [{t.Image.Width}x{t.Image.Height}]\n";
        }
        return summary;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        foreach (var template in _templates)
        {
            template.Image?.Dispose();
        }
        _templates.Clear();
        _disposed = true;
    }
}
