using Avalonia;

namespace PhotoSafeCheck;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Contains("--verify-bundle", StringComparer.Ordinal))
        {
            foreach (var resource in new[] { "photosafe.ico", "photosafe.png", "complete.wav" })
            {
                using var stream = BundledAssets.Open(resource);
                if (stream is null || stream.Length == 0)
                {
                    Console.Error.WriteLine("Missing embedded asset: " + resource);
                    return 2;
                }
            }
            using (var links = BundledAssets.OpenLinks())
            {
                if (links is null || links.Length == 0) return 3;
                using var json = System.Text.Json.JsonDocument.Parse(links);
                if (!json.RootElement.TryGetProperty("githubReleasesUrl", out _)) return 4;
            }
            Console.WriteLine("PhotoSafe Check standalone bundle verified.");
            return 0;
        }
        StartupDiagnostics.Log("Starting PhotoSafe Check v0.4.0-rc.1; OS=" + System.Runtime.InteropServices.RuntimeInformation.OSDescription);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            StartupDiagnostics.Report("Fatal unhandled exception", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            StartupDiagnostics.Report("Unobserved background task exception", e.Exception);
            e.SetObserved();
        };
        try
        {
            // IMPORTANT: accessing Dispatcher.UIThread before Avalonia sets up its
            // platform backend creates a premature dispatcher and causes
            // PlatformNotSupportedException in Dispatcher.MainLoop.
            // UI exception handling is registered in App.OnFrameworkInitializationCompleted.
            StartupDiagnostics.Log("Launching desktop UI");
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            StartupDiagnostics.Log("UI exited normally");
            return 0;
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Report("Application startup failed", ex);
            StartupDiagnostics.ShowFatalError(ex);
            return 1;
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
}
