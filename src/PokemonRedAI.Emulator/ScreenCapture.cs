using System.Runtime.InteropServices;
using PokemonRedAI.Core.ScreenReader;

namespace PokemonRedAI.Emulator;

public class ScreenCapture : IScreenCapture
{
    private IntPtr _windowHandle;
    private bool _isConnected;
    private bool _disposed;

    // Game Boy screen dimensions
    public const int GameBoyWidth = 160;
    public const int GameBoyHeight = 144;

    public bool IsConnected => _isConnected;
    public int ScreenWidth => GameBoyWidth;
    public int ScreenHeight => GameBoyHeight;

    public bool Connect(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
            return false;

        _windowHandle = windowHandle;
        _isConnected = true;
        return true;
    }

    public void Disconnect()
    {
        _windowHandle = IntPtr.Zero;
        _isConnected = false;
    }

    public byte[]? CaptureFrame()
    {
        if (!_isConnected || _windowHandle == IntPtr.Zero)
            return null;

        try
        {
            var rect = new RECT();
            if (!GetClientRect(_windowHandle, ref rect))
                return null;

            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;

            if (width <= 0 || height <= 0)
                return null;

            IntPtr hdcWindow = GetDC(_windowHandle);
            if (hdcWindow == IntPtr.Zero)
                return null;

            try
            {
                IntPtr hdcMemory = CreateCompatibleDC(hdcWindow);
                if (hdcMemory == IntPtr.Zero)
                    return null;

                try
                {
                    IntPtr hBitmap = CreateCompatibleBitmap(hdcWindow, width, height);
                    if (hBitmap == IntPtr.Zero)
                        return null;

                    try
                    {
                        IntPtr hOld = SelectObject(hdcMemory, hBitmap);
                        BitBlt(hdcMemory, 0, 0, width, height, hdcWindow, 0, 0, SRCCOPY);
                        SelectObject(hdcMemory, hOld);

                        var bmi = new BITMAPINFO
                        {
                            biSize = 40,
                            biWidth = width,
                            biHeight = -height, // Negative for top-down
                            biPlanes = 1,
                            biBitCount = 32,
                            biCompression = 0
                        };

                        byte[] pixels = new byte[width * height * 4];
                        GetDIBits(hdcMemory, hBitmap, 0, (uint)height, pixels, ref bmi, 0);

                        // Scale to Game Boy resolution if needed
                        if (width != GameBoyWidth || height != GameBoyHeight)
                        {
                            return ScaleToGameBoy(pixels, width, height);
                        }

                        return pixels;
                    }
                    finally
                    {
                        DeleteObject(hBitmap);
                    }
                }
                finally
                {
                    DeleteDC(hdcMemory);
                }
            }
            finally
            {
                ReleaseDC(_windowHandle, hdcWindow);
            }
        }
        catch
        {
            return null;
        }
    }

    public ScreenPixel[,]? CaptureFrameAsPixels()
    {
        var rawData = CaptureFrame();
        if (rawData == null)
            return null;

        var pixels = new ScreenPixel[GameBoyWidth, GameBoyHeight];

        for (int y = 0; y < GameBoyHeight; y++)
        {
            for (int x = 0; x < GameBoyWidth; x++)
            {
                int index = (y * GameBoyWidth + x) * 4;
                pixels[x, y] = new ScreenPixel(
                    rawData[index + 2], // R
                    rawData[index + 1], // G
                    rawData[index]      // B
                );
            }
        }

        return pixels;
    }

    private byte[] ScaleToGameBoy(byte[] source, int srcWidth, int srcHeight)
    {
        byte[] result = new byte[GameBoyWidth * GameBoyHeight * 4];

        float xRatio = (float)srcWidth / GameBoyWidth;
        float yRatio = (float)srcHeight / GameBoyHeight;

        for (int y = 0; y < GameBoyHeight; y++)
        {
            for (int x = 0; x < GameBoyWidth; x++)
            {
                int srcX = (int)(x * xRatio);
                int srcY = (int)(y * yRatio);

                int srcIndex = (srcY * srcWidth + srcX) * 4;
                int dstIndex = (y * GameBoyWidth + x) * 4;

                result[dstIndex] = source[srcIndex];
                result[dstIndex + 1] = source[srcIndex + 1];
                result[dstIndex + 2] = source[srcIndex + 2];
                result[dstIndex + 3] = source[srcIndex + 3];
            }
        }

        return result;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Disconnect();
    }

    #region Win32 Imports

    private const int SRCCOPY = 0x00CC0020;

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, ref RECT lpRect);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight,
        IntPtr hdcSrc, int nXSrc, int nYSrc, int dwRop);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines,
        byte[] lpvBits, ref BITMAPINFO lpbi, uint uUsage);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO
    {
        public int biSize;
        public int biWidth;
        public int biHeight;
        public short biPlanes;
        public short biBitCount;
        public int biCompression;
        public int biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public int biClrUsed;
        public int biClrImportant;
    }

    #endregion
}
