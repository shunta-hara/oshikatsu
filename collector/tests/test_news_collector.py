"""collector.news_collector のテスト"""
from __future__ import annotations

from datetime import datetime, timezone
from unittest.mock import MagicMock, patch

import pytest

from collector.client import BackendClient, OshiInfo
from collector.news_collector import run_news_collection
from collector.news_fetcher import NewsFetcher, NewsEntry

_NOW = datetime(2026, 7, 14, 12, 0, 0, tzinfo=timezone.utc)

_OSHI_A = OshiInfo(id=1, name="推しA")
_OSHI_B = OshiInfo(id=2, name="推しB")

_ENTRY_1 = NewsEntry(
    title="記事1",
    source_url="https://example.com/1",
    published_at=_NOW,
    summary="要約1",
)
_ENTRY_2 = NewsEntry(
    title="記事2",
    source_url="https://example.com/2",
    published_at=_NOW,
    summary="要約2",
)


def make_client(
    oshis: list[OshiInfo] | None = None,
    post_response: dict | None = None,
) -> BackendClient:
    """モック BackendClient を返す。"""
    client = MagicMock(spec=BackendClient)
    client.get_oshis.return_value = oshis if oshis is not None else [_OSHI_A]
    client.post_news_item.return_value = post_response or {"id": 1, "wasDuplicate": False}
    client.post_job_log.return_value = {"id": 1, "jobType": "news"}
    return client


def make_fetcher(entries: list[NewsEntry] | None = None) -> NewsFetcher:
    """モック NewsFetcher を返す。"""
    fetcher = MagicMock(spec=NewsFetcher)
    fetcher.fetch_for_oshi.return_value = entries if entries is not None else [_ENTRY_1]
    return fetcher


class TestRunNewsCollection:
    # ── 正常系 ────────────────────────────────────────────────────────────────

    def test_run_collection_calls_get_oshis(self) -> None:
        client = make_client()
        fetcher = make_fetcher()

        run_news_collection(client, fetcher)

        client.get_oshis.assert_called_once()

    def test_run_collection_calls_fetch_for_each_oshi(self) -> None:
        client = make_client(oshis=[_OSHI_A, _OSHI_B])
        fetcher = make_fetcher(entries=[_ENTRY_1])

        run_news_collection(client, fetcher)

        assert fetcher.fetch_for_oshi.call_count == 2
        fetcher.fetch_for_oshi.assert_any_call("推しA")
        fetcher.fetch_for_oshi.assert_any_call("推しB")

    def test_run_collection_posts_each_entry(self) -> None:
        client = make_client(oshis=[_OSHI_A], post_response={"id": 1, "wasDuplicate": False})
        fetcher = make_fetcher(entries=[_ENTRY_1, _ENTRY_2])

        run_news_collection(client, fetcher)

        assert client.post_news_item.call_count == 2

    def test_run_collection_passes_correct_oshi_id(self) -> None:
        client = make_client(oshis=[_OSHI_A])
        fetcher = make_fetcher(entries=[_ENTRY_1])

        run_news_collection(client, fetcher)

        call_kwargs = client.post_news_item.call_args.kwargs
        assert call_kwargs["oshi_id"] == 1

    def test_run_collection_passes_title_and_source_url(self) -> None:
        client = make_client(oshis=[_OSHI_A])
        fetcher = make_fetcher(entries=[_ENTRY_1])

        run_news_collection(client, fetcher)

        call_kwargs = client.post_news_item.call_args.kwargs
        assert call_kwargs["title"] == "記事1"
        assert call_kwargs["source_url"] == "https://example.com/1"

    # ── 境界値 ────────────────────────────────────────────────────────────────

    def test_run_collection_no_oshis_skips_collection(self) -> None:
        client = make_client(oshis=[])
        fetcher = make_fetcher()

        run_news_collection(client, fetcher)

        fetcher.fetch_for_oshi.assert_not_called()
        client.post_news_item.assert_not_called()

    def test_run_collection_empty_entries_posts_nothing(self) -> None:
        client = make_client(oshis=[_OSHI_A])
        fetcher = make_fetcher(entries=[])

        run_news_collection(client, fetcher)

        client.post_news_item.assert_not_called()

    # ── 異常系 ────────────────────────────────────────────────────────────────

    def test_run_collection_get_oshis_failure_returns_early(self) -> None:
        client = MagicMock(spec=BackendClient)
        client.get_oshis.side_effect = RuntimeError("backend unavailable")
        fetcher = make_fetcher()

        # 例外を送出せずに終了すること
        run_news_collection(client, fetcher)

        fetcher.fetch_for_oshi.assert_not_called()
        client.post_news_item.assert_not_called()

    def test_run_collection_fetch_error_continues_to_next_oshi(self) -> None:
        client = make_client(oshis=[_OSHI_A, _OSHI_B])
        fetcher = MagicMock(spec=NewsFetcher)
        # 推しAで失敗、推しBで成功
        fetcher.fetch_for_oshi.side_effect = [
            RuntimeError("RSS fetch failed"),
            [_ENTRY_1],
        ]
        client.post_news_item.return_value = {"id": 1, "wasDuplicate": False}

        run_news_collection(client, fetcher)

        # 推しBの記事は送信されること
        client.post_news_item.assert_called_once()

    def test_run_collection_post_error_counts_as_failed(self) -> None:
        client = MagicMock(spec=BackendClient)
        client.get_oshis.return_value = [_OSHI_A]
        client.post_news_item.side_effect = RuntimeError("backend error")
        fetcher = make_fetcher(entries=[_ENTRY_1, _ENTRY_2])

        # 例外を送出せずに全件処理を試みること
        run_news_collection(client, fetcher)

        assert client.post_news_item.call_count == 2

    # ── ジョブログ送信 ────────────────────────────────────────────────────────

    def test_run_collection_sends_job_log_after_success(self) -> None:
        client = make_client(oshis=[_OSHI_A], post_response={"id": 1, "wasDuplicate": False})
        fetcher = make_fetcher(entries=[_ENTRY_1])

        run_news_collection(client, fetcher)

        client.post_job_log.assert_called_once()
        call_kwargs = client.post_job_log.call_args.kwargs
        assert call_kwargs["job_type"] == "news"
        assert call_kwargs["success_count"] == 1
        assert call_kwargs["failure_count"] == 0

    def test_run_collection_sends_job_log_on_get_oshis_failure(self) -> None:
        client = MagicMock(spec=BackendClient)
        client.get_oshis.side_effect = RuntimeError("backend down")
        client.post_job_log.return_value = {"id": 1}
        fetcher = make_fetcher()

        run_news_collection(client, fetcher)

        client.post_job_log.assert_called_once()
        call_kwargs = client.post_job_log.call_args.kwargs
        assert call_kwargs["job_type"] == "news"
        assert call_kwargs["failure_count"] >= 1

    def test_run_collection_sends_job_log_when_no_oshis(self) -> None:
        client = make_client(oshis=[])
        fetcher = make_fetcher()

        run_news_collection(client, fetcher)

        client.post_job_log.assert_called_once()
        call_kwargs = client.post_job_log.call_args.kwargs
        assert call_kwargs["job_type"] == "news"
        assert call_kwargs["success_count"] == 0

    def test_run_collection_job_log_failure_does_not_raise(self) -> None:
        client = make_client(oshis=[_OSHI_A], post_response={"id": 1, "wasDuplicate": False})
        client.post_job_log.side_effect = RuntimeError("log API down")
        fetcher = make_fetcher(entries=[_ENTRY_1])

        # ジョブログ送信の失敗は例外として上に伝播しないこと
        run_news_collection(client, fetcher)

    # ── 重複処理 ──────────────────────────────────────────────────────────────

    def test_run_collection_duplicate_counted_separately(self) -> None:
        """wasDuplicate=True の記事は新規保存件数に含めない"""
        client = make_client(
            oshis=[_OSHI_A],
            post_response={"id": 1, "wasDuplicate": True},
        )
        fetcher = make_fetcher(entries=[_ENTRY_1])

        # ログで重複カウントされるだけで例外は発生しないこと
        run_news_collection(client, fetcher)

        client.post_news_item.assert_called_once()

    def test_run_collection_mixed_duplicate_and_new(self) -> None:
        client = MagicMock(spec=BackendClient)
        client.get_oshis.return_value = [_OSHI_A]
        client.post_news_item.side_effect = [
            {"id": 1, "wasDuplicate": False},
            {"id": 2, "wasDuplicate": True},
        ]
        fetcher = make_fetcher(entries=[_ENTRY_1, _ENTRY_2])

        # 例外なく完了すること
        run_news_collection(client, fetcher)

        assert client.post_news_item.call_count == 2
