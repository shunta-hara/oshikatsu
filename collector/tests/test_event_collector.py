"""collector.event_collector のテスト（FEAT-050）"""
from __future__ import annotations

from datetime import datetime, timezone
from unittest.mock import MagicMock

import pytest

from collector.client import BackendClient, BatchResult, TalentSourceInfo
from collector.event_collector import SOURCE_TYPE_EVENT, run_event_collection
from collector.rss_fetcher import RssEntry, RssFetcher

_NOW = datetime(2026, 10, 6, 12, 0, 0, tzinfo=timezone.utc)

_FEED_URL_1 = "https://eplus.jp/talent/event-feed.rss"
_FEED_URL_2 = "https://ticketpea.jp/artist/rss"

_SOURCE_EVENT_1 = TalentSourceInfo(
    id=1,
    talent_id=10,
    source_type="event",
    source_id=_FEED_URL_1,
    is_enabled=True,
)

_SOURCE_EVENT_2 = TalentSourceInfo(
    id=2,
    talent_id=11,
    source_type="event",
    source_id=_FEED_URL_2,
    is_enabled=True,
)

_SOURCE_YOUTUBE = TalentSourceInfo(
    id=3,
    talent_id=12,
    source_type="youtube",
    source_id="UC_channel",
    is_enabled=True,
)

_ENTRY_1 = RssEntry(
    title="イベントタイトル1",
    source_url="https://eplus.jp/event/123",
    published_at=_NOW,
    summary="イベント説明1",
)

_ENTRY_2 = RssEntry(
    title="イベントタイトル2",
    source_url="https://eplus.jp/event/456",
    published_at=_NOW,
    summary="イベント説明2",
)

_BATCH_SAVED = BatchResult(received_count=2, saved_count=2, skipped_count=0)
_BATCH_SKIP = BatchResult(received_count=1, saved_count=0, skipped_count=1)


def make_client(batch_result: BatchResult | None = None) -> BackendClient:
    client = MagicMock(spec=BackendClient)
    client.post_news_batch.return_value = batch_result or _BATCH_SAVED
    return client


def make_fetcher(entries: list[RssEntry] | None = None) -> RssFetcher:
    fetcher = MagicMock(spec=RssFetcher)
    fetcher.fetch.return_value = entries if entries is not None else [_ENTRY_1]
    return fetcher


# ── run_event_collection のテスト ────────────────────────────────────────────

class TestRunEventCollection:
    def test_no_event_sources_skips_collection(self) -> None:
        client = make_client()
        fetcher = make_fetcher()

        run_event_collection(client, fetcher, [])

        client.post_news_batch.assert_not_called()
        fetcher.fetch.assert_not_called()

    def test_non_event_sources_are_ignored(self) -> None:
        """source_type != "event" のソースは処理されない。"""
        client = make_client()
        fetcher = make_fetcher()

        run_event_collection(client, fetcher, [_SOURCE_YOUTUBE])

        fetcher.fetch.assert_not_called()
        client.post_news_batch.assert_not_called()

    def test_successful_collection_calls_post_batch(self) -> None:
        client = make_client()
        fetcher = make_fetcher(entries=[_ENTRY_1, _ENTRY_2])

        run_event_collection(client, fetcher, [_SOURCE_EVENT_1])

        client.post_news_batch.assert_called_once()

    def test_correct_talent_id_is_passed_to_batch(self) -> None:
        client = make_client()
        fetcher = make_fetcher(entries=[_ENTRY_1])

        run_event_collection(client, fetcher, [_SOURCE_EVENT_1])

        call_kwargs = client.post_news_batch.call_args.kwargs
        assert call_kwargs["talent_id"] == _SOURCE_EVENT_1.talent_id

    def test_source_type_is_event_in_batch_items(self) -> None:
        client = make_client()
        fetcher = make_fetcher(entries=[_ENTRY_1])

        run_event_collection(client, fetcher, [_SOURCE_EVENT_1])

        items = client.post_news_batch.call_args.kwargs["items"]
        assert len(items) == 1
        assert items[0].source_type == SOURCE_TYPE_EVENT

    def test_batch_items_contain_source_url(self) -> None:
        client = make_client()
        fetcher = make_fetcher(entries=[_ENTRY_1])

        run_event_collection(client, fetcher, [_SOURCE_EVENT_1])

        items = client.post_news_batch.call_args.kwargs["items"]
        assert items[0].source_url == _ENTRY_1.source_url

    def test_batch_items_contain_published_at(self) -> None:
        client = make_client()
        fetcher = make_fetcher(entries=[_ENTRY_1])

        run_event_collection(client, fetcher, [_SOURCE_EVENT_1])

        items = client.post_news_batch.call_args.kwargs["items"]
        assert items[0].published_at == _NOW

    def test_source_id_is_used_as_feed_url(self) -> None:
        """talent_sources.source_id がそのままフィード URL として使用される。"""
        client = make_client()
        fetcher = make_fetcher(entries=[_ENTRY_1])

        run_event_collection(client, fetcher, [_SOURCE_EVENT_1])

        fetcher.fetch.assert_called_with(_FEED_URL_1)

    def test_fetch_failure_skips_talent_and_continues_others(self) -> None:
        """フィード取得エラー時は当該タレントをスキップして他のタレントの収集を続ける。"""
        client = make_client()
        fetcher = MagicMock(spec=RssFetcher)
        fetcher.fetch.side_effect = [
            RuntimeError("feed unavailable"),
            [_ENTRY_1],
        ]

        run_event_collection(client, fetcher, [_SOURCE_EVENT_1, _SOURCE_EVENT_2])

        # 2番目のタレントは正常に送信される
        client.post_news_batch.assert_called_once()

    def test_http_error_skips_talent_and_continues_others(self) -> None:
        """HTTP 4xx エラー時も当該タレントをスキップして他のタレントの収集を続ける。"""
        client = make_client()
        fetcher = MagicMock(spec=RssFetcher)
        fetcher.fetch.side_effect = [
            RuntimeError("RSS fetch HTTP error: 404"),
            [_ENTRY_2],
        ]

        run_event_collection(client, fetcher, [_SOURCE_EVENT_1, _SOURCE_EVENT_2])

        client.post_news_batch.assert_called_once()

    def test_empty_entries_skips_post_batch(self) -> None:
        """エントリが空（robots.txt 禁止またはフィード空）の場合は送信しない。"""
        client = make_client()
        fetcher = make_fetcher(entries=[])

        run_event_collection(client, fetcher, [_SOURCE_EVENT_1])

        client.post_news_batch.assert_not_called()

    def test_post_batch_failure_continues_without_propagating(self) -> None:
        """一括送信失敗は例外を伝播させない。"""
        client = MagicMock(spec=BackendClient)
        client.post_news_batch.side_effect = RuntimeError("backend down")
        fetcher = make_fetcher(entries=[_ENTRY_1])

        # 例外が外部に伝播しないことを確認
        run_event_collection(client, fetcher, [_SOURCE_EVENT_1])

    def test_calls_fetch_for_each_source(self) -> None:
        client = make_client()
        fetcher = make_fetcher(entries=[_ENTRY_1])

        run_event_collection(client, fetcher, [_SOURCE_EVENT_1, _SOURCE_EVENT_2])

        assert fetcher.fetch.call_count == 2

    def test_each_source_uses_its_own_feed_url(self) -> None:
        client = make_client()
        fetcher = make_fetcher(entries=[_ENTRY_1])

        run_event_collection(client, fetcher, [_SOURCE_EVENT_1, _SOURCE_EVENT_2])

        calls = [call.args[0] for call in fetcher.fetch.call_args_list]
        assert _FEED_URL_1 in calls
        assert _FEED_URL_2 in calls

    def test_duplicate_sources_saved_only_once_via_backend(self) -> None:
        """重複 sourceUrl はバックエンド側でスキップされる（BatchResult に反映）。"""
        client = make_client(
            batch_result=BatchResult(received_count=2, saved_count=1, skipped_count=1)
        )
        fetcher = make_fetcher(entries=[_ENTRY_1, _ENTRY_2])

        run_event_collection(client, fetcher, [_SOURCE_EVENT_1])

        result = client.post_news_batch.call_args  # 呼ばれた
        assert result is not None
