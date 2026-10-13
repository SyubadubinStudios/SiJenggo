using Avalonia;

namespace SiJenggo.App;

/// <summary>Command line: <c>--capture &lt;dir&gt;</c> plays an automatic tour and saves screenshots, <c>--quality N</c> overrides graphics.</summary>
public sealed record LaunchOptions(string? CaptureDirectory, int? Quality, bool Windowed);

internal static class Program
{
    public static LaunchOptions Options { get; private set; } = new(null, null, false);

    // Avalonia needs an STA main thread on Windows.
    [STAThread]
    public static void Main(string[] args)
    {
        string? capture = null;
        int? quality = null;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--capture" && i + 1 < args.Length)
            {
                capture = args[++i];
            }
            else if (args[i] == "--quality" && i + 1 < args.Length && int.TryParse(args[++i], out int q))
            {
                quality = q;
            }
        }

        Options = new LaunchOptions(capture, quality, capture is not null);
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
