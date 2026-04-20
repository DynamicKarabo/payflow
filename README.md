<div align="center">

# PayFlow - Multi-Tenant Payment Processing Platform

![.NET 9.0](https://img.shields.io/badge/.NET-9.0-blue?style=for-the-badge&logo=.net)
![SQL Server 2022](https://img.shields.io/badge/SQL%20Server-2022-blue?style=for-the-badge&logo=microsoftsqlserver)
![Redis](https://img.shields.io/badge/Redis-red?style=for-the-badge&logo=redis)
![Hangfire](https://img.shields.io/badge/Hangfire-black?style=for-the-badge)

A production-ready payment processing platform built with **.NET 9**, featuring strict state machines, multi-tenancy isolation, and event-driven architecture.

</div>

## 🏗️ Architecture Overview

```mermaid
flowchart TB
    subgraph API_Layer ["API Layer (Minimal APIs)"]
        Auth_Middleware["Auth Middleware"]
        Error_Handler["Error Handler (RFC 9457)"]
        Endpoints["Endpoints
POST /v1/payments
GET /v1/payments/{id}"]
    end

    subgraph App_Layer ["Application Layer (MediatR)"]
        CreatePaymentCommand["CreatePaymentCommand"]
        ValidationBehavior["ValidationBehavior"]
        FluentValidation["FluentValidation"]
    end

    subgraph Infra_Layer ["Infrastructure Layer"]
        Redis["Redis
Idempotency
Distributed Locks"]
        Gateway["Payment Gateway
Polly Resilience
3 Retries / Circuit Breaker"]
        ServiceBus["Service Bus
Domain Events
Webhooks / SettlementBatch"]
    end

    subgraph Domain_Layer ["Domain Layer (Clean Architecture)"]
        PaymentAggregate["Payment Aggregate
State Machine
Domain Events"]
    end

    subgraph Persistence ["Persistence (EF Core + SQL)"]
        SQL_Server["SQL Server 2022
Multi-tenant
Row-version Concurrency"]
    end

    API_Layer --> App_Layer
    App_Layer --> Infra_Layer
    App_Layer --> Domain_Layer
    Infra_Layer --> Persistence
    Domain_Layer --> Persistence
```

## 🔄 Payment State Machine

```mermaid
stateDiagram-v2
    [*] --> Created
    
    Created --> Authorised : Authorise()
    Authorised --> Captured : Capture()
    Captured --> Settled : Settle()
    
    Created --> Failed : Fail()
    Authorised --> Failed : Fail()
    Authorised --> Cancelled : Cancel()
    Captured --> Failed : Fail()
    
    Settled --> Refunded : Refund()
    Refunded --> [*]
    
    Failed --> [*]
    Cancelled --> [*]
    Settled --> [*]
    
    note right of Refunded : Child transaction
```

## ✨ Key Features

### 🏢 Multi-Tenancy
- **Shared Database/Schema** approach with EF Core global query filters
- Tenant isolation enforced at database level via `TenantId`
- API key authentication with bcrypt-secured secrets
- Tenant status handling (Active/Suspended/Closed)

### 🔁 Idempotency
- Redis-based with 24-hour TTL
- `SET NX` pattern with processing sentinel
- Detects in-flight and duplicate requests
- Graceful fallback if Redis unavailable

### 🛡️ Resilience
- **Gateway Adapter**: Polly resilience pipeline
  - 3 retries with jittered exponential backoff
  - Circuit breaker (opens after 50% failure rate)
  - 10-second timeout per attempt

### ⚙️ Background Jobs (Hangfire)
- **Webhook Delivery**: Exponential backoff (30s → 5m → 30m → 2h → 5h → 24h)
- **Settlement Batch**: Nightly at 00:30 UTC with Redis distributed locking

### 🔔 Webhook Dispatcher (NEW)
- Domain events automatically create WebhookDelivery records
- In-process event publisher routes events to matching webhook endpoints
- Hangfire schedules HTTP delivery with retry logic

### 📊 Dashboard Stats (NEW)
- `GET /v1/dashboard/stats` — tenant-scoped payment aggregates
- Returns: total payments, total amount, success rate, pending settlements

### 🔒 Security
- HMAC-SHA256 webhook signatures with timestamp validation (300s tolerance)
- HTTPS enforcement for webhook endpoints
- No sensitive data (PAN/CVV) in payloads
- Per-IP rate limiting (100 requests/minute)
- CORS with configurable allowed origins

## 🛠️ Technology Stack

| Layer | Technology | Logo |
|-------|------------|------|
| Runtime | .NET 9.0 | `󰅲` |
| API | ASP.NET Core Minimal APIs | `󰅲` |
| ORM | Entity Framework Core 9.0 (SQL Server) | `󰆼` |
| Cache | StackExchange.Redis | `󰔟` |
| Messaging | Azure Service Bus | `󰔟` |
| Background Jobs | Hangfire | `󰏆` |
| Resilience | Polly | `󰏆` |
| Validation | FluentValidation | `󰏆` |
| Frontend | React 19 + TypeScript + Tailwind | `󰅲` |

## 📁 Project Structure

```
payflow/
├── src/
│   ├── PayFlow.Domain/           # Core domain logic
│   │   ├── Entities/             # Payment, Refund, Tenant, ApiKey
│   │   ├── ValueObjects/         # Money, Currency, Ids
│   │   ├── Events/               # Domain events
│   │   ├── Enums/                # PaymentStatus, RefundStatus
│   │   └── Exceptions/           # Domain exceptions
│   │
│   ├── PayFlow.Application/      # Application services
│   │   ├── Commands/             # MediatR commands
│   │   ├── Interfaces/           # Repository abstractions
│   │   ├── DTOs/                 # Response DTOs
│   │   └── Behaviors/            # Pipeline behaviors
│   │
│   ├── PayFlow.Infrastructure/   # External concerns
│   │   ├── Persistence/          # EF Core DbContexts
│   │   ├── Redis/                # Idempotency service
│   │   ├── Gateways/             # Payment gateway adapters
│   │   ├── Jobs/                 # Hangfire jobs
│   │   ├── ServiceBus/           # Event publishing
│   │   └── Signing/              # HMAC webhook signing
│   │
│   └── PayFlow.Api/              # API entry point
│       ├── Middleware/           # Auth, Error handling
│       ├── Endpoints/            # Minimal API routes
│       └── Configuration/        # DI configuration
│
├── frontend/                     # React Frontend
│   ├── src/
│   │   ├── api/                  # API clients
│   │   ├── components/           # Reusable components
│   │   ├── contexts/             # Auth context
│   │   ├── hooks/                # Custom hooks
│   │   ├── pages/                # Page components
│   │   └── types/                # TypeScript types
│   └── ...
│
└── tests/                        # Unit & Integration Tests
    ├── PayFlow.Domain.Tests/
    └── PayFlow.Integration.Tests/
```

## 🚀 Quick Start

```bash
# Restore and build
dotnet build

# Run tests
dotnet test

# Run the API
dotnet run --project src/PayFlow.Api/PayFlow.Api.csproj
```

## 📡 API Endpoints

### Create Payment
```bash
POST /v1/payments
Authorization: Bearer pk_live_xxxxx
Idempotency-Key: unique-key-123

{
  "amount": 10000,
  "currency": "GBP",
  "customerId": "cus_123",
  "paymentMethod": {
    "type": "card",
    "token": "tok_xxx"
  },
  "autoCapture": false
}
```

### Get Payment
```bash
GET /v1/payments/pay_abc123
Authorization: Bearer pk_live_xxxxx
```

## 🔍 Health Checks

- **Ready**: `/health/ready` - Checks database connectivity
- **Live**: `/health/live` - Basic liveness probe

## 📊 Background Jobs Dashboard

- **Hangfire Dashboard**: `/admin/hangfire`

## ✅ Tests

```bash
# Backend
dotnet test
# Domain: 19 passed | Integration: 26 passed

# Frontend
cd frontend && npm test
# 34 tests across API client, AuthContext, LoginPage, DashboardPage
```

### Test Coverage
- **Domain Tests**: Payment state machine, refund logic, events
- **Integration Tests**: Multi-tenancy isolation, Redis idempotency, webhook signing
- **Security Tests**: HMAC verification, HTTPS enforcement, sensitive data scrubbing
- **Frontend Tests**: API client, auth context, page components

## 📄 License

MIT License
