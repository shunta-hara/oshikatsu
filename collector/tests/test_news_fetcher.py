"""collector.news_fetcher のテスト"""
from __future__ import annotations

from datetime import timezone

import pytest
import responses as resp

from collector.news_fetcher import NewsFetcher, NewsEntry, _parse_pub_date, _strip_html

# テスト用推し名
_OSHI_NAME = "test-oshi"

# NewsFetcher が構築する RSS URL（クエリパラメータを一致させる）
_RSS_URL = "https://news.google.com/rss/search"

# テスト用サンプル RSS（ASCII テキストで記述し encode で bytes に変換）
_SAMPLE_RSS = """<?xml version="1.0" encoding="UTF-8"?>
<rss version="2.0">
  <channel>
    <title>Google News</title>
    <link>https://news.google.com</link>
    <item>
      <title>Test Oshi Latest News - Test Media</title>
      <link>https://news.google.com/rss/articles/abc123</link>
      <pubDate>Mon, 14 Jul 2026 09:00:00 GMT</pubDate>
      <description>&lt;p&gt;Test summary text&lt;/p&gt;</description>
    </item>
    <item>
      <title>Test Oshi Second Article - Another Media</title>
      <link>https://news.google.com/rss/articles/def456</link>
      <pubDate>Mon, 14 Jul 2026 10:00:00 GMT</pubDate>
      <description>&lt;ol&gt;&lt;li&gt;Summary 1&lt;/li&gt;&lt;/ol&gt;</description>
    </item>
  </channel>
</rss>
""".encode("utf-8")

_EMPTY_RSS = """<?xml version="1.0" encoding="UTF-8"?>
<rss version="2.0">
  <channel>
    <title>Google News</title>
  </channel>
</rss>
""".encode("utf-8")

_MALFORMED_XML = b"<rss><not-closed"


class TestFetchForOshi:
    # ── 正常系 ────────────────────────────────────────────────────────────────

    @resp.activate
    def test_fetch_for_oshi_returns_entries(self) -> None:
        resp.add(resp.GET, _RSS_URL, body=_SAMPLE_RSS, status=200,
                 content_type="application/rss+xml")
        fetcher = NewsFetcher(timeout=5, max_retries=3)

        entries = fetcher.fetch_for_oshi(_OSHI_NAME)

        assert len(entries) == 2
        assert all(isinstance(e, NewsEntry) for e in entries)

    @resp.activate
    def test_fetch_for_oshi_parses_title(self) -> None:
        resp.add(resp.GET, _RSS_URL, body=_SAMPLE_RSS, status=200,
                 content_type="application/rss+xml")
        fetcher = NewsFetcher(timeout=5, max_retries=3)

        entries = fetcher.fetch_for_oshi(_OSHI_NAME)

        assert entries[0].title == "Test Oshi Latest News - Test Media"

    @resp.activate
    def test_fetch_for_oshi_parses_source_url(self) -> None:
        resp.add(resp.GET, _RSS_URL, body=_SAMPLE_RSS, status=200,
                 content_type="application/rss+xml")
        fetcher = NewsFetcher(timeout=5, max_retries=3)

        entries = fetcher.fetch_for_oshi(_OSHI_NAME)

        assert entries[0].source_url == "https://news.google.com/rss/articles/abc123"

    @resp.activate
    def test_fetch_for_oshi_parses_published_at(self) -> None:
        resp.add(resp.GET, _RSS_URL, body=_SAMPLE_RSS, status=200,
                 content_type="application/rss+xml")
        fetcher = NewsFetcher(timeout=5, max_retries=3)

        entries = fetcher.fetch_for_oshi(_OSHI_NAME)

        assert entries[0].published_at is not None
        assert entries[0].published_at.tzinfo == timezone.utc
        assert entries[0].published_at.year == 2026

    @resp.activate
    def test_fetch_for_oshi_strips_html_from_summary(self) -> None:
        resp.add(resp.GET, _RSS_URL, body=_SAMPLE_RSS, status=200,
                 content_type="application/rss+xml")
        fetcher = NewsFetcher(timeout=5, max_retries=3)

        entries = fetcher.fetch_for_oshi(_OSHI_NAME)

        # HTML タグが除去されていること
        assert "<p>" not in (entries[0].summary or "")
        assert "Test summary text" in (entries[0].summary or "")

    # ── 境界値 ────────────────────────────────────────────────────────────────

    @resp.activate
    def test_fetch_for_oshi_empty_feed_returns_empty_list(self) -> None:
        resp.add(resp.GET, _RSS_URL, body=_EMPTY_RSS, status=200,
                 content_type="application/rss+xml")
        fetcher = NewsFetcher(timeout=5, max_retries=3)

        entries = fetcher.fetch_for_oshi(_OSHI_NAME)

        assert entries == []

    @resp.activate
    def test_fetch_for_oshi_malformed_xml_returns_empty_list(self) -> None:
        resp.add(resp.GET, _RSS_URL, body=_MALFORMED_XML, status=200,
                 content_type="application/rss+xml")
        fetcher = NewsFetcher(timeout=5, max_retries=3)

        entries = fetcher.fetch_for_oshi(_OSHI_NAME)

        assert entries == []

    @resp.activate
    def test_fetch_for_oshi_item_without_link_is_skipped(self) -> None:
        rss_no_link = """<?xml version="1.0" encoding="UTF-8"?>
<rss version="2.0">
  <channel>
    <item>
      <title>Title Only</title>
    </item>
    <item>
      <title>Normal Article</title>
      <link>https://news.google.com/rss/articles/valid</link>
    </item>
  </channel>
</rss>
""".encode("utf-8")
        resp.add(resp.GET, _RSS_URL, body=rss_no_link, status=200,
                 content_type="application/rss+xml")
        fetcher = NewsFetcher(timeout=5, max_retries=3)

        entries = fetcher.fetch_for_oshi(_OSHI_NAME)

        assert len(entries) == 1
        assert entries[0].title == "Normal Article"

    @resp.activate
    def test_fetch_for_oshi_item_without_pubdate_has_none_published_at(self) -> None:
        rss_no_date = """<?xml version="1.0" encoding="UTF-8"?>
<rss version="2.0">
  <channel>
    <item>
      <title>No Date Article</title>
      <link>https://news.google.com/rss/articles/nodate</link>
    </item>
  </channel>
</rss>
""".encode("utf-8")
        resp.add(resp.GET, _RSS_URL, body=rss_no_date, status=200,
                 content_type="application/rss+xml")
        fetcher = NewsFetcher(timeout=5, max_retries=3)

        entries = fetcher.fetch_for_oshi(_OSHI_NAME)

        assert len(entries) == 1
        assert entries[0].published_at is None

    @resp.activate
    def test_fetch_for_oshi_summary_truncated_at_500_chars(self) -> None:
        long_text = "a" * 600
        rss_long_desc = (
            '<?xml version="1.0" encoding="UTF-8"?>'
            '<rss version="2.0"><channel><item>'
            "<title>Long Article</title>"
            "<link>https://news.google.com/rss/articles/long</link>"
            "<description>" + long_text + "</description>"
            "</item></channel></rss>"
        ).encode("utf-8")
        resp.add(resp.GET, _RSS_URL, body=rss_long_desc, status=200,
                 content_type="application/rss+xml")
        fetcher = NewsFetcher(timeout=5, max_retries=3)

        entries = fetcher.fetch_for_oshi(_OSHI_NAME)

        assert len(entries) == 1
        assert entries[0].summary is not None
        assert len(entries[0].summary) == 500

    # ── 異常系・リトライ ──────────────────────────────────────────────────────

    @resp.activate
    def test_fetch_for_oshi_retries_on_500(self) -> None:
        resp.add(resp.GET, _RSS_URL, body=b"", status=500)
        resp.add(resp.GET, _RSS_URL, body=_SAMPLE_RSS, status=200,
                 content_type="application/rss+xml")
        fetcher = NewsFetcher(timeout=5, max_retries=3)

        import unittest.mock as mock
        with mock.patch("collector.news_fetcher.time.sleep"):
            entries = fetcher.fetch_for_oshi(_OSHI_NAME)

        assert len(entries) == 2
        assert len(resp.calls) == 2

    @resp.activate
    def test_fetch_for_oshi_retries_on_429(self) -> None:
        resp.add(resp.GET, _RSS_URL, body=b"", status=429)
        resp.add(resp.GET, _RSS_URL, body=_SAMPLE_RSS, status=200,
                 content_type="application/rss+xml")
        fetcher = NewsFetcher(timeout=5, max_retries=3)

        import unittest.mock as mock
        with mock.patch("collector.news_fetcher.time.sleep"):
            entries = fetcher.fetch_for_oshi(_OSHI_NAME)

        assert len(entries) == 2

    @resp.activate
    def test_fetch_for_oshi_raises_after_max_retries(self) -> None:
        for _ in range(3):
            resp.add(resp.GET, _RSS_URL, body=b"", status=500)
        fetcher = NewsFetcher(timeout=5, max_retries=3)

        import unittest.mock as mock
        with mock.patch("collector.news_fetcher.time.sleep"):
            with pytest.raises(RuntimeError, match="Failed to fetch RSS"):
                fetcher.fetch_for_oshi(_OSHI_NAME)

    @resp.activate
    def test_fetch_for_oshi_raises_on_network_error(self) -> None:
        import requests as requests_lib
        resp.add(resp.GET, _RSS_URL,
                 body=requests_lib.exceptions.ConnectionError("Connection refused"))
        fetcher = NewsFetcher(timeout=5, max_retries=1)

        with pytest.raises(RuntimeError, match="Failed to fetch RSS"):
            fetcher.fetch_for_oshi(_OSHI_NAME)


class TestHelpers:
    def test_strip_html_removes_tags(self) -> None:
        result = _strip_html("<p>text<br/>body</p>")
        assert result == "textbody"

    def test_strip_html_empty_string(self) -> None:
        result = _strip_html("")
        assert result == ""

    def test_parse_pub_date_valid_rfc2822(self) -> None:
        result = _parse_pub_date("Mon, 14 Jul 2026 09:00:00 GMT")
        assert result is not None
        assert result.tzinfo == timezone.utc
        assert result.year == 2026
        assert result.month == 7
        assert result.day == 14

    def test_parse_pub_date_invalid_returns_none(self) -> None:
        result = _parse_pub_date("not-a-date")
        assert result is None

    def test_parse_pub_date_empty_returns_none(self) -> None:
        result = _parse_pub_date("")
        assert result is None
