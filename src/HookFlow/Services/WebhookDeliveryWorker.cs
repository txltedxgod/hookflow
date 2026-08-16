using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using HookFlow.Data;
using HookFlow.Models;
using Microsoft.EntityFrameworkCore;

namespace HookFlow.Services;

public class WebhookDeliveryWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISignatureService _signatureService;
    private readonly ILogger<WebhookDeliveryWorker> _logger;
    private readonly IConfiguration _config;

    public WebhookDeliveryWorker(
        IServiceProvider serviceProvider,
        IHttpClientFactory httpClientFactory,
        ISignatureService signatureService,
        ILogger<WebhookDeliveryWorker> logger,
        IConfiguration config)
    {
        _serviceProvider = serviceProvider;
        _httpClientFactory = httpClientFactory;
        _signatureService = signatureService;
        _logger = logger;
        _config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Webhook Delivery Worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingWebhooksAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing webhook queue");
            }

            await Task.Delay(1000, stoppingToken);
        }
    }

    private async Task ProcessPendingWebhooksAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var maxRetries = _config.GetValue<int>("HookFlow:MaxRetryAttempts", 5);
        var initialBackoff = _config.GetValue<int>("HookFlow:InitialBackoffSeconds", 2);

        var now = DateTime.UtcNow;
        var pendingAttempts = await db.DeliveryAttempts
            .Include(a => a.Subscription)
            .Where(a => a.Status == DeliveryStatus.Pending && a.ScheduledAtUtc <= now)
            .OrderBy(a => a.ScheduledAtUtc)
            .Take(20)
            .ToListAsync(ct);

        if (!pendingAttempts.Any()) return;

        var client = _httpClientFactory.CreateClient("WebhookClient");

        foreach (var attempt in pendingAttempts)
        {
            if (attempt.Subscription == null || !attempt.Subscription.IsActive)
            {
                attempt.Status = DeliveryStatus.Exhausted;
                attempt.ErrorMessage = "Subscription inactive or missing";
                continue;
            }

            attempt.AttemptCount++;
            var sw = Stopwatch.StartNew();

            try
            {
                var signature = _signatureService.ComputeHmacSha256(attempt.Payload, attempt.Subscription.SecretKey);
                using var request = new HttpRequestMessage(HttpMethod.Post, attempt.Subscription.TargetUrl)
                {
                    Content = new StringContent(attempt.Payload, Encoding.UTF8, "application/json")
                };

                request.Headers.Add("X-HookFlow-Event", attempt.EventType);
                request.Headers.Add("X-HookFlow-Delivery", attempt.Id.ToString());
                request.Headers.Add("X-HookFlow-Signature", $"sha256={signature}");
                request.Headers.Add("User-Agent", "HookFlow-Dispatcher/1.0");

                var response = await client.SendAsync(request, ct);
                sw.Stop();

                attempt.DurationMs = sw.ElapsedMilliseconds;
                attempt.ExecutedAtUtc = DateTime.UtcNow;
                attempt.ResponseStatusCode = (int)response.StatusCode;

                var bodyText = await response.Content.ReadAsStringAsync(ct);
                attempt.ResponseBody = bodyText.Length > 2000 ? bodyText[..2000] : bodyText;

                if (response.IsSuccessStatusCode)
                {
                    attempt.Status = DeliveryStatus.Success;
                    attempt.ErrorMessage = null;
                }
                else
                {
                    HandleFailure(attempt, maxRetries, initialBackoff, $"HTTP {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                attempt.DurationMs = sw.ElapsedMilliseconds;
                attempt.ExecutedAtUtc = DateTime.UtcNow;
                HandleFailure(attempt, maxRetries, initialBackoff, ex.Message);
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private static void HandleFailure(WebhookDeliveryAttempt attempt, int maxRetries, int initialBackoff, string reason)
    {
        attempt.ErrorMessage = reason;
        if (attempt.AttemptCount >= maxRetries)
        {
            attempt.Status = DeliveryStatus.Exhausted;
        }
        else
        {
            // Exponential backoff: 2s, 4s, 8s, 16s...
            var delaySeconds = Math.Pow(2, attempt.AttemptCount - 1) * initialBackoff;
            attempt.ScheduledAtUtc = DateTime.UtcNow.AddSeconds(delaySeconds);
            attempt.Status = DeliveryStatus.Pending;
        }
    }
}
