"""
YouTube Data API v3 を使って YouTube チャンネルの最新動画を収集するモジュール。

API キーが未設定の場合は収集をスキップしてログに記録する。
無料クォータ（10,000 ユニット/日）を超えないよう、
1サイクルあたりの API 呼び出し数を記録してログに出力する。
"""
from __future__ import annotations

import time
from dataclasses import dataclass
from datetime import datetime, timezone
from typing import Any

import requests

from collector.client import BackendClient, NewsBatchItem, TalentSourceInfo
from collector.logger import get_logger

logger = get_logger(__name__)

# YouTube Data API v3 のエンドポイント
_YOUTUBE_SEARCH_URL = "https://www.googleapis.com/youtube/v3/search"

# 1チャンネルあたり取得する動画数（search.list は 100 ユニット/呼び出し）
_MAX_RESULTS_PER_CHANNEL = 10

# リトライ対象の HTTP ステータスコード（レート制限・一時的なエラー）
_RETRYABLE_STATUS_CODES = {429, 500, 502, 503, 504}

# ソースタイプ定数
SOURCE_TYPE_YOUTUBE = "youtube"


@dataclass
class YouTubeVideoEntry:
    """YouTube から取得した動画1件"""

    title: str
    source_url: str
    published_at: datetime | None
    channel_title: str | None


def _backoff(attempt: int) -> float:
    """指数バックオフの待機秒数（上限 60 秒）を返す。"""
    return min(2 ** (attempt - 1), 60)


class YouTubeFetcher:
    """YouTube Data API v3 から動画リストを取得するクラス。"""

    def __init__(
        self,
        api_key: str,
        timeout: int = 30,
        max_retries: int = 3,
    ) -> None:
        self._api_key = api_key
        self._timeout = timeout
        self._max_retries = max_retries
        self._session = requests.Session()
        self._api_call_count = 0

    @property
    def api_call_count(self) -> int:
        """このインスタンスが行った API 呼び出し回数。"""
        return self._api_call_count

    def fetch_channel_videos(
        self,
        channel_id: str,
        max_results: int = _MAX_RESULTS_PER_CHANNEL,
    ) -> list[YouTubeVideoEntry]:
        """
        指定チャンネルの最新動画リストを取得する。

        ネットワーク障害・一時的なサーバーエラー（429/5xx）時は
        指数バックオフで最大 max_retries 回リトライする。

        Returns:
            YouTubeVideoEntry のリスト。取得失敗時は RuntimeError を送出。
        """
        params: dict[str, Any] = {
            "key": self._api_key,
            "channelId": channel_id,
            "part": "snippet",
            "order": "date",
            "type": "video",
            "maxResults": max_results,
        }

        last_exception: Exception | None = None

        for attempt in range(1, self._max_retries + 1):
            try:
                response = self._session.get(
                    _YOUTUBE_SEARCH_URL, params=params, timeout=self._timeout
                )
                self._api_call_count += 1

                if response.status_code in _RETRYABLE_STATUS_CODES:
                    wait = _backoff(attempt)
                    logger.warning(
                        "YouTube API returned %d for channel '%s' (attempt %d/%d). Retrying in %.1fs.",
                        response.status_code,
                        channel_id,
                        attempt,
                        self._max_retries,
                        wait,
                    )
                    if attempt < self._max_retries:
                        time.sleep(wait)
                    continue

                response.raise_for_status()
                return self._parse_response(response.json(), channel_id)

            except requests.exceptions.RequestException as exc:
                last_exception = exc
                wait = _backoff(attempt)
                logger.warning(
                    "YouTube API failed for channel '%s' (attempt %d/%d): %s. Retrying in %.1fs.",
                    channel_id,
                    attempt,
                    self._max_retries,
                    exc,
                    wait,
                )
                if attempt < self._max_retries:
                    time.sleep(wait)

        raise RuntimeError(
            f"YouTube API failed for channel '{channel_id}' after {self._max_retries} attempts."
        ) from last_exception

    @staticmethod
    def _parse_response(data: Any, channel_id: str) -> list[YouTubeVideoEntry]:
        """YouTube API レスポンス JSON を解析して動画エントリのリストを返す。"""
        entries: list[YouTubeVideoEntry] = []

        if not isinstance(data, dict):
            logger.error("YouTube API: unexpected response format for channel '%s'", channel_id)
            return entries

        items = data.get("items", [])
        if not isinstance(items, list):
            logger.warning("YouTube API: no items in response for channel '%s'", channel_id)
            return entries

        for item in items:
            if not isinstance(item, dict):
                continue

            snippet = item.get("snippet", {})
            video_id_obj = item.get("id", {})

            if not isinstance(snippet, dict) or not isinstance(video_id_obj, dict):
                continue

            video_id = video_id_obj.get("videoId")
            title = snippet.get("title")

            if not video_id or not title:
                logger.debug("YouTube API: skipping item missing videoId or title")
                continue

            source_url = f"https://www.youtube.com/watch?v={video_id}"
            channel_title = snippet.get("channelTitle")

            # publishedAt は RFC 3339 形式（例: "2026-10-01T12:00:00Z"）
            published_at: datetime | None = None
            published_at_str = snippet.get("publishedAt")
            if published_at_str and isinstance(published_at_str, str):
                try:
                    published_at = datetime.fromisoformat(
                        published_at_str.replace("Z", "+00:00")
                    ).astimezone(timezone.utc)
                except (ValueError, TypeError):
                    logger.warning(
                        "YouTube API: failed to parse publishedAt '%s' for video '%s'",
                        published_at_str,
                        video_id,
                    )

            entries.append(
                YouTubeVideoEntry(
                    title=str(title),
                    source_url=source_url,
                    published_at=published_at,
                    channel_title=str(channel_title) if channel_title else None,
                )
            )

        return entries


def run_youtube_collection(
    client: BackendClient,
    fetcher: YouTubeFetcher,
    talent_sources: list[TalentSourceInfo],
) -> None:
    """
    YouTube チャンネルの動画を1サイクル収集して backend に一括送信する。

    収集成功・失敗・件数をログ出力する。
    API 呼び出し回数（クォータ消費）をログに出力する。
    ネットワークエラー時は YouTubeFetcher 内部でリトライ済み。
    """
    youtube_sources = [s for s in talent_sources if s.source_type == SOURCE_TYPE_YOUTUBE]

    if not youtube_sources:
        logger.info("YouTube ソースが 0 件です。YouTube 収集をスキップします。")
        return

    logger.info("YouTube 収集バッチ開始（チャンネル数: %d）", len(youtube_sources))

    total_saved = 0
    total_skipped = 0
    total_failed = 0

    for source in youtube_sources:
        channel_id = source.source_id
        talent_id = source.talent_id
        logger.info(
            "YouTube 収集開始: talent_id=%d, channel_id=%s",
            talent_id,
            channel_id,
        )

        try:
            entries = fetcher.fetch_channel_videos(channel_id)
        except Exception as exc:
            logger.error(
                "YouTube 収集失敗: talent_id=%d, channel_id=%s: %s",
                talent_id,
                channel_id,
                exc,
            )
            total_failed += 1
            continue

        logger.info(
            "YouTube 取得動画数: %d (talent_id=%d, channel_id=%s)",
            len(entries),
            talent_id,
            channel_id,
        )

        if not entries:
            continue

        collected_at = datetime.now(timezone.utc)
        batch_items = [
            NewsBatchItem(
                title=entry.title,
                source_url=entry.source_url,
                collected_at=collected_at,
                summary=None,
                published_at=entry.published_at,
                source_name=entry.channel_title,
                source_type=SOURCE_TYPE_YOUTUBE,
            )
            for entry in entries
        ]

        try:
            result = client.post_news_batch(
                talent_id=talent_id,
                items=batch_items,
            )
            total_saved += result.saved_count
            total_skipped += result.skipped_count
            logger.info(
                "YouTube 送信完了: talent_id=%d - 受信: %d件, 新規保存: %d件, 重複スキップ: %d件",
                talent_id,
                result.received_count,
                result.saved_count,
                result.skipped_count,
            )
        except Exception as exc:
            logger.error(
                "YouTube 一括送信失敗: talent_id=%d: %s",
                talent_id,
                exc,
            )
            total_failed += 1
            continue

    logger.info(
        "YouTube 収集バッチ完了 - 新規保存: %d件, 重複スキップ: %d件, 失敗: %d件, API呼び出し回数: %d",
        total_saved,
        total_skipped,
        total_failed,
        fetcher.api_call_count,
    )
