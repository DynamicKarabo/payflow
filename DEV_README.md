# PayFlow Local Development

One-command local stack with Docker Compose.

## Prerequisites

- Docker Desktop (or Docker Engine + Docker Compose)
- .NET 9 SDK (for API if running without container)
- Node.js 20+ (for frontend if running without container)

## Quick Start

```bash
cd ~/Projects/payflow
./run-dev.sh
```

This starts:
- **PostgreSQL** on `localhost:5432` (user/payflow)
- **Redis** on `localhost:6379`
- **ML Fraud Scoring** on `http://localhost:8000`
- **API** on `http://localhost:5000`
- **Frontend** on `http://localhost:5173`

First build will take 5–10 min (downloads base images). Subsequent starts are instant.

## Services

| Service | URL | Notes |
|---------|-----|-------|
| Frontend | http://localhost:5173 | React + Vite |
| API | http://localhost:5000 | Swagger at `/swagger` |
| ML Scoring | http://localhost:8000 | `POST /score`, `GET /health` |
| PostgreSQL | localhost:5432 | DB: `payflow`, User: `payflow` |
| Redis | localhost:6379 | Caching + idempotency |

## Testing the Fraud Score

1. Open frontend → Payments → **Create Payment**
2. Submit any amount (e.g. 1500 USD)
3. After creation, payment appears in list with a **risk badge** (Low/Medium/High)
   - High: fraud_probability ≥ 0.7
   - Medium: 0.3 ≤ fraud_probability < 0.7
   - Low: fraud_probability < 0.3

Or test ML service directly:
```bash
curl -X POST http://localhost:8000/score \
  -H "Content-Type: application/json" \
  -d '{
    "transaction_id": "test_123",
    "amount": 1500.00,
    "currency": "USD",
    "country": "US",
    "ip_address": "192.168.1.1",
    "timestamp": "2026-04-27T14:30:00Z"
  }'
```

## Architecture

```
┌─────────────────┐     ┌─────────────────────┐
│   Frontend      │────▶│     API (.NET)      │
│   (React)       │     │  - Payment handling │
└─────────────────┘     │  - Redis caching    │
                        │  - ML scoring call │
                        └──────────▲──────────┘
                                   │
                        ┌──────────▼──────────┐
                        │  ML Service         │
                        │  (FastAPI + XGBoost)│
                        └─────────────────────┘
```

- ML service trained on IEEE-CIS Fraud Detection dataset (AUC ~0.87)
- Scores cached in Redis for 30 days
- API fails open (returns 0.0 if ML unavailable)

## Troubleshooting

**Port already in use**
```bash
# Stop conflicting services
docker-compose down
# Or change ports in docker-compose.yml
```

**ML service returns 503**
- Model may still be loading (first start only ~5s)
- Check logs: `docker logs payflow-ml`

**Database connection errors**
- Ensure Postgres container is healthy: `docker-compose ps`
- API waits for DB healthcheck before starting

## Stopping

```bash
docker-compose down
# Or Ctrl+C if running in foreground
```

## Rebuilding After Code Changes

```bash
# Rebuild API
docker-compose build api

# Rebuild ML service
docker-compose build ml-service

# Restart everything
docker-compose up -d
```
