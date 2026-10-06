"""
イベント/ライブ告知 RSS フィードの収集モジュール（FEAT-050）。

talent_sources テーブルの source_type = "event" エントリから
フィード URL（source_id）を取得し、収集する。

【設計方針】
- フィード URL は talent_sources.source_id の値のみを使用する（コードに URL を埋め込まない）。
- robots.txt で取得が禁止されているフィードは取得しない（RssFetcher が確認）。
- フィード取得エラー時は対象タレントをスキップし、他ソースの収集を止めない。
- 取得したアイテムには出典 URL と日時を保持する。
- タイムアウト・リトライ上限（無限リトライ禁止）は RssFetcher に委ねる。
"""
from __future__ import annotations

from datetime import datetime, timezone

from collector.client import BackendClient, NewsBatchItem, TalentSourceInfo
from collector.logger import get_logger
from collector.rss_fetcher import RssFetcher

logger = get_logger(__name__)

# ソースタイプ定数
SOURCE_TYPE_EVENT = "event"


def run_event_collection(
    client: BackendClient,
    fetcher: RssFetcher,
    talent_sources: list[TalentSourceInfo],
) -> None:
    """
    イベント/ライブ告知 RSS フィードを収集して backend に一括送信する。

    source_id がフィード URL として使用される。
    フィード取得エラー・robots.txt 禁止時は対象タレントをスキップして他の収集を続行する。
    """
    event_sources = [s for s in talent_sources if s.source_type == SOURCE_TYPE_EVENT]

    if not event_sources:
        logger.info("イベント RSS ソースが 0 件です。イベント収集をスキップします。")
        return

    logger.info("イベント RSS 収集バッチ開始（ソース数: %d）", len(event_sources))

    total_saved = 0
    total_skipped = 0
    total_failed = 0

    for source in event_sources:
        feed_url = source.source_id
        talent_id = source.talent_id

        logger.info(
            "イベント RSS 収集開始: talent_id=%d, url=%s",
            talent_id,
            feed_url,
        )

        try:
            entries = fetcher.fetch(feed_url)
        except Exception as exc:
            logger.warning(
                "イベント RSS 収集失敗（スキップ）: talent_id=%d, url=%s: %s",
                talent_id,
                feed_url,
                exc,
            )
            total_failed += 1
            continue

        if not entries:
            logger.info(
                "イベント RSS エントリなし（robots.txt 禁止またはフィード空）: talent_id=%d, url=%s",
                talent_id,
                feed_url,
            )
            continue

        logger.info(
            "イベント RSS 取得エントリ数: %d (talent_id=%d, url=%s)",
            len(entries),
            talent_id,
            feed_url,
        )

        collected_at = datetime.now(timezone.utc)
        batch_items = [
            NewsBatchItem(
                title=entry.title,
                source_url=entry.source_url,
                collected_at=collected_at,
                summary=entry.summary,
                published_at=entry.published_at,
                source_name=None,
                source_type=SOURCE_TYPE_EVENT,
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
                "イベント RSS 送信完了: talent_id=%d - 受信: %d件, 新規保存: %d件, 重複スキップ: %d件",
                talent_id,
                result.received_count,
                result.saved_count,
                result.skipped_count,
            )
        except Exception as exc:
            logger.error(
                "イベント RSS 一括送信失敗: talent_id=%d: %s",
                talent_id,
                exc,
            )
            total_failed += 1
            continue

    logger.info(
        "イベント RSS 収集バッチ完了 - 新規保存: %d件, 重複スキップ: %d件, 失敗: %d件",
        total_saved,
        total_skipped,
        total_failed,
    )
