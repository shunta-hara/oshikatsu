---
name: project-oshi-tracking
description: oshikatsu 推し活トラッキング仕様書の状態。FEAT・スプリント番号の現在の最大値を追跡する。
metadata:
  type: project
---

docs/spec-oshi-tracking.md に仕様書を生成・追記済み。

現在の状態:
- FEAT-001〜016 / Sprint 1〜15: 実装・合格済み（2026-10-03 時点）
- FEAT-017〜022 / Sprint 16〜21: 2026-10-03 に追記（バグ修正・デプロイ準備）

追記内容の概要:
- FEAT-017（Sprint 16）: Npgsql UTC オフセット制約による 500 エラー修正 / backend
- FEAT-018（Sprint 17）: 日付範囲フィルタの JST 基準化 / frontend
- FEAT-019（Sprint 18）: 共通スタイル・ナビゲーション統一 / frontend
- FEAT-020（Sprint 19）: frontend API URL 環境変数化・SPA ルーティング / frontend
- FEAT-021（Sprint 20）: backend Dockerfile・CORS・HTTPS・マイグレーション自動適用 / backend
- FEAT-022（Sprint 21）: collector GitHub Actions 定期実行・コールドスタート対策 / collector

技術的背景:
- collector 書き込みは backend 内部 API 経由（直接 DB 書き込みは不採用）
- デプロイ先: frontend=Cloudflare Pages / backend=Render / DB=Neon / collector=GitHub Actions
- deploy-plan.md の対応項目 1〜8 を FEAT-020〜022 で網羅

**Why:** ローカル動作確認（2026-10-03）でバグと UI 問題が発見され、無料枠デプロイ計画も確定した。
**How to apply:** 次に仕様追記を依頼されたら FEAT-023〜、Sprint 22〜 から採番する。
