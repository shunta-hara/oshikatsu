---
name: project-oshi-tracking
description: oshikatsuプロジェクトの推し活トラッキング仕様書の生成・スプリント計画の記録
metadata:
  type: project
---

仕様書 `docs/spec-oshi-tracking.md` を2026-07-14に生成済み。

**Why:** プロジェクト初回スプリント計画。コードなしの新規プロジェクトで、CLAUDE.mdとルールファイル（.claude/rules/*.md）のみ整備済みの状態から仕様書を作成した。

**How to apply:** generator エージェントへの引き渡し時は `sprint=1 spec=docs/spec-oshi-tracking.md` を指定する。

## 仕様概要

- 機能数: 16件（Must: 9 / Should: 4 / Could: 3）
- スプリント数: 10

## スプリント構成

| Sprint | 目標 | 主体層 | 対象機能 |
|--------|------|--------|---------|
| 1 | DBスキーマとバックエンドAPI基盤の確立 | backend | FEAT-003, FEAT-004 |
| 2 | ユーザー認証API | backend | FEAT-001 |
| 3 | 推し登録・管理API | backend | FEAT-002 |
| 4 | collector書き込み方式の確立・バッチ基盤 | backend + collector | FEAT-005, FEAT-009 |
| 5 | ニュース収集バッチ | collector | FEAT-006 |
| 6 | 出演番組収集バッチ | collector | FEAT-007 |
| 7 | フロントエンド基盤・認証UI | frontend | FEAT-001 |
| 8 | 推し管理UI | frontend | FEAT-002 |
| 9 | ニュース・出演番組一覧UI | frontend | FEAT-008 |
| 10 | 推し別フィルタリング・収集ジョブ監視 | frontend + backend + collector | FEAT-010, FEAT-011 |

## 重要な設計決定

- collector → backend 内部API 経由でDBに書き込む（直接DB接続なし）
- 内部APIはAPIキー認証で保護
- 書き込み方式は `docs/collector-write-strategy.md` に記録する（Sprint 4で作成）
- 1ユーザーの推し上限: 20件
- 収集サイクル: 初期1時間ごと
- 一覧ページネーション: 1ページ20件
