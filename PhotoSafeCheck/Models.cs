using System.Security.Cryptography;
using System.Text.Json;

namespace PhotoSafeCheck;

public sealed class PhotoRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Path { get; set; } = "";
    public string Name { get; set; } = "";
    public long Bytes { get; set; }
    public long ModifiedTicks { get; set; }
    public string Status { get; set; } = "Not checked";
    public string Detail { get; set; } = "";
    public DateTimeOffset? CheckedAtUtc { get; set; }
}

public sealed class ProjectState
{
    public string Folder { get; set; } = "";
    public int DelaySeconds { get; set; } = 12;
    // Zero = unlimited; any positive number = user-defined limit (500 is a suggested example).
    public int DailyLimit { get; set; } = 0;
    public DateOnly DailyDate { get; set; } = DateOnly.FromDateTime(DateTime.Now);
    public int DailyUsed { get; set; }
    public bool PlayCompletionChime { get; set; } = true;
    public List<PhotoRecord> Photos { get; set; } = new();
}

public static class Storage
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public static string BaseDir => StoragePaths.DataDirectory;
    private static string StateFile => System.IO.Path.Combine(BaseDir, "state.json");
    private static string TokenFile => System.IO.Path.Combine(BaseDir, "pairing-token.txt");

    public static ProjectState Load()
    {
        try { return JsonSerializer.Deserialize<ProjectState>(File.ReadAllText(StateFile)) ?? new(); }
        catch { return new(); }
    }

    public static void Save(ProjectState value)
    {
        Directory.CreateDirectory(BaseDir);
        var tmp = StateFile + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(value, Options));
        File.Move(tmp, StateFile, true);
    }

    public static string GetOrCreatePairingToken()
    {
        Directory.CreateDirectory(BaseDir);
        if (File.Exists(TokenFile))
        {
            var prior = File.ReadAllText(TokenFile).Trim();
            if (prior.Length >= 32) return prior;
        }
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        File.WriteAllText(TokenFile, token);
        // Token is only for local pairing; never send it to Google Photos.
        try
        {
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(TokenFile, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch { /* Some filesystems do not support Unix permissions. */ }
        return token;
    }
}
