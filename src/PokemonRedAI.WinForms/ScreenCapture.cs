using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace PokemonRedAI.WinForms;

public class ScreenCapture
{
    private readonly IntPtr _windowHandle;

    public ScreenCapture(IntPtr windowHandle)
    {
        _windowHandle = windowHandle;
    }

    public Bitmap? CaptureWindow()
    {
        if (_windowHandle == IntPtr.Zero || !IsWindow(_windowHandle))
            return null;

        try
        {
            // Get window rectangle
            if (!GetWindowRect(_windowHandle, out RECT rect))
                return null;

            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;

            if (width <= 0 || height <= 0)
                return null;

            // Create bitmap
            var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);

            using (var graphics = Graphics.FromImage(bitmap))
            {
                var hdcDest = graphics.GetHdc();
                var hdcSrc = GetWindowDC(_windowHandle);

                // Use BitBlt to capture the window
                BitBlt(hdcDest, 0, 0, width, height, hdcSrc, 0, 0, SRCCOPY);

                ReleaseDC(_windowHandle, hdcSrc);
                graphics.ReleaseHdc(hdcDest);
            }

            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    public Bitmap? CaptureClientArea()
    {
        if (_windowHandle == IntPtr.Zero || !IsWindow(_windowHandle))
            return null;

        try
        {
            // Get client rectangle
            if (!GetClientRect(_windowHandle, out RECT clientRect))
                return null;

            int width = clientRect.Right - clientRect.Left;
            int height = clientRect.Bottom - clientRect.Top;

            if (width <= 0 || height <= 0)
                return null;

            // Convert client coordinates to screen coordinates
            POINT topLeft = new POINT { X = 0, Y = 0 };
            ClientToScreen(_windowHandle, ref topLeft);

            // Create bitmap
            var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);

            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(topLeft.X, topLeft.Y, 0, 0, new Size(width, height));
            }

            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    #region Win32 Imports

    private const int SRCCOPY = 0x00CC0020;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int width, int height,
        IntPtr hdcSrc, int xSrc, int ySrc, int rop);

    #endregion
}
