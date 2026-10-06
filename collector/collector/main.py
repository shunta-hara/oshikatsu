"""
collector エントリーポイント。
環境変数から設定を読み込み、ニュース収集バッチと番組収集バッチを1回実行する。
"""
from __future__ import annotations

import sys

from collector.client import BackendClient
from collector.config import load_config
from collector.event_collector import run_event_collection
from collector.health_wait import wait_for_backend
from collector.logger import get_logger, setup_logging
from collector.news_collector import run_news_collection
from collector.news_fetcher import NewsFetcher
from collector.program_collector import run_program_collection
from collector.program_fetcher import ProgramFetcher
from collector.rss_fetcher import RssFetcher
from collector.x_collector import run_x_collection
from collector.youtube_collector import YouTubeFetcher, run_youtube_collection


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

    # YouTube 収集（API キーが設定されている場合のみ）
    if config.youtube_api_key:
        try:
            talent_sources = client.get_talent_sources(source_type="youtube")
        except Exception as exc:
            logger.error("YouTube ソース一覧の取得に失敗しました: %s", exc)
            talent_sources = []

        if talent_sources:
            # TALENT_ID が指定された場合は対象タレントのソースのみに絞る
            if config.talent_id is not None:
                talent_sources = [s for s in talent_sources if s.talent_id == config.talent_id]

            youtube_fetcher = YouTubeFetcher(
                api_key=config.youtube_api_key,
                timeout=config.request_timeout_seconds,
                max_retries=config.max_retries,
            )
            run_youtube_collection(client, youtube_fetcher, talent_sources)
        else:
            logger.info("YouTube ソースが 0 件です。YouTube 収集をスキップします。")
    else:
        logger.info(
            "YOUTUBE_API_KEY が未設定です。YouTube 収集をスキップします。"
        )

    # RSS フェッチャー（X 収集・イベント収集で共用）
    rss_fetcher = RssFetcher(
        timeout=config.request_timeout_seconds,
        max_retries=config.max_retries,
        access_interval=1.0,
    )

    # X RSS 収集（X_RSS_BASE_URL が設定されている場合のみ）
    if config.x_rss_base_url:
        try:
            x_sources = client.get_talent_sources(source_type="x_post")
        except Exception as exc:
            logger.error("X RSS ソース一覧の取得に失敗しました: %s", exc)
            x_sources = []

        if config.talent_id is not None:
            x_sources = [s for s in x_sources if s.talent_id == config.talent_id]

        run_x_collection(client, rss_fetcher, x_sources, config.x_rss_base_url)
    else:
        logger.info("X_RSS_BASE_URL が未設定です。X 収集をスキップします。")

    # イベント RSS 収集
    try:
        event_sources = client.get_talent_sources(source_type="event")
    except Exception as exc:
        logger.error("イベント RSS ソース一覧の取得に失敗しました: %s", exc)
        event_sources = []

    if config.talent_id is not None:
        event_sources = [s for s in event_sources if s.talent_id == config.talent_id]

    run_event_collection(client, rss_fetcher, event_sources)

    logger.info("Collector finished")


if __name__ == "__main__":
    main()
