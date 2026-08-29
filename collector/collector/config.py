"""
collector 設定モジュール。
すべての設定値は環境変数から読み込む。コードへのハードコード禁止。
"""
from __future__ import annotations

import os


class CollectorConfig:
    """環境変数から読み込んだ設定を保持するクラス。"""

    def __init__(self) -> None:
        self.backend_base_url: str = self._require("BACKEND_BASE_URL")
        self.collector_api_key: str = self._require("COLLECTOR_API_KEY")
        self.request_timeout_seconds: int = int(
            os.environ.get("REQUEST_TIMEOUT_SECONDS", "30")
        )
        self.max_retries: int = int(os.environ.get("MAX_RETRIES", "3"))

    @staticmethod
    def _require(key: str) -> str:
        """必須環境変数を取得する。未設定なら ValueError を送出する。"""
        value = os.environ.get(key, "").strip()
        if not value:
            raise ValueError(
                f"Required environment variable '{key}' is not set or empty."
            )
        return value


def load_config() -> CollectorConfig:
    """CollectorConfig を構築して返すファクトリ関数。"""
    return CollectorConfig()
