using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Media.Imaging;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace PhotoSafeCheck;

public sealed class MainWindow : Window
{
    private static readonly Color Bg = Color.Parse("#101725");
    private static readonly IBrush Subtle = new SolidColorBrush(Color.Parse("#A8B7CD"));
    private static readonly IBrush Highlight = new SolidColorBrush(Color.Parse("#67DCC5"));
    private static readonly IBrush Tile = new SolidColorBrush(Color.Parse("#1B2638"));
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif", ".heic", ".heif",
        ".tif", ".tiff", ".bmp", ".dng", ".raw", ".cr2", ".nef", ".arw",
        ".mp4", ".mov", ".avi", ".mkv", ".3gp"
    };

    private readonly ScannerCoordinator coordinator = new(Storage.Load());
    private readonly ObservableCollection<PhotoRecord> rows = new();
    private readonly TextBox folderBox = new() { IsReadOnly = true, MinWidth = 350 };
    private readonly TextBox delayBox = new() { Width = 64 };
    private readonly TextBox limitBox = new() { Width = 92 };
    private readonly CheckBox chimeBox = new() { Content = "Play a chime when finished", Foreground = Subtle };
    private readonly TextBlock info = new() { TextWrapping = TextWrapping.Wrap, Foreground = Subtle };
    private readonly TextBlock summary = new() { FontSize = 15, FontWeight = FontWeight.SemiBold };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, Foreground = Highlight };
    private readonly TextBlock bridgeText = new() { Foreground = Subtle, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock bridgeIndicator = new() { Text = "●  Connecting to desktop bridge…", FontWeight = FontWeight.SemiBold, Foreground = Subtle };
    private string? bridgeError;
    private bool bridgeStarting;
    private readonly TextBlock welcomeHint = new() { Foreground = Subtle, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock folderStep = new() { Foreground = Subtle, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock browserStep = new() { Foreground = Subtle, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock scanStep = new() { Foreground = Subtle, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock countAll = new() { FontSize = 29, FontWeight = FontWeight.Bold };
    private readonly TextBlock countPossible = new() { FontSize = 29, FontWeight = FontWeight.Bold };
    private readonly TextBlock countMissing = new() { FontSize = 29, FontWeight = FontWeight.Bold };
    private readonly TextBlock countReview = new() { FontSize = 29, FontWeight = FontWeight.Bold };
    private readonly ProgressBar progress = new() { Minimum = 0, Maximum = 100, Height = 10 };
    private readonly TextBlock progressLabel = new() { Foreground = Subtle, FontSize = 12 };
    private readonly TextBox findBox = new() { Watermark = "Find a filename or folder...", MinWidth = 230 };
    private readonly TextBlock displayedCount = new() { Foreground = Subtle, FontSize = 12 };
    private readonly TextBlock selectedName = new() { FontSize = 17, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock selectedStatus = new() { FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock selectedDetail = new() { Foreground = Subtle, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock selectedPath = new() { Foreground = Subtle, FontSize = 11, TextWrapping = TextWrapping.Wrap };
    private readonly Image preview = new() { Height = 205, Stretch = Stretch.Uniform };
    private Bitmap? previewBitmap;
    private PhotoRecord? selectedPhoto;
    private readonly Button reveal = new() { Content = "Show in folder", IsEnabled = false };
    private readonly ListBox resultList = new();
    private readonly ComboBox filter = new() { Width = 202 };
    private readonly Button start = new() { Content = "Start scanning" };
    private readonly Button pause = new() { Content = "Pause", IsEnabled = false };
    private readonly Button copyMissing = new() { Content = "Copy not-found photos to upload folder…" };
    private readonly Button cancelCopy = new() { Content = "Cancel copying", IsEnabled = false };
    private bool copying;
    private CancellationTokenSource? copyCancellation;
    private readonly string pairingToken = Storage.GetOrCreatePairingToken();
    private readonly ExtensionDistributionLinks extensionDownloadLinks = ExtensionDistributionLinks.Load();
    private readonly DispatcherTimer refreshTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private LocalBridge? bridge;
    private bool indexing;
    private int displayFingerprint;
    private string lastSelectionId = "";
    private bool waitingForCompletion;

    public MainWindow()
    {
        Title = "PhotoSafe Check  ·  v0.3.6 Friendly Preview";
        try
        {
            using var iconStream = BundledAssets.Open("photosafe.ico");
            if (iconStream is not null) Icon = new WindowIcon(iconStream);
        }
        catch (Exception ex) { StartupDiagnostics.Log("App icon unavailable: " + ex.Message); }
        Width = 1250; Height = 860; MinWidth = 960; MinHeight = 680;
        Background = B(Bg);
        folderBox.Text = coordinator.State.Folder;
        delayBox.Text = coordinator.State.DelaySeconds.ToString(CultureInfo.InvariantCulture);
        limitBox.Text = coordinator.State.DailyLimit.ToString(CultureInfo.InvariantCulture);
        chimeBox.IsChecked = coordinator.State.PlayCompletionChime;

        // The sidebar stays in place while the main document scrolls.
        var shell = new Grid { ColumnDefinitions = new ColumnDefinitions("252,*") };
        var sidebar = new StackPanel { Spacing = 18, Margin = new Thickness(21, 29) };
        var branding = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        try
        {
            using var logoStream = BundledAssets.Open("photosafe.png");
            if (logoStream is not null) branding.Children.Add(new Image { Source = new Bitmap(logoStream), Width = 52, Height = 52 });
        }
        catch (Exception ex) { StartupDiagnostics.Log("Sidebar logo unavailable: " + ex.Message); }
        branding.Children.Add(new TextBlock { Text = "PHOTO / SAFE", FontSize = 21,
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = FontWeight.Bold, Foreground = B("#F1F8FF") });
        sidebar.Children.Add(branding);
        sidebar.Children.Add(new TextBlock { Text = "Find the gaps. Keep the memories.",
            Foreground = Subtle, TextWrapping = TextWrapping.Wrap, FontSize = 12 });
        sidebar.Children.Add(new Border { Height = 1, Background = B("#34475A") });
        sidebar.Children.Add(Txt("YOUR CHECKLIST", 11, B("#8FA1B9"), FontWeight.Bold));
        sidebar.Children.Add(SideStep("01", "Choose & index photos", folderStep));
        sidebar.Children.Add(SideStep("02", "Connect your browser", browserStep));
        sidebar.Children.Add(SideStep("03", "Run a comparison", scanStep));
        sidebar.Children.Add(new Border { Height = 1, Background = B("#34475A"), Margin = new Thickness(0, 7) });
        sidebar.Children.Add(SideNote("HOW MATCHING WORKS",
            "Possible match means Google Photos returned a related result. It does not verify the original file or image quality."));
        sidebar.Children.Add(SideNote("A SAFE PROMISE",
            "PhotoSafe Check never deletes, moves, edits or overwrites original photos. Staging creates new copies only."));
        sidebar.Children.Add(Txt("v0.3.6 · Friendly preview", 11, B("#8297B1")));
        sidebar.Children.Add(Txt(StoragePaths.IsPortable
            ? "Portable mode · progress in Data next to the app"
            : "Local mode · progress in application data",
            11, B("#8297B1")));
        var sidebarBorder = new Border { Background = B("#172338"), Child = sidebar,
            BorderBrush = B("#26384E"), BorderThickness = new Thickness(0,0,1,0) };
        Grid.SetColumn(sidebarBorder, 0);
        shell.Children.Add(sidebarBorder);

        var document = new StackPanel { Margin = new Thickness(28, 24, 28, 34), Spacing = 17 };
        document.Children.Add(new TextBlock { Text = "Your photo backup check", FontSize = 31,
            FontWeight = FontWeight.Bold, Foreground = Brushes.White });
        document.Children.Add(welcomeHint);
        document.Children.Add(new Border { Background = B("#183D37"), CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16, 13), Child = Txt(
            "READ-ONLY BY DESIGN  ·  No photo deletion or modification. Copying upload candidates is optional and creates new, separate files.",
            12, B("#B0F6D9")) });

        var metrics = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*"), ColumnSpacing = 12 };
        metrics.Children.Add(StatCard(0, "LOCAL FILES", countAll, "#DDEAFF"));
        metrics.Children.Add(StatCard(1, "POSSIBLE MATCH", countPossible, "#9EF0CE"));
        metrics.Children.Add(StatCard(2, "NOT FOUND BY SEARCH", countMissing, "#F8C78F"));
        metrics.Children.Add(StatCard(3, "NEEDS REVIEW", countReview, "#CDC1FF"));
        document.Children.Add(metrics);

        var choosePanel = new StackPanel { Spacing = 11 };
        choosePanel.Children.Add(SectionHeading("1", "Choose your local photos",
            "Select a folder, then let PhotoSafe Check count photos and videos. Your originals stay untouched."));
        folderBox.MinWidth = 320;
        folderBox.HorizontalAlignment = HorizontalAlignment.Stretch;
        var chooseRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 9 };
        Grid.SetColumn(folderBox, 0); chooseRow.Children.Add(folderBox);
        var chooseButton = Action("Browse folders", ChooseFolder, primary: false);
        Grid.SetColumn(chooseButton, 1); chooseRow.Children.Add(chooseButton);
        var indexButton = Action("Index local files", IndexPhotos, primary: true);
        Grid.SetColumn(indexButton, 2); chooseRow.Children.Add(indexButton);
        choosePanel.Children.Add(chooseRow);
        document.Children.Add(Card(choosePanel));

        var browserPanel = new StackPanel { Spacing = 10 };
        browserPanel.Children.Add(SectionHeading("2", "Connect Google Photos",
            "Sign in through your usual Chrome or Edge window. The extension never asks for a Google password."));
        browserPanel.Children.Add(Txt("Install the browser companion", 16, Brushes.White, FontWeight.SemiBold));
        browserPanel.Children.Add(Txt("Choose the Chrome Web Store for automatic updates, or download the ZIP from GitHub Releases and load it manually.",
            12, Subtle));
        var extensionInstallButtons = new WrapPanel();
        var storeInstallButton = Action("Get from Chrome Web Store ↗", (_, _) =>
            OpenWeb(extensionDownloadLinks.ChromeWebStoreUrl!), true);
        storeInstallButton.IsEnabled = extensionDownloadLinks.HasChromeWebStore;
        extensionInstallButtons.Children.Add(storeInstallButton);
        var githubInstallButton = Action("Download from GitHub Releases ↗", (_, _) =>
            OpenWeb(extensionDownloadLinks.GitHubReleasesUrl!));
        githubInstallButton.IsEnabled = extensionDownloadLinks.HasGitHubReleases;
        extensionInstallButtons.Children.Add(githubInstallButton);
        browserPanel.Children.Add(extensionInstallButtons);
        if (!extensionDownloadLinks.HasChromeWebStore || !extensionDownloadLinks.HasGitHubReleases)
            browserPanel.Children.Add(Txt("Download links will activate after publication. The included extension can still be loaded manually.",
                11, B("#F5C78C")));
        browserPanel.Children.Add(Action("Open extension folder (source builds)", OpenBundledExtensionFolder));
        browserPanel.Children.Add(new Border { Height = 1, Background = B("#34475A"), Margin = new Thickness(0, 2) });
        browserPanel.Children.Add(Txt("Already installed? Connect it to the desktop app:", 12, Subtle));
        var browserButtons = new WrapPanel();
        browserButtons.Children.Add(Action("Open Google Photos", (s,e) => OpenWeb("https://photos.google.com/"), true));
        browserButtons.Children.Add(Action("Copy pairing key", CopyPairingKey));
        browserButtons.Children.Add(Action("Extension setup help", ShowExtensionHelp));
        browserPanel.Children.Add(browserButtons);
        bridgeText.Text = "Starting private connection on 127.0.0.1:37541…";
        browserPanel.Children.Add(bridgeIndicator);
        browserPanel.Children.Add(bridgeText);
        var bridgeActions = new WrapPanel();
        bridgeActions.Children.Add(Action("Retry connection", async (_, _) => await RestartBridge()));
        bridgeActions.Children.Add(Action("Test desktop bridge", async (_, _) => await TestBridge()));
        browserPanel.Children.Add(bridgeActions);
        browserPanel.Children.Add(Txt("Keep the pairing key private. The desktop app sends only search terms and receives search outcomes; not your Google credentials or image bytes.",
            11, Subtle));
        document.Children.Add(Card(browserPanel));

        var scanningPanel = new StackPanel { Spacing = 12 };
        scanningPanel.Children.Add(SectionHeading("3", "Check for possible missing photos",
            "Slow, gentle filename searches. You can pause and resume without losing saved results."));
        var scanControls = new WrapPanel();
        start.Click += StartScanning;
        start.Content = "Start / resume checking";
        Decorate(start, true);
        pause.Click += (s,e) => { waitingForCompletion = false; coordinator.Pause(); Refresh(); };
        Decorate(pause, false);
        scanControls.Children.Add(start); scanControls.Children.Add(pause);
        scanningPanel.Children.Add(scanControls);
        progress.Foreground = B("#67DCC5");
        scanningPanel.Children.Add(progress);
        scanningPanel.Children.Add(progressLabel);
        scanningPanel.Children.Add(status);
        var settingLine = new WrapPanel();
        settingLine.Children.Add(Txt("Delay after each search (seconds)", 12, Subtle));
        delayBox.Margin = new Thickness(7,0,18,5);
        settingLine.Children.Add(delayBox);
        settingLine.Children.Add(Txt("Daily searches (0 = unlimited)", 12, Subtle));
        limitBox.Margin = new Thickness(7,0,18,5);
        settingLine.Children.Add(limitBox);
        scanningPanel.Children.Add(settingLine);
        scanningPanel.Children.Add(chimeBox);
        scanningPanel.Children.Add(Txt("Default: 12 seconds after each completed search, any time including overnight. The extension pauses on account/access warnings.",
            11, Subtle));
        document.Children.Add(Card(scanningPanel));

        var resultsPanel = new StackPanel { Spacing = 12 };
        resultsPanel.Children.Add(SectionHeading("4", "Review what was found",
            "Search your results, filter by outcome, and inspect local image previews. No outcome is proof that a file can be deleted."));
        resultsPanel.Children.Add(summary);
        var filtersRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 9 };
        Grid.SetColumn(findBox,0); filtersRow.Children.Add(findBox);
        foreach (var f in new[] { "All", "Not checked", "Possible match", "Not found by search", "Needs review" })
            filter.Items.Add(f);
        filter.SelectedIndex = 0;
        filter.SelectionChanged += (s,e) => RefreshList(force: true);
        findBox.TextChanged += (s,e) => RefreshList(force: true);
        filter.MinWidth = 185;
        Grid.SetColumn(filter,1); filtersRow.Children.Add(filter);
        resultsPanel.Children.Add(filtersRow);
        resultsPanel.Children.Add(displayedCount);

        resultList.ItemsSource = rows;
        resultList.MinHeight = 250;
        resultList.MaxHeight = 465;
        resultList.ItemTemplate = new FuncDataTemplate<PhotoRecord>((item, _) => ResultRow(item));
        resultList.SelectionChanged += OnPhotoSelected;
        var details = new StackPanel { Spacing = 10, Margin = new Thickness(14) };
        details.Children.Add(Txt("SELECTED LOCAL FILE", 11, B("#90A8C1"), FontWeight.Bold));
        details.Children.Add(new Border { Background = B("#0D1725"), CornerRadius = new CornerRadius(10),
            MinHeight = 210, Child = preview });
        selectedName.Text = "Select a photo to preview";
        selectedDetail.Text = "Choose any row to inspect its local file and the scan's findings.";
        details.Children.Add(selectedName);
        details.Children.Add(selectedStatus);
        details.Children.Add(selectedDetail);
        details.Children.Add(selectedPath);
        reveal.Click += OpenSelectedFolder;
        Decorate(reveal, false);
        details.Children.Add(reveal);
        var split = new Grid { ColumnDefinitions = new ColumnDefinitions("*,310"), ColumnSpacing = 13 };
        var tableContainer = new Border { Background = B("#121E30"), CornerRadius = new CornerRadius(12),
            Padding = new Thickness(7), Child = resultList };
        Grid.SetColumn(tableContainer,0); split.Children.Add(tableContainer);
        var detailsContainer = new Border { Background = B("#152338"), CornerRadius = new CornerRadius(12), Child = details };
        Grid.SetColumn(detailsContainer,1); split.Children.Add(detailsContainer);
        resultsPanel.Children.Add(split);
        document.Children.Add(Card(resultsPanel));

        var exportPanel = new StackPanel { Spacing = 10 };
        exportPanel.Children.Add(SectionHeading("5", "Save reports or prepare upload copies",
            "Export lists for review. Optionally prepare copies of items not found by search for manual uploading."));
        var exportRow = new WrapPanel();
        exportRow.Children.Add(Action("Export separate result lists", ExportSeparateLists, true));
        exportRow.Children.Add(Action("Export full CSV", ExportCsv));
        copyMissing.Click += CopyMissingPhotos; Decorate(copyMissing, false);
        copyMissing.Content = "Copy not-found files to upload folder";
        exportRow.Children.Add(copyMissing);
        cancelCopy.Click += (_,_) => copyCancellation?.Cancel();
        Decorate(cancelCopy, false);
        exportRow.Children.Add(cancelCopy);
        exportPanel.Children.Add(exportRow);
        exportPanel.Children.Add(Txt("Upload copies are stored in '" + PhotoExportService.UploadFolderName +
            "' inside your selected folder, preserve subfolders, and never overwrite existing files. Uploads are always manual.", 11, Subtle));
        document.Children.Add(Card(exportPanel));

        var more = new Expander { Header = "Advanced testing and troubleshooting", IsExpanded = false };
        var advanced = new StackPanel { Spacing = 9, Margin = new Thickness(9) };
        var resetRow = new WrapPanel();
        resetRow.Children.Add(Action("Clear last result", ClearLastResult));
        resetRow.Children.Add(Action("Retry needs-review", RequeueReview));
        resetRow.Children.Add(Action("Clear all results…", ClearAllResults));
        advanced.Children.Add(resetRow);
        advanced.Children.Add(Txt("These buttons only reset PhotoSafe Check's scan findings. They do not change photos or reset the daily search count.",
            11, Subtle));
        advanced.Children.Add(new Border { Height = 1, Background = B("#34475A"), Margin = new Thickness(0, 6) });
        advanced.Children.Add(Txt("STORAGE AND PRIVACY", 12, Brushes.White, FontWeight.Bold));
        advanced.Children.Add(Txt("Current mode: " + StoragePaths.CurrentName +
            " · Your scan history and pairing information live at: " + StoragePaths.DataDirectory,
            11, Subtle));
        advanced.Children.Add(Action("Storage mode and cleanup…", ShowStorageSettings));
        more.Content = advanced;
        document.Children.Add(more);
        info.Text = "Choose a folder and index it to begin. Your local photos are always left unchanged.";
        document.Children.Add(new Border { Background = B("#24364C"), Padding = new Thickness(14),
            CornerRadius = new CornerRadius(9), Child = info });
        document.Children.Add(Txt("PhotoSafe Check is an experimental backup discovery aid, not a certified cloud backup verifier. Please keep your original photos.",
            12, B("#9CB2CA")));

        var scroll = new ScrollViewer { Content = document };
        Grid.SetColumn(scroll,1); shell.Children.Add(scroll);
        Content = shell;

        refreshTimer.Tick += (s,e) => Refresh();
        refreshTimer.Start();
        Opened += async (s,e) => await StartBridge();
        Closing += (s,e) => {
            coordinator.Pause("App closing."); refreshTimer.Stop();
            previewBitmap?.Dispose();
            if (bridge is not null) _ = bridge.DisposeAsync();
        };
        RefreshList(true);
        Refresh();
    }

    private static IBrush B(string hex) => new SolidColorBrush(Color.Parse(hex));
    private static IBrush B(Color value) => new SolidColorBrush(value);
    // Never pass default(FontWeight) to Avalonia: its numeric value is zero,
    // which throws during text layout ("Font weight must be > 0").
    private static TextBlock Txt(string text, double size, IBrush brush) =>
        Txt(text, size, brush, FontWeight.Normal);

    private static TextBlock Txt(string text, double size, IBrush brush, FontWeight weight) => new()
    {
        Text = text, FontSize = size, Foreground = brush, FontWeight = weight,
        TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center
    };
    private static Border Card(Control child) => new()
    {
        Background = B("#1B2A40"), BorderBrush = B("#30465F"), BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(14), Padding = new Thickness(19), Child = child
    };
    private static Control SectionHeading(string number, string heading, string description)
    {
        var inner = new StackPanel { Spacing = 4 };
        inner.Children.Add(Txt($"{number}   {heading}", 19, Brushes.White, FontWeight.Bold));
        inner.Children.Add(Txt(description, 12, B("#B7C7DC")));
        return inner;
    }
    private static Control SideStep(string number, string title, TextBlock dynamicStatus)
    {
        var content = new StackPanel { Spacing = 4 };
        content.Children.Add(Txt(number + "    " + title, 13, Brushes.White, FontWeight.SemiBold));
        content.Children.Add(dynamicStatus);
        return content;
    }
    private static Control SideNote(string title, string description)
    {
        var inner = new StackPanel { Spacing = 7 };
        inner.Children.Add(Txt(title, 10, B("#88DCC9"), FontWeight.Bold));
        inner.Children.Add(Txt(description, 12, B("#BAC9DC")));
        return inner;
    }
    private static Border StatCard(int col, string label, TextBlock count, string color)
    {
        count.Foreground = B(color);
        var stack = new StackPanel { Spacing = 7 };
        stack.Children.Add(Txt(label, 10, B("#AFC1D5"), FontWeight.Bold));
        stack.Children.Add(count);
        var result = new Border { Background = B("#1C2D43"), CornerRadius = new CornerRadius(12),
            Padding = new Thickness(15,12), Child = stack, BorderBrush = B("#314863"), BorderThickness = new Thickness(1) };
        Grid.SetColumn(result, col);
        return result;
    }
    private static void Decorate(Button button, bool primary)
    {
        button.Background = primary ? B("#70DBC3") : B("#2A405A");
        button.Foreground = primary ? B("#102B29") : Brushes.White;
        button.BorderThickness = new Thickness(0);
        button.CornerRadius = new CornerRadius(9);
        button.FontWeight = FontWeight.SemiBold;
        button.Padding = new Thickness(13,9);
        button.Margin = new Thickness(0,0,9,6);
    }
    private static Button Action(string label, EventHandler<RoutedEventArgs> handler, bool primary = false)
    {
        var button = new Button { Content = label };
        Decorate(button, primary);
        button.Click += handler;
        return button;
    }
    private static Border ResultRow(PhotoRecord? record)
    {
        var panel = new StackPanel { Spacing = 5, Margin = new Thickness(7,9) };
        if (record is not null)
        {
            var name = Txt(record.Name, 13, Brushes.White, FontWeight.SemiBold);
            panel.Children.Add(name);
            panel.Children.Add(Txt(record.Status, 11, StatusBrush(record.Status), FontWeight.SemiBold));
            panel.Children.Add(new TextBlock { Text = record.Detail, Foreground = B("#A9BDD4"),
                FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 2 });
        }
        return new Border { Child = panel, CornerRadius = new CornerRadius(8) };
    }
    private static IBrush StatusBrush(string status) => status switch
    {
        "Possible match" => B("#8FE1C6"),
        "Not found by search" => B("#F8C78F"),
        "Needs review" => B("#CEBDFF"),
        _ => B("#A8BAD0")
    };

    private void CopyPairingKey(object? sender, RoutedEventArgs e)
    {
        _ = CopyPairingAsync();
    }
    private async Task CopyPairingAsync()
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null) { info.Text = "Clipboard is unavailable. Check your desktop settings."; return; }
        await clipboard.SetTextAsync(pairingToken);
        info.Text = "Pairing key copied. Open the PhotoSafe companion extension and paste it there.";
    }
    private void ShowExtensionHelp(object? sender, RoutedEventArgs e)
    {
        var popup = new Window { Title = "Install and connect your browser extension", Width = 620, Height = 520,
            CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = B(Bg) };
        var content = new StackPanel { Margin = new Thickness(25), Spacing = 14 };
        content.Children.Add(Txt("Install the companion extension", 22, Brushes.White, FontWeight.Bold));
        content.Children.Add(Txt("Recommended: Use 'Get from Chrome Web Store' after the extension is published. Chrome manages its updates.", 13, Subtle));
        content.Children.Add(Txt("Alternative: Open GitHub Releases, download the extension ZIP, and extract it. In Chrome open chrome://extensions (Edge: edge://extensions), enable Developer mode, click Load unpacked and select the extracted folder containing manifest.json.", 13, Subtle));
        content.Children.Add(Txt("Offline/preview: Select 'Open included extension folder' and use Load unpacked. A ZIP cannot be installed directly by dragging it into Chrome.", 13, Subtle));
        content.Children.Add(Txt("Connect to the desktop application", 18, Brushes.White, FontWeight.SemiBold));
        content.Children.Add(Txt("1. Copy the pairing key from PhotoSafe Check, then paste it into the extension popup.", 13, Subtle));
        content.Children.Add(Txt("2. Open photos.google.com and sign in normally. Click 'Use this Photos tab' in the extension.", 13, Subtle));
        content.Children.Add(Txt("3. Start scanning in the desktop app and leave that Google Photos tab open.", 13, Subtle));
        var done = new Button { Content = "Got it" }; Decorate(done, true);
        done.Click += (_,_) => popup.Close(); content.Children.Add(done);
        popup.Content = new ScrollViewer { Content = content };
        popup.ShowDialog(this);
    }
    private void OpenBundledExtensionFolder(object? sender, RoutedEventArgs e)
    {
        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, "Extension"),
            Path.GetFullPath(Path.Combine(baseDir, "..", "Extension")),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "Extension"))
        };
        var folder = candidates.FirstOrDefault(Directory.Exists);
        if (folder is null)
        {
            info.Text = "The standalone app does not include the browser extension. Install it from the Chrome Web Store or the separate GitHub Releases extension ZIP.";
            return;
        }
        try { Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true }); }
        catch (Exception ex) { info.Text = "Couldn't open extension folder: " + ex.Message; }
    }

    private void OpenWeb(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { info.Text = "Couldn't open the browser: " + ex.Message; }
    }
    private void ClearLastResult(object? sender, RoutedEventArgs e)
    {
        if (coordinator.Snapshot().Enabled || copying)
        { info.Text = "Pause scanning and finish copying before clearing a result."; return; }
        var count = coordinator.ClearLastResult();
        info.Text = count == 0 ? "No previous result to clear." :
            $"Cleared the most recent search result ({count} file(s)). Original photos untouched.";
        Refresh();
    }
    private void RequeueReview(object? sender, RoutedEventArgs e)
    {
        if (coordinator.Snapshot().Enabled || copying)
        { info.Text = "Pause scanning and finish copying before retrying uncertain results."; return; }
        var count = coordinator.RequeueNeedsReview();
        info.Text = $"Requeued {count:N0} needs-review files. Only local scan metadata changed.";
        Refresh();
    }
    private void OnPhotoSelected(object? sender, SelectionChangedEventArgs e)
    {
        var photo = resultList.SelectedItem as PhotoRecord;
        if (photo is null) return;
        ShowPhoto(photo);
    }
    private void ShowPhoto(PhotoRecord photo)
    {
        selectedPhoto = photo;
        lastSelectionId = photo.Id;
        selectedName.Text = photo.Name;
        selectedStatus.Text = photo.Status;
        selectedStatus.Foreground = StatusBrush(photo.Status);
        selectedDetail.Text = string.IsNullOrWhiteSpace(photo.Detail)
            ? "This file has not been checked yet." : photo.Detail;
        selectedPath.Text = photo.Path;
        reveal.IsEnabled = File.Exists(photo.Path);
        preview.Source = null;
        previewBitmap?.Dispose(); previewBitmap = null;
        try
        {
            var ext = Path.GetExtension(photo.Path).ToLowerInvariant();
            if (File.Exists(photo.Path) && photo.Bytes < 65_000_000 &&
                ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".webp" or ".gif")
            {
                // Decode only the selected local preview; no media bytes are sent to Google.
                using var input = File.OpenRead(photo.Path);
                previewBitmap = Bitmap.DecodeToWidth(input, 370);
                preview.Source = previewBitmap;
            }
        }
        catch { selectedDetail.Text += "\n(Preview unavailable for this file.)"; }
    }
    private void OpenSelectedFolder(object? sender, RoutedEventArgs e)
    {
        var path = selectedPhoto?.Path;
        if (path is null || !File.Exists(path)) { info.Text = "The local file is no longer available."; return; }
        try
        {
            ProcessStartInfo p;
            if (OperatingSystem.IsWindows())
            {
                p = new ProcessStartInfo("explorer.exe") { UseShellExecute = false };
                p.ArgumentList.Add("/select," + path);
            }
            else if (OperatingSystem.IsMacOS())
            {
                p = new ProcessStartInfo("open") { UseShellExecute = false };
                p.ArgumentList.Add("-R"); p.ArgumentList.Add(path);
            }
            else
            {
                p = new ProcessStartInfo("xdg-open") { UseShellExecute = false };
                p.ArgumentList.Add(Path.GetDirectoryName(path)!);
            }
            Process.Start(p);
        }
        catch (Exception ex) { info.Text = "Could not open the containing folder: " + ex.Message; }
    }

    private async Task StartBridge()
    {
        if (bridgeStarting) return;
        bridgeStarting = true;
        bridgeError = null;
        bridgeIndicator.Text = "●  Starting local connection…";
        bridgeIndicator.Foreground = B("#F8C78F");
        try
        {
            var newBridge = new LocalBridge(coordinator, pairingToken);
            bridge = newBridge;
            await newBridge.StartAsync();
            StartupDiagnostics.Log("Local bridge listening on 127.0.0.1:37541");
            bridgeText.Text = "Bridge ready. Copy the pairing key and connect in Chrome/Edge. No Google login happens in the app.";
        }
        catch (Exception ex)
        {
            bridgeError = ex.Message;
            StartupDiagnostics.Report("Local bridge failed to start", ex);
            bridgeText.Text = "Bridge offline: " + ex.Message +
                ". Another PhotoSafe Check instance may already be using port 37541. Try Retry connection.";
        }
        finally
        {
            bridgeStarting = false;
            RefreshBridgeIndicator();
        }
    }

    private async Task RestartBridge()
    {
        if (bridgeStarting) return;
        bridgeStarting = true;
        bridgeIndicator.Text = "●  Restarting local connection…";
        bridgeIndicator.Foreground = B("#F8C78F");
        try
        {
            if (bridge is not null) await bridge.DisposeAsync();
            bridge = null;
        }
        catch (Exception ex) { StartupDiagnostics.Report("Bridge shutdown failed", ex); }
        finally { bridgeStarting = false; }
        await StartBridge();
    }

    private async Task TestBridge()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            using var request = new HttpRequestMessage(HttpMethod.Get, "http://127.0.0.1:37541/api/status");
            request.Headers.TryAddWithoutValidation("X-PhotoSafe-Token", pairingToken);
            using var response = await http.SendAsync(request);
            if (response.IsSuccessStatusCode)
                info.Text = "Desktop bridge test succeeded. If the extension still cannot connect, reload it and check browser permissions.";
            else
                info.Text = $"Desktop bridge responded with HTTP {(int)response.StatusCode}. Try Retry connection or re-pair the extension.";
        }
        catch (Exception ex)
        {
            info.Text = "Desktop bridge test failed: " + ex.Message +
                ". Make sure only one copy of PhotoSafe Check is running, then Retry connection.";
        }
        RefreshBridgeIndicator();
    }

    private void RefreshBridgeIndicator()
    {
        if (bridgeStarting) return;
        if (bridgeError is not null || bridge is null || !bridge.IsListening)
        {
            bridgeIndicator.Text = "●  Desktop connection: OFFLINE";
            bridgeIndicator.Foreground = B("#FF9F9F");
        }
        else if (bridge.LastActivityUtc is { } seen && DateTimeOffset.UtcNow - seen < TimeSpan.FromSeconds(25))
        {
            bridgeIndicator.Text = "●  Extension connected recently";
            bridgeIndicator.Foreground = B("#8DE6C6");
        }
        else
        {
            bridgeIndicator.Text = "●  Desktop bridge: READY · Waiting for browser";
            bridgeIndicator.Foreground = B("#F8C78F");
        }
    }

    private async void ChooseFolder(object? sender, RoutedEventArgs e)
    {
        var chosen = await StorageProvider.OpenFolderPickerAsync(new() { Title = "Select a local photo folder", AllowMultiple = false });
        if (chosen.Count == 0) return;
        if (copying || indexing || coordinator.Snapshot().Enabled)
        { info.Text = "Pause scanning, indexing and copying before choosing a different folder."; return; }
        var path = chosen[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path)) return;
        coordinator.State.Folder = path;
        folderBox.Text = path;
        // Choosing a folder does not remove files; index button commits to a new list.
        info.Text = "Folder selected. Click Index photos. Existing scan results remain until indexing.";
    }

    private async void IndexPhotos(object? sender, RoutedEventArgs e)
    {
        if (indexing || copying || !Directory.Exists(coordinator.State.Folder))
        { info.Text = "Choose an existing local folder first."; return; }
        if (coordinator.Snapshot().Enabled)
        { info.Text = "Pause scanning before indexing."; return; }
        indexing = true;
        info.Text = "Indexing local files (read-only)…";
        try
        {
            var prior = coordinator.ListPhotos("All", int.MaxValue)
                .ToDictionary(p => p.Path, StringComparer.OrdinalIgnoreCase);
            var found = await Task.Run(() =>
            {
                var collected = new List<PhotoRecord>();
                var pending = new Stack<string>();
                pending.Push(coordinator.State.Folder);
                while (pending.Count > 0)
                {
                    var current = pending.Pop();
                    try
                    {
                        foreach (var subfolder in Directory.EnumerateDirectories(current))
                        {
                            // NEVER index copies produced by PhotoSafe Check, or we'd create
                            // new 'missing' records for our own staging copies.
                            if (string.Equals(Path.GetFullPath(current), Path.GetFullPath(coordinator.State.Folder),
                                  OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) &&
                                string.Equals(Path.GetFileName(subfolder), PhotoExportService.UploadFolderName,
                                  StringComparison.OrdinalIgnoreCase)) continue;
                            try { if ((File.GetAttributes(subfolder) & FileAttributes.ReparsePoint) == 0) pending.Push(subfolder); }
                            catch { /* Skip inaccessible or symbolic-link folders. */ }
                        }
                        foreach (var file in Directory.EnumerateFiles(current))
                        {
                            if (!Extensions.Contains(Path.GetExtension(file))) continue;
                            try
                            {
                                var details = new FileInfo(file);
                                if ((details.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                                var ticks = details.LastWriteTimeUtc.Ticks;
                                if (prior.TryGetValue(file, out var old) && old.Bytes == details.Length && old.ModifiedTicks == ticks)
                                    collected.Add(old);
                                else collected.Add(new PhotoRecord
                                { Path = file, Name = details.Name, Bytes = details.Length, ModifiedTicks = ticks });
                            }
                            catch { /* Some files cannot be inspected; never modify them. */ }
                        }
                    }
                    catch { /* Ignore inaccessible folders. */ }
                }
                return collected.OrderBy(p => p.Path, StringComparer.OrdinalIgnoreCase).ToList();
            });
            coordinator.ReplacePhotos(found);
            info.Text = $"Local inventory updated: {found.Count:N0} photo/video files. Re-indexing does not change the originals.";
            RefreshList(force: true);
        }
        catch (Exception ex) { info.Text = "Index failed: " + ex.Message; }
        finally { indexing = false; Refresh(); }
    }

    private void StartScanning(object? sender, RoutedEventArgs e)
    {
        if (copying || indexing) { info.Text = "Finish indexing or copying first, then start another scan."; return; }
        if (!int.TryParse(delayBox.Text, out var delay) || delay is < 5 or > 3600)
        { info.Text = "Delay must be a whole number from 5 to 3600 seconds."; return; }
        if (!int.TryParse(limitBox.Text, out var limit) || limit < 0)
        { info.Text = "Daily limit must be 0 (unlimited) or a positive whole number."; return; }
        if (coordinator.Snapshot().TotalFiles == 0)
        { info.Text = "Index a photo folder first."; return; }
        if (coordinator.Snapshot().NotChecked == 0)
        { info.Text = "No unchecked files remain. To repeat uncertain results, use Retry needs-review files first."; return; }
        coordinator.UpdateSettings(delay, limit, chimeBox.IsChecked == true);
        coordinator.Start();
        waitingForCompletion = true;
        info.Text = "Activate the Google Photos tab using the extension popup. Keep that tab open while scanning.";
        Refresh();
    }

    private void ShowStorageSettings(object? sender, RoutedEventArgs e)
    {
        var popup = new Window { Title = "Storage & privacy", Width = 670, Height = 490,
            MinWidth = 510, Background = B(Bg), WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var panel = new StackPanel { Margin = new Thickness(24), Spacing = 13 };
        panel.Children.Add(Txt("Where PhotoSafe Check keeps your data", 21, Brushes.White, FontWeight.Bold));
        panel.Children.Add(Txt("Current mode: " + StoragePaths.CurrentName, 14, B("#91E1C9"), FontWeight.SemiBold));
        panel.Children.Add(Txt("Current app data: " + StoragePaths.DataDirectory, 12, Subtle));
        panel.Children.Add(Txt("Portable location: " + StoragePaths.PortableDirectory, 11, Subtle));
        panel.Children.Add(Txt("Local location: " + StoragePaths.LocalDirectory, 11, Subtle));
        panel.Children.Add(Txt("Your original photos, optional upload copies, and exported reports are never erased by these controls.", 12, Subtle));
        var change = Action("Choose storage mode next time I open the app", (_, _) =>
        {
            try
            {
                StoragePaths.AskOnNextLaunch();
                info.Text = "The storage chooser will appear next launch. This session still uses " + StoragePaths.CurrentName + " storage.";
                popup.Close();
            }
            catch (Exception ex) { info.Text = "Unable to save this preference: " + ex.Message; popup.Close(); }
        }, true);
        panel.Children.Add(change);
        panel.Children.Add(Txt("Switching storage does not delete old history. On the next launch, choose whether to carry over previous settings and the browser pairing key.", 11, Subtle));
        if (StoragePaths.IsPortable)
        {
            var candidates = AppDataCleanup.Preview();
            panel.Children.Add(Txt($"Old AppData cleanup: {candidates.Count} recognized file(s) available", 13, Brushes.White));
            var clean = Action("Review and clean old AppData files…", async (_, _) =>
            {
                var current = AppDataCleanup.Preview();
                if (current.Count == 0)
                { info.Text = "No recognized old PhotoSafe Check AppData files were found."; popup.Close(); return; }
                var confirm = new Window { Title = "Clear PhotoSafe Check AppData?", Width = 620, Height = 360,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = B(Bg) };
                var confirmPanel = new StackPanel { Margin = new Thickness(20), Spacing = 13 };
                confirmPanel.Children.Add(Txt("Only these app-created data files will be removed:", 17, Brushes.White, FontWeight.SemiBold));
                confirmPanel.Children.Add(Txt(string.Join("\n", current), 12, Subtle));
                confirmPanel.Children.Add(Txt("This action cannot be undone. It will NOT access photos, uploads, exported reports or unrelated files.", 12, B("#F6B98E")));
                var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
                var cancel = new Button { Content = "Cancel", Padding = new Thickness(14, 8) };
                var yes = new Button { Content = "Remove these app data files", Padding = new Thickness(14, 8) };
                cancel.Click += (_, _) => confirm.Close(false);
                yes.Click += (_, _) => confirm.Close(true);
                row.Children.Add(cancel); row.Children.Add(yes);
                confirmPanel.Children.Add(row);
                confirm.Content = new ScrollViewer { Content = confirmPanel };
                if (await confirm.ShowDialog<bool>(popup))
                {
                    try { info.Text = AppDataCleanup.Clean(); }
                    catch (Exception ex) { info.Text = "Cleanup could not finish: " + ex.Message; }
                }
                popup.Close();
            });
            panel.Children.Add(clean);
        }
        else
            panel.Children.Add(Txt("To remove local AppData later: switch to Portable mode, reopen PhotoSafe Check, and use the cleanup button here. Active Local mode data cannot be cleaned while the app is running.", 12, B("#F6B98E")));
        popup.Content = new ScrollViewer { Content = panel };
        popup.ShowDialog(this);
    }

    private async void ClearAllResults(object? sender, RoutedEventArgs e)
    {
        if (coordinator.Snapshot().Enabled || copying)
        {
            info.Text = "Pause scanning and finish copying before clearing saved results.";
            return;
        }
        var dialog = new Window
        {
            Title = "Clear saved scan results?",
            Width = 475, Height = 235,
            CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Bg)
        };
        var panel = new StackPanel { Margin = new Thickness(22), Spacing = 16 };
        panel.Children.Add(new TextBlock { Text = "Clear all previous scan findings?",
            FontSize = 19, FontWeight = FontWeight.SemiBold, Foreground = Brushes.White });
        panel.Children.Add(new TextBlock { Text =
            "This resets all comparison statuses to Not checked, so you can test from scratch. " +
            "It will NOT delete, move, rename or modify photos, or reset today's search quota.",
            Foreground = Subtle, TextWrapping = TextWrapping.Wrap });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "Cancel", Padding = new Thickness(16, 8) };
        var confirm = new Button { Content = "Clear saved results", Padding = new Thickness(16, 8) };
        cancel.Click += (_, _) => dialog.Close(false);
        confirm.Click += (_, _) => dialog.Close(true);
        buttons.Children.Add(cancel);
        buttons.Children.Add(confirm);
        panel.Children.Add(buttons);
        dialog.Content = panel;
        if (await dialog.ShowDialog<bool>(this))
        {
            var count = coordinator.ClearAllResults();
            info.Text = $"Reset {count:N0} scan findings. Local and cloud photos remain completely untouched.";
            RefreshList(force: true);
            Refresh();
        }
    }

    private async void ExportCsv(object? sender, RoutedEventArgs e)
    {
        if (coordinator.Snapshot().Enabled || copying)
        { info.Text = "Pause scanning and finish copying before exporting a report."; return; }
        var destination = await StorageProvider.SaveFilePickerAsync(new()
        {
            Title = "Save comparison report",
            SuggestedFileName = "photosafe-report.csv",
            FileTypeChoices = new[] { new FilePickerFileType("CSV report") { Patterns = new[] { "*.csv" } } }
        });
        var path = destination?.TryGetLocalPath();
        if (path is null) return;
        if (!string.Equals(Path.GetExtension(path), ".csv", StringComparison.OrdinalIgnoreCase))
        {
            info.Text = "For safety, reports can only be saved as .csv files.";
            return;
        }
        if (coordinator.ListPhotos("All", int.MaxValue).Any(p =>
            string.Equals(p.Path, path, StringComparison.OrdinalIgnoreCase)))
        {
            info.Text = "Refusing to overwrite an indexed media file.";
            return;
        }
        static string Q(string s)
        {
            // Prevent spreadsheet software from evaluating untrusted filenames as formulas.
            var clean = s.Replace("\r", " ").Replace("\n", " ");
            if (clean.TrimStart().StartsWith('=') || clean.TrimStart().StartsWith('+') ||
                clean.TrimStart().StartsWith('-') || clean.TrimStart().StartsWith('@'))
                clean = "'" + clean;
            return "\"" + clean.Replace("\"", "\"\"") + "\"";
        }
        var all = coordinator.ListPhotos("All", int.MaxValue);
        var lines = new[] { "Path,Filename,SizeBytes,Status,Detail,CheckedUtc" }.Concat(
            all.Select(p => string.Join(",", Q(p.Path), Q(p.Name), p.Bytes.ToString(CultureInfo.InvariantCulture),
                Q(p.Status), Q(p.Detail), Q(p.CheckedAtUtc?.ToString("O") ?? ""))));
        try
        {
            await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await using var writer = new StreamWriter(file);
            foreach (var line in lines) await writer.WriteLineAsync(line);
            info.Text = "New report exported to: " + path;
        }
        catch (IOException ex) { info.Text = "Report was not saved. Select a new, unused CSV filename. " + ex.Message; }
        catch (Exception ex) { info.Text = "CSV export failed: " + ex.Message; }
    }

    private async void ExportSeparateLists(object? sender, RoutedEventArgs e)
    {
        if (coordinator.Snapshot().Enabled || copying)
        { info.Text = "Pause scanning and finish copying before exporting lists."; return; }
        var chosen = await StorageProvider.OpenFolderPickerAsync(new()
        { Title = "Choose a folder for separate PhotoSafe CSV lists", AllowMultiple = false });
        var folder = chosen.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(folder)) return;
        try
        {
            var snapshot = coordinator.ListPhotos("All", int.MaxValue);
            var report = await PhotoExportService.ExportSeparateListsAsync(folder, snapshot);
            info.Text = $"Created NEW CSV files: {report.FoundCount:N0} possible matches, " +
                $"{report.NotFoundCount:N0} not found by search" +
                (report.NeedsReviewCount > 0 ? $", {report.NeedsReviewCount:N0} needs review (third list)" : "") +
                $". Saved in: {folder}. These are search results, not proof of backup.";
        }
        catch (Exception ex) { info.Text = "Couldn't export separate CSV lists: " + ex.Message; }
    }

    private async void CopyMissingPhotos(object? sender, RoutedEventArgs e)
    {
        if (copying || indexing || coordinator.Snapshot().Enabled)
        { info.Text = "Pause scanning and finish indexing before copying."; return; }
        var root = coordinator.State.Folder;
        if (!Directory.Exists(root)) { info.Text = "Choose and index an existing local folder first."; return; }
        var items = coordinator.ListPhotos("Not found by search", int.MaxValue);
        if (items.Count == 0) { info.Text = "No not-found results to copy."; return; }
        var totalBytes = items.Aggregate(0.0, (sum, item) => sum + Math.Max(0, item.Bytes));
        var target = Path.Combine(root, PhotoExportService.UploadFolderName);
        var dialog = new Window
        {
            Title = "Prepare copies for manual upload?", Width = 560, Height = 345,
            CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Bg)
        };
        var panel = new StackPanel { Margin = new Thickness(23), Spacing = 13 };
        panel.Children.Add(new TextBlock { Text = "Copy not-found files?", FontSize = 20,
            FontWeight = FontWeight.SemiBold, Foreground = Brushes.White });
        panel.Children.Add(new TextBlock { Text = $"Create NEW copies of {items.Count:N0} file(s), " +
            $"about {totalBytes / 1048576.0:N1} MiB, inside:\n{target}\n\n" +
            "Local originals will remain untouched. Existing destination files will be skipped, NEVER overwritten. " +
            "Files in this folder are upload candidates, NOT proven missing Google Photos backups. " +
            "You will upload them manually. If this folder is OneDrive-synced, the new copies may sync automatically.",
            Foreground = Subtle, TextWrapping = TextWrapping.Wrap });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "Cancel", Padding = new Thickness(14, 8) };
        var confirm = new Button { Content = "Create copies", Padding = new Thickness(14, 8) };
        cancel.Click += (_, _) => dialog.Close(false);
        confirm.Click += (_, _) => dialog.Close(true);
        buttons.Children.Add(cancel);
        buttons.Children.Add(confirm);
        panel.Children.Add(buttons);
        dialog.Content = panel;
        if (!await dialog.ShowDialog<bool>(this)) return;

        copying = true;
        var copyCts = new CancellationTokenSource();
        copyCancellation = copyCts;
        Refresh();
        try
        {
            var progress = new Progress<PhotoExportService.CopyProgress>(p =>
            {
                if (copying) info.Text = $"Preparing upload copies: {p.Completed:N0}/{p.Total:N0} — {p.CurrentName}";
            });
            var result = await Task.Run(() => PhotoExportService.CopyNotFoundAsync(
                root, items, progress, copyCts.Token));
            info.Text = $"Copy {(result.Cancelled ? "cancelled" : "finished")}: " +
                $"{result.Copied:N0} new copies, {result.AlreadyPresent:N0} already present (skipped), " +
                $"{result.SkippedChangedOrUnsafe:N0} changed/unsafe, {result.Failed:N0} failed. " +
                $"Output: {result.Destination}. Originals untouched. " +
                (result.Issues.Count > 0 ? "First issue: " + result.Issues[0] : "");
        }
        catch (Exception ex) { info.Text = "Couldn't complete copying: " + ex.Message; }
        finally
        {
            copyCts.Dispose();
            copyCancellation = null;
            copying = false;
            Refresh();
        }
    }

    private void Refresh()
    {
        var state = coordinator.Snapshot();
        if (waitingForCompletion && !state.Enabled)
        {
            waitingForCompletion = false;
            if (state.TotalFiles > 0 && state.NotChecked == 0 &&
                state.Message.StartsWith("Scan pass complete", StringComparison.Ordinal))
            {
                info.Text = "Scan finished! Check the report before making any manual backup decisions.";
                if (coordinator.State.PlayCompletionChime) CompletionChime.Play();
            }
        }
        start.IsEnabled = !state.Enabled && !copying && !indexing;
        pause.IsEnabled = state.Enabled;
        copyMissing.IsEnabled = !state.Enabled && !copying && state.NotFoundBySearch > 0;
        cancelCopy.IsEnabled = copying;
        countAll.Text = state.TotalFiles.ToString("N0");
        countPossible.Text = state.PossibleMatches.ToString("N0");
        countMissing.Text = state.NotFoundBySearch.ToString("N0");
        countReview.Text = state.NeedsReview.ToString("N0");
        var finished = state.TotalFiles - state.NotChecked;
        progress.Value = state.TotalFiles == 0 ? 0 : 100.0 * finished / state.TotalFiles;
        progressLabel.Text = state.TotalFiles == 0
            ? "No local files indexed yet."
            : $"{finished:N0} of {state.TotalFiles:N0} checked  ·  " +
              $"{(100.0 * finished / state.TotalFiles):N1}%  ·  " +
              $"~{FormatRemaining(state.EstimatedSecondsRemaining)} remaining (estimate)";
        welcomeHint.Text = state.TotalFiles == 0
            ? "Start with a local folder. You'll be guided through a simple, read-only comparison."
            : state.NotChecked == 0 ? "This pass is complete. Review the results before deciding what to upload manually."
            : "Ready when you are: continue searching Google Photos or review existing findings.";
        folderStep.Text = state.TotalFiles > 0 ? $"Ready · {state.TotalFiles:N0} files indexed" : "Choose a folder to begin";
        RefreshBridgeIndicator();
        var clientSeen = bridge?.LastActivityUtc;
        browserStep.Text = bridgeError is not null || bridge is null || !bridge.IsListening
            ? "Desktop bridge offline · Retry connection" :
            clientSeen is { } at && DateTimeOffset.UtcNow - at < TimeSpan.FromSeconds(25)
            ? "Browser connected recently" : "Bridge ready · Connect the extension";
        scanStep.Text = state.Enabled ? "Scanning now" : state.NotChecked == 0 && state.TotalFiles > 0
            ? "Results ready for review" : "Waiting to start / resume";
        summary.Text = $"{state.PossibleMatches:N0} possible matches · {state.NotFoundBySearch:N0} not found · " +
            $"{state.NeedsReview:N0} uncertain · {state.NotChecked:N0} not checked";
        status.Text = (state.Enabled ? "● SCANNING ENABLED   " : "● IDLE / PAUSED   ") + state.Message +
                      $"    ·   Today: {state.DailyUsed:N0}" + (state.DailyLimit == 0 ? " / unlimited" : $" / {state.DailyLimit:N0}");
        var fingerprint = HashCode.Combine(state.TotalFiles, state.NotChecked,
            state.PossibleMatches, state.NotFoundBySearch, state.NeedsReview,
            filter.SelectedItem?.ToString());
        if (fingerprint != displayFingerprint)
        {
            displayFingerprint = fingerprint;
            RefreshList(force: true);
        }
    }

    private static string FormatRemaining(int seconds)
    {
        if (seconds <= 0) return "0 min";
        var time = TimeSpan.FromSeconds(seconds);
        return time.TotalHours >= 1 ? $"{(int)time.TotalHours}h {time.Minutes}m" : $"{Math.Max(1, (int)Math.Ceiling(time.TotalMinutes))}m";
    }

    private void RefreshList(bool force)
    {
        if (!force) return;
        var selected = lastSelectionId;
        var query = (findBox.Text ?? "").Trim();
        var matching = coordinator.ListPhotos(filter.SelectedItem?.ToString() ?? "All", int.MaxValue)
            .Where(p => query.Length == 0 ||
                p.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                p.Path.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(600).ToList();
        rows.Clear();
        foreach (var record in matching) rows.Add(record);
        displayedCount.Text = matching.Count == 600
            ? "Showing the first 600 matching entries. Narrow the filename search for more."
            : $"Showing {matching.Count:N0} entries";
        var reselect = matching.FirstOrDefault(p => p.Id == selected);
        if (reselect is not null) resultList.SelectedItem = reselect;
        else if (matching.Count > 0) resultList.SelectedIndex = 0;
        else
        {
            selectedPhoto = null; lastSelectionId = ""; reveal.IsEnabled = false;
            preview.Source = null; previewBitmap?.Dispose(); previewBitmap = null;
            selectedName.Text = "No results in this view";
            selectedStatus.Text = "";
            selectedDetail.Text = "Try another filter or filename search.";
            selectedPath.Text = "";
        }
    }
}
