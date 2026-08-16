namespace HookFlow.DTOs;

public record CreateSubscriptionRequest(
    string Name,
    string TargetUrl,
    string? SecretKey,
    string? EventType
);

public record IngestEventRequest(
    string EventType,
    object Data
);

public record SubscriptionDto(
    Guid Id,
    string Name,
    string TargetUrl,
    string EventType,
    bool IsActive,
    DateTime CreatedAtUtc
);

public record DeliveryAttemptDto(
    Guid Id,
    Guid SubscriptionId,
    string EventType,
    string Status,
    int AttemptCount,
    int? ResponseStatusCode,
    string? ErrorMessage,
    long DurationMs,
    DateTime CreatedAtUtc,
    DateTime? ExecutedAtUtc
);
