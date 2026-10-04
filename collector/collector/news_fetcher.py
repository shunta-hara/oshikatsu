"""
Google News RSS からニュースを取得するモジュール。
収集先の robots.txt が許可する公開フィードのみ使用する。
"""
from __future__ import annotations

import html
import re
import time
import urllib.parse
import xml.etree.ElementTree as ET
from dataclasses import dataclass, field
from datetime import datetime, timezone
from email.utils import parsedate_to_datetime

import requests

from collector.logger import get_logger

logger = get_logger(__name__)

_RSS_BASE_URL = "https://news.google.com/rss/search"
_RETRYABLE_STATUS_CODES = {429, 500, 502, 503, 504}
_SUMMARY_MAX_LENGTH = 500


@dataclass
class NewsEntry:
    """RSS から取得したニュース記事1件"""

    title: str
    source_url: str
    published_at: datetime | None
    summary: str | None
    source_name: str | None = field(default=None)


def _strip_html(text: str) -> str:
    """HTML タグを除去して空白を正規化する。"""
    return re.sub(r"<[^>]+>", "", text).strip()


def _decode_html_entities(text: str) -> str:
    """HTML 文字参照（&nbsp;・&amp; など）をデコードする（FEAT-036）。"""
    return html.unescape(text)


def _extract_source_name_from_title(title: str) -> str | None:
    """
    タイトル末尾の ` - 媒体名` から媒体名を取り出す（FEAT-036）。
    ` - ` が存在しない場合は None を返す。媒体名は最後の ` - ` の後ろ。
    """
    idx = title.rfind(" - ")
    if idx < 0:
        return None
    return title[idx + 3:].strip() or None


def _parse_pub_date(pub_date_str: str) -> datetime | None:
    """RFC 2822 形式の pubDate を UTC datetime に変換する。変換失敗時は None を返す。"""
    try:
        dt = parsedate_to_datetime(pub_date_str)
        return dt.astimezone(timezone.utc)
    except Exception:
        return None


def _backoff(attempt: int) -> float:
    """指数バックオフの待機秒数（上限 60 秒）を返す。"""
    return min(2 ** (attempt - 1), 60)


class NewsFetcher:
    """Google News RSS からニュースを取得するクラス。"""

    def __init__(self, timeout: int = 30, max_retries: int = 3) -> None:
        self._timeout = timeout
        self._max_retries = max_retries
        self._session = requests.Session()

    def fetch_for_oshi(self, oshi_name: str) -> list[NewsEntry]:
        """
        指定した推しの名前でニュースを収集する。

        ネットワーク障害・一時的なサーバーエラー（429/5xx）時は
        指数バックオフで最大 max_retries 回リトライする。

        Returns:
            取得したニュース記事のリスト。取得失敗時は RuntimeError を送出。
        """
        url = self._build_url(oshi_name)
        last_exception: Exception | None = None

        for attempt in range(1, self._max_retries + 1):
            try:
                response = self._session.get(url, timeout=self._timeout)

                if response.status_code in _RETRYABLE_STATUS_CODES:
                    wait = _backoff(attempt)
                    logger.warning(
                        "RSS fetch returned %d for '%s' (attempt %d/%d). Retrying in %.1fs.",
                        response.status_code,
                        oshi_name,
                        attempt,
                        self._max_retries,
                        wait,
                    )
                    if attempt < self._max_retries:
                        time.sleep(wait)
                    continue

                response.raise_for_status()
                return self._parse_rss(response.content, oshi_name)

            except requests.exceptions.RequestException as exc:
                last_exception = exc
                wait = _backoff(attempt)
                logger.warning(
                    "RSS fetch failed for '%s' (attempt %d/%d): %s. Retrying in %.1fs.",
                    oshi_name,
                    attempt,
                    self._max_retries,
                    exc,
                    wait,
                )
                if attempt < self._max_retries:
                    time.sleep(wait)

        raise RuntimeError(
            f"Failed to fetch RSS for '{oshi_name}' after {self._max_retries} attempts."
        ) from last_exception

    @staticmethod
    def _build_url(oshi_name: str) -> str:
        """Google News RSS の検索 URL を構築する。"""
        params = urllib.parse.urlencode(
            {"q": oshi_name, "hl": "ja", "gl": "JP", "ceid": "JP:ja"}
        )
        return f"{_RSS_BASE_URL}?{params}"

    @staticmethod
    def _parse_rss(content: bytes, oshi_name: str) -> list[NewsEntry]:
        """RSS XML を解析してニュース記事のリストを返す。"""
        entries: list[NewsEntry] = []

        try:
            root = ET.fromstring(content)
        except ET.ParseError as exc:
            logger.error("Failed to parse RSS XML for '%s': %s", oshi_name, exc)
            return entries

        for item in root.findall(".//item"):
            title_el = item.find("title")
            link_el = item.find("link")
            pub_date_el = item.find("pubDate")
            desc_el = item.find("description")
            source_el = item.find("source")

            title = title_el.text if title_el is not None and title_el.text else None
            link = link_el.text if link_el is not None and link_el.text else None

            if not title or not link:
                logger.debug("Skipping RSS item: missing title or link.")
                continue

            published_at: datetime | None = None
            if pub_date_el is not None and pub_date_el.text:
                published_at = _parse_pub_date(pub_date_el.text.strip())

            # FEAT-036: description の HTML タグ除去 + HTML 文字参照デコード
            summary: str | None = None
            if desc_el is not None and desc_el.text:
                raw = _decode_html_entities(_strip_html(desc_el.text))
                summary = raw[:_SUMMARY_MAX_LENGTH] if raw else None

            # FEAT-036: 媒体名は <source> 要素を優先し、なければタイトル末尾 ` - 媒体名` から取得
            source_name: str | None = None
            if source_el is not None and source_el.text:
                source_name = source_el.text.strip() or None
            if not source_name:
                source_name = _extract_source_name_from_title(title)

            entries.append(
                NewsEntry(
                    title=title,
                    source_url=link,
                    published_at=published_at,
                    summary=summary,
                    source_name=source_name,
                )
            )

        return entries
