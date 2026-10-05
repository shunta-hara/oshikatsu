"""
backend 内部 API クライアント。
collector が収集したデータを backend に送信する。
"""
from __future__ import annotations

import time
from dataclasses import dataclass
from datetime import datetime, timezone
from typing import Any

import requests

from collector.config import CollectorConfig
from collector.logger import get_logger

logger = get_logger(__name__)

# リトライ対象の HTTP ステータスコード
_RETRYABLE_STATUS_CODES = {429, 500, 502, 503, 504}


@dataclass
class OshiInfo:
    """タレント情報（backend 内部 API から取得）"""

    talent_id: int
    name: str


@dataclass
class BatchResult:
    """一括登録 API のレスポンス"""

    received_count: int
    saved_count: int
    skipped_count: int


@dataclass
class NewsBatchItem:
    """ニュース一括登録の1アイテム"""

    title: str
    source_url: str
    collected_at: datetime
    summary: str | None = None
    published_at: datetime | None = None
    source_name: str | None = None


@dataclass
class ProgramBatchItem:
    """出演番組一括登録の1アイテム"""

    program_name: str
    source_url: str
    collected_at: datetime
    broadcaster: str | None = None
    aired_at: datetime | None = None
    source_name: str | None = None


class BackendClient:
    """backend 内部 API への HTTP クライアント。"""

    def __init__(self, config: CollectorConfig) -> None:
        self._base_url = config.backend_base_url.rstrip("/")
        self._api_key = config.collector_api_key
        self._timeout = config.request_timeout_seconds
        self._max_retries = config.max_retries
        self._session = requests.Session()
        self._session.headers.update(
            {
                "X-Api-Key": self._api_key,
                "Content-Type": "application/json",
            }
        )

    def get_oshis(self) -> list[OshiInfo]:
        """
        全推し一覧を backend 内部 API から取得する。

        返り値は OshiInfo のリスト。
        不正なデータ形式のアイテムはスキップする。
        """
        raw: Any = self._get_with_retry("/api/v1/internal/oshis")

        if not isinstance(raw, list):
            logger.warning("Unexpected response format from /api/v1/internal/oshis: %r", raw)
            return []

        result: list[OshiInfo] = []
        for item in raw:
            if not isinstance(item, dict):
                logger.warning("Unexpected oshi item format: %r", item)
                continue
            talent_id = item.get("talentId")
            name = item.get("name")
            if not isinstance(talent_id, int) or not isinstance(name, str):
                logger.warning("Malformed talent data (talentId=%r, name=%r), skipping.", talent_id, name)
                continue
            result.append(OshiInfo(talent_id=talent_id, name=name))

        return result

    def post_news_item(
        self,
        *,
        talent_id: int,
        title: str,
        source_url: str,
        collected_at: datetime,
        summary: str | None = None,
        published_at: datetime | None = None,
    ) -> dict[str, Any]:
        """
        ニュースアイテムを backend に送信する（単件 API）。

        published_at が None の場合は backend 側で collected_at で代替され、
        is_published_at_estimated が True になる。
        """
        payload: dict[str, Any] = {
            "talentId": talent_id,
            "title": title,
            "sourceUrl": source_url,
            "collectedAt": _to_iso(collected_at),
        }
        if summary is not None:
            payload["summary"] = summary
        if published_at is not None:
            payload["publishedAt"] = _to_iso(published_at)

        return self._post_with_retry("/api/v1/internal/news", payload)

    def post_news_batch(
        self,
        *,
        talent_id: int,
        items: list[NewsBatchItem],
    ) -> BatchResult:
        """
        ニュースを一括登録する。

        各アイテムを backend の一括登録 API に送信する。
        重複 sourceUrl はサーバー側でスキップされる。
        """
        items_payload: list[dict[str, Any]] = []
        for item in items:
            entry: dict[str, Any] = {
                "title": item.title,
                "sourceUrl": item.source_url,
                "collectedAt": _to_iso(item.collected_at),
            }
            if item.summary is not None:
                entry["summary"] = item.summary
            if item.published_at is not None:
                entry["publishedAt"] = _to_iso(item.published_at)
            if item.source_name is not None:
                entry["sourceName"] = item.source_name
            items_payload.append(entry)

        payload: dict[str, Any] = {"talentId": talent_id, "items": items_payload}
        raw = self._post_with_retry("/api/v1/internal/news/batch", payload)
        return BatchResult(
            received_count=raw.get("receivedCount", 0),
            saved_count=raw.get("savedCount", 0),
            skipped_count=raw.get("skippedCount", 0),
        )

    def post_job_log(
        self,
        *,
        job_type: str,
        executed_at: datetime,
        success_count: int,
        failure_count: int,
    ) -> dict[str, Any]:
        """
        バッチ実行ログを backend 内部 API に送信する。

        job_type は "news" または "program"。
        """
        payload: dict[str, Any] = {
            "jobType": job_type,
            "executedAt": _to_iso(executed_at),
            "successCount": success_count,
            "failureCount": failure_count,
        }
        return self._post_with_retry("/api/v1/internal/job-logs", payload)

    def post_program_appearance(
        self,
        *,
        talent_id: int,
        program_name: str,
        source_url: str,
        collected_at: datetime,
        broadcaster: str | None = None,
        aired_at: datetime | None = None,
    ) -> dict[str, Any]:
        """
        出演番組情報を backend に送信する（単件 API）。

        aired_at が None の場合は backend 側で collected_at で代替され、
        is_aired_at_estimated が True になる。
        """
        payload: dict[str, Any] = {
            "talentId": talent_id,
            "programName": program_name,
            "sourceUrl": source_url,
            "collectedAt": _to_iso(collected_at),
        }
        if broadcaster is not None:
            payload["broadcaster"] = broadcaster
        if aired_at is not None:
            payload["airedAt"] = _to_iso(aired_at)

        return self._post_with_retry("/api/v1/internal/programs", payload)

    def post_program_batch(
        self,
        *,
        talent_id: int,
        items: list[ProgramBatchItem],
    ) -> BatchResult:
        """
        出演番組情報を一括登録する。

        各アイテムを backend の一括登録 API に送信する。
        重複 sourceUrl はサーバー側でスキップされる。
        """
        items_payload: list[dict[str, Any]] = []
        for item in items:
            entry: dict[str, Any] = {
                "programName": item.program_name,
                "sourceUrl": item.source_url,
                "collectedAt": _to_iso(item.collected_at),
            }
            if item.broadcaster is not None:
                entry["broadcaster"] = item.broadcaster
            if item.aired_at is not None:
                entry["airedAt"] = _to_iso(item.aired_at)
            if item.source_name is not None:
                entry["sourceName"] = item.source_name
            items_payload.append(entry)

        payload: dict[str, Any] = {"talentId": talent_id, "items": items_payload}
        raw = self._post_with_retry("/api/v1/internal/programs/batch", payload)
        return BatchResult(
            received_count=raw.get("receivedCount", 0),
            saved_count=raw.get("savedCount", 0),
            skipped_count=raw.get("skippedCount", 0),
        )

    def check_health(self) -> bool:
        """
        /api/v1/health を呼び出し、200 OK なら True を返す。
        エラー・非 200 の場合は False を返す（リトライしない）。
        コールドスタート確認用のヘルスポーリングに使う。
        """
        url = f"{self._base_url}/api/v1/health"
        try:
            response = self._session.get(url, timeout=self._timeout)
            return response.status_code == 200
        except requests.exceptions.RequestException:
            return False

    def _get_with_retry(self, path: str) -> Any:
        """
        指数バックオフ付きリトライで GET を実行する。
        最大 max_retries 回まで一時的な失敗（429/5xx/ネットワークエラー）をリトライする。
        """
        url = f"{self._base_url}{path}"
        last_exception: Exception | None = None

        for attempt in range(1, self._max_retries + 1):
            try:
                response = self._session.get(url, timeout=self._timeout)

                if response.status_code in _RETRYABLE_STATUS_CODES:
                    wait = _backoff(attempt)
                    logger.warning(
                        "GET %s returned %d (attempt %d/%d). Retrying in %.1fs.",
                        path,
                        response.status_code,
                        attempt,
                        self._max_retries,
                        wait,
                    )
                    if attempt < self._max_retries:
                        time.sleep(wait)
                    continue

                response.raise_for_status()
                return response.json()

            except requests.exceptions.RequestException as exc:
                last_exception = exc
                wait = _backoff(attempt)
                logger.warning(
                    "GET %s failed (attempt %d/%d): %s. Retrying in %.1fs.",
                    path,
                    attempt,
                    self._max_retries,
                    exc,
                    wait,
                )
                if attempt < self._max_retries:
                    time.sleep(wait)

        raise RuntimeError(
            f"GET {path} failed after {self._max_retries} attempts."
        ) from last_exception

    def _post_with_retry(self, path: str, payload: dict[str, Any]) -> dict[str, Any]:
        """
        指数バックオフ付きリトライで POST を実行する。
        最大 max_retries 回まで一時的な失敗（429/5xx/ネットワークエラー）をリトライする。
        """
        url = f"{self._base_url}{path}"
        last_exception: Exception | None = None

        for attempt in range(1, self._max_retries + 1):
            try:
                response = self._session.post(
                    url, json=payload, timeout=self._timeout
                )

                if response.status_code in _RETRYABLE_STATUS_CODES:
                    wait = _backoff(attempt)
                    logger.warning(
                        "POST %s returned %d (attempt %d/%d). Retrying in %.1fs.",
                        path,
                        response.status_code,
                        attempt,
                        self._max_retries,
                        wait,
                    )
                    if attempt < self._max_retries:
                        time.sleep(wait)
                    continue

                response.raise_for_status()
                return response.json()  # type: ignore[no-any-return]

            except requests.exceptions.RequestException as exc:
                last_exception = exc
                wait = _backoff(attempt)
                logger.warning(
                    "POST %s failed (attempt %d/%d): %s. Retrying in %.1fs.",
                    path,
                    attempt,
                    self._max_retries,
                    exc,
                    wait,
                )
                if attempt < self._max_retries:
                    time.sleep(wait)

        raise RuntimeError(
            f"POST {path} failed after {self._max_retries} attempts."
        ) from last_exception


def _to_iso(dt: datetime) -> str:
    """datetime を UTC ISO 8601 文字列に変換する。"""
    if dt.tzinfo is None:
        dt = dt.replace(tzinfo=timezone.utc)
    return dt.isoformat()


def _backoff(attempt: int) -> float:
    """指数バックオフの待機秒数（上限 60 秒）を返す。"""
    return min(2 ** (attempt - 1), 60)
