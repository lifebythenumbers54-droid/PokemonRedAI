using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using PokemonRedAI.Core.ScreenReader;

namespace PokemonRedAI.WinForms;

/// <summary>
/// Extension methods for Bitmap conversion and manipulation.
/// </summary>
public static class BitmapExtensions
{
    /// <summary>
    /// Converts a Bitmap to a 2D array of ScreenPixel structs.
    /// Uses unsafe code for fast pixel access.
    /// </summary>
    public static ScreenPixel[,] ToScreenPixels(this Bitmap bitmap)
    {
        if (bitmap == null)
            throw new ArgumentNullException(nameof(bitmap));

        int width = bitmap.Width;
        int height = bitmap.Height;
        var pixels = new ScreenPixel[width, height];

        // Use LockBits for fast pixel access
        var rect = new Rectangle(0, 0, width, height);
        var bitmapData = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

        try
        {
            int bytesPerPixel = 4; // ARGB = 4 bytes
            int stride = bitmapData.Stride;
            IntPtr scan0 = bitmapData.Scan0;

            byte[] pixelBuffer = new byte[stride * height];
            Marshal.Copy(scan0, pixelBuffer, 0, pixelBuffer.Length);

            for (int y = 0; y < height; y++)
            {
                int rowOffset = y * stride;
                for (int x = 0; x < width; x++)
                {
                    int pixelOffset = rowOffset + (x * bytesPerPixel);
                    // ARGB format: B, G, R, A
                    byte b = pixelBuffer[pixelOffset];
                    byte g = pixelBuffer[pixelOffset + 1];
                    byte r = pixelBuffer[pixelOffset + 2];
                    // Alpha at pixelOffset + 3, not needed

                    pixels[x, y] = new ScreenPixel(r, g, b);
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(bitmapData);
        }

        return pixels;
    }

    /// <summary>
    /// Converts a Bitmap to a 2D array of ScreenPixel structs (safe version, slower).
    /// Use this if the fast version causes issues.
    /// </summary>
    public static ScreenPixel[,] ToScreenPixelsSafe(this Bitmap bitmap)
    {
        if (bitmap == null)
            throw new ArgumentNullException(nameof(bitmap));

        int width = bitmap.Width;
        int height = bitmap.Height;
        var pixels = new ScreenPixel[width, height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var color = bitmap.GetPixel(x, y);
                pixels[x, y] = new ScreenPixel(color.R, color.G, color.B);
            }
        }

        return pixels;
    }

    /// <summary>
    /// Creates a copy of a bitmap region.
    /// </summary>
    public static Bitmap CopyRegion(this Bitmap source, Rectangle region)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        // Clamp region to source bounds
        int x = Math.Max(0, region.X);
        int y = Math.Max(0, region.Y);
        int width = Math.Min(region.Width, source.Width - x);
        int height = Math.Min(region.Height, source.Height - y);

        if (width <= 0 || height <= 0)
            return new Bitmap(1, 1);

        var dest = new Bitmap(width, height, source.PixelFormat);
        using (var g = Graphics.FromImage(dest))
        {
            g.DrawImage(source,
                new Rectangle(0, 0, width, height),
                new Rectangle(x, y, width, height),
                GraphicsUnit.Pixel);
        }
        return dest;
    }
}
