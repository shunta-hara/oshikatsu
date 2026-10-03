"""collector.health_wait のテスト"""
from __future__ import annotations

from unittest.mock import MagicMock

import pytest

from collector.health_wait import wait_for_backend


class TestWaitForBackend:
    # ── 正常系 ────────────────────────────────────────────────────────────────

    def test_backend_ready_immediately_returns_true(
        self, mocker: MagicMock
    ) -> None:
        """backend が初回ヘルスチェックで応答すれば True を返す。"""
        client = MagicMock()
        client.check_health.return_value = True
        sleep_mock = mocker.patch("collector.health_wait.time.sleep")

        result = wait_for_backend(client, max_wait_seconds=120, poll_interval_seconds=10)

        assert result is True
        client.check_health.assert_called_once()
        sleep_mock.assert_not_called()

    def test_backend_ready_after_retries_returns_true(
        self, mocker: MagicMock
    ) -> None:
        """backend が数回のリトライ後に応答すれば True を返す。"""
        client = MagicMock()
        client.check_health.side_effect = [False, False, True]
        sleep_mock = mocker.patch("collector.health_wait.time.sleep")

        result = wait_for_backend(client, max_wait_seconds=120, poll_interval_seconds=10)

        assert result is True
        assert client.check_health.call_count == 3
        assert sleep_mock.call_count == 2

    def test_backend_ready_after_retries_sleeps_correct_interval(
        self, mocker: MagicMock
    ) -> None:
        """ポーリング間隔として poll_interval_seconds が使われる。"""
        client = MagicMock()
        client.check_health.side_effect = [False, True]
        sleep_mock = mocker.patch("collector.health_wait.time.sleep")

        wait_for_backend(client, max_wait_seconds=120, poll_interval_seconds=15)

        sleep_mock.assert_called_once()
        actual_sleep = sleep_mock.call_args[0][0]
        # 残り時間が poll_interval_seconds より大きい場合は poll_interval_seconds を使う
        assert actual_sleep == pytest.approx(15.0, abs=1.0)

    # ── 異常系 ────────────────────────────────────────────────────────────────

    def test_backend_never_ready_returns_false(self, mocker: MagicMock) -> None:
        """タイムアウトまでに backend が起動しなければ False を返す。"""
        client = MagicMock()
        client.check_health.return_value = False
        mocker.patch("collector.health_wait.time.sleep")

        # max_wait_seconds=0 → 初回チェック後に即タイムアウト
        result = wait_for_backend(client, max_wait_seconds=0, poll_interval_seconds=10)

        assert result is False

    def test_backend_never_ready_logs_error(
        self, mocker: MagicMock, caplog: pytest.LogCaptureFixture
    ) -> None:
        """タイムアウト時にエラーログを出力する。"""
        client = MagicMock()
        client.check_health.return_value = False
        mocker.patch("collector.health_wait.time.sleep")

        import logging
        with caplog.at_level(logging.ERROR, logger="collector.health_wait"):
            wait_for_backend(client, max_wait_seconds=0, poll_interval_seconds=10)

        assert any("Aborting collection" in record.message for record in caplog.records)

    # ── 境界値 ────────────────────────────────────────────────────────────────

    def test_sleep_capped_at_remaining_time(self, mocker: MagicMock) -> None:
        """残り時間が poll_interval_seconds より短ければ残り時間だけ待つ。"""
        client = MagicMock()
        # 最初の check_health は False（→ sleep）、その後 True
        call_count = 0

        def health_side_effect() -> bool:
            nonlocal call_count
            call_count += 1
            return call_count > 1

        client.check_health.side_effect = health_side_effect

        sleep_calls: list[float] = []
        mocker.patch(
            "collector.health_wait.time.sleep",
            side_effect=lambda s: sleep_calls.append(s),
        )

        # max_wait_seconds=5 で poll_interval_seconds=60 を指定 → 5秒以上は待てない
        result = wait_for_backend(client, max_wait_seconds=5, poll_interval_seconds=60)

        assert result is True
        assert len(sleep_calls) == 1
        # sleep は min(60, remaining) ≤ 5 になるはず
        assert sleep_calls[0] <= 5.0

    def test_backend_ready_on_exactly_first_attempt(
        self, mocker: MagicMock
    ) -> None:
        """max_wait_seconds に関わらず、初回成功なら sleep なしで True を返す。"""
        client = MagicMock()
        client.check_health.return_value = True
        sleep_mock = mocker.patch("collector.health_wait.time.sleep")

        # max_wait_seconds=0 でも初回成功なら True
        result = wait_for_backend(client, max_wait_seconds=0, poll_interval_seconds=10)

        assert result is True
        sleep_mock.assert_not_called()
