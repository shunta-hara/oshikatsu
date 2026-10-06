"""collector.rss_fetcher のテスト（FEAT-049, FEAT-050）"""
from __future__ import annotations

from datetime import datetime, timezone
from unittest.mock import MagicMock, patch

import pytest
import responses as responses_lib

import requests

from collector.rss_fetcher import RssEntry, RssFetcher, _parse_date, parse_rss_xml

# ── テスト用 XML フィクスチャ ────────────────────────────────────────────────
# bytes に日本語を含められないため ".encode('utf-8')" を使う

_RSS2_XML: bytes = (
    '<?xml version="1.0" encoding="UTF-8"?>'
    "<rss version=\"2.0\">"
    "<channel>"
    "<title>Test Feed</title>"
    "<item>"
    "<title>記事タイトル1</title>"
    "<link>https://example.com/article/1</link>"
    "<pubDate>Mon, 06 Oct 2026 12:00:00 +0000</pubDate>"
    "<description>記事の説明1</description>"
    "</item>"
    "<item>"
    "<title>記事タイトル2</title>"
    "<link>https://example.com/article/2</link>"
    "<pubDate>Tue, 07 Oct 2026 09:00:00 +0900</pubDate>"
    "</item>"
    "</channel>"
    "</rss>"
).encode("utf-8")

_ATOM_XML: bytes = (
    '<?xml version="1.0" encoding="UTF-8"?>'
    '<feed xmlns="http://www.w3.org/2005/Atom">'
    "<title>Test Atom Feed</title>"
    "<entry>"
    "<title>Atom entry 1</title>"
    '<link href="https://example.com/atom/1"/>'
    "<published>2026-10-06T12:00:00Z</published>"
    "<summary>Atom summary 1</summary>"
    "</entry>"
    "<entry>"
    "<title>Atom entry 2</title>"
    '<link href="https://example.com/atom/2"/>'
    "<updated>2026-10-07T09:00:00Z</updated>"
    "</entry>"
    "</feed>"
).encode("utf-8")

_RSS2_MISSING_TITLE: bytes = (
    "<rss version=\"2.0\"><channel>"
    "<item><link>https://example.com/no-title</link></item>"
    "</channel></rss>"
).encode("utf-8")

_RSS2_MISSING_LINK: bytes = (
    "<rss version=\"2.0\"><channel>"
    "<item><title>Title only</title></item>"
    "</channel></rss>"
).encode("utf-8")

_RSS2_EMPTY: bytes = b"<rss version=\"2.0\"><channel></channel></rss>"

_INVALID_XML: bytes = b"this is not xml <broken"

_FEED_URL = "https://example.com/feed.rss"
_ROBOTS_URL = "https://example.com/robots.txt"


# ── parse_rss_xml のテスト ─────────────────────────────────────────────────────

class TestParseRssXml:
    def test_parse_valid_rss2_returns_two_entries(self) -> None:
        result = parse_rss_xml(_RSS2_XML)

        assert len(result) == 2

    def test_rss2_entry_has_correct_title(self) -> None:
        result = parse_rss_xml(_RSS2_XML)

        # Unicode: 記事タイトル1
        assert result[0].title == "記事タイトル1"

    def test_rss2_entry_has_correct_source_url(self) -> None:
        result = parse_rss_xml(_RSS2_XML)

        assert result[0].source_url == "https://example.com/article/1"

    def test_rss2_entry_has_parsed_published_at(self) -> None:
        result = parse_rss_xml(_RSS2_XML)

        assert result[0].published_at is not None
        assert result[0].published_at.tzinfo == timezone.utc

    def test_rss2_entry_has_description_as_summary(self) -> None:
        result = parse_rss_xml(_RSS2_XML)

        # Unicode: 記事の説明1
        assert result[0].summary == "記事の説明1"

    def test_rss2_second_entry_without_description_has_none_summary(self) -> None:
        result = parse_rss_xml(_RSS2_XML)

        # 2件目は description 要素なし → None
        assert result[1].summary is None

    def test_parse_valid_atom_returns_two_entries(self) -> None:
        result = parse_rss_xml(_ATOM_XML)

        assert len(result) == 2

    def test_atom_entry_has_correct_title(self) -> None:
        result = parse_rss_xml(_ATOM_XML)

        assert result[0].title == "Atom entry 1"

    def test_atom_entry_has_href_as_source_url(self) -> None:
        result = parse_rss_xml(_ATOM_XML)

        assert result[0].source_url == "https://example.com/atom/1"

    def test_atom_entry_has_parsed_published_at(self) -> None:
        result = parse_rss_xml(_ATOM_XML)

        assert result[0].published_at is not None
        assert result[0].published_at == datetime(2026, 10, 6, 12, 0, 0, tzinfo=timezone.utc)

    def test_atom_second_entry_uses_updated_when_published_missing(self) -> None:
        result = parse_rss_xml(_ATOM_XML)

        # 2件目は published なく updated あり
        assert result[1].published_at is not None

    def test_atom_entry_has_summary(self) -> None:
        result = parse_rss_xml(_ATOM_XML)

        assert result[0].summary == "Atom summary 1"

    def test_rss2_item_missing_title_is_skipped(self) -> None:
        result = parse_rss_xml(_RSS2_MISSING_TITLE)

        assert result == []

    def test_rss2_item_missing_link_is_skipped(self) -> None:
        result = parse_rss_xml(_RSS2_MISSING_LINK)

        assert result == []

    def test_rss2_empty_channel_returns_empty(self) -> None:
        result = parse_rss_xml(_RSS2_EMPTY)

        assert result == []

    def test_invalid_xml_returns_empty(self) -> None:
        result = parse_rss_xml(_INVALID_XML)

        assert result == []


# ── _parse_date のテスト ───────────────────────────────────────────────────────

class TestParseDate:
    def test_rfc2822_format_parsed_correctly(self) -> None:
        result = _parse_date("Mon, 06 Oct 2026 12:00:00 +0000")

        assert result is not None
        assert result.tzinfo == timezone.utc
        assert result.year == 2026
        assert result.month == 10
        assert result.day == 6

    def test_iso8601_z_suffix_parsed_correctly(self) -> None:
        result = _parse_date("2026-10-06T12:00:00Z")

        assert result is not None
        assert result == datetime(2026, 10, 6, 12, 0, 0, tzinfo=timezone.utc)

    def test_iso8601_with_offset_parsed_as_utc(self) -> None:
        result = _parse_date("2026-10-07T09:00:00+09:00")

        assert result is not None
        assert result.tzinfo == timezone.utc
        # +09:00 は UTC 00:00
        assert result.hour == 0

    def test_none_input_returns_none(self) -> None:
        result = _parse_date(None)

        assert result is None

    def test_invalid_date_string_returns_none(self) -> None:
        result = _parse_date("not-a-date-string")

        assert result is None


# ── RssFetcher.is_allowed_by_robots のテスト ────────────────────────────────

class TestRssFetcherRobots:
    def test_allowed_by_robots_returns_true(self) -> None:
        with patch("collector.rss_fetcher.RobotFileParser") as mock_cls:
            mock_rp = MagicMock()
            mock_rp.can_fetch.return_value = True
            mock_cls.return_value = mock_rp

            fetcher = RssFetcher()
            result = fetcher.is_allowed_by_robots(_FEED_URL)

        assert result is True
        mock_rp.set_url.assert_called_once()
        mock_rp.read.assert_called_once()

    def test_disallowed_by_robots_returns_false(self) -> None:
        with patch("collector.rss_fetcher.RobotFileParser") as mock_cls:
            mock_rp = MagicMock()
            mock_rp.can_fetch.return_value = False
            mock_cls.return_value = mock_rp

            fetcher = RssFetcher()
            result = fetcher.is_allowed_by_robots(_FEED_URL)

        assert result is False

    def test_robots_fetch_failure_is_fail_open(self) -> None:
        """robots.txt の取得失敗はフェイルオープン（許可）とみなす。"""
        with patch("collector.rss_fetcher.RobotFileParser") as mock_cls:
            mock_rp = MagicMock()
            mock_rp.read.side_effect = OSError("connection refused")
            mock_rp.can_fetch.return_value = True
            mock_cls.return_value = mock_rp

            fetcher = RssFetcher()
            result = fetcher.is_allowed_by_robots(_FEED_URL)

        # フェイルオープンなので True を期待
        assert result is True
        # allow_all = True が設定されているはず
        assert mock_rp.allow_all is True

    def test_robots_cached_per_host(self) -> None:
        """同一ホストの robots.txt は1回だけ取得される。"""
        with patch("collector.rss_fetcher.RobotFileParser") as mock_cls:
            mock_rp = MagicMock()
            mock_rp.can_fetch.return_value = True
            mock_cls.return_value = mock_rp

            fetcher = RssFetcher()
            fetcher.is_allowed_by_robots("https://example.com/feed1.rss")
            fetcher.is_allowed_by_robots("https://example.com/feed2.rss")

        # RobotFileParser は1回だけ生成・read される
        assert mock_cls.call_count == 1
        assert mock_rp.read.call_count == 1

    def test_different_hosts_fetched_separately(self) -> None:
        """異なるホストは別々に robots.txt を取得する。"""
        with patch("collector.rss_fetcher.RobotFileParser") as mock_cls:
            mock_rp = MagicMock()
            mock_rp.can_fetch.return_value = True
            mock_cls.return_value = mock_rp

            fetcher = RssFetcher()
            fetcher.is_allowed_by_robots("https://example.com/feed.rss")
            fetcher.is_allowed_by_robots("https://other.com/feed.rss")

        # 2ホスト分、2回生成される
        assert mock_cls.call_count == 2


# ── RssFetcher.fetch のテスト ────────────────────────────────────────────────

def _make_fetcher_allow_robots(access_interval: float = 0.0) -> RssFetcher:
    """robots.txt を常に許可するモックを注入した RssFetcher を返す。"""
    fetcher = RssFetcher(timeout=5, max_retries=3, access_interval=access_interval)
    fetcher.is_allowed_by_robots = MagicMock(return_value=True)  # type: ignore[assignment]
    return fetcher


class TestRssFetcherFetch:
    @responses_lib.activate
    def test_fetch_rss2_success_returns_entries(self) -> None:
        responses_lib.add(
            responses_lib.GET, _FEED_URL, body=_RSS2_XML, status=200,
        )
        fetcher = _make_fetcher_allow_robots()

        result = fetcher.fetch(_FEED_URL)

        assert len(result) == 2
        assert result[0].source_url == "https://example.com/article/1"

    @responses_lib.activate
    def test_fetch_atom_success_returns_entries(self) -> None:
        responses_lib.add(
            responses_lib.GET, _FEED_URL, body=_ATOM_XML, status=200,
        )
        fetcher = _make_fetcher_allow_robots()

        result = fetcher.fetch(_FEED_URL)

        assert len(result) == 2
        assert result[0].source_url == "https://example.com/atom/1"

    def test_fetch_robots_disallowed_returns_empty_list(self) -> None:
        fetcher = RssFetcher(timeout=5, max_retries=3)
        fetcher.is_allowed_by_robots = MagicMock(return_value=False)  # type: ignore[assignment]

        result = fetcher.fetch(_FEED_URL)

        assert result == []

    @responses_lib.activate
    @patch("collector.rss_fetcher.time.sleep")
    def test_fetch_5xx_retries_and_succeeds(self, mock_sleep: MagicMock) -> None:
        responses_lib.add(responses_lib.GET, _FEED_URL, status=503)
        responses_lib.add(responses_lib.GET, _FEED_URL, body=_RSS2_XML, status=200)
        fetcher = _make_fetcher_allow_robots()

        result = fetcher.fetch(_FEED_URL)

        assert len(result) == 2
        mock_sleep.assert_called()

    @responses_lib.activate
    @patch("collector.rss_fetcher.time.sleep")
    def test_fetch_5xx_max_retries_exceeded_raises_runtime_error(
        self, mock_sleep: MagicMock
    ) -> None:
        responses_lib.add(responses_lib.GET, _FEED_URL, status=500)
        responses_lib.add(responses_lib.GET, _FEED_URL, status=500)
        fetcher = _make_fetcher_allow_robots()
        fetcher._max_retries = 2

        with pytest.raises(RuntimeError):
            fetcher.fetch(_FEED_URL)

    @responses_lib.activate
    @patch("collector.rss_fetcher.time.sleep")
    def test_fetch_429_retries_and_succeeds(self, mock_sleep: MagicMock) -> None:
        responses_lib.add(responses_lib.GET, _FEED_URL, status=429)
        responses_lib.add(responses_lib.GET, _FEED_URL, body=_RSS2_XML, status=200)
        fetcher = _make_fetcher_allow_robots()

        result = fetcher.fetch(_FEED_URL)

        assert len(result) == 2

    @responses_lib.activate
    def test_fetch_4xx_raises_runtime_error_immediately(self) -> None:
        """4xx（429 以外）はリトライせず即座に RuntimeError を送出する。"""
        responses_lib.add(responses_lib.GET, _FEED_URL, status=404)
        fetcher = _make_fetcher_allow_robots()

        with pytest.raises(RuntimeError, match="HTTP error"):
            fetcher.fetch(_FEED_URL)

        # 1回だけリクエストが送られた（リトライなし）
        assert len(responses_lib.calls) == 1

    @responses_lib.activate
    def test_fetch_403_raises_runtime_error_without_retry(self) -> None:
        responses_lib.add(responses_lib.GET, _FEED_URL, status=403)
        fetcher = _make_fetcher_allow_robots()

        with pytest.raises(RuntimeError):
            fetcher.fetch(_FEED_URL)

        assert len(responses_lib.calls) == 1

    @responses_lib.activate
    @patch("collector.rss_fetcher.time.sleep")
    def test_fetch_network_error_retries_and_succeeds(
        self, mock_sleep: MagicMock
    ) -> None:
        responses_lib.add(
            responses_lib.GET,
            _FEED_URL,
            body=requests.exceptions.ConnectionError("network error"),
        )
        responses_lib.add(responses_lib.GET, _FEED_URL, body=_RSS2_XML, status=200)
        fetcher = _make_fetcher_allow_robots()

        result = fetcher.fetch(_FEED_URL)

        assert len(result) == 2

    @responses_lib.activate
    @patch("collector.rss_fetcher.time.sleep")
    def test_fetch_network_error_max_retries_raises(
        self, mock_sleep: MagicMock
    ) -> None:
        responses_lib.add(
            responses_lib.GET,
            _FEED_URL,
            body=requests.exceptions.ConnectionError("err"),
        )
        responses_lib.add(
            responses_lib.GET,
            _FEED_URL,
            body=requests.exceptions.ConnectionError("err"),
        )
        fetcher = _make_fetcher_allow_robots()
        fetcher._max_retries = 2

        with pytest.raises(RuntimeError):
            fetcher.fetch(_FEED_URL)

    @responses_lib.activate
    @patch("collector.rss_fetcher.time.sleep")
    def test_access_interval_sleep_called_on_success(
        self, mock_sleep: MagicMock
    ) -> None:
        responses_lib.add(responses_lib.GET, _FEED_URL, body=_RSS2_XML, status=200)
        fetcher = _make_fetcher_allow_robots(access_interval=2.0)

        fetcher.fetch(_FEED_URL)

        mock_sleep.assert_called_with(2.0)

    @responses_lib.activate
    def test_access_interval_zero_no_extra_sleep(self) -> None:
        responses_lib.add(responses_lib.GET, _FEED_URL, body=_RSS2_XML, status=200)
        fetcher = _make_fetcher_allow_robots(access_interval=0.0)

        # access_interval=0 なのでスリープしないことを確認（例外なしで完了）
        result = fetcher.fetch(_FEED_URL)

        assert len(result) > 0
