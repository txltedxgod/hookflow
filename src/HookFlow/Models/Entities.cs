namespace HookFlow.Models;

public enum DeliveryStatus
{
    Pending = 0,
    Success = 1,
    Failed = 2,
    Exhausted = 3
}

public class WebhookSubscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string TargetUrl { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string EventType { get; set; } = "*"; // e.g. "order.created" or wildcard "*"
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<WebhookDeliveryAttempt> DeliveryAttempts { get; set; } = new();
}

public class WebhookDeliveryAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SubscriptionId { get; set; }
    public WebhookSubscription? Subscription { get; set; }

    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DeliveryStatus Status { get; set; } = DeliveryStatus.Pending;

    public int AttemptCount { get; set; } = 0;
    public int? ResponseStatusCode { get; set; }
    public string? ResponseBody { get; set; }
    public string? ErrorMessage { get; set; }
    public long DurationMs { get; set; }

    public DateTime ScheduledAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ExecutedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
