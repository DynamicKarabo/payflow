from __future__ import annotations
import json
import joblib
import os
from datetime import datetime
from typing import Optional
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
import pandas as pd

ROOT = os.path.dirname(__file__)
MODEL_PATH = os.path.join(ROOT, "model.pkl")
PIPE_PATH = os.path.join(ROOT, "feature_pipeline.pkl")
META_PATH = os.path.join(ROOT, "metadata.json")

app = FastAPI(title="PayFlow Fraud Scoring")

model = None
pipeline = None
metadata = {}


class Transaction(BaseModel):
    transaction_id: str
    amount: float
    currency: Optional[str] = "USD"
    country: Optional[str] = "US"
    device_id: Optional[str] = None
    ip_address: Optional[str] = None
    timestamp: Optional[str] = None
    # optional precomputed fields
    country_match: Optional[int] = None
    ip_risk_score: Optional[float] = None


@app.on_event("startup")
def load_artifacts():
    global model, pipeline, metadata
    try:
        model = joblib.load(MODEL_PATH)
        pipeline = joblib.load(PIPE_PATH)
        with open(META_PATH, "r") as f:
            metadata = json.load(f)
        print("Loaded model and pipeline")
    except Exception as e:
        print("Warning: could not load artifacts at startup:", e)


@app.get("/health")
def health():
    return {"status": "healthy"}


@app.post("/score")
def score(tx: Transaction):
    if model is None or pipeline is None:
        raise HTTPException(status_code=503, detail="Model artifacts not loaded")

    # Build DataFrame for transformer
    record = {
        "amount": tx.amount,
        "country": tx.country,
        "ip_address": tx.ip_address or "",
        "timestamp": tx.timestamp or datetime.utcnow().isoformat() + "Z",
    }
    if tx.country_match is not None:
        record["country_match"] = int(tx.country_match)
    if tx.ip_risk_score is not None:
        record["ip_risk_score"] = float(tx.ip_risk_score)

    df = pd.DataFrame([record])
    try:
        X = pipeline.transform(df)
        proba = float(model.predict_proba(X)[:, 1][0])
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Scoring failed: {e}")

    if proba >= 0.7:
        risk = "high"
    elif proba >= 0.3:
        risk = "medium"
    else:
        risk = "low"

    return {
        "transaction_id": tx.transaction_id,
        "fraud_probability": proba,
        "risk_level": risk,
        "model_version": metadata.get("training_timestamp", "unknown"),
    }
