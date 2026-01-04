namespace PokemonRedAI.Core.ScreenReader;

public interface IScreenCapture : IDisposable
{
    bool IsConnected { get; }
    int ScreenWidth { get; }
    int ScreenHeight { get; }

    bool Connect(IntPtr windowHandle);
    void Disconnect();
    byte[]? CaptureFrame();
    ScreenPixel[,]? CaptureFrameAsPixels();
}

public readonly struct ScreenPixel
{
    public byte R { get; init; }
    public byte G { get; init; }
    public byte B { get; init; }

    public ScreenPixel(byte r, byte g, byte b)
    {
        R = r;
        G = g;
        B = b;
    }

    public bool IsBlack => R == 0 && G == 0 && B == 0;
    public bool IsWhite => R == 255 && G == 255 && B == 255;

    public bool Matches(ScreenPixel other, int tolerance = 0)
    {
        return Math.Abs(R - other.R) <= tolerance &&
               Math.Abs(G - other.G) <= tolerance &&
               Math.Abs(B - other.B) <= tolerance;
    }

    public override string ToString() => $"RGB({R}, {G}, {B})";
}
