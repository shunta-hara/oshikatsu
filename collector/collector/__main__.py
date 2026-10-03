"""
`python -m collector` で実行したときのエントリーポイント。
GitHub Actions のワークフローと Dockerfile の CMD はこの形式で起動する。
"""
from __future__ import annotations

from collector.main import main

main()
