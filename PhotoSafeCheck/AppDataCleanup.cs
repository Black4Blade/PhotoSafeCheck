namespace PhotoSafeCheck;

/// <summary>
/// Optional user-confirmed cleanup of known PhotoSafe-owned application metadata.
/// This NEVER traverses photo collections or scans arbitrary folders.
/// </summary>
internal static class AppDataCleanup
{
    private static readonly string[] RelativeFiles =
    {
        "state.json", "state.json.tmp", "pairing-token.txt", "storage-choice.json", "storage-choice.json.tmp",
        Path.Combine("logs", "startup.log"), Path.Combine("logs", "startup.previous.log")
    };
    private static bool SafeDirectory(string path)
    {
        if (!Directory.Exists(path)) return false;
        return (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0;
    }
    internal static IReadOnlyList<string> Preview()
    {
        var root = StoragePaths.LocalDirectory;
        if (!SafeDirectory(root)) return Array.Empty<string>();
        var entries = new List<string>();
        foreach (var name in RelativeFiles)
        {
            var file = Path.Combine(root, name);
            var parent = Path.GetDirectoryName(file)!;
            if (!SafeDirectory(parent)) continue;
            if (File.Exists(file) && (File.GetAttributes(file) & FileAttributes.ReparsePoint) == 0)
                entries.Add(name);
        }
        return entries;
    }
    internal static string Clean()
    {
        if (!StoragePaths.IsPortable)
            return "Local mode is active. Switch to Portable mode first, then clean the old local data.";
        var root = StoragePaths.LocalDirectory;
        if (!SafeDirectory(root)) return "No safe PhotoSafe Check AppData folder found.";
        var items = Preview();
        foreach (var name in items)
        {
            var file = Path.Combine(root, name);
            File.Delete(file); // Explicitly approved PhotoSafe metadata ONLY.
        }
        var logs = Path.Combine(root, "logs");
        if (SafeDirectory(logs) && !Directory.EnumerateFileSystemEntries(logs).Any())
            Directory.Delete(logs);
        var extra = Directory.EnumerateFileSystemEntries(root).Any();
        if (!extra) Directory.Delete(root);
        return $"Cleared {items.Count} known PhotoSafe Check file(s) from AppData." +
            (extra ? " Other unknown items were left untouched; review them manually if necessary." :
            " The now-empty application folder was removed.");
    }
}
