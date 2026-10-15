using System.Text.Json.Serialization;

namespace PhotoSafeCheck;

public sealed record ScannerJob(string Id, string Query, string Stem, int LocalFileCount);
public sealed record ScannerStatus(
    bool Enabled, int TotalFiles, int NotChecked, int PossibleMatches,
    int NotFoundBySearch, int NeedsReview, int DailyUsed, int DailyLimit,
    int DelaySeconds, string Message, string? ActiveFile,
    DateTimeOffset? NextAllowedUtc, int RemainingSearches, int EstimatedSecondsRemaining);
public sealed record JobResponse(string State, ScannerJob? Job, string Message,
    DateTimeOffset? NextAllowedUtc, int RemainingSearches, int EstimatedSecondsRemaining);
public sealed record ScanReport(string Id, string Status, string Detail);

// All client operations are serialized. This coordinator handles only inventory
// and statuses, never writes to photo files.
public sealed class ScannerCoordinator
{
    private readonly object sync = new();
    private readonly ProjectState state;
    private bool enabled;
    private string message = "Ready. Index a folder, then connect the browser extension.";
    private DateTimeOffset? nextAllowed;
    private int consecutiveErrors;
    private string? jobOwner;
    private ScannerJob? activeJob;
    private DateTimeOffset leaseExpiry;

    public ScannerCoordinator(ProjectState existing) => state = existing;

    public ProjectState State => state;

    public void UpdateSettings(int delay, int dailyLimit, bool playCompletionChime)
    {
        lock (sync)
        {
            state.DelaySeconds = delay;
            state.DailyLimit = dailyLimit;
            state.PlayCompletionChime = playCompletionChime;
            Storage.Save(state);
        }
    }

    public void Start()
    {
        lock (sync)
        {
            enabled = true;
            consecutiveErrors = 0;
            message = "Scanning enabled. Keep Google Photos open in the activated browser tab.";
        }
    }

    public void Pause(string why = "Paused by user. Progress saved.")
    {
        lock (sync)
        {
            enabled = false;
            message = why;
            // Keep all unreported files unverified, and allow a new lease on next run.
            activeJob = null;
            jobOwner = null;
        }
    }

    public void ReplacePhotos(List<PhotoRecord> records)
    {
        lock (sync)
        {
            enabled = false;
            activeJob = null;
            jobOwner = null;
            nextAllowed = null;
            state.Photos = records;
            message = $"Indexed {records.Count:N0} files. No original files were changed.";
            Storage.Save(state);
        }
    }

    public int RequeueNeedsReview()
    {
        lock (sync)
        {
            enabled = false;
            activeJob = null;
            jobOwner = null;
            nextAllowed = null;
            var count = 0;
            foreach (var photo in state.Photos.Where(p => p.Status == "Needs review"))
            {
                photo.Status = "Not checked";
                photo.Detail = "";
                photo.CheckedAtUtc = null;
                count++;
            }
            message = $"Reset {count:N0} uncertain results for a manual retry.";
            Storage.Save(state);
            return count;
        }
    }

    // Reset only the most recently recorded search (including same-stem variants).
    // Never touch the local media or the Google Photos account.
    public int ClearLastResult()
    {
        lock (sync)
        {
            if (enabled) return 0;
            var mostRecent = state.Photos
                .Where(p => p.CheckedAtUtc.HasValue && p.Status != "Not checked")
                .OrderByDescending(p => p.CheckedAtUtc)
                .FirstOrDefault();
            if (mostRecent is null) return 0;
            var stem = Path.GetFileNameWithoutExtension(mostRecent.Name);
            var count = 0;
            foreach (var photo in state.Photos.Where(p => p.CheckedAtUtc.HasValue &&
                p.Status != "Not checked" && string.Equals(
                Path.GetFileNameWithoutExtension(p.Name), stem, StringComparison.OrdinalIgnoreCase)))
            {
                photo.Status = "Not checked";
                photo.Detail = "";
                photo.CheckedAtUtc = null;
                count++;
            }
            activeJob = null;
            jobOwner = null;
            nextAllowed = null;
            consecutiveErrors = 0;
            message = $"Cleared last search result for {stem} ({count} file(s)). Ready to retry.";
            Storage.Save(state);
            return count;
        }
    }

    // Clear scan statuses, not the inventory, files or today's actual search count.
    public int ClearAllResults()
    {
        lock (sync)
        {
            if (enabled) return 0;
            activeJob = null;
            jobOwner = null;
            nextAllowed = null;
            consecutiveErrors = 0;
            var count = 0;
            foreach (var photo in state.Photos)
            {
                if (photo.Status == "Not checked" && photo.CheckedAtUtc is null &&
                    string.IsNullOrEmpty(photo.Detail)) continue;
                photo.Status = "Not checked";
                photo.Detail = "";
                photo.CheckedAtUtc = null;
                count++;
            }
            message = $"Cleared {count:N0} stored scan result(s). Indexed photo files unchanged.";
            Storage.Save(state);
            return count;
        }
    }

    private void RolloverDate()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        if (state.DailyDate == today) return;
        state.DailyDate = today;
        state.DailyUsed = 0;
        Storage.Save(state);
    }

    // Count distinct filename stems, not individual local image variants.
    // ETA is deliberately approximate: Google Photos may load more slowly,
    // browsers throttle background tabs, and backoff can extend the duration.
    private int RemainingSearches() => state.Photos
        .Where(p => p.Status == "Not checked")
        .Select(p => Path.GetFileNameWithoutExtension(p.Name))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();

    private int EstimatedSecondsRemaining()
    {
        var remaining = RemainingSearches();
        if (remaining == 0) return 0;
        // From v0.2.6 the 12-second pacing starts AFTER a completed search,
        // so Google Photos' load time and the inter-search delay are additive.
        // No delay follows the final result.
        const int typicalPageSeconds = 13;
        var waiting = nextAllowed.HasValue
            ? Math.Max(0, (int)Math.Ceiling((nextAllowed.Value - DateTimeOffset.UtcNow).TotalSeconds))
            : 0;
        var delaysBetweenSearches = Math.Max(0, remaining - 1);
        return (int)Math.Min(int.MaxValue,
            (long)remaining * typicalPageSeconds + (long)delaysBetweenSearches * state.DelaySeconds + waiting);
    }

    private JobResponse Response(string stateName, ScannerJob? job, string detail, DateTimeOffset? next) =>
        new(stateName, job, detail, next, RemainingSearches(), EstimatedSecondsRemaining());

    public ScannerStatus Snapshot()
    {
        lock (sync)
        {
            RolloverDate();
            return new ScannerStatus(enabled, state.Photos.Count,
                state.Photos.Count(p => p.Status == "Not checked"),
                state.Photos.Count(p => p.Status == "Possible match"),
                state.Photos.Count(p => p.Status == "Not found by search"),
                state.Photos.Count(p => p.Status == "Needs review"),
                state.DailyUsed, state.DailyLimit, state.DelaySeconds,
                message, activeJob?.Stem, nextAllowed, RemainingSearches(), EstimatedSecondsRemaining());
        }
    }

    public List<PhotoRecord> ListPhotos(string filter, int max)
    {
        lock (sync)
        {
            return state.Photos
                .Where(p => filter == "All" || p.Status == filter)
                .Take(max)
                .Select(p => new PhotoRecord
                {
                    Id = p.Id, Name = p.Name, Path = p.Path, Bytes = p.Bytes,
                    ModifiedTicks = p.ModifiedTicks, Status = p.Status,
                    Detail = p.Detail, CheckedAtUtc = p.CheckedAtUtc
                }).ToList();
        }
    }

    public JobResponse NextJob(string clientId)
    {
        lock (sync)
        {
            RolloverDate();
            if (!enabled) return state.Photos.Count > 0 && state.Photos.All(p => p.Status != "Not checked") &&
                message.StartsWith("Scan pass complete", StringComparison.Ordinal)
                    ? Response("complete", null, message, null)
                    : Response("paused", null, message, nextAllowed);
            var now = DateTimeOffset.UtcNow;
            // Resume leases before testing pacing; a pending job must not get stuck.
            if (activeJob is not null)
            {
                if (now >= leaseExpiry)
                {
                    activeJob = null;
                    jobOwner = null;
                }
                else if (jobOwner == clientId)
                    return Response("ready", activeJob, "Resume active search.", null);
                else
                    return Response("busy", null, "Another tab holds the active search.", leaseExpiry);
            }
            var first = state.Photos.FirstOrDefault(p => p.Status == "Not checked");
            if (first is null)
            {
                enabled = false;
                nextAllowed = null;
                message = "Scan pass complete. Review all uncertain results before making manual decisions.";
                return Response("complete", null, message, null);
            }
            // Completion above deliberately precedes the pacing/daily-limit gates.
            if (state.DailyLimit > 0 && state.DailyUsed >= state.DailyLimit)
                return Response("daily-limit", null, "Daily limit reached. The scanner can resume tomorrow.", null);
            if (nextAllowed > now)
                return Response("waiting", null, "Waiting for pacing/backoff delay.", nextAllowed);
            var stem = Path.GetFileNameWithoutExtension(first.Name).Trim();
            if (string.IsNullOrWhiteSpace(stem))
            {
                first.Status = "Needs review";
                first.Detail = "Filename has no searchable stem.";
                Storage.Save(state);
                return Response("waiting", null, "Skipped file with blank filename stem.", now.AddSeconds(2));
            }
            // Do not strip underscores or internal words; only strip the file extension.
            var count = state.Photos.Count(p => p.Status == "Not checked" &&
                string.Equals(Path.GetFileNameWithoutExtension(p.Name), stem, StringComparison.OrdinalIgnoreCase));
            var query = "\"" + stem.Replace("\"", "") + "\"";
            activeJob = new ScannerJob(Guid.NewGuid().ToString("N"), query, stem, count);
            jobOwner = clientId;
            leaseExpiry = now.AddMinutes(5);
            state.DailyUsed++;
            nextAllowed = null; // Start cooldown only when a result is reported.
            message = $"Searching for {query} ({count} local filename variant(s)).";
            Storage.Save(state);
            return Response("ready", activeJob, message, null);
        }
    }

    public bool Report(string clientId, ScanReport report)
    {
        lock (sync)
        {
            if (activeJob is null || jobOwner != clientId || report.Id != activeJob.Id)
                return false;
            var valid = new[] { "Possible match", "Not found by search", "Needs review", "Paused" };
            if (!valid.Contains(report.Status)) return false;
            var ambiguousVariants = activeJob.LocalFileCount > 1 && report.Status == "Possible match";
            foreach (var photo in state.Photos.Where(p => p.Status == "Not checked" &&
                         string.Equals(Path.GetFileNameWithoutExtension(p.Name),
                            activeJob.Stem, StringComparison.OrdinalIgnoreCase)))
            {
                photo.Status = report.Status == "Paused" || ambiguousVariants
                    ? "Needs review" : report.Status;
                var detail = ambiguousVariants
                    ? "Multiple local files share this name; cannot determine which variant is online. " + report.Detail
                    : report.Detail;
                photo.Detail = detail.Length > 500 ? detail[..500] : detail;
                photo.CheckedAtUtc = DateTimeOffset.UtcNow;
            }
            var completedAt = DateTimeOffset.UtcNow;
            // Exact inter-search cooldown, not a clock that expires while the
            // previous Google Photos page is still loading.
            nextAllowed = completedAt.AddSeconds(state.DelaySeconds);
            if (report.Status is "Needs review")
            {
                consecutiveErrors++;
                var backoffSeconds = Math.Min(900, 15 * (1 << Math.Min(6, consecutiveErrors)));
                nextAllowed = completedAt.AddSeconds(Math.Max(state.DelaySeconds, backoffSeconds));
                if (consecutiveErrors >= 6)
                {
                    enabled = false;
                    message = "Paused after 6 uncertain searches in a row. Inspect the Google Photos tab and use Copy page diagnostics in the extension before retrying.";
                }
            }
            else consecutiveErrors = 0;
            if (report.Status == "Paused")
            {
                enabled = false;
                nextAllowed = null;
                message = "Google access/sign-in warning detected. Paused for manual review.";
            }
            else if (enabled)
                message = $"Last: {activeJob.Stem} — {report.Status}.";
            activeJob = null;
            jobOwner = null;
            if (enabled && state.Photos.Count > 0 && state.Photos.All(p => p.Status != "Not checked"))
            {
                enabled = false;
                nextAllowed = null;
                message = "Scan pass complete. Review all uncertain results before making manual decisions.";
            }
            Storage.Save(state);
            return true;
        }
    }
}
