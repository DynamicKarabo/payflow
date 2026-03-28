# PayFlow — System Overview

## Purpose

PayFlow is a multi-tenant payment processing platform built for reliability and auditability. It handles payment lifecycle management from authorisation through settlement, with first-class support for safe retries, tenant data isolation, and signed webhook delivery.

---

## Core Design Goals

| Goal | Mechanism |
|---|---|
| Cross-tenant isolation | EF Core global query filters keyed on `TenantId` |
| Duplicate charge prevention | Redis `SET NX` idempotency keys per payment intent |
| Traceable payment state | Strict domain state machine: `Created → Authorised → Captured → Settled` |
| Reliable webhook delivery | Hangfire-backed exponential backoff + HMAC-SHA256 payload signing |
| Environment separation | `test` / `live` modes scoped at the API key level |
| Partial refunds | Refund ledger linked to captured payments with partial amount validation |
| Settlement batching | Nightly Hangfire recurring job aggregates settled payments per tenant |

---

## Technology Stack

| Layer | Technology |
|---|---|
| Language / Runtime | C# · .NET 8 |
| API | ASP.NET Core Minimal APIs |
| Domain / Application | Clean Architecture — Domain, Application, Infrastructure, API |
| ORM | EF Core 8 (SQL Server) |
| Primary Database | SQL Server 2022 |
| Cache / Idempotency | Redis (StackExchange.Redis) |
| Message Bus | Azure Service Bus (topics + subscriptions) |
| Background Jobs | Hangfire (SQL Server storage) |
| Containerisation | Docker + Docker Compose |
| Auth | API key authentication (hashed, tenant-scoped) |

---

## High-Level Architecture

```
                        ┌─────────────────────────────┐
                        │        API Gateway /         │
                        │     Reverse Proxy (NGINX)    │
                        └────────────┬────────────────┘
                                     │
                        ┌────────────▼────────────────┐
                        │     PayFlow API (.NET 8)     │
                        │  Minimal APIs + Middleware   │
                        └──┬─────────┬────────────────┘
                           │         │
          ┌────────────────▼──┐  ┌───▼──────────────────┐
          │   SQL Server       │  │        Redis          │
          │  (EF Core, multi-  │  │  (Idempotency keys,   │
          │   tenant filtered) │  │   distributed locks)  │
          └────────────────────┘  └───────────────────────┘
                           │
          ┌────────────────▼──────────────────────┐
          │          Azure Service Bus             │
          │   payment.events topic (fan-out)       │
          └─────┬─────────────────────────┬────────┘
                │                         │
     ┌──────────▼──────────┐   ┌──────────▼──────────┐
     │  Webhook Dispatcher │   │  Settlement Processor │
     │  (Hangfire Worker)  │   │  (Hangfire Recurring) │
     └─────────────────────┘   └──────────────────────┘
```

---

## Solution Structure

```
PayFlow/
├── src/
│   ├── PayFlow.Domain/               # Entities, value objects, domain events, state machine
│   ├── PayFlow.Application/          # Use cases, command/query handlers (MediatR), interfaces
│   ├── PayFlow.Infrastructure/       # EF Core, Redis, Service Bus, Hangfire, external gateways
│   └── PayFlow.Api/                  # Minimal API endpoints, middleware, DI composition root
├── tests/
│   ├── PayFlow.Domain.Tests/
│   ├── PayFlow.Application.Tests/
│   └── PayFlow.Integration.Tests/
├── docker-compose.yml
├── docker-compose.override.yml       # Local dev overrides
└── PayFlow.sln
```

---

## Key Flows (Summary)

1. **Payment Creation** — API receives intent, Redis idempotency check, persists `Created` payment, publishes `PaymentCreated` domain event.
2. **Authorisation** — Gateway adapter called, state transitions to `Authorised`, event published to Service Bus.
3. **Capture** — Explicit capture endpoint or auto-capture flag triggers `Captured` transition.
4. **Settlement** — Nightly batch job moves `Captured` payments to `Settled`, aggregates per tenant.
5. **Refund** — Refund request validated against captured amount, partial ledger entry written.
6. **Webhook Delivery** — Service Bus consumer enqueues Hangfire job, signs payload with tenant HMAC secret, delivers with exponential backoff.

---

## Spec Index

| File | Covers |
|---|---|
| `01-domain-model.md` | Entities, value objects, state machine, domain events |
| `02-multi-tenancy.md` | Tenant scoping strategy, EF Core filters, API key isolation |
| `03-payment-processing.md` | Idempotency, payment intent flow, gateway integration |
| `04-webhooks.md` | Delivery pipeline, HMAC signing, retry policy |
| `05-refunds-and-settlements.md` | Partial refund rules, settlement batching |
| `06-infrastructure.md` | Redis, Service Bus, Hangfire, configuration |
| `07-database.md` | Schema, EF Core setup, migrations strategy |
