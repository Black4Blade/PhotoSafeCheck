using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PhotoSafeCheck;

// Only plays the packaged notification sound. Does not touch user media files.
internal static class CompletionChime
{
    [DllImport("winmm.dll", EntryPoint = "PlaySoundW", CharSet = CharSet.Unicode)]
    private static extern bool PlaySoundWindows(string sound, IntPtr module, uint flags);

    public static void Play()
    {
        var sound = Path.Combine(AppContext.BaseDirectory, "Assets", "complete.wav");
        if (!File.Exists(sound)) return;
        try
        {
            if (OperatingSystem.IsWindows())
            {
                // SND_ASYNC | SND_NODEFAULT | SND_FILENAME, no user file modification.
                PlaySoundWindows(sound, IntPtr.Zero, 0x0001u | 0x0002u | 0x00020000u);
            }
            else if (OperatingSystem.IsMacOS())
                Launch("afplay", sound);
            else if (OperatingSystem.IsLinux())
            {
                if (!Launch("paplay", sound)) Launch("aplay", sound);
            }
        }
        catch { /* Audio is optional, never interrupt scan/reporting. */ }
    }

    private static bool Launch(string command, string file)
    {
        try
        {
            var args = new ProcessStartInfo(command) { UseShellExecute = false,
                CreateNoWindow = true };
            args.ArgumentList.Add(file);
            using var process = Process.Start(args);
            return process != null;
        }
        catch { return false; }
    }
}
