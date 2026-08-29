"""collector.program_fetcher のテスト"""
from __future__ import annotations

from datetime import timezone

import pytest
import responses as resp

from collector.program_fetcher import (
    ProgramEntry,
    ProgramFetcher,
    _backoff,
    _parse_pub_date,
)

# テスト用推し名
_OSHI_NAME = "test-oshi"

# ProgramFetcher が構築する RSS URL（クエリパラメータを一致させる）
_RSS_URL = "https://news.google.com/rss/search"

# テスト用サンプル RSS
_SAMPLE_RSS = """<?xml version="1.0" encoding="UTF-8"?>
<rss version="2.0">
  <channel>
    <title>Google News</title>
    <link>https://news.google.com</link>
    <item>
      <title>test-oshi が音楽番組「ミュージックステーション」に出演 - 芸能ニュース</title>
      <link>https://news.google.com/rss/articles/prog001</link>
      <pubDate>Mon, 14 Jul 2026 18:00:00 GMT</pubDate>
    </item>
    <item>
      <title>test-oshi バラエティ番組「笑っていいとも」に出演決定 - スポーツ報知</title>
      <link>https://news.google.com/rss/articles/prog002</link>
      <pubDate>Mon, 14 Jul 2026 19:00:00 GMT</pubDate>
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
        fetcher = ProgramFetcher(timeout=5, max_retries=3)

        entries = fetcher.fetch_for_oshi(_OSHI_NAME)

        assert len(entries) == 2
        assert all(isinstance(e, ProgramEntry) for e in entries)

    @resp.activate
    def test_fetch_for_oshi_parses_program_name(self) -> None:
        resp.add(resp.GET, _RSS_URL, body=_SAMPLE_RSS, status=200,
                 content_type="application/rss+xml")
        fetcher = ProgramFetcher(timeout=5, max_retries=3)

        entries = fetcher.fetch_for_oshi(_OSHI_NAME)

        assert "ミュージックステーション" in entries[0].program_name

    @resp.activate
    def test_fetch_for_oshi_parses_source_url(self) -> None:
        resp.add(resp.GET, _RSS_URL, body=_SAMPLE_RSS, status=200,
                 content_type="application/rss+xml")
        fetcher = ProgramFetcher(timeout=5, max_retries=3)

        entries = fetcher.fetch_for_oshi(_OSHI_NAME)

        assert entries[0].source_url == "https://news.google.com/rss/articles/prog001"

    @resp.activate
    def test_fetch_for_oshi_parses_aired_at(self) -> None:
        resp.add(resp.GET, _RSS_URL, body=_SAMPLE_RSS, status=200,
                 content_type="application/rss+xml")
        fetcher = ProgramFetcher(timeout=5, max_retries=3)

        entries = fetcher.fetch_for_oshi(_OSHI_NAME)

        assert entries[0].aired_at is not None
        assert entries[0].aired_at.tzinfo == timezone.utc
        assert entries[0].aired_at.year == 2026

    @resp.activate
    def test_fetch_for_oshi_broadcaster_is_none(self) -> None:
        """broadcaster は RSS から抽出できないため None になる。"""
        resp.add(resp.GET, _RSS_URL, body=_SAMPLE_RSS, status=200,
                 content_type="application/rss+xml")
        fetcher = ProgramFetcher(timeout=5, max_retries=3)

        entries = fetcher.fetch_for_oshi(_OSHI_NAME)

        assert entries[0].broadcaster is None

    # ── 境界値 ────────────────────────────────────────────────────────────────

    @resp.activate
    def test_fetch_for_oshi_empty_feed_returns_empty_list(self) -> None:
        resp.add(resp.GET, _RSS_URL, body=_EMPTY_RSS, status=200,
                 content_type="application/rss+xml")
        fetcher = ProgramFetcher(timeout=5, max_retries=3)

        entries = fetcher.fetch_for_oshi(_OSHI_NAME)

        assert entries == []

    @resp.activate
    def test_fetch_for_oshi_malformed_xml_returns_empty_list(self) -> None:
        resp.add(resp.GET, _RSS_URL, body=_MALFORMED_XML, status=200,
                 content_type="application/rss+xml")
        fetcher = ProgramFetcher(timeout=5, max_retries=3)

        entries = fetcher.fetch_for_oshi(_OSHI_NAME)

        assert entries == []

    @resp.activate
    def test_fetch_for_oshi_item_without_link_is_skipped(self) -> None:
        rss_no_link = """<?xml version="1.0" encoding="UTF-8"?>
<rss version="2.0">
  <channel>
    <item>
      <title>リンクなし番組</title>
    </item>
    <item>
      <title>正常な番組出演情報</title>
      <link>https://news.google.com/rss/articles/valid</link>
    </item>
  </channel>
</rss>
""".encode("utf-8")
        resp.add(resp.GET, _RSS_URL, body=rss_no_link, status=200,
                 content_type="application/rss+xml")
        fetcher = ProgramFetcher(timeout=5, max_retries=3)

        entries = fetcher.fetch_for_oshi(_OSHI_NAME)

        assert len(entries) == 1
        assert entries[0].program_name == "正常な番組出演情報"

    @resp.activate
    def test_fetch_for_oshi_item_without_pubdate_has_none_aired_at(self) -> None:
        rss_no_date = """<?xml version="1.0" encoding="UTF-8"?>
<rss version="2.0">
  <channel>
    <item>
      <title>日時なし番組</title>
      <link>https://news.google.com/rss/articles/nodate</link>
    </item>
  </channel>
</rss>
""".encode("utf-8")
        resp.add(resp.GET, _RSS_URL, body=rss_no_date, status=200,
                 content_type="application/rss+xml")
        fetcher = ProgramFetcher(timeout=5, max_retries=3)

        entries = fetcher.fetch_for_oshi(_OSHI_NAME)

        assert len(entries) == 1
        assert entries[0].aired_at is None

    @resp.activate
    def test_fetch_for_oshi_program_name_truncated_at_200_chars(self) -> None:
        long_title = "a" * 250
        rss_long_title = (
            '<?xml version="1.0" encoding="UTF-8"?>'
            '<rss version="2.0"><channel><item>'
            f"<title>{long_title}</title>"
            "<link>https://news.google.com/rss/articles/long</link>"
            "</item></channel></rss>"
        ).encode("utf-8")
        resp.add(resp.GET, _RSS_URL, body=rss_long_title, status=200,
                 content_type="application/rss+xml")
        fetcher = ProgramFetcher(timeout=5, max_retries=3)

        entries = fetcher.fetch_for_oshi(_OSHI_NAME)

        assert len(entries) == 1
        assert entries[0].program_name is not None
        assert len(entries[0].program_name) == 200

    # ── 異常系・リトライ ──────────────────────────────────────────────────────

    @resp.activate
    def test_fetch_for_oshi_retries_on_500(self) -> None:
        resp.add(resp.GET, _RSS_URL, body=b"", status=500)
        resp.add(resp.GET, _RSS_URL, body=_SAMPLE_RSS, status=200,
                 content_type="application/rss+xml")
        fetcher = ProgramFetcher(timeout=5, max_retries=3)

        import unittest.mock as mock
        with mock.patch("collector.program_fetcher.time.sleep"):
            entries = fetcher.fetch_for_oshi(_OSHI_NAME)

        assert len(entries) == 2
        assert len(resp.calls) == 2

    @resp.activate
    def test_fetch_for_oshi_retries_on_429(self) -> None:
        resp.add(resp.GET, _RSS_URL, body=b"", status=429)
        resp.add(resp.GET, _RSS_URL, body=_SAMPLE_RSS, status=200,
                 content_type="application/rss+xml")
        fetcher = ProgramFetcher(timeout=5, max_retries=3)

        import unittest.mock as mock
        with mock.patch("collector.program_fetcher.time.sleep"):
            entries = fetcher.fetch_for_oshi(_OSHI_NAME)

        assert len(entries) == 2

    @resp.activate
    def test_fetch_for_oshi_raises_after_max_retries(self) -> None:
        for _ in range(3):
            resp.add(resp.GET, _RSS_URL, body=b"", status=500)
        fetcher = ProgramFetcher(timeout=5, max_retries=3)

        import unittest.mock as mock
        with mock.patch("collector.program_fetcher.time.sleep"):
            with pytest.raises(RuntimeError, match="Failed to fetch program RSS"):
                fetcher.fetch_for_oshi(_OSHI_NAME)

    @resp.activate
    def test_fetch_for_oshi_raises_on_network_error(self) -> None:
        import requests as requests_lib
        resp.add(resp.GET, _RSS_URL,
                 body=requests_lib.exceptions.ConnectionError("Connection refused"))
        fetcher = ProgramFetcher(timeout=5, max_retries=1)

        with pytest.raises(RuntimeError, match="Failed to fetch program RSS"):
            fetcher.fetch_for_oshi(_OSHI_NAME)


class TestHelpers:
    def test_parse_pub_date_valid_rfc2822(self) -> None:
        result = _parse_pub_date("Mon, 14 Jul 2026 18:00:00 GMT")
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

    def test_backoff_attempt1_returns_1(self) -> None:
        assert _backoff(1) == 1

    def test_backoff_attempt2_returns_2(self) -> None:
        assert _backoff(2) == 2

    def test_backoff_attempt3_returns_4(self) -> None:
        assert _backoff(3) == 4

    def test_backoff_large_attempt_capped_at_60(self) -> None:
        assert _backoff(100) == 60
