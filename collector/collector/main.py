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

    news_fetcher = NewsFetcher(
        timeout=config.request_timeout_seconds,
        max_retries=config.max_retries,
    )
    run_news_collection(client, news_fetcher)

    program_fetcher = ProgramFetcher(
        timeout=config.request_timeout_seconds,
        max_retries=config.max_retries,
    )
    run_program_collection(client, program_fetcher)

    logger.info("Collector finished")


if __name__ == "__main__":
    main()
