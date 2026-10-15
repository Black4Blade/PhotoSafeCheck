using System.Text.Json;

namespace PhotoSafeCheck;

internal enum StorageMode { Portable, Local }

/// <summary>
/// Storage is selected by the user before anything (including diagnostics) writes
/// to disk. In portable mode no files are deliberately created in AppData.
/// </summary>
internal static class StoragePaths
{
    private static StorageMode? active;
    private const string ChoiceName = "storage-choice.json";
    internal static string PortableDirectory => Path.Combine(AppContext.BaseDirectory, "Data");
    internal static string LocalDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PhotoSafeCheck");
    internal static string MarkerFile => Path.Combine(AppContext.BaseDirectory, "portable.mode");
    internal static bool IsSelected => active.HasValue;
    internal static bool IsPortable => active == StorageMode.Portable;
    internal static string CurrentName => active switch { StorageMode.Portable => "Portable", StorageMode.Local => "Local", _ => "Not chosen" };
    internal static string DataDirectory => active switch
    {
        StorageMode.Portable => PortableDirectory,
        StorageMode.Local => LocalDirectory,
        _ => throw new InvalidOperationException("Choose a storage mode before opening app data.")
    };
    private static string RootChoice => Path.Combine(AppContext.BaseDirectory, ChoiceName);
    private static string LocalChoice => Path.Combine(LocalDirectory, ChoiceName);

    // Read-only startup checks. Do not create AppData when checking preferences.
    internal static bool TryReadPreference(out StorageMode? mode, out StorageMode? previous)
    {
        mode = null;
        previous = null;
        // Normally the app-folder preference wins. If its directory became read-only
        // and a newer local override was written, honor that newer choice.
        var candidates = new[] { RootChoice, LocalChoice }
            .Where(File.Exists)
            .OrderByDescending(File.GetLastWriteTimeUtc);
        foreach (var path in candidates)
        {
            if (!File.Exists(path)) continue;
            try
            {
                using var json = JsonDocument.Parse(File.ReadAllText(path));
                var root = json.RootElement;
                if (root.TryGetProperty("lastMode", out var prev) && Parse(prev.GetString()) is { } old)
                    previous = old;
                if (root.TryGetProperty("mode", out var value)) mode = Parse(value.GetString());
                return true; // Explicit "ask" takes precedence over any older preference.
            }
            catch { /* Corrupted preferences will trigger the chooser. */ }
        }
        return false;
    }
    internal static StorageMode SuggestedMode => File.Exists(MarkerFile) ? StorageMode.Portable : StorageMode.Local;
    private static StorageMode? Parse(string? s) => s?.ToLowerInvariant() switch
    {
        "portable" => StorageMode.Portable, "local" => StorageMode.Local, _ => null
    };
    internal static string PathFor(StorageMode mode) => mode == StorageMode.Portable ? PortableDirectory : LocalDirectory;

    internal static void SelectSaved(StorageMode mode) => active = mode;

    internal static void Choose(StorageMode mode, bool remember, bool carryPrevious, StorageMode? previous)
    {
        var destination = PathFor(mode);
        // Portable must be actually writable for persistent scan history, not just launchable.
        Directory.CreateDirectory(destination);
        if ((File.GetAttributes(destination) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("The chosen data directory is a symbolic link or junction. Choose an ordinary folder instead.");
        var probe = Path.Combine(destination, ".photosafe-write-test-" + Guid.NewGuid().ToString("N"));
        using (File.Create(probe)) { }
        File.Delete(probe);
        if (carryPrevious)
        {
            var source = previous.HasValue ? PathFor(previous.Value)
                : (mode == StorageMode.Portable ? LocalDirectory : PortableDirectory);
            if (!Path.GetFullPath(source).Equals(Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
            {
                // Only application-owned metadata. Never copy/inspect any indexed photo path.
                foreach (var name in new[] { "state.json", "pairing-token.txt" })
                {
                    var from = Path.Combine(source, name);
                    var to = Path.Combine(destination, name);
                    if (File.Exists(from) && !File.Exists(to) &&
                        (File.GetAttributes(from) & FileAttributes.ReparsePoint) == 0)
                        File.Copy(from, to, overwrite: false);
                }
            }
        }
        SaveChoice(mode, remember);
        active = mode;
    }

    internal static void AskOnNextLaunch()
    {
        if (!active.HasValue) throw new InvalidOperationException("No storage mode is active.");
        SaveChoice(active.Value, remember: false);
    }
    private static void SaveChoice(StorageMode mode, bool remember)
    {
        var json = JsonSerializer.Serialize(new {
            mode = remember ? mode.ToString().ToLowerInvariant() : "ask",
            lastMode = mode.ToString().ToLowerInvariant()
        }, new JsonSerializerOptions { WriteIndented = true });
        // Prefer beside the executable so a portable folder keeps its choice.
        try
        {
            var tmp = RootChoice + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, RootChoice, overwrite: true); // App-owned preference only.
            // Remove obsolete local preference so it cannot override a moved install.
            // Do NOT purge user data or the LocalChoice here.
        }
        catch (UnauthorizedAccessException) when (mode == StorageMode.Local)
        {
            Directory.CreateDirectory(LocalDirectory);
            File.WriteAllText(LocalChoice, json);
        }
        catch (IOException) when (mode == StorageMode.Local)
        {
            Directory.CreateDirectory(LocalDirectory);
            File.WriteAllText(LocalChoice, json);
        }
    }
}
