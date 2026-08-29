---
name: project-oshikatsu-sprints
description: oshikatsuプロジェクトのスプリント評価履歴と最終状態
metadata:
  type: project
---

全11スプリントが完了し、Sprint 11で最終評価を実施した。

**Why:** FEAT-001〜012をカバーするために11スプリントが計画された。

**How to apply:** 新規スプリント開始時はこのプロジェクトが完結済みであることを参照する。

## Sprint 11 評価 (2026-08-29)

- 対象: FEAT-012（新着コンテンツ通知バッジ）
- 結果: 合格
- ビルド/テスト: backend 178/178, frontend 107/107
- 動的テスト: unread-count API（2件計上・リセット後0件）・認証チェック（401）すべて正常
- コード品質: 必須違反0件
- マイグレーション: `20260829000001_AddLastViewedFeedAtToUsers` 正常適用

## Sprint 10 評価 (2026-07-17)

- 対象: FEAT-010（推し別フィルタリング）、FEAT-011（収集ジョブ監視）
- 結果: 合格
- ビルド/テスト: backend 163/163, frontend 96/96, collector 117/117
- 動的テスト: API・URLクエリパラメータ同期・ジョブログ記録すべて正常
- コード品質: 必須違反0件

## Must/Should FEAT 全体完了状況

FEAT-001〜009 すべて実装済み（Sprint 1〜9）。  
Should: FEAT-010, FEAT-011, FEAT-012 実装済み（Sprint 10〜11）。  
FEAT-013（ニュース内キーワード検索）は Could 相当に降格または未実装。  
FEAT-014〜016（Could）は未実装。

## 技術的メモ

- CollectorController: ユーザー向け GET /status は `[Authorize]` (JWT)、内部 POST /job-logs は `ApiKeyAuthFilter`
- FeedService: oshiId フィルタ時、ユーザーの推し一覧でバリデーションしてから絞り込む（他ユーザーの推しIDを指定されても空配列返却）
- DashboardPage: useSearchParams で URL クエリパラメータを読み書き、NewsFeedList/ProgramFeedList に key prop で強制再マウント
- collector requirements-dev.txt に pytest・responses が分離されている（requirements.txt は runtime のみ）
- FeedService.GetUnreadCountAsync: LastViewedFeedAt=null（未閲覧）は 0 を返す。mark-as-read でリセット後も画面滞在中はバッジ件数を維持する設計
- NotificationBadge: role="status" + aria-label でアクセシビリティ対応
