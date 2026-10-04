"""collector.news_fetcher のテスト"""
from __future__ import annotations

from datetime import timezone

import pytest
import responses as resp

from collector.news_fetcher import (
    NewsFetcher,
    NewsEntry,
    _decode_html_entities,
    _extract_source_name_from_title,
    _parse_pub_date,
    _strip_html,
)

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

    # ── FEAT-036: HTML 文字参照デコード ─────────────────────────────────────────

    def test_decode_html_entities_nbsp(self) -> None:
        result = _decode_html_entities("text&nbsp;&nbsp;media")
        assert "\xa0" in result or "text" in result
        assert "&nbsp;" not in result

    def test_decode_html_entities_amp(self) -> None:
        result = _decode_html_entities("A &amp; B")
        assert result == "A & B"

    def test_decode_html_entities_lt_gt(self) -> None:
        result = _decode_html_entities("&lt;p&gt;text&lt;/p&gt;")
        assert result == "<p>text</p>"

    def test_decode_html_entities_no_entities(self) -> None:
        result = _decode_html_entities("plain text")
        assert result == "plain text"

    def test_decode_html_entities_empty(self) -> None:
        result = _decode_html_entities("")
        assert result == ""

    # ── FEAT-036: タイトルからの媒体名抽出 ──────────────────────────────────────

    def test_extract_source_name_from_title_normal(self) -> None:
        result = _extract_source_name_from_title("記事タイトル - スポニチ Sponichi Annex")
        assert result == "スポニチ Sponichi Annex"

    def test_extract_source_name_from_title_last_dash(self) -> None:
        # 複数の ` - ` がある場合は最後を使う
        result = _extract_source_name_from_title("KAWAII LAB. Weverse LIVE - CS Kana Itak - Weverse")
        assert result == "Weverse"

    def test_extract_source_name_from_title_no_dash(self) -> None:
        result = _extract_source_name_from_title("ダッシュなし記事")
        assert result is None

    def test_extract_source_name_from_title_empty(self) -> None:
        result = _extract_source_name_from_title("")
        assert result is None


class TestSourceNameExtraction:
    """FEAT-036: NewsFetcher が source_name を抽出するテスト"""

    # RSS に <source> 要素があればそれを使う
    _RSS_WITH_SOURCE = """<?xml version="1.0" encoding="UTF-8"?>
<rss version="2.0">
  <channel>
    <item>
      <title>推しAの記事 - Yahoo!ニュース</title>
      <link>https://example.com/1</link>
      <pubDate>Mon, 14 Jul 2026 09:00:00 GMT</pubDate>
      <source url="https://www.sponichi.co.jp">スポニチアネックス</source>
    </item>
  </channel>
</rss>
""".encode("utf-8")

    # <source> 要素なし → タイトル末尾から抽出
    _RSS_WITHOUT_SOURCE = """<?xml version="1.0" encoding="UTF-8"?>
<rss version="2.0">
  <channel>
    <item>
      <title>推しAの記事 - モデルプレス</title>
      <link>https://example.com/2</link>
      <pubDate>Mon, 14 Jul 2026 09:00:00 GMT</pubDate>
    </item>
  </channel>
</rss>
""".encode("utf-8")

    # タイトルに ` - ` なし → source_name は None
    _RSS_NO_DASH_TITLE = """<?xml version="1.0" encoding="UTF-8"?>
<rss version="2.0">
  <channel>
    <item>
      <title>媒体名なし記事タイトル</title>
      <link>https://example.com/3</link>
      <pubDate>Mon, 14 Jul 2026 09:00:00 GMT</pubDate>
    </item>
  </channel>
</rss>
""".encode("utf-8")

    # description に HTML 文字参照が含まれる Google News 形式
    _RSS_HTML_ENTITIES_DESC = """<?xml version="1.0" encoding="UTF-8"?>
<rss version="2.0">
  <channel>
    <item>
      <title>記事タイトル - スポニチ</title>
      <link>https://example.com/4</link>
      <pubDate>Mon, 14 Jul 2026 09:00:00 GMT</pubDate>
      <description>&lt;a href="https://example.com"&gt;記事タイトル&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font&gt;スポニチ&lt;/font&gt;</description>
    </item>
  </channel>
</rss>
""".encode("utf-8")

    _RSS_URL = "https://news.google.com/rss/search"

    @resp.activate
    def test_source_name_from_source_element(self) -> None:
        resp.add(resp.GET, self._RSS_URL, body=self._RSS_WITH_SOURCE, status=200,
                 content_type="application/rss+xml")
        fetcher = NewsFetcher(timeout=5, max_retries=1)

        entries = fetcher.fetch_for_oshi("推しA")

        assert len(entries) == 1
        # <source> 要素の内容が優先される
        assert entries[0].source_name == "スポニチアネックス"

    @resp.activate
    def test_source_name_from_title_suffix(self) -> None:
        resp.add(resp.GET, self._RSS_URL, body=self._RSS_WITHOUT_SOURCE, status=200,
                 content_type="application/rss+xml")
        fetcher = NewsFetcher(timeout=5, max_retries=1)

        entries = fetcher.fetch_for_oshi("推しA")

        assert len(entries) == 1
        assert entries[0].source_name == "モデルプレス"

    @resp.activate
    def test_source_name_is_none_when_no_dash_in_title(self) -> None:
        resp.add(resp.GET, self._RSS_URL, body=self._RSS_NO_DASH_TITLE, status=200,
                 content_type="application/rss+xml")
        fetcher = NewsFetcher(timeout=5, max_retries=1)

        entries = fetcher.fetch_for_oshi("推しA")

        assert len(entries) == 1
        assert entries[0].source_name is None

    @resp.activate
    def test_description_html_entities_decoded(self) -> None:
        resp.add(resp.GET, self._RSS_URL, body=self._RSS_HTML_ENTITIES_DESC, status=200,
                 content_type="application/rss+xml")
        fetcher = NewsFetcher(timeout=5, max_retries=1)

        entries = fetcher.fetch_for_oshi("推しA")

        assert len(entries) == 1
        # &amp;nbsp; が デコードされて &nbsp; になり、さらに \xa0 になる
        # また &lt;a&gt; タグは除去されて文字列が残る
        summary = entries[0].summary or ""
        assert "&amp;" not in summary
        assert "&nbsp;" not in summary
