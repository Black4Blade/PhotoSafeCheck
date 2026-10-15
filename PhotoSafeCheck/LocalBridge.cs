using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace PhotoSafeCheck;

// A private, localhost-only bridge. This is NOT a web service and must never be
// exposed to a LAN interface. The extension sees stems and statuses, not file bytes.
public sealed class LocalBridge : IAsyncDisposable
{
    public const int Port = 37541;
    private readonly ScannerCoordinator scanner;
    private readonly string token;
    private WebApplication? app;
    public DateTimeOffset? LastActivityUtc { get; private set; }
    public bool IsListening { get; private set; }

    public LocalBridge(ScannerCoordinator scanner, string token)
    {
        this.scanner = scanner;
        this.token = token;
    }

    public async Task StartAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = Array.Empty<string>() });
        builder.WebHost.UseUrls($"http://127.0.0.1:{Port}");
        builder.Logging.ClearProviders();
        app = builder.Build();

        // Never serve Access-Control-Allow-Origin to arbitrary websites.
        // Chrome extensions use the narrow loopback host permission instead.
        app.Use(async (ctx, next) =>
        {
            ctx.Response.Headers["Cache-Control"] = "no-store";
            ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
            var origin = ctx.Request.Headers["Origin"].ToString();
            if (origin.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                origin.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                ctx.Response.StatusCode = 403;
                return;
            }
            await next();
        });

        app.MapGet("/api/status", (HttpContext context) =>
        {
            if (!Authorized(context)) return Results.Unauthorized();
            return Results.Json(scanner.Snapshot());
        });
        app.MapGet("/api/job", (HttpContext context) =>
        {
            if (!Authorized(context)) return Results.Unauthorized();
            var client = context.Request.Query["client"].ToString();
            if (!ValidClient(client)) return Results.BadRequest(new { error = "Invalid client ID" });
            return Results.Json(scanner.NextJob(client));
        });
        app.MapPost("/api/report", (HttpContext context, ScanReport report) =>
        {
            if (!Authorized(context)) return Results.Unauthorized();
            var client = context.Request.Query["client"].ToString();
            if (!ValidClient(client)) return Results.BadRequest(new { error = "Invalid client ID" });
            if (string.IsNullOrWhiteSpace(report.Id) || string.IsNullOrWhiteSpace(report.Status) ||
                report.Detail is null || report.Detail.Length > 2000)
                return Results.BadRequest(new { error = "Invalid report" });
            return scanner.Report(client, report)
                ? Results.Ok(new { saved = true })
                : Results.Conflict(new { error = "Job expired, already reported, or belongs to another tab" });
        });
        await app.StartAsync();
        IsListening = true;
    }

    private bool Authorized(HttpContext context)
    {
        var received = context.Request.Headers["X-PhotoSafe-Token"].ToString();
        if (received.Length != token.Length) return false;
        var valid = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(received), Encoding.UTF8.GetBytes(token));
        if (valid) LastActivityUtc = DateTimeOffset.UtcNow;
        return valid;
    }

    private static bool ValidClient(string client) =>
        client.Length is >= 1 and <= 100 && client.All(c => char.IsLetterOrDigit(c) || c is '-' or '_');

    public async ValueTask DisposeAsync()
    {
        IsListening = false;
        if (app is not null)
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }
}
