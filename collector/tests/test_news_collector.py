"""collector.news_collector のテスト"""
from __future__ import annotations

from datetime import datetime, timezone
from unittest.mock import MagicMock

import pytest

from collector.client import BackendClient, BatchResult, NewsBatchItem, OshiInfo
from collector.news_collector import run_news_collection
from collector.news_fetcher import NewsEntry, NewsFetcher

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

_BATCH_RESULT_SAVED = BatchResult(received_count=1, saved_count=1, skipped_count=0)
_BATCH_RESULT_DUPLICATE = BatchResult(received_count=1, saved_count=0, skipped_count=1)


def make_client(
    batch_result: BatchResult | None = None,
) -> BackendClient:
    """モック BackendClient を返す。"""
    client = MagicMock(spec=BackendClient)
    client.post_news_batch.return_value = batch_result or _BATCH_RESULT_SAVED
    client.post_job_log.return_value = {"id": 1, "jobType": "news"}
    return client


def make_fetcher(entries: list[NewsEntry] | None = None) -> NewsFetcher:
    """モック NewsFetcher を返す。"""
    fetcher = MagicMock(spec=NewsFetcher)
    fetcher.fetch_for_oshi.return_value = entries if entries is not None else [_ENTRY_1]
    return fetcher


class TestRunNewsCollection:
    # ── 正常系 ────────────────────────────────────────────────────────────────

    def test_run_collection_calls_fetch_for_each_oshi(self) -> None:
        client = make_client()
        fetcher = make_fetcher(entries=[_ENTRY_1])

        run_news_collection(client, fetcher, [_OSHI_A, _OSHI_B])

        assert fetcher.fetch_for_oshi.call_count == 2
        fetcher.fetch_for_oshi.assert_any_call("推しA")
        fetcher.fetch_for_oshi.assert_any_call("推しB")

    def test_run_collection_calls_batch_once_per_oshi(self) -> None:
        client = make_client()
        fetcher = make_fetcher(entries=[_ENTRY_1, _ENTRY_2])

        run_news_collection(client, fetcher, [_OSHI_A])

        client.post_news_batch.assert_called_once()

    def test_run_collection_passes_correct_oshi_id_to_batch(self) -> None:
        client = make_client()
        fetcher = make_fetcher(entries=[_ENTRY_1])

        run_news_collection(client, fetcher, [_OSHI_A])

        call_kwargs = client.post_news_batch.call_args.kwargs
        assert call_kwargs["oshi_id"] == 1

    def test_run_collection_batch_items_contain_titles_and_urls(self) -> None:
        client = make_client()
        fetcher = make_fetcher(entries=[_ENTRY_1, _ENTRY_2])

        run_news_collection(client, fetcher, [_OSHI_A])

        call_kwargs = client.post_news_batch.call_args.kwargs
        items: list[NewsBatchItem] = call_kwargs["items"]
        assert len(items) == 2
        assert items[0].title == "記事1"
        assert items[0].source_url == "https://example.com/1"
        assert items[1].title == "記事2"
        assert items[1].source_url == "https://example.com/2"

    def test_run_collection_returns_collected_urls_per_oshi(self) -> None:
        client = make_client()
        fetcher = make_fetcher(entries=[_ENTRY_1, _ENTRY_2])

        result = run_news_collection(client, fetcher, [_OSHI_A])

        assert result[1] == {"https://example.com/1", "https://example.com/2"}

    def test_run_collection_returns_urls_for_multiple_oshis(self) -> None:
        client = make_client()
        fetcher = MagicMock(spec=NewsFetcher)
        fetcher.fetch_for_oshi.side_effect = [
            [_ENTRY_1],  # 推しA
            [_ENTRY_2],  # 推しB
        ]

        result = run_news_collection(client, fetcher, [_OSHI_A, _OSHI_B])

        assert result[1] == {"https://example.com/1"}
        assert result[2] == {"https://example.com/2"}

    # ── 境界値 ────────────────────────────────────────────────────────────────

    def test_run_collection_empty_oshi_list_skips_collection(self) -> None:
        client = make_client()
        fetcher = make_fetcher()

        result = run_news_collection(client, fetcher, [])

        fetcher.fetch_for_oshi.assert_not_called()
        client.post_news_batch.assert_not_called()
        assert result == {}

    def test_run_collection_empty_oshi_list_sends_job_log(self) -> None:
        client = make_client()
        fetcher = make_fetcher()

        run_news_collection(client, fetcher, [])

        client.post_job_log.assert_called_once()
        call_kwargs = client.post_job_log.call_args.kwargs
        assert call_kwargs["job_type"] == "news"
        assert call_kwargs["success_count"] == 0

    def test_run_collection_empty_entries_skips_batch(self) -> None:
        client = make_client()
        fetcher = make_fetcher(entries=[])

        result = run_news_collection(client, fetcher, [_OSHI_A])

        client.post_news_batch.assert_not_called()
        assert result == {}

    def test_run_collection_empty_entries_returns_empty_url_set(self) -> None:
        client = make_client()
        fetcher = make_fetcher(entries=[])

        result = run_news_collection(client, fetcher, [_OSHI_A])

        # entries が空のときは URL セットを記録しない（キーなし）
        assert 1 not in result

    # ── 異常系 ────────────────────────────────────────────────────────────────

    def test_run_collection_fetch_error_continues_to_next_oshi(self) -> None:
        client = make_client()
        fetcher = MagicMock(spec=NewsFetcher)
        fetcher.fetch_for_oshi.side_effect = [
            RuntimeError("RSS fetch failed"),
            [_ENTRY_1],
        ]

        result = run_news_collection(client, fetcher, [_OSHI_A, _OSHI_B])

        # 推しBは正常に送信される
        client.post_news_batch.assert_called_once()
        assert 2 in result

    def test_run_collection_fetch_error_counts_as_failed(self) -> None:
        client = make_client()
        fetcher = MagicMock(spec=NewsFetcher)
        fetcher.fetch_for_oshi.side_effect = RuntimeError("RSS fetch failed")

        run_news_collection(client, fetcher, [_OSHI_A])

        call_kwargs = client.post_job_log.call_args.kwargs
        assert call_kwargs["failure_count"] == 1

    def test_run_collection_batch_post_error_counts_as_failed(self) -> None:
        client = MagicMock(spec=BackendClient)
        client.post_news_batch.side_effect = RuntimeError("backend error")
        client.post_job_log.return_value = {"id": 1}
        fetcher = make_fetcher(entries=[_ENTRY_1])

        run_news_collection(client, fetcher, [_OSHI_A])

        call_kwargs = client.post_job_log.call_args.kwargs
        assert call_kwargs["failure_count"] == 1

    def test_run_collection_batch_post_error_continues_to_next_oshi(self) -> None:
        client = MagicMock(spec=BackendClient)
        client.post_news_batch.side_effect = [
            RuntimeError("backend error"),
            _BATCH_RESULT_SAVED,
        ]
        client.post_job_log.return_value = {"id": 1}
        fetcher = MagicMock(spec=NewsFetcher)
        fetcher.fetch_for_oshi.return_value = [_ENTRY_1]

        run_news_collection(client, fetcher, [_OSHI_A, _OSHI_B])

        assert client.post_news_batch.call_count == 2

    # ── ジョブログ送信 ────────────────────────────────────────────────────────

    def test_run_collection_sends_job_log_with_saved_count(self) -> None:
        client = make_client(batch_result=BatchResult(received_count=2, saved_count=2, skipped_count=0))
        fetcher = make_fetcher(entries=[_ENTRY_1, _ENTRY_2])

        run_news_collection(client, fetcher, [_OSHI_A])

        client.post_job_log.assert_called_once()
        call_kwargs = client.post_job_log.call_args.kwargs
        assert call_kwargs["job_type"] == "news"
        assert call_kwargs["success_count"] == 2
        assert call_kwargs["failure_count"] == 0

    def test_run_collection_job_log_failure_does_not_raise(self) -> None:
        client = make_client()
        client.post_job_log.side_effect = RuntimeError("log API down")
        fetcher = make_fetcher(entries=[_ENTRY_1])

        # ジョブログ送信の失敗は例外として上に伝播しないこと
        run_news_collection(client, fetcher, [_OSHI_A])

    # ── 重複処理（バッチレスポンスから読み取る） ────────────────────────────

    def test_run_collection_skipped_counted_from_batch_result(self) -> None:
        """バッチレスポンスの skippedCount が正しくログされる"""
        batch_result = BatchResult(received_count=2, saved_count=1, skipped_count=1)
        client = make_client(batch_result=batch_result)
        fetcher = make_fetcher(entries=[_ENTRY_1, _ENTRY_2])

        # 例外なく完了すること
        run_news_collection(client, fetcher, [_OSHI_A])

        # job_log には saved_count が渡る
        call_kwargs = client.post_job_log.call_args.kwargs
        assert call_kwargs["success_count"] == 1

    def test_run_collection_multiple_oshis_totals_accumulated(self) -> None:
        """複数の推しのバッチ結果が合算される"""
        client = MagicMock(spec=BackendClient)
        client.post_news_batch.return_value = BatchResult(
            received_count=1, saved_count=1, skipped_count=0
        )
        client.post_job_log.return_value = {"id": 1}
        fetcher = make_fetcher(entries=[_ENTRY_1])

        run_news_collection(client, fetcher, [_OSHI_A, _OSHI_B])

        call_kwargs = client.post_job_log.call_args.kwargs
        assert call_kwargs["success_count"] == 2  # 2推し × saved_count=1
