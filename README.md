# PayFlow

Modern payment processing platform with real-time ML fraud detection. Built with .NET 8, Next.js, and microservices architecture.

---

## Tech Stack

| Layer | Technology |
|-------|-----------|
| **Backend API** | .NET 8, C#, Minimal APIs, MediatR |
| **Frontend** | Next.js 14, TypeScript, Tailwind CSS |
| **Database** | PostgreSQL 17 |
| **Cache** | Redis 7 |
| **ML Service** | Python FastAPI, XGBoost, pandas |
| **Orchestration** | Docker Compose |
| **CI/CD** | GitHub Actions |
| **Testing** | xUnit, Integration Tests (28 passing) |

---

## MVP: Fraud Detection Microservice

**Production-grade ML pipeline integrated into payment flow.**

### Architecture
```
┌─────────┐    ┌──────────────┐    ┌──────────┐
│ Payment │───▶│   PayFlow    │───▶│  Redis   │
│ Gateway │    │   .NET API   │    │  Cache   │
└─────────┘    └──────┬───────┘    └──────────┘
                      │
                      ▼
            ┌──────────────────┐
            │  ml-service       │
            │  (Python FastAPI) │
            │  XGBoost Model    │
            └──────────────────┘
```

### Model Details
- **Algorithm:** XGBoost (gradient-boosted trees)
- **Training data:** IEEE-CIS Fraud Detection dataset (synthetic fintech transactions)
- **Features:** Transaction amount, card country, time delta, product category, etc.
- **Output:** `fraud_probability` (0–1), `risk_level` (low/medium/high), `model_version`
- **Caching:** Redis with 30-day TTL to minimize model inference calls

### Integration Points
- Payment creation endpoint scores every transaction automatically
- Frontend displays real-time risk badges (green/yellow/red)
- Backend uses fail-open design: ML service returns `0.0` on errors, logs exception
- Score persisted in `PaymentResponse.FraudScore` for audit trail

---

## Local Development

### Prerequisites
- Docker + Docker Compose
- .NET 8 SDK (optional, for native builds)
- Node.js 20+ (optional, for frontend dev)

### Quick Start
```bash
# Clone and start everything
git clone https://github.com/your-username/payflow.git
cd payflow
./run-dev.sh

# Services start in order:
# 1. PostgreSQL  (localhost:5432)
# 2. Redis       (localhost:6379)
# 3. ML Service  (http://localhost:8000)
# 4. .NET API    (http://localhost:5000)
# 5. Frontend    (http://localhost:5173)

# Health checks
curl http://localhost:8000/health           # ML service
curl http://localhost:5000/health           # API
```

### Access Points
| Service | URL | Notes |
|---------|-----|-------|
| Frontend | http://localhost:5173 | Next.js dev server |
| API | http://localhost:5000 | Swagger at /swagger |
| ML Scoring | http://localhost:8000 | POST /score |
| PostgreSQL | localhost:5432 | User: `postgres`, Password: `postgres` |
| Redis | localhost:6379 | No password (local only) |

### Running Tests
```bash
cd src/PayFlow.Api
dotnet test --logger "console;verbosity=detailed"
```
**All 28 integration tests pass** (Hangfire & Redis optional during tests).

---

## Production Deployment

### Environment Variables
```bash
# .NET API (src/PayFlow.Api/Program.cs reads these)
FraudScoring__ServiceUrl=https://ml-service.yourdomain.com
Redis__ConnectionString=redis://redis-12345.upstash.io:6379
ConnectionStrings__PayFlowDbContext=Host=...
AcceptPaymentCard__WebhookUrl=https://api.yourdomain.com/webhooks/...

# ML Service (ml-service/.env)
REDIS_URL=redis://...
MODEL_PATH=/app/model.pkl
```

### Deployment Options
- **Frontend:** Vercel (single-click) or Railway
- **API:** Railway (Docker), Azure Container Apps, AWS ECS
- **ML Service:** Railway (Python service), modal.com (serverless GPUs), GCP Vertex AI
- **Database:** Supabase, Neon, or self-hosted Postgres
- **Redis:** Upstash (serverless), Redis Cloud

---

## Key Design Decisions

### Fail-Open for ML
If the ML service is down or returns an error, the API uses `fraudScore: 0.0` and logs the exception — payments still flow.

### Microservice Isolation
ML service runs independently (Python) separate from .NET stack. Enables:
- Independent scaling (GPU inference on separate nodes)
- Team autonomy (data scientists deploy models without touching .NET)
- Technology diversity (right tool per job)

### Docker-First Workflow
Everything containerized. `docker-compose.yml` defines full stack locally. Production mirrors local setup.

---

## Future Roadmap

- [ ] Real-time streaming fraud detection (Kafka + Redis Streams)
- [ ] Model retraining pipeline (scheduled XGBoost retrain on new data)
- [ ] Explainability dashboard (SHAP values per transaction)
- [ ] Multi-model ensemble (add TabNet/DeepFM for comparison)
- [ ] A/B testing layer (canary model rollouts)
- [ ] Regulatory compliance audit log (GDPR/POPIA)

---

## Credits

Built by Karabo Oliphant — fintech engineer targeting US remote contracts (2026).

**Want to work together?** DM open for ML engineering roles (focus: risk, fraud, payments).

---

## License

MIT. Use as boilerplate for your own payment platform with ML.
