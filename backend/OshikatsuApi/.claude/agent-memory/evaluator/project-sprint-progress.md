---
name: project-sprint-progress
description: oshikatsu プロジェクトのスプリント進捗状況（評価結果サマリー）
metadata:
  type: project
---

Sprint 1〜4 まで完了済み（2026-07-14 時点）。

**Why:** スプリント間の引き継ぎ情報として、どこまで評価済みかを記録する。

**How to apply:** 次の評価セッション開始時にスプリント番号を確認し、前回評価内容との整合性チェックに使う。

## 評価済みスプリント

| Sprint | 対象機能 | 評価結果 | テスト数 |
|---|---|---|---|
| 1 | FEAT-003, FEAT-004（DBスキーマ・API基盤） | 合格 | 不明（記録なし） |
| 2 | FEAT-001 backend（認証API） | 合格 | 40/40 |
| 3 | FEAT-002 backend（推し管理API） | 合格（推定） | 不明 |
| 4 | FEAT-005, FEAT-009（collector基盤・内部API） | 合格 | backend 95/95, collector 30/30 |

## Sprint 4 主な実装内容
- `POST /api/v1/internal/news`（APIキー認証付き）
- `POST /api/v1/internal/programs`（APIキー認証付き）
- `ApiKeyAuthFilter.cs` — IConfiguration から `CollectorSettings:ApiKey` で読み込み
- `collector/collector/client.py` — BackendClient（タイムアウト・指数バックオフリトライ）
- `collector/collector/config.py` — 環境変数から設定読み込み
- `collector/collector/logger.py` — ロガーセットアップ
- `docs/collector-write-strategy.md` — 書き込み方式ドキュメント

## 次スプリント（Sprint 5）
- FEAT-006: ニュース収集バッチ
- 主体層: collector
- BackendClient（Sprint 4 実装済み）を使って Google News RSS 等から収集
