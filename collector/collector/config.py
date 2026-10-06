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
        self.talent_id: int | None = self._parse_optional_int("TALENT_ID")
        # YouTube Data API v3 キー（未設定時は YouTube 収集をスキップ）
        self.youtube_api_key: str | None = self._parse_optional_str("YOUTUBE_API_KEY")

    @staticmethod
    def _require(key: str) -> str:
        """必須環境変数を取得する。未設定なら ValueError を送出する。"""
        value = os.environ.get(key, "").strip()
        if not value:
            raise ValueError(
                f"Required environment variable '{key}' is not set or empty."
            )
        return value

    @staticmethod
    def _parse_optional_str(key: str) -> str | None:
        """
        オプションの文字列環境変数を取得する。
        未設定・空文字は None を返す。
        """
        value = os.environ.get(key, "").strip()
        return value if value else None

    @staticmethod
    def _parse_optional_int(key: str) -> int | None:
        """
        オプションの整数環境変数を取得する。
        未設定・空文字は None を返す。
        設定されているが整数でない場合は ValueError を送出する。
        """
        value = os.environ.get(key, "").strip()
        if not value:
            return None
        try:
            return int(value)
        except ValueError:
            raise ValueError(
                f"Environment variable '{key}' must be an integer, got: {value!r}"
            )


def load_config() -> CollectorConfig:
    """CollectorConfig を構築して返すファクトリ関数。"""
    return CollectorConfig()
