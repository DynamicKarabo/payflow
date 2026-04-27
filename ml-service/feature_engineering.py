from __future__ import annotations
import numpy as np
import pandas as pd
from sklearn.base import BaseEstimator, TransformerMixin
from typing import List, Optional


class FraudFeatureTransformer(BaseEstimator, TransformerMixin):
    """Transform raw transaction dict/DF into model features.

    Simplification: device velocity and amount_z_score (which require history) are omitted.
    Instead features used: amount, hour_of_day, day_of_week, country_match, ip_risk_score.

    At transform time, if country_match or ip_risk_score are missing, they are synthesized
    with simple heuristics so the service can score single transactions.
    """

    def __init__(self, feature_names: Optional[List[str]] = None):
        self.feature_names = feature_names or [
            "amount",
            "hour_of_day",
            "day_of_week",
            "country_match",
            "ip_risk_score",
        ]

    def fit(self, X: pd.DataFrame, y=None):
        return self

    def _ip_to_risk(self, ip: str) -> float:
        try:
            # crude deterministic pseudo-risk from IP string
            parts = [int(p) for p in ip.split(".") if p.isdigit()]
            if parts:
                return (parts[-1] % 255) / 255.0
        except Exception:
            pass
        return 0.1

    def _country_match_heuristic(self, country: str) -> int:
        # Simulate that common countries are usually matching (low risk)
        if not country or not isinstance(country, str):
            return 0
        if country.upper() in ("US", "CA", "GB", "DE", "FR"):
            return 1
        return 0

    def transform(self, X: pd.DataFrame) -> np.ndarray:
        df = X.copy()
        # Ensure required columns exist
        if "amount" not in df.columns:
            df["amount"] = 0.0
        # timestamp -> hour, day
        if "timestamp" in df.columns:
            ts = pd.to_datetime(df["timestamp"], utc=True, errors="coerce")
            df["hour_of_day"] = ts.dt.hour.fillna(0).astype(int)
            df["day_of_week"] = ts.dt.dayofweek.fillna(0).astype(int)
        else:
            df["hour_of_day"] = df.get("hour_of_day", 0).astype(int)
            df["day_of_week"] = df.get("day_of_week", 0).astype(int)

        # country_match: if provided use it, else heuristic from country
        if "country_match" not in df.columns:
            df["country_match"] = df.get("country", "").apply(self._country_match_heuristic)
        else:
            df["country_match"] = df["country_match"].fillna(0).astype(int)

        # ip_risk_score: if provided use it, else compute from IP
        if "ip_risk_score" not in df.columns:
            df["ip_risk_score"] = df.get("ip_address", "").apply(self._ip_to_risk)
        else:
            df["ip_risk_score"] = df["ip_risk_score"].fillna(0.0).astype(float)

        # Select and order features
        out = df.loc[:, [
            "amount",
            "hour_of_day",
            "day_of_week",
            "country_match",
            "ip_risk_score",
        ]]

        # Ensure numeric types
        out = out.apply(pd.to_numeric, errors="coerce").fillna(0.0)
        return out.values

    def get_feature_names_out(self) -> List[str]:
        return self.feature_names
