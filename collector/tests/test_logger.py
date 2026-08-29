"""collector.logger のテスト"""
from __future__ import annotations

import logging

from collector.logger import get_logger, setup_logging


class TestLogger:
    # ── 正常系 ────────────────────────────────────────────────────────────────

    def test_get_logger_returns_logger_with_given_name(self) -> None:
        logger = get_logger("test.module")

        assert isinstance(logger, logging.Logger)
        assert logger.name == "test.module"

    def test_setup_logging_configures_root_handler(self) -> None:
        setup_logging(level=logging.DEBUG)
        root = logging.getLogger()

        assert root.level == logging.DEBUG
        assert len(root.handlers) == 1

    def test_setup_logging_idempotent_no_duplicate_handlers(self) -> None:
        setup_logging()
        setup_logging()
        root = logging.getLogger()

        # 重複ハンドラがないこと
        assert len(root.handlers) == 1

    # ── 境界値 ────────────────────────────────────────────────────────────────

    def test_get_logger_different_names_are_different_loggers(self) -> None:
        logger_a = get_logger("module.a")
        logger_b = get_logger("module.b")

        assert logger_a is not logger_b
        assert logger_a.name != logger_b.name

    def test_get_logger_same_name_returns_same_instance(self) -> None:
        logger1 = get_logger("same.name")
        logger2 = get_logger("same.name")

        assert logger1 is logger2
