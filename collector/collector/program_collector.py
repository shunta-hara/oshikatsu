"""
出演番組収集バッチのメインロジック。
全推しの出演番組情報を1サイクルで収集し、backend 内部 API 経由で保存する。
"""
from __future__ import annotations

from datetime import datetime, timezone

from collector.client import BackendClient
from collector.logger import get_logger
from collector.program_fetcher import ProgramFetcher

logger = get_logger(__name__)


def run_program_collection(client: BackendClient, fetcher: ProgramFetcher) -> None:
    """
    全推しの出演番組情報を1サイクル収集して backend に送信する。

    収集成功・失敗・件数をログ出力する。
    バッチ終了後に実行ログを backend に送信する。
    ネットワークエラー時は BackendClient / ProgramFetcher 内部でリトライ済み。
    """
    logger.info("番組収集バッチ開始")
    executed_at = datetime.now(timezone.utc)

    # 推し一覧を backend 内部 API から取得
    try:
        oshis = client.get_oshis()
    except Exception as exc:
        logger.error("推し一覧の取得に失敗しました: %s", exc)
        _send_job_log(client, job_type="program", executed_at=executed_at, success_count=0, failure_count=1)
        return

    if not oshis:
        logger.info("登録済みの推しが 0 件です。収集をスキップします。")
        _send_job_log(client, job_type="program", executed_at=executed_at, success_count=0, failure_count=0)
        return

    logger.info("収集対象推し数: %d", len(oshis))

    total_saved = 0
    total_duplicates = 0
    total_failed = 0

    for oshi in oshis:
        logger.info("収集開始: '%s' (id=%d)", oshi.name, oshi.id)
        saved = 0
        duplicates = 0
        failed = 0

        # 番組情報をフィードから収集
        try:
            entries = fetcher.fetch_for_oshi(oshi.name)
        except Exception as exc:
            logger.error(
                "番組収集失敗: 推し='%s' (id=%d): %s",
                oshi.name,
                oshi.id,
                exc,
            )
            total_failed += 1
            continue

        logger.info("取得番組数: %d (推し: '%s')", len(entries), oshi.name)

        # 各番組情報を backend 内部 API に送信
        collected_at = datetime.now(timezone.utc)
        for entry in entries:
            try:
                result = client.post_program_appearance(
                    oshi_id=oshi.id,
                    program_name=entry.program_name,
                    source_url=entry.source_url,
                    collected_at=collected_at,
                    broadcaster=entry.broadcaster,
                    aired_at=entry.aired_at,
                )
                if result.get("wasDuplicate"):
                    duplicates += 1
                    logger.debug(
                        "重複スキップ: '%s' - '%s'", oshi.name, entry.source_url
                    )
                else:
                    saved += 1
            except Exception as exc:
                logger.error(
                    "番組送信失敗: 推し='%s', URL='%s': %s",
                    oshi.name,
                    entry.source_url,
                    exc,
                )
                failed += 1

        logger.info(
            "収集完了: 推し='%s' (id=%d) - 新規保存: %d件, 重複スキップ: %d件, 失敗: %d件",
            oshi.name,
            oshi.id,
            saved,
            duplicates,
            failed,
        )

        total_saved += saved
        total_duplicates += duplicates
        total_failed += failed

    logger.info(
        "番組収集バッチ完了 - 新規保存: %d件, 重複スキップ: %d件, 失敗: %d件",
        total_saved,
        total_duplicates,
        total_failed,
    )

    _send_job_log(
        client,
        job_type="program",
        executed_at=executed_at,
        success_count=total_saved,
        failure_count=total_failed,
    )


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
