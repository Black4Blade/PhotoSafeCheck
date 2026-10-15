using System.Runtime.InteropServices;
using System.Text;

namespace PhotoSafeCheck;

internal static class StartupDiagnostics
{
    private static readonly object Gate = new();
    private static readonly List<string> Pending = new();
    internal static string LogFile => StoragePaths.IsSelected
        ? Path.Combine(StoragePaths.DataDirectory, "logs", "startup.log")
        : "(No log file created: storage mode not chosen yet)";

    internal static void Log(string message)
    {
        var line = $"[{DateTimeOffset.Now:O}] {message}{Environment.NewLine}";
        try { Console.Error.WriteLine($"PhotoSafe Check: {message}"); } catch { }
        lock (Gate)
        {
            if (!StoragePaths.IsSelected) { Pending.Add(line); return; }
            Write(line);
        }
    }
    internal static void FlushPending()
    {
        lock (Gate)
        {
            if (!StoragePaths.IsSelected) return;
            foreach (var line in Pending) Write(line);
            Pending.Clear();
        }
    }
    private static void Write(string line)
    {
        try
        {
            var log = LogFile;
            var dir = Path.GetDirectoryName(log)!;
            Directory.CreateDirectory(dir);
            if (File.Exists(log) && new FileInfo(log).Length > 1_048_576)
            {
                var previous = Path.Combine(dir, "startup.previous.log");
                File.Move(log, previous, overwrite: true); // Only PhotoSafe logs.
            }
            File.AppendAllText(log, line, Encoding.UTF8);
        }
        catch { /* Logging failure must not cause a crash. */ }
    }
    internal static void Report(string reason, Exception? error) =>
        Log(reason + Environment.NewLine + (error?.ToString() ?? "(No exception object available)"));
    internal static void ShowFatalError(Exception error)
    {
        var message = "PhotoSafe Check couldn't start.\n\n" + error.GetType().Name + ": " + error.Message +
            "\n\nDetails saved to:\n" + LogFile;
        if (!OperatingSystem.IsWindows()) return;
        try { MessageBoxW(IntPtr.Zero, message, "PhotoSafe Check startup error", 0x10u); }
        catch { }
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "MessageBoxW")]
    private static extern int MessageBoxW(IntPtr hwnd, string message, string title, uint type);
}
