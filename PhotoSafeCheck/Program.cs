using Avalonia;

namespace PhotoSafeCheck;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        StartupDiagnostics.Log("Starting PhotoSafe Check v0.3.6; OS=" + System.Runtime.InteropServices.RuntimeInformation.OSDescription);
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
