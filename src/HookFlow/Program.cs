using System.Text.Json;
using HookFlow.Data;
using HookFlow.DTOs;
using HookFlow.Models;
using HookFlow.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to DI container
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=hookflow.db";
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));

builder.Services.AddHttpClient("WebhookClient", client =>
{
    client.Timeout = TimeSpan.FromSeconds(builder.Configuration.GetValue<int>("HookFlow:DeliveryTimeoutSeconds", 10));
});

builder.Services.AddSingleton<ISignatureService, SignatureService>();
builder.Services.AddHostedService<WebhookDeliveryWorker>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Auto-migrate database on start
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ----------------------------------------------------
// Subscriptions Endpoints
// ----------------------------------------------------

app.MapGet("/api/subscriptions", async (AppDbContext db) =>
{
    var list = await db.Subscriptions
        .OrderByDescending(s => s.CreatedAtUtc)
        .Select(s => new SubscriptionDto(s.Id, s.Name, s.TargetUrl, s.EventType, s.IsActive, s.CreatedAtUtc))
        .ToListAsync();
    return Results.Ok(list);
});

app.MapPost("/api/subscriptions", async ([FromBody] CreateSubscriptionRequest req, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.TargetUrl))
    {
        return Results.BadRequest(new { error = "Name and TargetUrl are required." });
    }

    var secret = string.IsNullOrWhiteSpace(req.SecretKey) 
        ? Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N")
        : req.SecretKey;

    var sub = new WebhookSubscription
    {
        Name = req.Name.Trim(),
        TargetUrl = req.TargetUrl.Trim(),
        SecretKey = secret,
        EventType = string.IsNullOrWhiteSpace(req.EventType) ? "*" : req.EventType.Trim()
    };

    db.Subscriptions.Add(sub);
    await db.SaveChangesAsync();

    return Results.Created($"/api/subscriptions/{sub.Id}", new
    {
        sub.Id,
        sub.Name,
        sub.TargetUrl,
        sub.EventType,
        sub.SecretKey,
        sub.IsActive,
        sub.CreatedAtUtc
    });
});

app.MapDelete("/api/subscriptions/{id:guid}", async (Guid id, AppDbContext db) =>
{
    var sub = await db.Subscriptions.FindAsync(id);
    if (sub == null) return Results.NotFound();

    db.Subscriptions.Remove(sub);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

// ----------------------------------------------------
// Event Ingestion & Webhook Dispatch Trigger
// ----------------------------------------------------

app.MapPost("/api/events/publish", async ([FromBody] IngestEventRequest req, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(req.EventType))
    {
        return Results.BadRequest(new { error = "EventType is required." });
    }

    var matchingSubs = await db.Subscriptions
        .Where(s => s.IsActive && (s.EventType == "*" || s.EventType == req.EventType))
        .ToListAsync();

    if (!matchingSubs.Any())
    {
        return Results.Ok(new { delivered_to = 0, message = "No active subscriptions matched event type." });
    }

    var payloadJson = JsonSerializer.Serialize(new
    {
        @event = req.EventType,
        timestamp = DateTime.UtcNow,
        data = req.Data
    });

    var attempts = matchingSubs.Select(s => new WebhookDeliveryAttempt
    {
        SubscriptionId = s.Id,
        EventType = req.EventType,
        Payload = payloadJson,
        Status = DeliveryStatus.Pending,
        ScheduledAtUtc = DateTime.UtcNow
    }).ToList();

    db.DeliveryAttempts.AddRange(attempts);
    await db.SaveChangesAsync();

    return Results.Accepted("/api/deliveries", new
    {
        dispatched_count = attempts.Count,
        delivery_ids = attempts.Select(a => a.Id)
    });
});

// ----------------------------------------------------
// Delivery Logs & Stats
// ----------------------------------------------------

app.MapGet("/api/deliveries", async (
    [FromQuery] string? status, 
    [FromQuery] Guid? subscriptionId,
    [FromQuery] int limit,
    AppDbContext db) =>
{
    limit = limit <= 0 ? 50 : Math.Min(limit, 200);

    var query = db.DeliveryAttempts.AsQueryable();

    if (!string.IsNullOrEmpty(status) && Enum.TryParse<DeliveryStatus>(status, true, out var parsedStatus))
    {
        query = query.Where(d => d.Status == parsedStatus);
    }

    if (subscriptionId.HasValue)
    {
        query = query.Where(d => d.SubscriptionId == subscriptionId.Value);
    }

    var deliveries = await query
        .OrderByDescending(d => d.CreatedAtUtc)
        .Take(limit)
        .Select(d => new DeliveryAttemptDto(
            d.Id,
            d.SubscriptionId,
            d.EventType,
            d.Status.ToString(),
            d.AttemptCount,
            d.ResponseStatusCode,
            d.ErrorMessage,
            d.DurationMs,
            d.CreatedAtUtc,
            d.ExecutedAtUtc
        ))
        .ToListAsync();

    return Results.Ok(deliveries);
});

app.MapPost("/api/deliveries/{id:guid}/retry", async (Guid id, AppDbContext db) =>
{
    var attempt = await db.DeliveryAttempts.FindAsync(id);
    if (attempt == null) return Results.NotFound();

    attempt.Status = DeliveryStatus.Pending;
    attempt.ScheduledAtUtc = DateTime.UtcNow;
    attempt.ErrorMessage = null;
    await db.SaveChangesAsync();

    return Results.Ok(new { message = "Delivery re-queued successfully.", attempt.Id });
});

app.MapGet("/api/stats", async (AppDbContext db) =>
{
    var totalSubscriptions = await db.Subscriptions.CountAsync();
    var totalDeliveries = await db.DeliveryAttempts.CountAsync();
    var successfulDeliveries = await db.DeliveryAttempts.CountAsync(d => d.Status == DeliveryStatus.Success);
    var failedDeliveries = await db.DeliveryAttempts.CountAsync(d => d.Status == DeliveryStatus.Exhausted);
    var pendingDeliveries = await db.DeliveryAttempts.CountAsync(d => d.Status == DeliveryStatus.Pending);

    return Results.Ok(new
    {
        subscriptions = totalSubscriptions,
        total_deliveries = totalDeliveries,
        successful = successfulDeliveries,
        failed = failedDeliveries,
        pending = pendingDeliveries
    });
});

app.Run();
