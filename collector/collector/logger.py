"""
ロガーセットアップモジュール。
各モジュールは get_logger(__name__) で使う。
"""
from __future__ import annotations

import logging
import sys


def setup_logging(level: int = logging.INFO) -> None:
    """ルートロガーを標準出力へ向けて設定する。"""
    handler = logging.StreamHandler(sys.stdout)
    handler.setFormatter(
        logging.Formatter(
            fmt="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
            datefmt="%Y-%m-%dT%H:%M:%S",
        )
    )
    root = logging.getLogger()
    root.setLevel(level)
    # 重複追加を防ぐ
    if not root.handlers:
        root.addHandler(handler)
    else:
        root.handlers.clear()
        root.addHandler(handler)


def get_logger(name: str) -> logging.Logger:
    """指定名のロガーを返す。"""
    return logging.getLogger(name)
