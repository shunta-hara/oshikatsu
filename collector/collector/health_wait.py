"""
backend コールドスタート対策モジュール。
Render のスリープ復帰（約1分）に備えて、collector は収集開始前に
ヘルスエンドポイントを確認し、backend が応答するまで待機する。
"""
from __future__ import annotations

import time

from collector.client import BackendClient
from collector.logger import get_logger

logger = get_logger(__name__)

_DEFAULT_MAX_WAIT_SECONDS = 120
_DEFAULT_POLL_INTERVAL_SECONDS = 10


def wait_for_backend(
    client: BackendClient,
    max_wait_seconds: int = _DEFAULT_MAX_WAIT_SECONDS,
    poll_interval_seconds: int = _DEFAULT_POLL_INTERVAL_SECONDS,
) -> bool:
    """
    backend のヘルスエンドポイント（GET /api/v1/health）が 200 を返すまで待機する。

    max_wait_seconds 以内に backend が起動すれば True を返す。
    タイムアウトした場合はエラーログを出力して False を返す。

    Args:
        client: backend との通信に使う BackendClient。
        max_wait_seconds: 最大待機秒数（デフォルト 120 秒 = 2 分）。
        poll_interval_seconds: ヘルスチェックの間隔秒数（デフォルト 10 秒）。

    Returns:
        backend が起動すれば True、タイムアウトすれば False。
    """
    logger.info(
        "Checking backend health before collection (max wait: %d seconds).",
        max_wait_seconds,
    )
    deadline = time.monotonic() + max_wait_seconds
    attempt = 0

    while True:
        attempt += 1
        if client.check_health():
            logger.info("Backend is ready (attempt %d).", attempt)
            return True

        remaining = deadline - time.monotonic()
        if remaining <= 0:
            logger.error(
                "Backend did not become ready within %d seconds after %d attempt(s). "
                "Aborting collection.",
                max_wait_seconds,
                attempt,
            )
            return False

        wait = min(float(poll_interval_seconds), remaining)
        logger.info(
            "Backend not ready (attempt %d). Retrying in %.1f seconds "
            "(%.0f seconds remaining).",
            attempt,
            wait,
            remaining,
        )
        time.sleep(wait)
