"""collector.x_collector のテスト（FEAT-049）"""
from __future__ import annotations

from datetime import datetime, timezone
from unittest.mock import MagicMock

import pytest

from collector.client import BackendClient, BatchResult, TalentSourceInfo
from collector.rss_fetcher import RssEntry, RssFetcher
from collector.x_collector import SOURCE_TYPE_X_POST, _build_x_rss_url, run_x_collection

_NOW = datetime(2026, 10, 6, 12, 0, 0, tzinfo=timezone.utc)

_BASE_URL = "https://nitter.example.com"

_SOURCE_X_1 = TalentSourceInfo(
    id=1,
    talent_id=10,
    source_type="x_post",
    source_id="someuser",
    is_enabled=True,
)

_SOURCE_X_2 = TalentSourceInfo(
    id=2,
    talent_id=11,
    source_type="x_post",
    source_id="@otheruser",
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
    title="ツイート1",
    source_url="https://nitter.example.com/someuser/status/111",
    published_at=_NOW,
    summary="ツイート本文1",
)

_ENTRY_2 = RssEntry(
    title="ツイート2",
    source_url="https://nitter.example.com/someuser/status/222",
    published_at=_NOW,
    summary="ツイート本文2",
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


# ── _build_x_rss_url のテスト ─────────────────────────────────────────────────

class TestBuildXRssUrl:
    def test_handle_without_at_prefix(self) -> None:
        result = _build_x_rss_url("https://nitter.example.com", "someuser")

        assert result == "https://nitter.example.com/someuser/rss"

    def test_handle_with_at_prefix_stripped(self) -> None:
        result = _build_x_rss_url("https://nitter.example.com", "@someuser")

        assert result == "https://nitter.example.com/someuser/rss"

    def test_base_url_trailing_slash_stripped(self) -> None:
        result = _build_x_rss_url("https://nitter.example.com/", "user")

        assert result == "https://nitter.example.com/user/rss"

    def test_base_url_multiple_trailing_slashes_stripped(self) -> None:
        result = _build_x_rss_url("https://nitter.example.com///", "user")

        assert result == "https://nitter.example.com/user/rss"


# ── run_x_collection のテスト ─────────────────────────────────────────────────

class TestRunXCollection:
    def test_empty_base_url_skips_all(self) -> None:
        client = make_client()
        fetcher = make_fetcher()

        run_x_collection(client, fetcher, [_SOURCE_X_1], x_rss_base_url="")

        client.post_news_batch.assert_not_called()
        fetcher.fetch.assert_not_called()

    def test_none_base_url_equivalent_to_empty(self) -> None:
        """x_rss_base_url が None 相当（空文字）の場合もスキップする。"""
        client = make_client()
        fetcher = make_fetcher()

        run_x_collection(client, fetcher, [_SOURCE_X_1], x_rss_base_url="")

        client.post_news_batch.assert_not_called()

    def test_no_x_sources_skips_collection(self) -> None:
        client = make_client()
        fetcher = make_fetcher()

        run_x_collection(client, fetcher, [], x_rss_base_url=_BASE_URL)

        client.post_news_batch.assert_not_called()
        fetcher.fetch.assert_not_called()

    def test_non_x_post_sources_are_ignored(self) -> None:
        """source_type != "x_post" のソースは処理されない。"""
        client = make_client()
        fetcher = make_fetcher()

        run_x_collection(client, fetcher, [_SOURCE_YOUTUBE], x_rss_base_url=_BASE_URL)

        fetcher.fetch.assert_not_called()
        client.post_news_batch.assert_not_called()

    def test_successful_collection_calls_post_batch(self) -> None:
        client = make_client()
        fetcher = make_fetcher(entries=[_ENTRY_1, _ENTRY_2])

        run_x_collection(client, fetcher, [_SOURCE_X_1], x_rss_base_url=_BASE_URL)

        client.post_news_batch.assert_called_once()

    def test_correct_talent_id_is_passed_to_batch(self) -> None:
        client = make_client()
        fetcher = make_fetcher(entries=[_ENTRY_1])

        run_x_collection(client, fetcher, [_SOURCE_X_1], x_rss_base_url=_BASE_URL)

        call_kwargs = client.post_news_batch.call_args.kwargs
        assert call_kwargs["talent_id"] == _SOURCE_X_1.talent_id

    def test_source_type_is_x_post_in_batch_items(self) -> None:
        client = make_client()
        fetcher = make_fetcher(entries=[_ENTRY_1])

        run_x_collection(client, fetcher, [_SOURCE_X_1], x_rss_base_url=_BASE_URL)

        items = client.post_news_batch.call_args.kwargs["items"]
        assert len(items) == 1
        assert items[0].source_type == SOURCE_TYPE_X_POST

    def test_batch_items_contain_source_url(self) -> None:
        client = make_client()
        fetcher = make_fetcher(entries=[_ENTRY_1])

        run_x_collection(client, fetcher, [_SOURCE_X_1], x_rss_base_url=_BASE_URL)

        items = client.post_news_batch.call_args.kwargs["items"]
        assert items[0].source_url == _ENTRY_1.source_url

    def test_batch_items_contain_published_at(self) -> None:
        client = make_client()
        fetcher = make_fetcher(entries=[_ENTRY_1])

        run_x_collection(client, fetcher, [_SOURCE_X_1], x_rss_base_url=_BASE_URL)

        items = client.post_news_batch.call_args.kwargs["items"]
        assert items[0].published_at == _NOW

    def test_fetch_url_constructed_from_base_url_and_handle(self) -> None:
        client = make_client()
        fetcher = make_fetcher(entries=[_ENTRY_1])

        run_x_collection(client, fetcher, [_SOURCE_X_1], x_rss_base_url=_BASE_URL)

        expected_url = f"{_BASE_URL}/{_SOURCE_X_1.source_id}/rss"
        fetcher.fetch.assert_called_with(expected_url)

    def test_fetch_url_strips_at_from_handle(self) -> None:
        """ハンドルが '@' 付きでも正しく URL を組み立てる。"""
        client = make_client()
        fetcher = make_fetcher(entries=[_ENTRY_2])

        run_x_collection(client, fetcher, [_SOURCE_X_2], x_rss_base_url=_BASE_URL)

        # source_id = "@otheruser" → URL は "@" なし
        expected_url = f"{_BASE_URL}/otheruser/rss"
        fetcher.fetch.assert_called_with(expected_url)

    def test_fetch_failure_skips_talent_and_continues_others(self) -> None:
        """プロキシエラー時は当該タレントをスキップして他のタレントの収集を続ける。"""
        client = make_client()
        fetcher = MagicMock(spec=RssFetcher)
        fetcher.fetch.side_effect = [
            RuntimeError("proxy down"),
            [_ENTRY_1],
        ]

        run_x_collection(
            client, fetcher, [_SOURCE_X_1, _SOURCE_X_2], x_rss_base_url=_BASE_URL
        )

        # 2番目のタレントは正常に送信される
        client.post_news_batch.assert_called_once()

    def test_empty_entries_skips_post_batch(self) -> None:
        """エントリが空（robots.txt 禁止またはフィード空）の場合は送信しない。"""
        client = make_client()
        fetcher = make_fetcher(entries=[])

        run_x_collection(client, fetcher, [_SOURCE_X_1], x_rss_base_url=_BASE_URL)

        client.post_news_batch.assert_not_called()

    def test_post_batch_failure_continues_without_propagating(self) -> None:
        """一括送信失敗は例外を伝播させない。"""
        client = MagicMock(spec=BackendClient)
        client.post_news_batch.side_effect = RuntimeError("backend down")
        fetcher = make_fetcher(entries=[_ENTRY_1])

        # 例外が外部に伝播しないことを確認
        run_x_collection(client, fetcher, [_SOURCE_X_1], x_rss_base_url=_BASE_URL)

    def test_calls_fetch_for_each_source(self) -> None:
        client = make_client()
        fetcher = make_fetcher(entries=[_ENTRY_1])

        run_x_collection(
            client, fetcher, [_SOURCE_X_1, _SOURCE_X_2], x_rss_base_url=_BASE_URL
        )

        assert fetcher.fetch.call_count == 2

    def test_multiple_sources_multiple_post_batch_calls(self) -> None:
        client = make_client()
        fetcher = make_fetcher(entries=[_ENTRY_1])

        run_x_collection(
            client, fetcher, [_SOURCE_X_1, _SOURCE_X_2], x_rss_base_url=_BASE_URL
        )

        assert client.post_news_batch.call_count == 2
