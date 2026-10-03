"""
ニュース収集バッチのメインロジック。
指定された推しのニュースを1サイクルで収集し、backend 内部 API 経由で一括保存する。
"""
from __future__ import annotations

from datetime import datetime, timezone

from collector.client import BackendClient, NewsBatchItem, OshiInfo
from collector.logger import get_logger
from collector.news_fetcher import NewsFetcher

logger = get_logger(__name__)


def run_news_collection(
    client: BackendClient,
    fetcher: NewsFetcher,
    oshis: list[OshiInfo],
) -> dict[int, set[str]]:
    """
    指定された推しのニュースを1サイクル収集して backend に一括送信する。

    収集成功・失敗・件数をログ出力する。
    バッチ終了後に実行ログを backend に送信する。
    ネットワークエラー時は BackendClient / NewsFetcher 内部でリトライ済み。

    Returns:
        推し ID → 収集した sourceURL の集合。番組収集での重複排除に使用する。
    """
    logger.info("ニュース収集バッチ開始")
    executed_at = datetime.now(timezone.utc)
    collected_urls: dict[int, set[str]] = {}

    if not oshis:
        logger.info("収集対象の推しが 0 件です。収集をスキップします。")
        _send_job_log(client, job_type="news", executed_at=executed_at, success_count=0, failure_count=0)
        return collected_urls

    logger.info("収集対象推し数: %d", len(oshis))

    total_saved = 0
    total_skipped = 0
    total_failed = 0

    for oshi in oshis:
        logger.info("収集開始: '%s' (id=%d)", oshi.name, oshi.id)

        # ニュース記事をフィードから収集
        try:
            entries = fetcher.fetch_for_oshi(oshi.name)
        except Exception as exc:
            logger.error(
                "ニュース収集失敗: 推し='%s' (id=%d): %s",
                oshi.name,
                oshi.id,
                exc,
            )
            total_failed += 1
            continue

        logger.info("取得記事数: %d (推し: '%s')", len(entries), oshi.name)

        if not entries:
            logger.info("取得記事なし: 推し='%s'", oshi.name)
            continue

        collected_at = datetime.now(timezone.utc)
        batch_items = [
            NewsBatchItem(
                title=entry.title,
                source_url=entry.source_url,
                collected_at=collected_at,
                summary=entry.summary,
                published_at=entry.published_at,
            )
            for entry in entries
        ]

        # 一括 API で1回送信
        try:
            result = client.post_news_batch(oshi_id=oshi.id, items=batch_items)
            total_saved += result.saved_count
            total_skipped += result.skipped_count
            logger.info(
                "収集完了: 推し='%s' (id=%d) - 受信: %d件, 新規保存: %d件, 重複スキップ: %d件",
                oshi.name,
                oshi.id,
                result.received_count,
                result.saved_count,
                result.skipped_count,
            )
        except Exception as exc:
            logger.error(
                "一括送信失敗: 推し='%s' (id=%d): %s",
                oshi.name,
                oshi.id,
                exc,
            )
            total_failed += 1
            continue

        # 収集した URL を記録（番組収集での重複排除に使用）
        collected_urls[oshi.id] = {entry.source_url for entry in entries}

    logger.info(
        "ニュース収集バッチ完了 - 新規保存: %d件, 重複スキップ: %d件, 失敗: %d件",
        total_saved,
        total_skipped,
        total_failed,
    )

    _send_job_log(
        client,
        job_type="news",
        executed_at=executed_at,
        success_count=total_saved,
        failure_count=total_failed,
    )

    return collected_urls


def _send_job_log(
    client: BackendClient,
    *,
    job_type: str,
    executed_at: datetime,
    success_count: int,
    failure_count: int,
) -> None:
    """バッチ実行ログを backend に送信する。送信失敗はログに出力するのみで握りつぶさない。"""
    try:
        client.post_job_log(
            job_type=job_type,
            executed_at=executed_at,
            success_count=success_count,
            failure_count=failure_count,
        )
        logger.info(
            "ジョブログ送信完了: type=%s, success=%d, failure=%d",
            job_type,
            success_count,
            failure_count,
        )
    except Exception as exc:
        logger.warning("ジョブログの送信に失敗しました: %s", exc)
