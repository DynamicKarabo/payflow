# PayFlow - Project Overview

PayFlow is a high-performance, multi-tenant payment processing platform built with a modern .NET 9 backend and a React 19 frontend. It is designed for scalability, security, and reliability, employing Clean Architecture and Event-Driven patterns.

## 🏗️ Backend Architecture

The backend follows Domain-Driven Design (DDD) principles and is structured into four distinct layers:

*   **Domain Layer (`PayFlow.Domain`)**: The "brain" of the system. It contains the **Payment Aggregate**, which manages a strict state machine (Created → Authorised → Captured → Settled/Failed). It utilizes Value Objects for financial data (`Money`, `Currency`) and technical constraints (`IdempotencyKey`).
*   **Application Layer (`PayFlow.Application`)**: Orchestrates use cases using **MediatR**. It handles command/query routing, input validation via **FluentValidation**, and defines the interfaces for persistence and infrastructure.
*   **Infrastructure Layer (`PayFlow.Infrastructure`)**: Implements external integrations:
    *   **Persistence**: EF Core with SQL Server 2022.
    *   **Background Processing**: Hangfire for scheduled tasks like nightly settlements and exponential backoff for webhook deliveries.
    *   **Messaging**: Azure Service Bus for internal domain events.
    *   **Caching/Locking**: Redis for distributed idempotency checks and resource locking.
*   **API Layer (`PayFlow.Api`)**: A lightweight entry point using **ASP.NET Core Minimal APIs**, featuring custom middleware for multi-tenant context resolution and API key-based authentication.

### 🏢 Multi-Tenancy & Security
*   **Tenant Isolation**: Implemented via a "Shared Database/Schema" strategy using EF Core Global Query Filters. Every record is scoped to a `TenantId`.
*   **Authentication**: API keys use bcrypt hashing for secure storage.
*   **Webhook Security**: All outbound notifications are signed with HMAC-SHA256 and include a timestamp to prevent replay attacks.

### 🔁 Reliability Features
*   **Idempotency**: A Redis-backed service ensures that duplicate requests (identified by `Idempotency-Key`) are processed exactly once.
*   **Polly Resilience**: The payment gateway adapter includes retry policies with jittered backoff and circuit breakers to handle intermittent external failures.
*   **Rate Limiting**: Per-IP fixed window rate limiting (100 requests/minute) protects against abuse.
*   **CORS**: Configurable cross-origin resource sharing for frontend integration.

---

## 🎨 Frontend State

The frontend is a modern merchant portal designed for high-signal financial management.

*   **Tech Stack**: React 19, TypeScript, Vite, and Tailwind CSS 4.
*   **Architecture**:
    *   **AuthContext**: Manages session state and detects tenant status (e.g., handling suspended accounts).
    *   **ApiClient**: A custom fetch wrapper that automatically handles API key injection, environment switching (Test vs. Live modes), and idempotency key generation.
*   **Current Capabilities**:
    *   **Payment Lifecycle**: A functional workflow for creating and tracking payments through their lifecycle.
    *   **Dashboard**: Live stats from API (total payments, amount, success rate, pending settlements).
    *   **Validation**: Real-time display of RFC 7807 Problem Details from the backend.
    *   **Testing**: 34 tests with Vitest + React Testing Library.
    *   **Status**: Fully functional prototype with API integration.

---

## 💻 Core Technology Stack

| Layer | Technology |
| :--- | :--- |
| **Backend Runtime** | .NET 9.0 |
| **API Framework** | ASP.NET Core Minimal APIs |
| **Database / ORM** | SQL Server 2022 / EF Core 9.0 |
| **Background Jobs** | Hangfire |
| **Distributed Cache** | Redis |
| **Frontend Framework** | React 19 |
| **Frontend Tooling** | Vite, TypeScript, Tailwind CSS 4 |
| **Testing** | xUnit, FluentAssertions |
