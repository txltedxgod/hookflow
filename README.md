# hookflow

A resilient, high-throughput webhook dispatch and retry gateway built with **C# / .NET 8**, ASP.NET Core Minimal APIs, and background workers.

## Features

- **Reliable Background Dispatch:** Asynchronous HTTP webhook delivery queue using .NET `BackgroundService`.
- **HMAC-SHA256 Signatures:** Cryptographic request signing (`X-HookFlow-Signature: sha256=...`) for authenticating payloads.
- **Configurable Exponential Backoff:** Automatic retries on HTTP errors (5xx, timeouts) with increasing backoff intervals.
- **Event Subscriptions & Filtering:** Route events based on wildcards (`*`) or specific topic keys (e.g. `order.paid`, `user.created`).
- **Delivery Auditing:** Full history with response status codes, latency timings, and truncated response bodies.
- **Manual Retry API:** Re-queue failed or exhausted webhook attempts on-demand.

## Architecture

```
                      ┌──────────────────────┐
                      │    Publish API       │
                      │  /api/events/publish │
                      └──────────┬───────────┘
                                 │
                                 ▼
                      ┌──────────────────────┐
                      │ Delivery Queue (DB)  │
                      └──────────┬───────────┘
                                 │
                                 ▼
                     ┌────────────────────────┐
                     │ WebhookDeliveryWorker  │
                     │  (BackgroundService)   │
                     └───────────┬────────────┘
                                 │
                 ┌───────────────┼───────────────┐
                 ▼               ▼               ▼
          Target Server A  Target Server B  Target Server C
          (HMAC Verified)  (HMAC Verified)  (HMAC Verified)
```

## Quick Start

### With .NET CLI

```bash
# Clone and build
dotnet build HookFlow.sln

# Run tests
dotnet test

# Run service
dotnet run --project src/HookFlow
```

Navigate to `http://localhost:5000/swagger` for Swagger UI.

### With Docker

```bash
docker compose up --build
```

## API Reference

### 1. Register Webhook Subscription
```http
POST /api/subscriptions
Content-Type: application/json

{
  "name": "Billing Webhook",
  "targetUrl": "https://api.example.com/webhooks/billing",
  "eventType": "payment.succeeded"
}
```

### 2. Ingest Event & Trigger Webhook Dispatch
```http
POST /api/events/publish
Content-Type: application/json

{
  "eventType": "payment.succeeded",
  "data": {
    "orderId": "ORD-92812",
    "amount": 49.99,
    "currency": "USD"
  }
}
```

### 3. Check Delivery Logs & Retries
```http
GET /api/deliveries?status=Failed
POST /api/deliveries/{id}/retry
GET /api/stats
```

## Verifying Webhook Signatures (Consumer Example)

```csharp
using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
var computedHash = Convert.ToHexString(hmac.ComputeHash(rawBodyBytes)).ToLowerInvariant();
bool isValid = computedHash == receivedSignatureHeader.Replace("sha256=", "");
```
