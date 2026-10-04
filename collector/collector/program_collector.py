"""
出演番組収集バッチのメインロジック。
指定された推しの出演番組情報を1サイクルで収集し、backend 内部 API 経由で一括保存する。

FEAT-024: 番組候補は以下のフィルタを適用してから送信する:
1. タイトルに PROGRAM_KEYWORDS のいずれかを含むものに絞る
2. 同じ実行で収集したニュースと URL が重複するものを除外する
"""
from __future__ import annotations

from datetime import datetime, timezone

from collector.client import BackendClient, OshiInfo, ProgramBatchItem
from collector.logger import get_logger
from collector.program_fetcher import ProgramEntry, ProgramFetcher

logger = get_logger(__name__)

# 番組候補を絞り込むキーワードリスト（FEAT-024）
# ここを変更することで絞り込み条件を一括管理できる
PROGRAM_KEYWORDS: list[str] = [
    "出演",
    "番組",
    "放送",
    "テレビ",
    "TV",
    "ラジオ",
    "ドラマ",
    "映画",
    "配信",
    "ライブ",
    "MC",
    "ゲスト",
]


def _is_program_related(title: str) -> bool:
    """タイトルが出演・放送関連語を含むか判定する。"""
    return any(kw in title for kw in PROGRAM_KEYWORDS)


def _filter_program_entries(
    entries: list[ProgramEntry],
    news_urls: set[str],
) -> list[ProgramEntry]:
    """
    番組候補をフィルタして返す。

    - タイトルに PROGRAM_KEYWORDS のいずれかを含まないものは除外
    - news_urls に含まれる sourceUrl を持つものは除外
    """
    filtered: list[ProgramEntry] = []
    for entry in entries:
        if not _is_program_related(entry.program_name):
            logger.debug(
                "番組候補をスキップ（キーワードなし）: '%s'", entry.program_name
            )
            continue
        if entry.source_url in news_urls:
            logger.debug(
                "番組候補をスキップ（ニュースと URL 重複）: '%s'", entry.source_url
            )
            continue
        filtered.append(entry)
    return filtered


def run_program_collection(
    client: BackendClient,
    fetcher: ProgramFetcher,
    oshis: list[OshiInfo],
    *,
    news_urls_by_oshi: dict[int, set[str]] | None = None,
) -> None:
    """
    指定された推しの出演番組情報を1サイクル収集して backend に一括送信する。

    収集成功・失敗・件数をログ出力する。
    バッチ終了後に実行ログを backend に送信する。
    ネットワークエラー時は BackendClient / ProgramFetcher 内部でリトライ済み。

    Args:
        news_urls_by_oshi: 同じ実行で収集したニュースの sourceURL（推し ID をキーとする辞書）。
                           指定した場合、各推しのニュース URL と重複する番組候補を除外する。
    """
    logger.info("番組収集バッチ開始")
    executed_at = datetime.now(timezone.utc)

    if not oshis:
        logger.info("収集対象の推しが 0 件です。収集をスキップします。")
        _send_job_log(client, job_type="program", executed_at=executed_at, success_count=0, failure_count=0)
        return

    logger.info("収集対象推し数: %d", len(oshis))

    total_saved = 0
    total_skipped = 0
    total_failed = 0

    for oshi in oshis:
        logger.info("収集開始: '%s' (id=%d)", oshi.name, oshi.id)

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

        logger.info("取得番組候補数: %d (推し: '%s')", len(entries), oshi.name)

        # キーワードフィルタ・URL重複排除を適用
        news_urls = (news_urls_by_oshi or {}).get(oshi.id, set())
        entries = _filter_program_entries(entries, news_urls)
        logger.info(
            "フィルタ後番組数: %d (推し: '%s', ニュース重複除外URL: %d件)",
            len(entries),
            oshi.name,
            len(news_urls),
        )

        if not entries:
            logger.info("送信対象の番組情報なし: 推し='%s'", oshi.name)
            continue

        collected_at = datetime.now(timezone.utc)
        batch_items = [
            ProgramBatchItem(
                program_name=entry.program_name,
                source_url=entry.source_url,
                collected_at=collected_at,
                broadcaster=entry.broadcaster,
                aired_at=entry.aired_at,
                source_name=entry.source_name,
            )
            for entry in entries
        ]

        # 一括 API で1回送信
        try:
            result = client.post_program_batch(oshi_id=oshi.id, items=batch_items)
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

    logger.info(
        "番組収集バッチ完了 - 新規保存: %d件, 重複スキップ: %d件, 失敗: %d件",
        total_saved,
        total_skipped,
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
