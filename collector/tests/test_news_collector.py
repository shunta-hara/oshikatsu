"""collector.news_collector のテスト"""
from __future__ import annotations

from datetime import datetime, timedelta, timezone
from unittest.mock import MagicMock, patch

import pytest

from collector.client import BackendClient, BatchResult, NewsBatchItem, OshiInfo
from collector.news_collector import NEWS_RETENTION_DAYS, _is_entry_expired, run_news_collection
from collector.news_fetcher import NewsEntry, NewsFetcher

_NOW = datetime(2026, 7, 14, 12, 0, 0, tzinfo=timezone.utc)

_OSHI_A = OshiInfo(talent_id=1, name="推しA")
_OSHI_B = OshiInfo(talent_id=2, name="推しB")

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

    def test_run_collection_passes_correct_talent_id_to_batch(self) -> None:
        client = make_client()
        fetcher = make_fetcher(entries=[_ENTRY_1])

        run_news_collection(client, fetcher, [_OSHI_A])

        call_kwargs = client.post_news_batch.call_args.kwargs
        assert call_kwargs["talent_id"] == 1

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

    # ── FEAT-036: source_name の伝播 ─────────────────────────────────────────

    def test_run_collection_batch_items_contain_source_name(self) -> None:
        """NewsEntry の source_name が NewsBatchItem に引き継がれる"""
        entry_with_source = NewsEntry(
            title="記事 - スポニチ",
            source_url="https://example.com/sponichi",
            published_at=_NOW,
            summary="要約",
            source_name="スポニチ",
        )
        client = make_client()
        fetcher = make_fetcher(entries=[entry_with_source])

        run_news_collection(client, fetcher, [_OSHI_A])

        items: list[NewsBatchItem] = client.post_news_batch.call_args.kwargs["items"]
        assert len(items) == 1
        assert items[0].source_name == "スポニチ"

    def test_run_collection_batch_items_source_name_none_when_not_extracted(self) -> None:
        """source_name が None の場合もエラーにならず None として渡す"""
        entry_no_source = NewsEntry(
            title="媒体名なし記事",
            source_url="https://example.com/nosource",
            published_at=_NOW,
            summary=None,
            source_name=None,
        )
        client = make_client()
        fetcher = make_fetcher(entries=[entry_no_source])

        run_news_collection(client, fetcher, [_OSHI_A])

        items: list[NewsBatchItem] = client.post_news_batch.call_args.kwargs["items"]
        assert len(items) == 1
        assert items[0].source_name is None


class TestIsEntryExpired:
    """_is_entry_expired のユニットテスト"""

    # ── 正常系 ─────────────────────────────────────────────────────────────────

    def test_not_expired_when_published_at_is_none(self) -> None:
        """published_at が None の場合は期限切れにならない"""
        entry = NewsEntry(
            title="記事",
            source_url="https://example.com/1",
            published_at=None,
            summary=None,
        )
        assert not _is_entry_expired(entry, _NOW)

    def test_not_expired_when_published_at_is_recent(self) -> None:
        """1日前の記事は期限切れにならない"""
        entry = NewsEntry(
            title="記事",
            source_url="https://example.com/1",
            published_at=_NOW - timedelta(days=1),
            summary=None,
        )
        assert not _is_entry_expired(entry, _NOW)

    def test_expired_when_published_at_is_91_days_old(self) -> None:
        """91日前の記事は期限切れ"""
        entry = NewsEntry(
            title="記事",
            source_url="https://example.com/1",
            published_at=_NOW - timedelta(days=91),
            summary=None,
        )
        assert _is_entry_expired(entry, _NOW)

    # ── 境界値 ─────────────────────────────────────────────────────────────────

    def test_expired_when_published_at_is_exactly_retention_days_old(self) -> None:
        """ちょうど保持期間（90日）前の記事は期限切れ"""
        entry = NewsEntry(
            title="記事",
            source_url="https://example.com/1",
            published_at=_NOW - timedelta(days=NEWS_RETENTION_DAYS),
            summary=None,
        )
        assert _is_entry_expired(entry, _NOW)

    def test_not_expired_when_published_at_is_one_second_within_retention(self) -> None:
        """保持期間より1秒以内の記事は期限切れにならない"""
        entry = NewsEntry(
            title="記事",
            source_url="https://example.com/1",
            published_at=_NOW - timedelta(days=NEWS_RETENTION_DAYS) + timedelta(seconds=1),
            summary=None,
        )
        assert not _is_entry_expired(entry, _NOW)


class TestRunNewsCollectionExpiredFilter:
    """run_news_collection の期限切れフィルタテスト"""

    def test_expired_entry_is_not_sent(self) -> None:
        """91日前の記事は送信されない"""
        old_entry = NewsEntry(
            title="古い記事",
            source_url="https://example.com/old",
            published_at=_NOW - timedelta(days=91),
            summary=None,
        )
        client = make_client()
        fetcher = make_fetcher(entries=[old_entry])

        with patch("collector.news_collector.datetime") as mock_dt:
            mock_dt.now.return_value = _NOW
            run_news_collection(client, fetcher, [_OSHI_A])

        client.post_news_batch.assert_not_called()

    def test_fresh_entry_is_sent(self) -> None:
        """1日前の記事は送信される"""
        fresh_entry = NewsEntry(
            title="新しい記事",
            source_url="https://example.com/fresh",
            published_at=_NOW - timedelta(days=1),
            summary=None,
        )
        client = make_client()
        fetcher = make_fetcher(entries=[fresh_entry])

        with patch("collector.news_collector.datetime") as mock_dt:
            mock_dt.now.return_value = _NOW
            run_news_collection(client, fetcher, [_OSHI_A])

        client.post_news_batch.assert_called_once()

    def test_mixed_entries_only_fresh_sent(self) -> None:
        """古い記事と新しい記事が混在する場合、新しい記事だけ送信される"""
        old_entry = NewsEntry(
            title="古い記事",
            source_url="https://example.com/old",
            published_at=_NOW - timedelta(days=91),
            summary=None,
        )
        fresh_entry = NewsEntry(
            title="新しい記事",
            source_url="https://example.com/fresh",
            published_at=_NOW - timedelta(days=1),
            summary=None,
        )
        client = make_client()
        fetcher = make_fetcher(entries=[old_entry, fresh_entry])

        with patch("collector.news_collector.datetime") as mock_dt:
            mock_dt.now.return_value = _NOW
            run_news_collection(client, fetcher, [_OSHI_A])

        items: list[NewsBatchItem] = client.post_news_batch.call_args.kwargs["items"]
        assert len(items) == 1
        assert items[0].source_url == "https://example.com/fresh"

    def test_entry_with_none_published_at_is_sent(self) -> None:
        """published_at が None の記事は送信される（収集日時が期限内のため）"""
        entry_no_published_at = NewsEntry(
            title="日時不明記事",
            source_url="https://example.com/unknown",
            published_at=None,
            summary=None,
        )
        client = make_client()
        fetcher = make_fetcher(entries=[entry_no_published_at])

        with patch("collector.news_collector.datetime") as mock_dt:
            mock_dt.now.return_value = _NOW
            run_news_collection(client, fetcher, [_OSHI_A])

        client.post_news_batch.assert_called_once()

    def test_expired_entries_not_included_in_collected_urls(self) -> None:
        """期限切れ記事の URL は collected_urls に含まれない"""
        fresh_entry = NewsEntry(
            title="新しい記事",
            source_url="https://example.com/fresh",
            published_at=_NOW - timedelta(days=1),
            summary=None,
        )
        old_entry = NewsEntry(
            title="古い記事",
            source_url="https://example.com/old",
            published_at=_NOW - timedelta(days=91),
            summary=None,
        )
        client = make_client()
        fetcher = make_fetcher(entries=[fresh_entry, old_entry])

        with patch("collector.news_collector.datetime") as mock_dt:
            mock_dt.now.return_value = _NOW
            result = run_news_collection(client, fetcher, [_OSHI_A])

        assert "https://example.com/fresh" in result[_OSHI_A.id]
        assert "https://example.com/old" not in result[_OSHI_A.id]
