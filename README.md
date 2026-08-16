# HookFlow

> High-throughput, resilient webhook delivery engine with **exponential backoff**, **HMAC-SHA256 signature verification**, dead-letter queue management, and asynchronous background workers built in **C# / .NET 8**.

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com)
[![C#](https://img.shields.io/badge/C%23-12.0-239120?style=flat-square&logo=csharp)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![CI](https://img.shields.io/badge/CI-Passing-238636?style=flat-square&logo=githubactions)](https://github.com/txltedxgod/hookflow/actions)
[![License](https://img.shields.io/badge/License-MIT-blue?style=flat-square)](LICENSE)

`#dotnet` `#csharp` `#webhooks` `#event-driven` `#background-service` `#resilience` `#microservices`

---

## 🏛️ Event Ingestion & Delivery Flow

```mermaid
flowchart TD
    Producer[App Event Producer] -->|POST /api/webhooks/dispatch| API[HookFlow API Gateway]
    API -->|Compute HMAC-SHA256 Signature| Hasher[Cryptographic Signer]
    Hasher -->|Persist Event Status: PENDING| DB[(Database / SQLite / PostgreSQL)]
    
    subgraph DeliveryEngine ["Background Worker Pool"]
        Worker[Delivery Background Worker] -->|Fetch Due Webhook Deliveries| DB
        Worker -->|HTTP POST with X-HookFlow-Signature| Target[Target Endpoint]
        
        Target -->|200 OK| Success[Mark Delivery SUCCESS]
        Target -->|4xx / 5xx / Timeout| RetryLogic{Attempt < MaxRetries?}
        
        RetryLogic -->|Yes| Backoff[Schedule Next Retry: 2^n * BaseDelay]
        Backoff --> DB
        RetryLogic -->|No| DLQ[Mark DEAD_LETTER_QUEUE]
    end
```

---

## Features

- **Reliable At-Least-Once Delivery:** Decoupled event ingestion from HTTP delivery via persistent queueing.
- **HMAC-SHA256 Payload Signing:** Secures payloads with standard `X-HookFlow-Signature` verification headers.
- **Jittered Exponential Backoff:** Automatic retries for transient HTTP errors (5xx, 429, socket timeouts).
- **Dead-Letter Queue (DLQ):** Failed events after maximum attempts are routed to DLQ for manual inspection and replay.
- **Unit & Integration Tested:** xUnit test suite covering delivery retry math and signature computation.

## Quick Start

```bash
# Clone & run via .NET CLI
dotnet run --project src/HookFlow.Api

# Run tests
dotnet test
```
