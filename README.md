# hookflow

> High-throughput webhook dispatch and retry gateway with exponential backoff and HMAC signatures in C# / .NET 8.

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com)
[![C#](https://img.shields.io/badge/C%23-12-239120?style=flat-square&logo=csharp)](https://docs.microsoft.com/dotnet/csharp/)
[![EF Core](https://img.shields.io/badge/EF%20Core-8.0-512BD4?style=flat-square)](https://docs.microsoft.com/ef/core)
[![Docker](https://img.shields.io/badge/Docker-Ready-2496ED?style=flat-square&logo=docker)](https://docker.com)
[![License](https://img.shields.io/badge/License-MIT-blue?style=flat-square)](LICENSE)

`#webhook` `#webhook-gateway` `#dotnet8` `#csharp` `#event-driven` `#background-workers` `#hmac-sha256` `#resilience`

---

## Features

- **Asynchronous Dispatch Queue:** Background HTTP delivery powered by .NET `BackgroundService`.
- **Cryptographic Security:** HMAC-SHA256 request signatures (`X-HookFlow-Signature: sha256=...`).
- **Resilient Retry Policies:** Automatic exponential backoff for failed deliveries with configurable max retries.
- **Event Filtering:** Wildcard (`*`) and topic-based subscription routing (`order.created`, `payment.failed`).
- **Delivery Auditing:** Real-time logging of HTTP response codes, latency timing, and response payloads.
- **Manual Retry API:** Re-queue and re-dispatch exhausted attempts with one click/request.

## Quick Start

### With .NET CLI

```bash
# Build solution
dotnet build HookFlow.sln

# Run unit & integration tests
dotnet test

# Run API
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
  "name": "Payment Webhook",
  "targetUrl": "https://api.example.com/webhooks/payment",
  "eventType": "payment.succeeded"
}
```

### 2. Ingest Event
```http
POST /api/events/publish
Content-Type: application/json

{
  "eventType": "payment.succeeded",
  "data": {
    "orderId": "ORD-1092",
    "amount": 99.00
  }
}
```

### 3. Monitoring & Retries
```http
GET /api/deliveries?status=Failed
POST /api/deliveries/{id}/retry
GET /api/stats
```
