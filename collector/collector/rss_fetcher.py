"""
汎用 RSS/Atom フィードフェッチャー。

robots.txt チェック・タイムアウト・指数バックオフリトライを含む。
外部 HTTP リクエストにはタイムアウトを設定し、
リトライ上限（無限リトライ禁止）を守る。
"""
from __future__ import annotations

import time
import xml.etree.ElementTree as ET
from dataclasses import dataclass
from datetime import datetime, timezone
from email.utils import parsedate_to_datetime
from urllib.parse import urljoin, urlparse
from urllib.robotparser import RobotFileParser

import requests

from collector.logger import get_logger

logger = get_logger(__name__)

# Atom フィード名前空間
_ATOM_NS = "http://www.w3.org/2005/Atom"

# robots.txt 確認で使用する User-Agent
_USER_AGENT = "oshikatsu-collector/1.0"


@dataclass
class RssEntry:
    """RSS/Atom フィードから取得した1件のエントリ"""

    title: str
    source_url: str
    published_at: datetime | None
    summary: str | None


def _backoff(attempt: int) -> float:
    """指数バックオフの待機秒数（上限 60 秒）を返す。"""
    return min(2 ** (attempt - 1), 60)


def _parse_date(date_str: str | None) -> datetime | None:
    """
    RSS の pubDate（RFC 2822）または Atom の published/updated（ISO 8601）を解析する。

    解析失敗時は None を返す。
    """
    if not date_str:
        return None
    # RFC 2822 形式（RSS 2.0 pubDate）: "Mon, 06 Oct 2026 12:00:00 +0000"
    try:
        dt = parsedate_to_datetime(date_str)
        return dt.astimezone(timezone.utc)
    except Exception:
        pass
    # ISO 8601 形式（Atom published/updated）: "2026-10-06T12:00:00Z"
    try:
        return datetime.fromisoformat(
            date_str.replace("Z", "+00:00")
        ).astimezone(timezone.utc)
    except Exception:
        pass
    logger.debug("RSS/Atom: 日付パース失敗 '%s'", date_str)
    return None


def _parse_rss2(root: ET.Element) -> list[RssEntry]:
    """RSS 2.0 フォーマットをパースして RssEntry のリストを返す。"""
    entries: list[RssEntry] = []
    channel = root.find("channel")
    if channel is None:
        return entries

    for item in channel.findall("item"):
        title_el = item.find("title")
        link_el = item.find("link")
        pub_date_el = item.find("pubDate")
        desc_el = item.find("description")

        title = title_el.text if title_el is not None and title_el.text else None
        link = link_el.text if link_el is not None and link_el.text else None

        if not title or not link:
            logger.debug("RSS2: title または link がないアイテムをスキップします")
            continue

        entries.append(
            RssEntry(
                title=title.strip(),
                source_url=link.strip(),
                published_at=_parse_date(
                    pub_date_el.text if pub_date_el is not None else None
                ),
                summary=(desc_el.text or "").strip() if desc_el is not None else None,
            )
        )
    return entries


def _parse_atom(root: ET.Element) -> list[RssEntry]:
    """Atom フォーマットをパースして RssEntry のリストを返す。"""
    entries: list[RssEntry] = []
    ns = {"atom": _ATOM_NS}

    for entry in root.findall("atom:entry", ns):
        title_el = entry.find("atom:title", ns)
        link_el = entry.find("atom:link", ns)
        published_el = entry.find("atom:published", ns)
        if published_el is None:
            published_el = entry.find("atom:updated", ns)
        summary_el = entry.find("atom:summary", ns)
        content_el = entry.find("atom:content", ns)

        title = title_el.text if title_el is not None and title_el.text else None

        # link は href 属性から取得する（最初の <link> 要素を使用）
        link: str | None = None
        if link_el is not None:
            link = link_el.get("href")

        if not title or not link:
            logger.debug("Atom: title または link がないエントリをスキップします")
            continue

        summary_text: str | None = None
        if summary_el is not None and summary_el.text:
            summary_text = summary_el.text.strip()
        elif content_el is not None and content_el.text:
            summary_text = content_el.text.strip()

        entries.append(
            RssEntry(
                title=title.strip(),
                source_url=link.strip(),
                published_at=_parse_date(
                    published_el.text if published_el is not None else None
                ),
                summary=summary_text,
            )
        )
    return entries


def parse_rss_xml(content: bytes) -> list[RssEntry]:
    """
    XML バイト列を RSS 2.0 または Atom としてパースして RssEntry のリストを返す。

    パースエラー時は空リストを返す。
    """
    try:
        root = ET.fromstring(content)
    except ET.ParseError as exc:
        logger.error("RSS/Atom: XML パースエラー: %s", exc)
        return []

    # Atom フィードの判定（名前空間付きタグ）
    if _ATOM_NS in root.tag:
        return _parse_atom(root)

    # RSS 2.0 / RDF として処理（デフォルト）
    return _parse_rss2(root)


class RssFetcher:
    """
    RSS/Atom フィードを取得するクラス。

    - robots.txt で禁止されているフィードは取得しない
    - タイムアウト・指数バックオフ付きリトライ（最大 max_retries 回）
    - 429 またはすべての 5xx はリトライ対象
    - その他の 4xx は即座に RuntimeError を送出
    - access_interval（秒）: 成功取得後のアクセス間隔（礼儀あるクロール用）
    """

    def __init__(
        self,
        timeout: int = 30,
        max_retries: int = 3,
        access_interval: float = 1.0,
    ) -> None:
        self._timeout = timeout
        self._max_retries = max_retries
        self._access_interval = access_interval
        self._session = requests.Session()
        self._session.headers.update({"User-Agent": _USER_AGENT})
        # robots.txt キャッシュ（スキームとホストをキーとする）
        self._robots_cache: dict[str, RobotFileParser] = {}

    def is_allowed_by_robots(self, url: str) -> bool:
        """
        robots.txt で対象 URL への取得が許可されているか確認する。

        - robots.txt の取得・解析に失敗した場合はフェイルオープン（許可）とみなす
        - 結果はホスト単位でキャッシュする（同一ホストの複数フィードで1回のみ取得）
        """
        parsed = urlparse(url)
        host_key = f"{parsed.scheme}://{parsed.netloc}"

        if host_key not in self._robots_cache:
            robots_url = urljoin(host_key, "/robots.txt")
            rp = RobotFileParser()
            rp.set_url(robots_url)
            try:
                rp.read()
            except Exception as exc:
                logger.warning(
                    "robots.txt 取得失敗 (%s): %s。取得を許可します（フェイルオープン）。",
                    robots_url,
                    exc,
                )
                # フェイルオープン: 許可とみなす
                rp.allow_all = True
            self._robots_cache[host_key] = rp

        return self._robots_cache[host_key].can_fetch(_USER_AGENT, url)

    def fetch(self, url: str) -> list[RssEntry]:
        """
        指定 URL の RSS/Atom フィードを取得してパースする。

        - robots.txt で禁止されている場合は空リストを返す（例外を送出しない）
        - 429 または 5xx はリトライする（最大 max_retries 回）
        - その他の HTTP エラー（4xx 等）は即座に RuntimeError を送出
        - ネットワークエラーはリトライする
        - 成功後に access_interval 秒だけスリープする

        Raises:
            RuntimeError: 4xx エラーまたは最大リトライ超過時
        """
        if not self.is_allowed_by_robots(url):
            logger.warning("robots.txt により取得禁止: %s", url)
            return []

        last_exception: Exception | None = None

        for attempt in range(1, self._max_retries + 1):
            try:
                response = self._session.get(url, timeout=self._timeout)

                # リトライ対象: 429 またはすべての 5xx
                if response.status_code == 429 or (
                    500 <= response.status_code < 600
                ):
                    wait = _backoff(attempt)
                    logger.warning(
                        "RSS フィード: %d 返却 URL=%s (試行 %d/%d)。%.1fs 後にリトライ。",
                        response.status_code,
                        url,
                        attempt,
                        self._max_retries,
                        wait,
                    )
                    if attempt < self._max_retries:
                        time.sleep(wait)
                    continue

                # 4xx 等の非リトライエラーは即座に例外送出
                response.raise_for_status()

                entries = parse_rss_xml(response.content)

                # アクセス間隔（礼儀あるクロール）
                if self._access_interval > 0:
                    time.sleep(self._access_interval)

                return entries

            except requests.exceptions.HTTPError as exc:
                # raise_for_status() による 4xx 等: リトライせず即座に失敗
                raise RuntimeError(
                    f"RSS fetch HTTP error for '{url}': {exc}"
                ) from exc

            except requests.exceptions.RequestException as exc:
                last_exception = exc
                wait = _backoff(attempt)
                logger.warning(
                    "RSS フィード取得失敗 URL=%s (試行 %d/%d): %s。%.1fs 後にリトライ。",
                    url,
                    attempt,
                    self._max_retries,
                    exc,
                    wait,
                )
                if attempt < self._max_retries:
                    time.sleep(wait)

        raise RuntimeError(
            f"RSS fetch failed for '{url}' after {self._max_retries} attempts."
        ) from last_exception
