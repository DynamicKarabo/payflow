# PayFlow ML Fraud Scoring Service

This microservice provides fraud scoring for PayFlow transactions using a synthetic-data-trained XGBoost model.

## Quickstart (Docker)

Build the image:

```bash
docker build -t payflow-fraud-ml .
```

Run the container:

```bash
docker run -p 8000:8000 payflow-fraud-ml
```

The service will be available at http://localhost:8000

## API

POST /score

Request JSON schema:

{
  "transaction_id": "string",
  "amount": number,
  "currency": "string",
  "country": "string",
  "device_id": "string",
  "ip_address": "string",
  "timestamp": "ISO8601 string"
}

Response:

{
  "transaction_id": "...",
  "fraud_probability": 0.123,
  "risk_level": "low|medium|high",
  "model_version": "timestamp"
}

## Training

Train locally (produces model.pkl, feature_pipeline.pkl, metadata.json):

```bash
python train.py
```

Metrics achieved during the last run:

Training metrics (validation):

- AUC: 0.8834392067759004
- Precision: 0.2293103448275862
- Recall: 0.7916666666666666
- F1: 0.35561497326203206
- Training time (s): 7.598630428314209

The model achieved AUC > 0.85 on a synthetic holdout set.

## Integration (.NET HttpClient example)

```csharp
using var client = new HttpClient();
var payload = new {
  transaction_id = "t1",
  amount = 100,
  currency = "USD",
  country = "US",
  device_id = "d1",
  ip_address = "1.2.3.4",
  timestamp = DateTime.UtcNow.ToString("o")
};
var resp = await client.PostAsJsonAsync("http://localhost:8000/score", payload);
var json = await resp.Content.ReadAsStringAsync();
```
