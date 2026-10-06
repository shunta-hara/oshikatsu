"""
X（旧 Twitter）公開 RSS プロキシフィードの収集モジュール（FEAT-049）。

X の公式 API は有料のため、公開 RSS プロキシ（nitter 系インスタンス等）経由で
タイムラインを収集する。

【重要な設計方針】
- プロキシのベース URL は環境変数 X_RSS_BASE_URL のみで指定する。
  既定 URL をコードに埋め込まない（不安定・利用規約グレーのため）。
- X_RSS_BASE_URL が未設定の場合、X 収集を丸ごとスキップしてログに記録する。
- robots.txt で取得が禁止されているフィードは取得しない（RssFetcher が確認）。
- プロキシ停止・失敗時は対象タレントをスキップし、他ソースの収集を止めない。
- タイムアウト・リトライ上限（無限リトライ禁止）は RssFetcher に委ねる。
"""
from __future__ import annotations

from datetime import datetime, timezone

from collector.client import BackendClient, NewsBatchItem, TalentSourceInfo
from collector.logger import get_logger
from collector.rss_fetcher import RssFetcher

logger = get_logger(__name__)

# ソースタイプ定数
SOURCE_TYPE_X_POST = "x_post"

# X ハンドルから RSS URL を組み立てるパスのサフィックス
_RSS_PATH_SUFFIX = "/rss"


def _build_x_rss_url(base_url: str, handle: str) -> str:
    """
    X ハンドルから RSS フィード URL を組み立てる。

    handle は '@' プレフィックス付き・なし両方に対応する。
    例: base_url="https://nitter.example.com", handle="@someuser"
        → "https://nitter.example.com/someuser/rss"
    """
    clean_handle = handle.lstrip("@")
    base = base_url.rstrip("/")
    return f"{base}/{clean_handle}{_RSS_PATH_SUFFIX}"


def run_x_collection(
    client: BackendClient,
    fetcher: RssFetcher,
    talent_sources: list[TalentSourceInfo],
    x_rss_base_url: str,
) -> None:
    """
    X 公開 RSS プロキシから投稿を収集して backend に一括送信する。

    x_rss_base_url が空の場合は収集をスキップしてログに記録する。
    プロキシエラー・robots.txt 禁止時は対象タレントをスキップして他の収集を続行する。
    """
    if not x_rss_base_url:
        logger.info("X_RSS_BASE_URL が未設定です。X 収集をスキップします。")
        return

    x_sources = [s for s in talent_sources if s.source_type == SOURCE_TYPE_X_POST]

    if not x_sources:
        logger.info("X ソースが 0 件です。X 収集をスキップします。")
        return

    logger.info("X RSS 収集バッチ開始（ソース数: %d）", len(x_sources))

    total_saved = 0
    total_skipped = 0
    total_failed = 0

    for source in x_sources:
        handle = source.source_id
        talent_id = source.talent_id
        rss_url = _build_x_rss_url(x_rss_base_url, handle)

        logger.info(
            "X RSS 収集開始: talent_id=%d, handle=%s",
            talent_id,
            handle,
        )

        try:
            entries = fetcher.fetch(rss_url)
        except Exception as exc:
            logger.warning(
                "X RSS 収集失敗（スキップ）: talent_id=%d, handle=%s: %s",
                talent_id,
                handle,
                exc,
            )
            total_failed += 1
            continue

        if not entries:
            logger.info(
                "X RSS エントリなし（robots.txt 禁止またはフィード空）: talent_id=%d, handle=%s",
                talent_id,
                handle,
            )
            continue

        logger.info(
            "X RSS 取得エントリ数: %d (talent_id=%d, handle=%s)",
            len(entries),
            talent_id,
            handle,
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
                source_type=SOURCE_TYPE_X_POST,
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
                "X RSS 送信完了: talent_id=%d - 受信: %d件, 新規保存: %d件, 重複スキップ: %d件",
                talent_id,
                result.received_count,
                result.saved_count,
                result.skipped_count,
            )
        except Exception as exc:
            logger.error(
                "X RSS 一括送信失敗: talent_id=%d: %s",
                talent_id,
                exc,
            )
            total_failed += 1
            continue

    logger.info(
        "X RSS 収集バッチ完了 - 新規保存: %d件, 重複スキップ: %d件, 失敗: %d件",
        total_saved,
        total_skipped,
        total_failed,
    )
