using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace PhotoSafeCheck;

/// <summary>Shown only when no remembered mode exists or the user requests it.</summary>
internal sealed class StorageModeWindow : Window
{
    private readonly RadioButton portable;
    private readonly RadioButton local;
    private readonly CheckBox remember;
    private readonly CheckBox carry;
    private readonly TextBlock error;

    internal StorageModeWindow(StorageMode suggested, StorageMode? previous,
        Action<StorageMode, bool, bool> onChosen)
    {
        Title = "PhotoSafe Check — Choose where to save progress";
        Width = 660; Height = 540; MinWidth = 560; MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.Parse("#101725"));
        try
        {
            var icon = Path.Combine(AppContext.BaseDirectory, "Assets", "photosafe.ico");
            if (File.Exists(icon)) Icon = new WindowIcon(icon);
        }
        catch { }
        var white = Brushes.White;
        var muted = new SolidColorBrush(Color.Parse("#B4C7DB"));
        var panel = new StackPanel { Spacing = 15, Margin = new Thickness(29) };
        panel.Children.Add(new TextBlock { Text = "Where should PhotoSafe Check save its data?",
            FontSize = 24, FontWeight = FontWeight.Bold, Foreground = white, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new TextBlock { Text = "Choose how the app stores your settings, scan progress, pairing key and logs. This NEVER affects your actual photos.",
            Foreground = muted, TextWrapping = TextWrapping.Wrap });

        portable = new RadioButton { GroupName = "mode", Content = "Portable — keep everything in the app folder", IsChecked = suggested == StorageMode.Portable,
            Foreground = white, FontWeight = FontWeight.SemiBold };
        local = new RadioButton { GroupName = "mode", Content = "Local — store data in my user profile", IsChecked = suggested == StorageMode.Local,
            Foreground = white, FontWeight = FontWeight.SemiBold };
        panel.Children.Add(portable);
        panel.Children.Add(new TextBlock { Text = "Stores app data in App/Data next to the executable. Ideal for a USB drive or an app you can delete cleanly. The folder must be writable.",
            Foreground = muted, Margin = new Thickness(23, 0, 0, 2), TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(local);
        panel.Children.Add(new TextBlock { Text = "Stores app data in your normal AppData (Windows) / application-data directory (Linux or macOS). Useful when the app folder is read-only. You can clean it later from inside the app.",
            Foreground = muted, Margin = new Thickness(23, 0, 0, 2), TextWrapping = TextWrapping.Wrap });
        remember = new CheckBox { Content = "Remember my choice (you can change it later)", IsChecked = true, Foreground = muted };
        carry = new CheckBox { Content = "Carry over previous scan history and browser pairing (never overwrite destination files)", IsChecked = true, Foreground = muted };
        panel.Children.Add(remember);
        panel.Children.Add(carry);
        error = new TextBlock { Foreground = new SolidColorBrush(Color.Parse("#FFB69C")), TextWrapping = TextWrapping.Wrap };
        panel.Children.Add(error);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 12 };
        var choose = new Button { Content = "Continue", Padding = new Thickness(20, 10), FontWeight = FontWeight.SemiBold };
        choose.Click += (_, _) =>
        {
            var mode = portable.IsChecked == true ? StorageMode.Portable : StorageMode.Local;
            try { onChosen(mode, remember.IsChecked == true, carry.IsChecked == true); }
            catch (Exception ex)
            {
                error.Text = "Could not use that storage location: " + ex.Message +
                    "\nTry Local mode if this folder is read-only.";
            }
        };
        actions.Children.Add(choose);
        panel.Children.Add(actions);
        Content = new ScrollViewer { Content = panel };
    }
}
