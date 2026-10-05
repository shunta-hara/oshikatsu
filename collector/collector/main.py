"""
collector エントリーポイント。
環境変数から設定を読み込み、ニュース収集バッチと番組収集バッチを1回実行する。
"""
from __future__ import annotations

import sys

from collector.client import BackendClient
from collector.config import load_config
from collector.health_wait import wait_for_backend
from collector.logger import get_logger, setup_logging
from collector.news_collector import run_news_collection
from collector.news_fetcher import NewsFetcher
from collector.program_collector import run_program_collection
from collector.program_fetcher import ProgramFetcher


def main() -> None:
    setup_logging()
    logger = get_logger(__name__)
    logger.info("Collector starting up")

    try:
        config = load_config()
    except ValueError as exc:
        print(f"Configuration error: {exc}", file=sys.stderr)
        sys.exit(1)

    client = BackendClient(config)

    # コールドスタート対策: 収集を開始する前に backend の起動を確認する
    if not wait_for_backend(client):
        logger.error("Backend is unavailable. Skipping collection and exiting.")
        sys.exit(1)

    # 全推し一覧を取得する（収集対象を確定するため1回だけ呼ぶ）
    try:
        all_oshis = client.get_oshis()
    except Exception as exc:
        logger.error("推し一覧の取得に失敗しました: %s", exc)
        sys.exit(1)

    if not all_oshis:
        logger.info("登録済みの推しが 0 件です。収集をスキップします。")
        return

    # TALENT_ID が指定された場合は対象を1人に絞る
    target_oshis = all_oshis
    if config.talent_id is not None:
        target_oshis = [o for o in all_oshis if o.talent_id == config.talent_id]
        if not target_oshis:
            logger.warning(
                "指定したタレント (TALENT_ID=%d) が見つかりません。収集をスキップします。",
                config.talent_id,
            )
            return

    logger.info(
        "収集対象: %d 件のタレント%s",
        len(target_oshis),
        f" (TALENT_ID={config.talent_id})" if config.talent_id is not None else "",
    )

    news_fetcher = NewsFetcher(
        timeout=config.request_timeout_seconds,
        max_retries=config.max_retries,
    )
    news_urls_by_oshi = run_news_collection(client, news_fetcher, target_oshis)

    program_fetcher = ProgramFetcher(
        timeout=config.request_timeout_seconds,
        max_retries=config.max_retries,
    )
    run_program_collection(
        client,
        program_fetcher,
        target_oshis,
        news_urls_by_oshi=news_urls_by_oshi,
    )

    logger.info("Collector finished")


if __name__ == "__main__":
    main()
