#!/bin/bash
set -e

echo "=== PayFlow Development Stack ==="
echo "This will start: PostgreSQL, Redis, ML Service, API, Frontend"
echo ""

# Check Docker is running
if ! docker info > /dev/null 2>&1; then
    echo "❌ Docker is not running. Start Docker Desktop and try again."
    exit 1
fi

# Build and start all services
echo "🔨 Building and starting services..."
docker-compose up --build -d

echo ""
echo "⏳ Waiting for services to be healthy..."
sleep 5

# Show status
echo ""
echo "📊 Service Status:"
docker-compose ps

echo ""
echo "=== URLs ==="
echo "Frontend:  http://localhost:5173"
echo "API:       http://localhost:5000"
echo "ML Service: http://localhost:8000"
echo "PostgreSQL: localhost:5432 (payflow/payflow)"
echo "Redis:      localhost:6379"
echo ""
echo "📝 Logs: docker-compose logs -f [service]"
echo "Stop:    docker-compose down"
echo ""

# Tail API logs in foreground
echo "📋 Tailing API logs (Ctrl+C to detach)..."
docker-compose logs -f api
