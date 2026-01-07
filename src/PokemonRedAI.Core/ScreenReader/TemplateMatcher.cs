using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace PokemonRedAI.Core.ScreenReader;

/// <summary>
/// Result of a template matching operation.
/// </summary>
public class TemplateMatchResult
{
    public bool Found { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public float Confidence { get; set; }

    public static TemplateMatchResult NotFound => new() { Found = false, Confidence = 0 };
}

/// <summary>
/// Provides template matching functionality to find smaller images within larger images.
/// Used for detecting game state by finding known UI elements.
/// </summary>
public class TemplateMatcher
{
    private readonly int _pixelTolerance;
    private readonly int _sampleStep;

    /// <summary>
    /// Creates a new TemplateMatcher.
    /// </summary>
    /// <param name="pixelTolerance">Color difference tolerance (0-255) for pixel matching</param>
    /// <param name="sampleStep">Step size for initial quick scan (1 = check every pixel, higher = faster but may miss)</param>
    public TemplateMatcher(int pixelTolerance = 30, int sampleStep = 2)
    {
        _pixelTolerance = pixelTolerance;
        _sampleStep = Math.Max(1, sampleStep);
    }

    /// <summary>
    /// Searches for a template bitmap within a larger screen bitmap.
    /// Returns the best match location and confidence (0.0 - 1.0).
    /// </summary>
    public TemplateMatchResult FindTemplate(Bitmap screen, Bitmap template, float minConfidence = 0.9f)
    {
        if (screen == null || template == null)
            return TemplateMatchResult.NotFound;

        if (template.Width > screen.Width || template.Height > screen.Height)
            return TemplateMatchResult.NotFound;

        // Get pixel data for both images
        var screenPixels = GetPixelData(screen);
        var templatePixels = GetPixelData(template);

        int screenWidth = screen.Width;
        int screenHeight = screen.Height;
        int templateWidth = template.Width;
        int templateHeight = template.Height;

        TemplateMatchResult bestMatch = TemplateMatchResult.NotFound;
        float bestConfidence = 0;

        // Slide template across screen
        int maxX = screenWidth - templateWidth;
        int maxY = screenHeight - templateHeight;

        for (int y = 0; y <= maxY; y += _sampleStep)
        {
            for (int x = 0; x <= maxX; x += _sampleStep)
            {
                float confidence = CalculateMatchConfidence(
                    screenPixels, screenWidth,
                    templatePixels, templateWidth, templateHeight,
                    x, y);

                if (confidence >= minConfidence && confidence > bestConfidence)
                {
                    bestConfidence = confidence;
                    bestMatch = new TemplateMatchResult
                    {
                        Found = true,
                        X = x,
                        Y = y,
                        Confidence = confidence
                    };

                    // Early exit if we found a very high confidence match
                    if (confidence >= 0.98f)
                        return bestMatch;
                }
            }
        }

        // If we found a promising match with sampling, refine it
        if (bestMatch.Found && _sampleStep > 1)
        {
            bestMatch = RefineMatch(screenPixels, screenWidth, screenHeight,
                templatePixels, templateWidth, templateHeight,
                bestMatch.X, bestMatch.Y, minConfidence);
        }

        return bestMatch;
    }

    /// <summary>
    /// Quick check if template exists anywhere in screen.
    /// More efficient than FindTemplate when location doesn't matter.
    /// </summary>
    public bool ContainsTemplate(Bitmap screen, Bitmap template, float minConfidence = 0.9f)
    {
        return FindTemplate(screen, template, minConfidence).Found;
    }

    /// <summary>
    /// Searches for a template only within a specific region of the screen.
    /// More efficient when you know approximately where to look.
    /// </summary>
    public TemplateMatchResult FindTemplateInRegion(
        Bitmap screen, Bitmap template,
        Rectangle searchRegion, float minConfidence = 0.9f)
    {
        if (screen == null || template == null)
            return TemplateMatchResult.NotFound;

        // Clamp search region to screen bounds
        int startX = Math.Max(0, searchRegion.X);
        int startY = Math.Max(0, searchRegion.Y);
        int endX = Math.Min(screen.Width - template.Width, searchRegion.Right - template.Width);
        int endY = Math.Min(screen.Height - template.Height, searchRegion.Bottom - template.Height);

        if (endX < startX || endY < startY)
            return TemplateMatchResult.NotFound;

        var screenPixels = GetPixelData(screen);
        var templatePixels = GetPixelData(template);

        int screenWidth = screen.Width;
        int templateWidth = template.Width;
        int templateHeight = template.Height;

        TemplateMatchResult bestMatch = TemplateMatchResult.NotFound;
        float bestConfidence = 0;

        for (int y = startY; y <= endY; y += _sampleStep)
        {
            for (int x = startX; x <= endX; x += _sampleStep)
            {
                float confidence = CalculateMatchConfidence(
                    screenPixels, screenWidth,
                    templatePixels, templateWidth, templateHeight,
                    x, y);

                if (confidence >= minConfidence && confidence > bestConfidence)
                {
                    bestConfidence = confidence;
                    bestMatch = new TemplateMatchResult
                    {
                        Found = true,
                        X = x,
                        Y = y,
                        Confidence = confidence
                    };

                    if (confidence >= 0.98f)
                        return bestMatch;
                }
            }
        }

        return bestMatch;
    }

    /// <summary>
    /// Calculates how well the template matches at a specific position.
    /// Returns confidence from 0.0 (no match) to 1.0 (perfect match).
    /// </summary>
    private float CalculateMatchConfidence(
        byte[] screenPixels, int screenWidth,
        byte[] templatePixels, int templateWidth, int templateHeight,
        int offsetX, int offsetY)
    {
        int matchingPixels = 0;
        int totalPixels = 0;

        // Sample pixels from template (skip some for speed)
        int sampleRate = Math.Max(1, Math.Min(templateWidth, templateHeight) / 16);

        for (int ty = 0; ty < templateHeight; ty += sampleRate)
        {
            for (int tx = 0; tx < templateWidth; tx += sampleRate)
            {
                int templateIdx = (ty * templateWidth + tx) * 3;
                int screenIdx = ((offsetY + ty) * screenWidth + (offsetX + tx)) * 3;

                byte tr = templatePixels[templateIdx];
                byte tg = templatePixels[templateIdx + 1];
                byte tb = templatePixels[templateIdx + 2];

                byte sr = screenPixels[screenIdx];
                byte sg = screenPixels[screenIdx + 1];
                byte sb = screenPixels[screenIdx + 2];

                if (Math.Abs(tr - sr) <= _pixelTolerance &&
                    Math.Abs(tg - sg) <= _pixelTolerance &&
                    Math.Abs(tb - sb) <= _pixelTolerance)
                {
                    matchingPixels++;
                }

                totalPixels++;
            }
        }

        return totalPixels > 0 ? (float)matchingPixels / totalPixels : 0;
    }

    /// <summary>
    /// Refines a match by checking neighboring positions with step size 1.
    /// </summary>
    private TemplateMatchResult RefineMatch(
        byte[] screenPixels, int screenWidth, int screenHeight,
        byte[] templatePixels, int templateWidth, int templateHeight,
        int initialX, int initialY, float minConfidence)
    {
        TemplateMatchResult bestMatch = new()
        {
            Found = true,
            X = initialX,
            Y = initialY,
            Confidence = CalculateMatchConfidence(screenPixels, screenWidth,
                templatePixels, templateWidth, templateHeight, initialX, initialY)
        };

        // Check positions around the initial match
        int searchRadius = _sampleStep;
        for (int dy = -searchRadius; dy <= searchRadius; dy++)
        {
            for (int dx = -searchRadius; dx <= searchRadius; dx++)
            {
                int x = initialX + dx;
                int y = initialY + dy;

                if (x < 0 || y < 0 ||
                    x > screenWidth - templateWidth ||
                    y > screenHeight - templateHeight)
                    continue;

                float confidence = CalculateMatchConfidence(
                    screenPixels, screenWidth,
                    templatePixels, templateWidth, templateHeight,
                    x, y);

                if (confidence > bestMatch.Confidence)
                {
                    bestMatch.X = x;
                    bestMatch.Y = y;
                    bestMatch.Confidence = confidence;
                }
            }
        }

        if (bestMatch.Confidence < minConfidence)
            return TemplateMatchResult.NotFound;

        return bestMatch;
    }

    /// <summary>
    /// Extracts RGB pixel data from a bitmap as a flat byte array.
    /// Format: [R, G, B, R, G, B, ...] row by row.
    /// </summary>
    private byte[] GetPixelData(Bitmap bitmap)
    {
        int width = bitmap.Width;
        int height = bitmap.Height;
        byte[] pixels = new byte[width * height * 3];

        var rect = new Rectangle(0, 0, width, height);
        var bitmapData = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

        try
        {
            int stride = bitmapData.Stride;
            byte[] buffer = new byte[stride * height];
            Marshal.Copy(bitmapData.Scan0, buffer, 0, buffer.Length);

            int pixelIdx = 0;
            for (int y = 0; y < height; y++)
            {
                int rowOffset = y * stride;
                for (int x = 0; x < width; x++)
                {
                    int bufferIdx = rowOffset + x * 4;
                    // ARGB: B, G, R, A
                    pixels[pixelIdx++] = buffer[bufferIdx + 2]; // R
                    pixels[pixelIdx++] = buffer[bufferIdx + 1]; // G
                    pixels[pixelIdx++] = buffer[bufferIdx];     // B
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(bitmapData);
        }

        return pixels;
    }
}
