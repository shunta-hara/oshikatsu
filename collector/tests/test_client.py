"""collector.client のテスト"""
from __future__ import annotations

from datetime import datetime, timezone
from unittest.mock import MagicMock

import pytest
import responses as resp

from collector.client import (
    BackendClient,
    BatchResult,
    NewsBatchItem,
    OshiInfo,
    ProgramBatchItem,
    _backoff,
    _to_iso,
)
from collector.config import CollectorConfig


def make_config(
    base_url: str = "http://backend:8080",
    api_key: str = "test-key",
    timeout: int = 5,
    max_retries: int = 3,
) -> CollectorConfig:
    config = MagicMock(spec=CollectorConfig)
    config.backend_base_url = base_url
    config.collector_api_key = api_key
    config.request_timeout_seconds = timeout
    config.max_retries = max_retries
    return config


NEWS_URL = "http://backend:8080/api/v1/internal/news"
PROG_URL = "http://backend:8080/api/v1/internal/programs"

_NOW = datetime(2026, 7, 14, 12, 0, 0, tzinfo=timezone.utc)


class TestPostNewsItem:
    # ── 正常系 ────────────────────────────────────────────────────────────────

    @resp.activate
    def test_post_news_item_success_returns_response(self) -> None:
        resp.add(resp.POST, NEWS_URL, json={"id": 1, "wasDuplicate": False}, status=200)
        client = BackendClient(make_config())

        result = client.post_news_item(
            oshi_id=1,
            title="テストニュース",
            source_url="https://example.com/news/1",
            collected_at=_NOW,
        )

        assert result["id"] == 1
        assert result["wasDuplicate"] is False

    @resp.activate
    def test_post_news_item_sends_api_key_header(self) -> None:
        resp.add(resp.POST, NEWS_URL, json={"id": 1}, status=200)
        client = BackendClient(make_config(api_key="my-secret"))

        client.post_news_item(
            oshi_id=1,
            title="title",
            source_url="https://example.com/1",
            collected_at=_NOW,
        )

        assert resp.calls[0].request.headers["X-Api-Key"] == "my-secret"

    @resp.activate
    def test_post_news_item_with_published_at_includes_field(self) -> None:
        resp.add(resp.POST, NEWS_URL, json={"id": 1}, status=200)
        client = BackendClient(make_config())
        published = datetime(2026, 7, 14, 10, 0, 0, tzinfo=timezone.utc)

        client.post_news_item(
            oshi_id=1,
            title="title",
            source_url="https://example.com/1",
            collected_at=_NOW,
            published_at=published,
        )

        import json
        body = json.loads(resp.calls[0].request.body)
        assert "publishedAt" in body

    @resp.activate
    def test_post_news_item_without_published_at_omits_field(self) -> None:
        resp.add(resp.POST, NEWS_URL, json={"id": 1}, status=200)
        client = BackendClient(make_config())

        client.post_news_item(
            oshi_id=1,
            title="title",
            source_url="https://example.com/1",
            collected_at=_NOW,
        )

        import json
        body = json.loads(resp.calls[0].request.body)
        assert "publishedAt" not in body

    # ── 異常系・リトライ ──────────────────────────────────────────────────────

    @resp.activate
    def test_post_news_item_retries_on_500(self) -> None:
        resp.add(resp.POST, NEWS_URL, json={}, status=500)
        resp.add(resp.POST, NEWS_URL, json={}, status=500)
        resp.add(resp.POST, NEWS_URL, json={"id": 1}, status=200)
        client = BackendClient(make_config(max_retries=3))

        # sleep をモック（テストを速くする）
        import unittest.mock as mock
        with mock.patch("collector.client.time.sleep"):
            result = client.post_news_item(
                oshi_id=1,
                title="title",
                source_url="https://example.com/retry",
                collected_at=_NOW,
            )

        assert result["id"] == 1
        assert len(resp.calls) == 3

    @resp.activate
    def test_post_news_item_raises_after_max_retries_exceeded(self) -> None:
        # 全試行 500 を返す
        for _ in range(3):
            resp.add(resp.POST, NEWS_URL, json={}, status=500)
        client = BackendClient(make_config(max_retries=3))

        import unittest.mock as mock
        with mock.patch("collector.client.time.sleep"):
            with pytest.raises(RuntimeError, match="failed after 3 attempts"):
                client.post_news_item(
                    oshi_id=1,
                    title="title",
                    source_url="https://example.com/fail",
                    collected_at=_NOW,
                )

    @resp.activate
    def test_post_news_item_retries_on_429(self) -> None:
        resp.add(resp.POST, NEWS_URL, json={}, status=429)
        resp.add(resp.POST, NEWS_URL, json={"id": 2}, status=200)
        client = BackendClient(make_config(max_retries=3))

        import unittest.mock as mock
        with mock.patch("collector.client.time.sleep"):
            result = client.post_news_item(
                oshi_id=1,
                title="title",
                source_url="https://example.com/429",
                collected_at=_NOW,
            )

        assert result["id"] == 2


class TestPostProgramAppearance:
    # ── 正常系 ────────────────────────────────────────────────────────────────

    @resp.activate
    def test_post_program_success_returns_response(self) -> None:
        resp.add(resp.POST, PROG_URL, json={"id": 10, "wasDuplicate": False}, status=200)
        client = BackendClient(make_config())

        result = client.post_program_appearance(
            oshi_id=1,
            program_name="テスト番組",
            source_url="https://tv.example.com/1",
            collected_at=_NOW,
        )

        assert result["id"] == 10
        assert result["wasDuplicate"] is False

    @resp.activate
    def test_post_program_with_aired_at_includes_field(self) -> None:
        resp.add(resp.POST, PROG_URL, json={"id": 10}, status=200)
        client = BackendClient(make_config())
        aired = datetime(2026, 7, 14, 20, 0, 0, tzinfo=timezone.utc)

        client.post_program_appearance(
            oshi_id=1,
            program_name="番組",
            source_url="https://tv.example.com/2",
            collected_at=_NOW,
            aired_at=aired,
        )

        import json
        body = json.loads(resp.calls[0].request.body)
        assert "airedAt" in body

    @resp.activate
    def test_post_program_without_aired_at_omits_field(self) -> None:
        resp.add(resp.POST, PROG_URL, json={"id": 10}, status=200)
        client = BackendClient(make_config())

        client.post_program_appearance(
            oshi_id=1,
            program_name="番組",
            source_url="https://tv.example.com/3",
            collected_at=_NOW,
        )

        import json
        body = json.loads(resp.calls[0].request.body)
        assert "airedAt" not in body

    # ── 重複 ─────────────────────────────────────────────────────────────────

    @resp.activate
    def test_post_program_duplicate_returns_duplicate_flag(self) -> None:
        resp.add(resp.POST, PROG_URL, json={"id": 10, "wasDuplicate": True}, status=200)
        client = BackendClient(make_config())

        result = client.post_program_appearance(
            oshi_id=1,
            program_name="番組",
            source_url="https://tv.example.com/dup",
            collected_at=_NOW,
        )

        assert result["wasDuplicate"] is True


class TestGetOshis:
    OSHIS_URL = "http://backend:8080/api/v1/internal/oshis"

    # ── 正常系 ────────────────────────────────────────────────────────────────

    @resp.activate
    def test_get_oshis_success_returns_list(self) -> None:
        resp.add(
            resp.GET,
            self.OSHIS_URL,
            json=[{"id": 1, "name": "推しA"}, {"id": 2, "name": "推しB"}],
            status=200,
        )
        client = BackendClient(make_config())

        result = client.get_oshis()

        assert len(result) == 2
        assert isinstance(result[0], OshiInfo)
        assert result[0].id == 1
        assert result[0].name == "推しA"
        assert result[1].id == 2
        assert result[1].name == "推しB"

    @resp.activate
    def test_get_oshis_sends_api_key_header(self) -> None:
        resp.add(resp.GET, self.OSHIS_URL, json=[], status=200)
        client = BackendClient(make_config(api_key="secret-key"))

        client.get_oshis()

        assert resp.calls[0].request.headers["X-Api-Key"] == "secret-key"

    @resp.activate
    def test_get_oshis_empty_list_returns_empty(self) -> None:
        resp.add(resp.GET, self.OSHIS_URL, json=[], status=200)
        client = BackendClient(make_config())

        result = client.get_oshis()

        assert result == []

    # ── 異常系・リトライ ──────────────────────────────────────────────────────

    @resp.activate
    def test_get_oshis_retries_on_500(self) -> None:
        resp.add(resp.GET, self.OSHIS_URL, json={}, status=500)
        resp.add(resp.GET, self.OSHIS_URL, json=[{"id": 1, "name": "推し"}], status=200)
        client = BackendClient(make_config(max_retries=3))

        import unittest.mock as mock
        with mock.patch("collector.client.time.sleep"):
            result = client.get_oshis()

        assert len(result) == 1
        assert len(resp.calls) == 2

    @resp.activate
    def test_get_oshis_raises_after_max_retries(self) -> None:
        for _ in range(3):
            resp.add(resp.GET, self.OSHIS_URL, json={}, status=500)
        client = BackendClient(make_config(max_retries=3))

        import unittest.mock as mock
        with mock.patch("collector.client.time.sleep"):
            with pytest.raises(RuntimeError, match="failed after 3 attempts"):
                client.get_oshis()

    @resp.activate
    def test_get_oshis_retries_on_429(self) -> None:
        resp.add(resp.GET, self.OSHIS_URL, json={}, status=429)
        resp.add(resp.GET, self.OSHIS_URL, json=[{"id": 1, "name": "推し"}], status=200)
        client = BackendClient(make_config(max_retries=3))

        import unittest.mock as mock
        with mock.patch("collector.client.time.sleep"):
            result = client.get_oshis()

        assert len(result) == 1

    # ── バリデーション ────────────────────────────────────────────────────────

    @resp.activate
    def test_get_oshis_filters_malformed_items(self) -> None:
        resp.add(
            resp.GET,
            self.OSHIS_URL,
            json=[
                {"id": 1, "name": "正常"},
                {"id": "not-an-int", "name": "不正ID"},
                {"id": 2},  # name がない
                "not-a-dict",
            ],
            status=200,
        )
        client = BackendClient(make_config())

        result = client.get_oshis()

        assert len(result) == 1
        assert result[0].id == 1
        assert result[0].name == "正常"


class TestPostJobLog:
    JOB_LOG_URL = "http://backend:8080/api/v1/internal/job-logs"

    # ── 正常系 ────────────────────────────────────────────────────────────────

    @resp.activate
    def test_post_job_log_success_returns_response(self) -> None:
        resp.add(resp.POST, self.JOB_LOG_URL, json={"id": 1, "jobType": "news"}, status=200)
        client = BackendClient(make_config())

        result = client.post_job_log(
            job_type="news",
            executed_at=_NOW,
            success_count=5,
            failure_count=0,
        )

        assert result["id"] == 1
        assert result["jobType"] == "news"

    @resp.activate
    def test_post_job_log_sends_api_key_header(self) -> None:
        resp.add(resp.POST, self.JOB_LOG_URL, json={"id": 1}, status=200)
        client = BackendClient(make_config(api_key="secret"))

        client.post_job_log(
            job_type="news",
            executed_at=_NOW,
            success_count=0,
            failure_count=1,
        )

        assert resp.calls[0].request.headers["X-Api-Key"] == "secret"

    @resp.activate
    def test_post_job_log_sends_correct_payload(self) -> None:
        resp.add(resp.POST, self.JOB_LOG_URL, json={"id": 1}, status=200)
        client = BackendClient(make_config())

        client.post_job_log(
            job_type="program",
            executed_at=_NOW,
            success_count=3,
            failure_count=2,
        )

        import json
        body = json.loads(resp.calls[0].request.body)
        assert body["jobType"] == "program"
        assert body["successCount"] == 3
        assert body["failureCount"] == 2
        assert "executedAt" in body

    # ── 異常系・リトライ ──────────────────────────────────────────────────────

    @resp.activate
    def test_post_job_log_retries_on_500(self) -> None:
        resp.add(resp.POST, self.JOB_LOG_URL, json={}, status=500)
        resp.add(resp.POST, self.JOB_LOG_URL, json={"id": 1}, status=200)
        client = BackendClient(make_config(max_retries=3))

        import unittest.mock as mock
        with mock.patch("collector.client.time.sleep"):
            result = client.post_job_log(
                job_type="news",
                executed_at=_NOW,
                success_count=0,
                failure_count=0,
            )

        assert result["id"] == 1
        assert len(resp.calls) == 2

    @resp.activate
    def test_post_job_log_raises_after_max_retries(self) -> None:
        for _ in range(3):
            resp.add(resp.POST, self.JOB_LOG_URL, json={}, status=500)
        client = BackendClient(make_config(max_retries=3))

        import unittest.mock as mock
        with mock.patch("collector.client.time.sleep"):
            with pytest.raises(RuntimeError, match="failed after 3 attempts"):
                client.post_job_log(
                    job_type="news",
                    executed_at=_NOW,
                    success_count=0,
                    failure_count=0,
                )


class TestCheckHealth:
    HEALTH_URL = "http://backend:8080/api/v1/health"

    # ── 正常系 ────────────────────────────────────────────────────────────────

    @resp.activate
    def test_check_health_returns_true_on_200(self) -> None:
        resp.add(resp.GET, self.HEALTH_URL, json={"status": "ok"}, status=200)
        client = BackendClient(make_config())
        assert client.check_health() is True

    @resp.activate
    def test_check_health_does_not_retry_on_200(self) -> None:
        resp.add(resp.GET, self.HEALTH_URL, json={"status": "ok"}, status=200)
        client = BackendClient(make_config())
        client.check_health()
        # ヘルスチェックはリトライしないので1回だけ呼ばれる
        assert len(resp.calls) == 1

    # ── 異常系 ────────────────────────────────────────────────────────────────

    @resp.activate
    def test_check_health_returns_false_on_500(self) -> None:
        resp.add(resp.GET, self.HEALTH_URL, json={}, status=500)
        client = BackendClient(make_config())
        assert client.check_health() is False

    @resp.activate
    def test_check_health_returns_false_on_503(self) -> None:
        resp.add(resp.GET, self.HEALTH_URL, json={}, status=503)
        client = BackendClient(make_config())
        assert client.check_health() is False

    @resp.activate
    def test_check_health_returns_false_on_connection_error(self) -> None:
        import requests.exceptions as req_exc
        resp.add(
            resp.GET,
            self.HEALTH_URL,
            body=req_exc.ConnectionError("Connection refused"),
        )
        client = BackendClient(make_config())
        assert client.check_health() is False

    @resp.activate
    def test_check_health_does_not_retry_on_failure(self) -> None:
        """ヘルスチェックは失敗してもリトライしない（ポーリングは呼び出し元が行う）"""
        resp.add(resp.GET, self.HEALTH_URL, json={}, status=500)
        client = BackendClient(make_config(max_retries=3))
        result = client.check_health()
        assert result is False
        # リトライしないので1回だけ呼ばれる
        assert len(resp.calls) == 1


class TestPostNewsBatch:
    NEWS_BATCH_URL = "http://backend:8080/api/v1/internal/news/batch"

    # ── 正常系 ────────────────────────────────────────────────────────────────

    @resp.activate
    def test_post_news_batch_returns_batch_result(self) -> None:
        resp.add(
            resp.POST,
            self.NEWS_BATCH_URL,
            json={"receivedCount": 2, "savedCount": 2, "skippedCount": 0},
            status=200,
        )
        client = BackendClient(make_config())
        items = [
            NewsBatchItem(title="記事1", source_url="https://example.com/1", collected_at=_NOW),
            NewsBatchItem(title="記事2", source_url="https://example.com/2", collected_at=_NOW),
        ]

        result = client.post_news_batch(oshi_id=1, items=items)

        assert isinstance(result, BatchResult)
        assert result.received_count == 2
        assert result.saved_count == 2
        assert result.skipped_count == 0

    @resp.activate
    def test_post_news_batch_sends_api_key_header(self) -> None:
        resp.add(
            resp.POST,
            self.NEWS_BATCH_URL,
            json={"receivedCount": 1, "savedCount": 1, "skippedCount": 0},
            status=200,
        )
        client = BackendClient(make_config(api_key="batch-key"))
        items = [NewsBatchItem(title="T", source_url="https://example.com/1", collected_at=_NOW)]

        client.post_news_batch(oshi_id=1, items=items)

        assert resp.calls[0].request.headers["X-Api-Key"] == "batch-key"

    @resp.activate
    def test_post_news_batch_sends_correct_payload_structure(self) -> None:
        resp.add(
            resp.POST,
            self.NEWS_BATCH_URL,
            json={"receivedCount": 1, "savedCount": 1, "skippedCount": 0},
            status=200,
        )
        client = BackendClient(make_config())
        items = [
            NewsBatchItem(
                title="テスト記事",
                source_url="https://example.com/1",
                collected_at=_NOW,
                summary="要約",
                published_at=datetime(2026, 7, 14, 10, 0, 0, tzinfo=timezone.utc),
            )
        ]

        client.post_news_batch(oshi_id=5, items=items)

        import json as _json
        body = _json.loads(resp.calls[0].request.body)
        assert body["oshiId"] == 5
        assert len(body["items"]) == 1
        assert body["items"][0]["title"] == "テスト記事"
        assert body["items"][0]["sourceUrl"] == "https://example.com/1"
        assert body["items"][0]["summary"] == "要約"
        assert "publishedAt" in body["items"][0]
        assert "collectedAt" in body["items"][0]

    @resp.activate
    def test_post_news_batch_omits_optional_fields_when_none(self) -> None:
        resp.add(
            resp.POST,
            self.NEWS_BATCH_URL,
            json={"receivedCount": 1, "savedCount": 1, "skippedCount": 0},
            status=200,
        )
        client = BackendClient(make_config())
        items = [
            NewsBatchItem(title="T", source_url="https://example.com/1", collected_at=_NOW)
        ]

        client.post_news_batch(oshi_id=1, items=items)

        import json as _json
        body = _json.loads(resp.calls[0].request.body)
        item = body["items"][0]
        assert "summary" not in item
        assert "publishedAt" not in item

    # ── 異常系・リトライ ──────────────────────────────────────────────────────

    @resp.activate
    def test_post_news_batch_retries_on_500(self) -> None:
        resp.add(resp.POST, self.NEWS_BATCH_URL, json={}, status=500)
        resp.add(
            resp.POST,
            self.NEWS_BATCH_URL,
            json={"receivedCount": 1, "savedCount": 1, "skippedCount": 0},
            status=200,
        )
        client = BackendClient(make_config(max_retries=3))
        items = [NewsBatchItem(title="T", source_url="https://example.com/1", collected_at=_NOW)]

        import unittest.mock as mock
        with mock.patch("collector.client.time.sleep"):
            result = client.post_news_batch(oshi_id=1, items=items)

        assert result.saved_count == 1
        assert len(resp.calls) == 2

    @resp.activate
    def test_post_news_batch_raises_after_max_retries(self) -> None:
        for _ in range(3):
            resp.add(resp.POST, self.NEWS_BATCH_URL, json={}, status=500)
        client = BackendClient(make_config(max_retries=3))
        items = [NewsBatchItem(title="T", source_url="https://example.com/1", collected_at=_NOW)]

        import unittest.mock as mock
        with mock.patch("collector.client.time.sleep"):
            with pytest.raises(RuntimeError, match="failed after 3 attempts"):
                client.post_news_batch(oshi_id=1, items=items)


class TestPostProgramBatch:
    PROGRAM_BATCH_URL = "http://backend:8080/api/v1/internal/programs/batch"

    # ── 正常系 ────────────────────────────────────────────────────────────────

    @resp.activate
    def test_post_program_batch_returns_batch_result(self) -> None:
        resp.add(
            resp.POST,
            self.PROGRAM_BATCH_URL,
            json={"receivedCount": 1, "savedCount": 1, "skippedCount": 0},
            status=200,
        )
        client = BackendClient(make_config())
        items = [
            ProgramBatchItem(
                program_name="テスト番組",
                source_url="https://tv.example.com/1",
                collected_at=_NOW,
            )
        ]

        result = client.post_program_batch(oshi_id=1, items=items)

        assert isinstance(result, BatchResult)
        assert result.received_count == 1
        assert result.saved_count == 1
        assert result.skipped_count == 0

    @resp.activate
    def test_post_program_batch_sends_correct_payload_structure(self) -> None:
        resp.add(
            resp.POST,
            self.PROGRAM_BATCH_URL,
            json={"receivedCount": 1, "savedCount": 1, "skippedCount": 0},
            status=200,
        )
        client = BackendClient(make_config())
        items = [
            ProgramBatchItem(
                program_name="番組名",
                source_url="https://tv.example.com/1",
                collected_at=_NOW,
                broadcaster="NHK",
                aired_at=datetime(2026, 7, 14, 20, 0, 0, tzinfo=timezone.utc),
            )
        ]

        client.post_program_batch(oshi_id=3, items=items)

        import json as _json
        body = _json.loads(resp.calls[0].request.body)
        assert body["oshiId"] == 3
        assert len(body["items"]) == 1
        assert body["items"][0]["programName"] == "番組名"
        assert body["items"][0]["sourceUrl"] == "https://tv.example.com/1"
        assert body["items"][0]["broadcaster"] == "NHK"
        assert "airedAt" in body["items"][0]
        assert "collectedAt" in body["items"][0]

    @resp.activate
    def test_post_program_batch_omits_optional_fields_when_none(self) -> None:
        resp.add(
            resp.POST,
            self.PROGRAM_BATCH_URL,
            json={"receivedCount": 1, "savedCount": 1, "skippedCount": 0},
            status=200,
        )
        client = BackendClient(make_config())
        items = [
            ProgramBatchItem(
                program_name="番組",
                source_url="https://tv.example.com/1",
                collected_at=_NOW,
            )
        ]

        client.post_program_batch(oshi_id=1, items=items)

        import json as _json
        body = _json.loads(resp.calls[0].request.body)
        item = body["items"][0]
        assert "broadcaster" not in item
        assert "airedAt" not in item

    @resp.activate
    def test_post_program_batch_retries_on_429(self) -> None:
        resp.add(resp.POST, self.PROGRAM_BATCH_URL, json={}, status=429)
        resp.add(
            resp.POST,
            self.PROGRAM_BATCH_URL,
            json={"receivedCount": 1, "savedCount": 1, "skippedCount": 0},
            status=200,
        )
        client = BackendClient(make_config(max_retries=3))
        items = [
            ProgramBatchItem(
                program_name="番組",
                source_url="https://tv.example.com/1",
                collected_at=_NOW,
            )
        ]

        import unittest.mock as mock
        with mock.patch("collector.client.time.sleep"):
            result = client.post_program_batch(oshi_id=1, items=items)

        assert result.saved_count == 1
        assert len(resp.calls) == 2

    # ── 重複処理 ─────────────────────────────────────────────────────────────

    @resp.activate
    def test_post_program_batch_handles_skipped_count(self) -> None:
        resp.add(
            resp.POST,
            self.PROGRAM_BATCH_URL,
            json={"receivedCount": 2, "savedCount": 1, "skippedCount": 1},
            status=200,
        )
        client = BackendClient(make_config())
        items = [
            ProgramBatchItem(program_name="番組A", source_url="https://tv.example.com/1", collected_at=_NOW),
            ProgramBatchItem(program_name="番組B", source_url="https://tv.example.com/2", collected_at=_NOW),
        ]

        result = client.post_program_batch(oshi_id=1, items=items)

        assert result.received_count == 2
        assert result.saved_count == 1
        assert result.skipped_count == 1


class TestHelpers:
    def test_to_iso_with_utc(self) -> None:
        dt = datetime(2026, 7, 14, 12, 0, 0, tzinfo=timezone.utc)
        assert _to_iso(dt) == "2026-07-14T12:00:00+00:00"

    def test_to_iso_naive_adds_utc(self) -> None:
        dt = datetime(2026, 7, 14, 12, 0, 0)
        result = _to_iso(dt)
        assert "+00:00" in result

    def test_backoff_attempt1_returns_1(self) -> None:
        assert _backoff(1) == 1

    def test_backoff_attempt2_returns_2(self) -> None:
        assert _backoff(2) == 2

    def test_backoff_attempt3_returns_4(self) -> None:
        assert _backoff(3) == 4

    def test_backoff_large_attempt_capped_at_60(self) -> None:
        assert _backoff(100) == 60
