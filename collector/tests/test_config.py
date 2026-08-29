"""collector.config のテスト"""
from __future__ import annotations

import os

import pytest

from collector.config import CollectorConfig, load_config


class TestCollectorConfig:
    # ── 正常系 ────────────────────────────────────────────────────────────────

    def test_load_config_reads_required_env_vars(self, monkeypatch: pytest.MonkeyPatch) -> None:
        monkeypatch.setenv("BACKEND_BASE_URL", "http://localhost:8080")
        monkeypatch.setenv("COLLECTOR_API_KEY", "secret-key")

        config = load_config()

        assert config.backend_base_url == "http://localhost:8080"
        assert config.collector_api_key == "secret-key"

    def test_default_timeout_is_30(self, monkeypatch: pytest.MonkeyPatch) -> None:
        monkeypatch.setenv("BACKEND_BASE_URL", "http://localhost:8080")
        monkeypatch.setenv("COLLECTOR_API_KEY", "key")
        monkeypatch.delenv("REQUEST_TIMEOUT_SECONDS", raising=False)

        config = load_config()

        assert config.request_timeout_seconds == 30

    def test_default_max_retries_is_3(self, monkeypatch: pytest.MonkeyPatch) -> None:
        monkeypatch.setenv("BACKEND_BASE_URL", "http://localhost:8080")
        monkeypatch.setenv("COLLECTOR_API_KEY", "key")
        monkeypatch.delenv("MAX_RETRIES", raising=False)

        config = load_config()

        assert config.max_retries == 3

    def test_custom_timeout_from_env(self, monkeypatch: pytest.MonkeyPatch) -> None:
        monkeypatch.setenv("BACKEND_BASE_URL", "http://localhost:8080")
        monkeypatch.setenv("COLLECTOR_API_KEY", "key")
        monkeypatch.setenv("REQUEST_TIMEOUT_SECONDS", "60")

        config = load_config()

        assert config.request_timeout_seconds == 60

    # ── 異常系 ────────────────────────────────────────────────────────────────

    def test_missing_backend_base_url_raises(self, monkeypatch: pytest.MonkeyPatch) -> None:
        monkeypatch.delenv("BACKEND_BASE_URL", raising=False)
        monkeypatch.setenv("COLLECTOR_API_KEY", "key")

        with pytest.raises(ValueError, match="BACKEND_BASE_URL"):
            load_config()

    def test_missing_collector_api_key_raises(self, monkeypatch: pytest.MonkeyPatch) -> None:
        monkeypatch.setenv("BACKEND_BASE_URL", "http://localhost:8080")
        monkeypatch.delenv("COLLECTOR_API_KEY", raising=False)

        with pytest.raises(ValueError, match="COLLECTOR_API_KEY"):
            load_config()

    # ── 境界値 ────────────────────────────────────────────────────────────────

    def test_empty_backend_base_url_raises(self, monkeypatch: pytest.MonkeyPatch) -> None:
        monkeypatch.setenv("BACKEND_BASE_URL", "")
        monkeypatch.setenv("COLLECTOR_API_KEY", "key")

        with pytest.raises(ValueError, match="BACKEND_BASE_URL"):
            load_config()

    def test_whitespace_only_api_key_raises(self, monkeypatch: pytest.MonkeyPatch) -> None:
        monkeypatch.setenv("BACKEND_BASE_URL", "http://localhost:8080")
        monkeypatch.setenv("COLLECTOR_API_KEY", "   ")

        with pytest.raises(ValueError, match="COLLECTOR_API_KEY"):
            load_config()
