"""collector.youtube_collector のテスト（FEAT-048）"""
from __future__ import annotations

import json
from datetime import datetime, timezone
from typing import Any
from unittest.mock import MagicMock, patch

import pytest
import responses as responses_lib

from collector.client import BackendClient, BatchResult, TalentSourceInfo
from collector.youtube_collector import (
    SOURCE_TYPE_YOUTUBE,
    YouTubeFetcher,
    YouTubeVideoEntry,
    run_youtube_collection,
)

_NOW = datetime(2026, 10, 6, 12, 0, 0, tzinfo=timezone.utc)

_SOURCE_YOUTUBE = TalentSourceInfo(
    id=1,
    talent_id=10,
    source_type="youtube",
    source_id="UC_testchannel",
    is_enabled=True,
)

_SOURCE_YOUTUBE_2 = TalentSourceInfo(
    id=2,
    talent_id=11,
    source_type="youtube",
    source_id="UC_otherchannel",
    is_enabled=True,
)

_ENTRY_1 = YouTubeVideoEntry(
    title="動画タイトル1",
    source_url="https://www.youtube.com/watch?v=abc123",
    published_at=_NOW,
    channel_title="テストチャンネル",
)

_ENTRY_2 = YouTubeVideoEntry(
    title="動画タイトル2",
    source_url="https://www.youtube.com/watch?v=def456",
    published_at=_NOW,
    channel_title="テストチャンネル",
)

_BATCH_RESULT_SAVED = BatchResult(received_count=2, saved_count=2, skipped_count=0)
_BATCH_RESULT_SKIP = BatchResult(received_count=2, saved_count=0, skipped_count=2)

_YOUTUBE_API_URL = "https://www.googleapis.com/youtube/v3/search"


def _make_api_response(video_ids: list[str], channel_title: str = "テストチャンネル") -> dict[str, Any]:
    """YouTube Data API v3 search.list レスポンスを模倣する dict を返す。"""
    items = [
        {
            "id": {"videoId": vid},
            "snippet": {
                "title": f"動画 {vid}",
                "channelTitle": channel_title,
                "publishedAt": "2026-10-06T12:00:00Z",
            },
        }
        for vid in video_ids
    ]
    return {"items": items}


def make_client(batch_result: BatchResult | None = None) -> BackendClient:
    """モック BackendClient を返す。"""
    client = MagicMock(spec=BackendClient)
    client.post_news_batch.return_value = batch_result or _BATCH_RESULT_SAVED
    return client


def make_fetcher(entries: list[YouTubeVideoEntry] | None = None) -> YouTubeFetcher:
    """モック YouTubeFetcher を返す。"""
    fetcher = MagicMock(spec=YouTubeFetcher)
    fetcher.fetch_channel_videos.return_value = entries if entries is not None else [_ENTRY_1]
    fetcher.api_call_count = 0
    return fetcher


# ── YouTubeFetcher._parse_response テスト ─────────────────────────────────────

class TestYouTubeFetcherParseResponse:
    def test_parse_valid_response_returns_entries(self) -> None:
        data = _make_api_response(["abc123", "def456"])

        result = YouTubeFetcher._parse_response(data, "UC_test")

        assert len(result) == 2
        assert result[0].source_url == "https://www.youtube.com/watch?v=abc123"
        assert result[1].source_url == "https://www.youtube.com/watch?v=def456"

    def test_parse_extracts_title(self) -> None:
        data = {"items": [
            {"id": {"videoId": "vid1"}, "snippet": {"title": "テスト動画", "channelTitle": "ch", "publishedAt": "2026-10-06T00:00:00Z"}}
        ]}

        result = YouTubeFetcher._parse_response(data, "UC_test")

        assert result[0].title == "テスト動画"

    def test_parse_extracts_channel_title(self) -> None:
        data = {"items": [
            {"id": {"videoId": "v1"}, "snippet": {"title": "T", "channelTitle": "My Channel", "publishedAt": "2026-10-06T00:00:00Z"}}
        ]}

        result = YouTubeFetcher._parse_response(data, "UC_test")

        assert result[0].channel_title == "My Channel"

    def test_parse_constructs_youtube_url(self) -> None:
        data = {"items": [
            {"id": {"videoId": "xyz789"}, "snippet": {"title": "T", "channelTitle": "ch", "publishedAt": "2026-10-06T00:00:00Z"}}
        ]}

        result = YouTubeFetcher._parse_response(data, "UC_test")

        assert result[0].source_url == "https://www.youtube.com/watch?v=xyz789"

    def test_parse_valid_published_at(self) -> None:
        data = {"items": [
            {"id": {"videoId": "v1"}, "snippet": {"title": "T", "channelTitle": "ch", "publishedAt": "2026-10-06T12:30:00Z"}}
        ]}

        result = YouTubeFetcher._parse_response(data, "UC_test")

        assert result[0].published_at is not None
        assert result[0].published_at.year == 2026
        assert result[0].published_at.tzinfo == timezone.utc

    def test_parse_invalid_published_at_returns_none(self) -> None:
        data = {"items": [
            {"id": {"videoId": "v1"}, "snippet": {"title": "T", "channelTitle": "ch", "publishedAt": "invalid-date"}}
        ]}

        result = YouTubeFetcher._parse_response(data, "UC_test")

        # published_at が None でも entry 自体は返る
        assert result[0].published_at is None

    def test_parse_missing_video_id_skips_item(self) -> None:
        data = {"items": [
            {"id": {}, "snippet": {"title": "T", "channelTitle": "ch"}}
        ]}

        result = YouTubeFetcher._parse_response(data, "UC_test")

        assert result == []

    def test_parse_missing_title_skips_item(self) -> None:
        data = {"items": [
            {"id": {"videoId": "v1"}, "snippet": {"channelTitle": "ch"}}
        ]}

        result = YouTubeFetcher._parse_response(data, "UC_test")

        assert result == []

    def test_parse_empty_items_returns_empty(self) -> None:
        data = {"items": []}

        result = YouTubeFetcher._parse_response(data, "UC_test")

        assert result == []

    def test_parse_unexpected_format_returns_empty(self) -> None:
        result = YouTubeFetcher._parse_response("not a dict", "UC_test")

        assert result == []


# ── YouTubeFetcher.fetch_channel_videos テスト（HTTP モック） ─────────────────

class TestYouTubeFetcherFetchChannelVideos:
    @responses_lib.activate
    def test_fetch_success_returns_entries(self) -> None:
        responses_lib.add(
            responses_lib.GET,
            _YOUTUBE_API_URL,
            json=_make_api_response(["v1", "v2"]),
            status=200,
        )
        fetcher = YouTubeFetcher(api_key="test_key", timeout=5, max_retries=1)

        result = fetcher.fetch_channel_videos("UC_test")

        assert len(result) == 2

    @responses_lib.activate
    def test_fetch_increments_api_call_count(self) -> None:
        responses_lib.add(
            responses_lib.GET,
            _YOUTUBE_API_URL,
            json=_make_api_response(["v1"]),
            status=200,
        )
        fetcher = YouTubeFetcher(api_key="test_key", timeout=5, max_retries=1)

        fetcher.fetch_channel_videos("UC_test")

        assert fetcher.api_call_count == 1

    @responses_lib.activate
    def test_fetch_retries_on_5xx(self) -> None:
        responses_lib.add(responses_lib.GET, _YOUTUBE_API_URL, status=500)
        responses_lib.add(responses_lib.GET, _YOUTUBE_API_URL, json=_make_api_response(["v1"]), status=200)
        fetcher = YouTubeFetcher(api_key="test_key", timeout=5, max_retries=2)

        result = fetcher.fetch_channel_videos("UC_test")

        assert len(result) == 1

    @responses_lib.activate
    def test_fetch_raises_after_max_retries(self) -> None:
        responses_lib.add(responses_lib.GET, _YOUTUBE_API_URL, status=500)
        responses_lib.add(responses_lib.GET, _YOUTUBE_API_URL, status=500)
        fetcher = YouTubeFetcher(api_key="test_key", timeout=5, max_retries=2)

        with pytest.raises(RuntimeError):
            fetcher.fetch_channel_videos("UC_test")

    @responses_lib.activate
    def test_fetch_retries_on_429(self) -> None:
        responses_lib.add(responses_lib.GET, _YOUTUBE_API_URL, status=429)
        responses_lib.add(responses_lib.GET, _YOUTUBE_API_URL, json=_make_api_response(["v1"]), status=200)
        fetcher = YouTubeFetcher(api_key="test_key", timeout=5, max_retries=2)

        result = fetcher.fetch_channel_videos("UC_test")

        assert len(result) == 1


# ── run_youtube_collection テスト ──────────────────────────────────────────────

class TestRunYouTubeCollection:
    def test_no_youtube_sources_skips_collection(self) -> None:
        client = make_client()
        fetcher = make_fetcher()

        run_youtube_collection(client, fetcher, [])

        client.post_news_batch.assert_not_called()

    def test_calls_fetch_for_each_source(self) -> None:
        client = make_client()
        fetcher = make_fetcher(entries=[_ENTRY_1])

        run_youtube_collection(client, fetcher, [_SOURCE_YOUTUBE, _SOURCE_YOUTUBE_2])

        assert fetcher.fetch_channel_videos.call_count == 2

    def test_calls_post_news_batch_with_youtube_source_type(self) -> None:
        client = make_client()
        fetcher = make_fetcher(entries=[_ENTRY_1])

        run_youtube_collection(client, fetcher, [_SOURCE_YOUTUBE])

        client.post_news_batch.assert_called_once()
        call_kwargs = client.post_news_batch.call_args.kwargs
        assert call_kwargs["talent_id"] == _SOURCE_YOUTUBE.talent_id
        items = call_kwargs["items"]
        assert len(items) == 1
        assert items[0].source_type == SOURCE_TYPE_YOUTUBE

    def test_passes_correct_talent_id_to_batch(self) -> None:
        client = make_client()
        fetcher = make_fetcher(entries=[_ENTRY_1])

        run_youtube_collection(client, fetcher, [_SOURCE_YOUTUBE])

        call_kwargs = client.post_news_batch.call_args.kwargs
        assert call_kwargs["talent_id"] == 10

    def test_empty_entries_skips_post(self) -> None:
        client = make_client()
        fetcher = make_fetcher(entries=[])

        run_youtube_collection(client, fetcher, [_SOURCE_YOUTUBE])

        client.post_news_batch.assert_not_called()

    def test_fetch_failure_continues_other_sources(self) -> None:
        client = make_client()
        fetcher = MagicMock(spec=YouTubeFetcher)
        # 最初のチャンネルで例外、2番目は正常
        fetcher.fetch_channel_videos.side_effect = [
            RuntimeError("API error"),
            [_ENTRY_1],
        ]
        fetcher.api_call_count = 0

        run_youtube_collection(client, fetcher, [_SOURCE_YOUTUBE, _SOURCE_YOUTUBE_2])

        # 2番目のチャンネルは正常に送信される
        client.post_news_batch.assert_called_once()

    def test_post_batch_failure_continues(self) -> None:
        client = MagicMock(spec=BackendClient)
        client.post_news_batch.side_effect = RuntimeError("API down")
        fetcher = make_fetcher(entries=[_ENTRY_1])

        # 例外が外部に伝播しないことを確認
        run_youtube_collection(client, fetcher, [_SOURCE_YOUTUBE])

    def test_batch_items_contain_source_url(self) -> None:
        client = make_client()
        fetcher = make_fetcher(entries=[_ENTRY_1])

        run_youtube_collection(client, fetcher, [_SOURCE_YOUTUBE])

        items = client.post_news_batch.call_args.kwargs["items"]
        assert items[0].source_url == _ENTRY_1.source_url

    def test_batch_items_contain_published_at(self) -> None:
        client = make_client()
        entry = YouTubeVideoEntry(
            title="テスト",
            source_url="https://www.youtube.com/watch?v=abc",
            published_at=_NOW,
            channel_title="ch",
        )
        fetcher = make_fetcher(entries=[entry])

        run_youtube_collection(client, fetcher, [_SOURCE_YOUTUBE])

        items = client.post_news_batch.call_args.kwargs["items"]
        assert items[0].published_at == _NOW

    def test_non_youtube_sources_are_ignored(self) -> None:
        """source_type != "youtube" のソースは処理されない。"""
        client = make_client()
        fetcher = make_fetcher(entries=[_ENTRY_1])
        x_source = TalentSourceInfo(
            id=3,
            talent_id=12,
            source_type="x_post",
            source_id="@handle",
            is_enabled=True,
        )

        run_youtube_collection(client, fetcher, [x_source])

        fetcher.fetch_channel_videos.assert_not_called()
        client.post_news_batch.assert_not_called()
