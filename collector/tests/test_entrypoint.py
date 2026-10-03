"""
`python -m collector` でパッケージを起動できることのテスト。
"""
from __future__ import annotations

import os
import subprocess
import sys
from pathlib import Path

COLLECTOR_ROOT = Path(__file__).resolve().parent.parent


def test_python_m_collector_runs_main() -> None:
    """__main__.py 経由で main() が呼ばれ、必須の環境変数が無いと終了コード1で終わる。"""
    required_keys = ("BACKEND_BASE_URL", "COLLECTOR_API_KEY")
    env = {k: v for k, v in os.environ.items() if k not in required_keys}

    result = subprocess.run(
        [sys.executable, "-m", "collector"],
        cwd=COLLECTOR_ROOT,
        env=env,
        capture_output=True,
        text=True,
        timeout=30,
    )

    assert "cannot be directly executed" not in result.stderr
    assert result.returncode == 1
    assert "BACKEND_BASE_URL" in result.stdout + result.stderr
