using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace PhotoSafeCheck;

public sealed class App : Application
{
    public override void Initialize()
    {
        StartupDiagnostics.Log("Initializing Avalonia Fluent theme");
        Styles.Add(new FluentTheme());
        RequestedThemeVariant = ThemeVariant.Dark;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Avalonia's platform dispatcher exists only after builder setup.
        // Never subscribe to Dispatcher.UIThread events from Program.Main.
        Dispatcher.UIThread.UnhandledException += (_, e) =>
            StartupDiagnostics.Report("UI thread exception", e.Exception);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (StoragePaths.TryReadPreference(out var remembered, out var previous) && remembered.HasValue)
            {
                StoragePaths.SelectSaved(remembered.Value);
                StartupDiagnostics.FlushPending();
                OpenMainWindow(desktop);
            }
            else
            {
                StartupDiagnostics.Log("Waiting for first-run storage mode choice");
                StorageModeWindow? chooser = null;
                chooser = new StorageModeWindow(previous ?? StoragePaths.SuggestedMode, previous,
                    (mode, remember, carryOver) =>
                    {
                        StoragePaths.Choose(mode, remember, carryOver, previous);
                        StartupDiagnostics.FlushPending();
                        // Change the lifetime's main window BEFORE closing the chooser,
                        // otherwise Avalonia may shut down with the welcome window.
                        OpenMainWindow(desktop);
                        desktop.MainWindow?.Show();
                        chooser!.Close();
                    });
                desktop.MainWindow = chooser;
            }
        }
        base.OnFrameworkInitializationCompleted();
    }
    private static void OpenMainWindow(IClassicDesktopStyleApplicationLifetime desktop)
    {
        StartupDiagnostics.Log("Creating main window (" + StoragePaths.CurrentName + " mode)");
        try
        {
            desktop.MainWindow = new MainWindow();
            StartupDiagnostics.Log("Main window created");
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Report("Friendly main-window initialization failed", ex);
            desktop.MainWindow = new Window
            {
                Title = "PhotoSafe Check — Startup Problem",
                Width = 690, Height = 340, MinWidth = 500,
                Background = new SolidColorBrush(Color.Parse("#142033")),
                Content = new ScrollViewer { Content = new TextBlock
                {
                    Margin = new Thickness(24), TextWrapping = TextWrapping.Wrap,
                    Foreground = Brushes.White, FontSize = 15,
                    Text = "The friendly interface could not open. No photos were changed.\n\n" +
                        ex.GetType().Name + ": " + ex.Message +
                        "\n\nDiagnostic log:\n" + StartupDiagnostics.LogFile
                }}
            };
        }
    }
}
