"""Train XGBoost fraud model on synthetic payment data and save artifacts.
Outputs:
- model.pkl
- feature_pipeline.pkl
- metadata.json
"""
from __future__ import annotations
import json
import os
from datetime import datetime
import time
import numpy as np
import pandas as pd
from sklearn.model_selection import train_test_split, RandomizedSearchCV
from sklearn.pipeline import Pipeline
from sklearn.preprocessing import StandardScaler
from sklearn.metrics import roc_auc_score, precision_recall_fscore_support
import joblib
from xgboost import XGBClassifier
from feature_engineering import FraudFeatureTransformer

RND = 42
np.random.seed(RND)

OUT_DIR = os.path.dirname(__file__)
MODEL_PATH = os.path.join(OUT_DIR, "model.pkl")
PIPE_PATH = os.path.join(OUT_DIR, "feature_pipeline.pkl")
META_PATH = os.path.join(OUT_DIR, "metadata.json")


def generate_synthetic_payments(n_samples: int = 12000, fraud_rate: float = 0.005) -> pd.DataFrame:
    """Generate synthetic transactions with a controllable fraud signal."""
    # amount: log-normal to simulate payments
    amounts = np.random.lognormal(mean=4.0, sigma=1.2, size=n_samples)
    hour = np.random.randint(0, 24, size=n_samples)
    day = np.random.randint(0, 7, size=n_samples)
    country = np.random.choice(["US", "CA", "GB", "DE", "FR", "NG", "RU", "CN"], size=n_samples, p=[0.45,0.1,0.08,0.08,0.08,0.07,0.07,0.07])
    # country_match more likely for domestic
    country_match = np.isin(country, ["US","CA","GB","DE","FR"]).astype(int)
    # ip_risk derived from random but correlated with some countries
    ip_risk = np.random.rand(n_samples) * (1 - 0.3*country_match) + (country == "NG")*0.4

    # Create a latent score that increases with amount and ip_risk and country mismatch and odd hours
    amt_scaled = (np.log1p(amounts) - np.mean(np.log1p(amounts))) / (np.std(np.log1p(amounts)) + 1e-9)
    hour_risk = ((hour >= 0) & (hour <= 6)).astype(float)  # late-night more risky

    # baseline logit chosen to produce target fraud rate approximately
    base_logit = np.log(fraud_rate / (1 - fraud_rate))
    logit = base_logit + 1.5 * amt_scaled + 2.5 * ip_risk + 1.2 * (1 - country_match) + 0.8 * hour_risk
    prob = 1 / (1 + np.exp(-logit))
    y = np.random.binomial(1, prob)

    df = pd.DataFrame({
        "amount": amounts,
        "hour_of_day": hour,
        "day_of_week": day,
        "country": country,
        "country_match": country_match,
        "ip_risk_score": ip_risk,
        "label": y,
    })
    return df


def build_and_train():
    print("Generating synthetic data...")
    df = generate_synthetic_payments()
    X = df.drop(columns=["label"]) 
    y = df["label"].values

    X_train, X_test, y_train, y_test = train_test_split(X, y, test_size=0.2, random_state=RND, stratify=y)

    pipeline = Pipeline([
        ("fe", FraudFeatureTransformer()),
        ("scaler", StandardScaler()),
    ])

    X_train_t = pipeline.fit_transform(X_train)
    X_test_t = pipeline.transform(X_test)

    # Handle class imbalance
    n_pos = int(y_train.sum())
    n_neg = len(y_train) - n_pos
    scale_pos_weight = max(1.0, n_neg / max(1, n_pos))

    model = XGBClassifier(
        objective="binary:logistic",
        use_label_encoder=False,
        eval_metric="auc",
        scale_pos_weight=scale_pos_weight,
        random_state=RND,
        n_jobs=1,
    )

    param_dist = {
        "n_estimators": [50, 100, 150, 200],
        "max_depth": [3, 4, 5, 6],
        "learning_rate": [0.01, 0.05, 0.1, 0.2],
        "subsample": [0.6, 0.8, 1.0],
        "colsample_bytree": [0.5, 0.7, 1.0],
    }

    print("Tuning model with RandomizedSearchCV (this may take a few minutes)...")
    rs = RandomizedSearchCV(
        estimator=model,
        param_distributions=param_dist,
        n_iter=30,
        scoring="roc_auc",
        cv=3,
        random_state=RND,
        n_jobs=-1,
        verbose=1,
    )

    t0 = time.time()
    rs.fit(X_train_t, y_train)
    t1 = time.time()
    best = rs.best_estimator_
    print(f"Tuning done in {t1-t0:.1f}s. Best params: {rs.best_params_}")

    # Evaluate
    y_proba = best.predict_proba(X_test_t)[:, 1]
    auc = roc_auc_score(y_test, y_proba)
    precision, recall, f1, _ = precision_recall_fscore_support(y_test, best.predict(X_test_t), average="binary", zero_division=0)

    metrics = {
        "auc": float(auc),
        "precision": float(precision),
        "recall": float(recall),
        "f1": float(f1),
        "training_time_seconds": t1 - t0,
    }

    print("Evaluation metrics:")
    print(json.dumps(metrics, indent=2))

    # Save artifacts
    joblib.dump(best, MODEL_PATH)
    joblib.dump(pipeline, PIPE_PATH)

    metadata = {
        "feature_names": pipeline.named_steps["fe"].get_feature_names_out(),
        "training_timestamp": datetime.utcnow().isoformat() + "Z",
        "metrics": metrics,
    }
    with open(META_PATH, "w") as f:
        json.dump(metadata, f, indent=2)

    print(f"Saved model to {MODEL_PATH}, pipeline to {PIPE_PATH}, metadata to {META_PATH}")


if __name__ == "__main__":
    build_and_train()
