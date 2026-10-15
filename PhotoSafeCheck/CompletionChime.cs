using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PhotoSafeCheck;

/// <summary>Plays the embedded completion chime; never reads or changes user media.</summary>
internal static class CompletionChime
{
    private const uint SndMemory = 0x0004;
    private const uint SndNoDefault = 0x0002;

    [DllImport("winmm.dll", EntryPoint = "PlaySoundW", ExactSpelling = true)]
    private static extern bool PlaySoundMemory(byte[] sound, IntPtr module, uint flags);

    public static void Play()
    {
        // Schedule playback: never block the UI or the end-of-scan notification.
        _ = Task.Run(async () =>
        {
            try
            {
                using var resource = BundledAssets.Open("complete.wav");
                if (resource is null) return;
                if (OperatingSystem.IsWindows())
                {
                    using var memory = new MemoryStream();
                    await resource.CopyToAsync(memory);
                    // SND_MEMORY doesn't support SND_ASYNC; playing synchronously on a worker is safe.
                    PlaySoundMemory(memory.ToArray(), IntPtr.Zero, SndMemory | SndNoDefault);
                    return;
                }
                if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux()) return;
                // macOS/Linux audio utilities accept filenames. Use an app-owned temporary
                // file and remove it once playback exits; it is never placed beside the EXE.
                var folder = Path.Combine(StoragePaths.DataDirectory, "TempAudio");
                Directory.CreateDirectory(folder);
                var file = Path.Combine(folder, $"chime-{Guid.NewGuid():N}.wav");
                try
                {
                    await using (var output = File.Create(file)) await resource.CopyToAsync(output);
                    if (OperatingSystem.IsMacOS()) await RunPlayer("afplay", file);
                    else if (!await RunPlayer("paplay", file)) await RunPlayer("aplay", file);
                }
                finally { try { File.Delete(file); } catch { } }
            }
            catch { /* Optional audio must never impact scan results. */ }
        });
    }

    private static async Task<bool> RunPlayer(string command, string file)
    {
        try
        {
            var start = new ProcessStartInfo(command) { UseShellExecute = false, CreateNoWindow = true };
            start.ArgumentList.Add(file);
            using var proc = Process.Start(start);
            if (proc is null) return false;
            await proc.WaitForExitAsync();
            return proc.ExitCode == 0;
        }
        catch { return false; }
    }
}
