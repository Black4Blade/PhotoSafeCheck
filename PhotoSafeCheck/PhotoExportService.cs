using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace PhotoSafeCheck;

/// <summary>
/// Only writes newly-created reports or copies to a dedicated staging folder.
/// NEVER deletes, moves, alters, or overwrites an existing photo or file.
/// </summary>
public static class PhotoExportService
{
    public const string UploadFolderName = "PhotoSafeCheck_NotFound_ToUpload";
    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static string QuoteCsv(string? value)
    {
        var cleaned = (value ?? "").Replace("\r", " ").Replace("\n", " ");
        // Avoid formula evaluation in spreadsheet software when the report is opened.
        if (cleaned.TrimStart().StartsWith('=') || cleaned.TrimStart().StartsWith('+') ||
            cleaned.TrimStart().StartsWith('-') || cleaned.TrimStart().StartsWith('@'))
            cleaned = "'" + cleaned;
        return "\"" + cleaned.Replace("\"", "\"\"") + "\"";
    }

    private static string UniqueOutputFile(string folder, string prefix, string extension)
    {
        // Exclusivity is enforced by FileMode.CreateNew at the point of writing.
        // The caller may retry a name collision, but must never overwrite.
        var baseName = $"{prefix}_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
        for (var index = 0; index < 100000; index++)
        {
            var name = baseName + (index == 0 ? "" : $"_{index}") + extension;
            var candidate = Path.Combine(folder, name);
            if (!File.Exists(candidate) && !Directory.Exists(candidate)) return candidate;
        }
        throw new IOException("Unable to allocate an unused report filename.");
    }

    private static async Task<string> WriteCsvAsync(string folder, string prefix,
        IReadOnlyList<PhotoRecord> photos, CancellationToken token)
    {
        var path = UniqueOutputFile(folder, prefix, ".csv");
        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write,
            FileShare.None, 65536, useAsync: true);
        await using var writer = new StreamWriter(file, new UTF8Encoding(true));
        await writer.WriteLineAsync("Path,Filename,SizeBytes,Status,Detail,CheckedUtc");
        foreach (var photo in photos)
        {
            token.ThrowIfCancellationRequested();
            await writer.WriteLineAsync(string.Join(",",
                QuoteCsv(photo.Path), QuoteCsv(photo.Name),
                photo.Bytes.ToString(CultureInfo.InvariantCulture),
                QuoteCsv(photo.Status), QuoteCsv(photo.Detail),
                QuoteCsv(photo.CheckedAtUtc?.ToString("O"))));
        }
        return path;
    }

    public sealed record ExportResult(string FoundCsv, string NotFoundCsv,
        string? ReviewCsv, int FoundCount, int NotFoundCount, int NeedsReviewCount);

    public static async Task<ExportResult> ExportSeparateListsAsync(string destinationFolder,
        IReadOnlyList<PhotoRecord> photoSnapshot, CancellationToken token = default)
    {
        if (!Directory.Exists(destinationFolder))
            throw new DirectoryNotFoundException("Choose an existing report destination folder.");
        var found = photoSnapshot.Where(p => p.Status == "Possible match").ToList();
        var notFound = photoSnapshot.Where(p => p.Status == "Not found by search").ToList();
        var uncertain = photoSnapshot.Where(p => p.Status == "Needs review").ToList();
        var foundPath = await WriteCsvAsync(destinationFolder, "PhotoSafe_Found_Possible", found, token);
        var missingPath = await WriteCsvAsync(destinationFolder, "PhotoSafe_NotFound_BySearch", notFound, token);
        var uncertainPath = uncertain.Count > 0
            ? await WriteCsvAsync(destinationFolder, "PhotoSafe_NeedsReview", uncertain, token) : null;
        return new ExportResult(foundPath, missingPath, uncertainPath,
            found.Count, notFound.Count, uncertain.Count);
    }

    public sealed record CopyProgress(int Completed, int Total, string CurrentName);
    public sealed record CopyResult(string Destination, int Copied, int AlreadyPresent,
        int SkippedChangedOrUnsafe, int Failed, long BytesCopied,
        bool Cancelled, IReadOnlyList<string> Issues);

    /// <summary>
    /// Copies only Not found by search records into a dedicated folder inside root.
    /// Keeps source-relative subdirectories and never overwrites an existing file.
    /// A failed/interrupted copy stays under a unique .photosafe-partial name,
    /// never a misleading complete target filename. No cleanup/deletions performed.
    /// </summary>
    public static async Task<CopyResult> CopyNotFoundAsync(string root,
        IReadOnlyList<PhotoRecord> snapshot, IProgress<CopyProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        var rootFull = Path.GetFullPath(root);
        if (!Directory.Exists(rootFull)) throw new DirectoryNotFoundException("Indexed source folder was not found.");
        if (HasReparsePoint(rootFull)) throw new IOException("The selected root is a symbolic link/junction; choose a regular folder.");
        var targetRoot = Path.Combine(rootFull, UploadFolderName);
        var sources = snapshot.Where(p => p.Status == "Not found by search").ToArray();
        if (sources.Length == 0) throw new InvalidOperationException("There are no not-found results to copy.");
        Directory.CreateDirectory(targetRoot);
        if (HasReparsePoint(targetRoot)) throw new IOException("The destination staging folder is a link/junction. Copy cancelled.");

        int copied = 0, present = 0, skipped = 0, failed = 0;
        long bytesCopied = 0;
        var issues = new List<string>();
        bool cancelled = false;
        for (int i = 0; i < sources.Length; i++)
        {
            var item = sources[i];
            if (cancellationToken.IsCancellationRequested) { cancelled = true; break; }
            progress?.Report(new CopyProgress(i, sources.Length, item.Name));
            try
            {
                var source = Path.GetFullPath(item.Path);
                var relative = Path.GetRelativePath(rootFull, source);
                if (!IsSafeRelative(relative) ||
                    relative.Equals(UploadFolderName, PathComparison) ||
                    relative.StartsWith(UploadFolderName + Path.DirectorySeparatorChar, PathComparison) ||
                    !AncestorsAreOrdinary(rootFull, source))
                {
                    skipped++;
                    AddIssue(issues, item.Name + ": source outside the selected folder or linked path.");
                    continue;
                }
                var details = new FileInfo(source);
                if (!details.Exists || HasReparsePoint(source) ||
                    details.Length != item.Bytes || details.LastWriteTimeUtc.Ticks != item.ModifiedTicks)
                {
                    skipped++;
                    AddIssue(issues, item.Name + ": missing or changed since indexing; re-index first.");
                    continue;
                }
                var destination = Path.Combine(targetRoot, relative);
                var destinationDir = Path.GetDirectoryName(destination)!;
                // Check all existing ancestors before creating directories.
                if (!AncestorsAreOrdinary(targetRoot, destinationDir))
                {
                    skipped++;
                    AddIssue(issues, item.Name + ": staging path contains a link/junction.");
                    continue;
                }
                Directory.CreateDirectory(destinationDir);
                if (!AncestorsAreOrdinary(targetRoot, destinationDir))
                {
                    skipped++;
                    AddIssue(issues, item.Name + ": unsafe staging directory.");
                    continue;
                }
                if (File.Exists(destination) || Directory.Exists(destination))
                {
                    present++;
                    continue; // Never touch a preexisting copy; it may be user-managed.
                }
                // Source is opened read-only; destination is a *new* temporary copy.
                var temporary = destination + ".photosafe-partial-" + Guid.NewGuid().ToString("N");
                await using (var input = new FileStream(source, FileMode.Open, FileAccess.Read,
                    FileShare.Read, 131072, useAsync: true))
                await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                    FileShare.None, 131072, useAsync: true))
                {
                    await input.CopyToAsync(output, cancellationToken);
                    await output.FlushAsync(cancellationToken);
                }
                var after = new FileInfo(source);
                if (after.Length != item.Bytes || after.LastWriteTimeUtc.Ticks != item.ModifiedTicks ||
                    new FileInfo(temporary).Length != item.Bytes)
                    throw new IOException("Source changed during copy. Incomplete copy remains marked .photosafe-partial.");
                // Verify byte-for-byte copy using SHA-256 before promoting the
                // newly-created staging file. Only reads both copies.
                {
                    await using var checkOriginal = new FileStream(source, FileMode.Open,
                        FileAccess.Read, FileShare.Read, 131072, useAsync: true);
                    await using var checkCopy = new FileStream(temporary, FileMode.Open,
                        FileAccess.Read, FileShare.Read, 131072, useAsync: true);
                    var originalHash = await SHA256.HashDataAsync(checkOriginal, cancellationToken);
                    var copiedHash = await SHA256.HashDataAsync(checkCopy, cancellationToken);
                    if (!originalHash.AsSpan().SequenceEqual(copiedHash))
                        throw new IOException("Copy hash mismatch; partial output left visibly marked. Original untouched.");
                }
                // All file handles have closed before finalizing, including on Windows.
                // Only rename a newly-created staging copy, never an original.
                File.Move(temporary, destination); // Does not overwrite destination.
                copied++;
                bytesCopied += item.Bytes;
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
                AddIssue(issues, item.Name + ": copying was cancelled. Any partial copy is clearly marked.");
                break;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                failed++;
                AddIssue(issues, item.Name + ": " + ex.Message);
            }
        }
        progress?.Report(new CopyProgress(copied + present + skipped + failed, sources.Length,
            cancelled ? "Cancelled" : "Complete"));
        return new CopyResult(targetRoot, copied, present, skipped, failed, bytesCopied, cancelled, issues);
    }

    private static bool IsSafeRelative(string relative) =>
        relative != "." && !Path.IsPathRooted(relative) &&
        relative != ".." &&
        !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
        !relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal);

    private static bool HasReparsePoint(string path)
    {
        try { return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0; }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
    }

    // Verify each existing directory from base to path; missing subdirectories
    // are allowed because they'll be created beneath a verified parent.
    private static bool AncestorsAreOrdinary(string baseDir, string target)
    {
        var basePath = Path.GetFullPath(baseDir);
        var path = Path.GetFullPath(target);
        var rel = Path.GetRelativePath(basePath, path);
        if (!IsSafeRelative(rel) && rel != ".") return false;
        if (HasReparsePoint(basePath)) return false;
        var current = basePath;
        if (rel == ".") return true;
        foreach (var part in rel.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, part);
            if (HasReparsePoint(current)) return false;
        }
        return true;
    }

    private static void AddIssue(List<string> issues, string message)
    {
        if (issues.Count < 12) issues.Add(message);
    }
}
