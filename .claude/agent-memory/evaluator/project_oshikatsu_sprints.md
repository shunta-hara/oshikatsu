---
name: project-oshikatsu-sprints
description: oshikatsuプロジェクトのスプリント評価履歴と最終状態
metadata:
  type: project
---

Sprint 14まで完了。FEAT-015（日付範囲フィルタ）実装済み。

**Why:** FEAT-001〜015をカバーするために14スプリントが計画・実施された。

**How to apply:** 新規スプリント開始時はこのプロジェクトの現在状態を参照する。

## Sprint 14 評価 (2026-08-30)

- 対象: FEAT-015（日付範囲フィルタ）
- 結果: 合格
- ビルド/テスト: backend 228/228, frontend 169/169
- 動的テスト: from+to両方・片方のみ・from=to境界・to<fromバリデーション（news/programs両方）すべて正常
- コード品質: 必須違反0件
- Npgsql: DateTimeOffset? + timestamptz カラム比較 正常動作確認済み（Npgsql 9.0.4）

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

## FEAT 全体完了状況

FEAT-001〜012 すべて実装済み（Sprint 1〜11）。
FEAT-013（ニュース内キーワード検索）: Sprint 12で実装済み。
FEAT-014（マルチ推しダッシュボード）: Sprint 13で実装済み。
FEAT-015（日付範囲フィルタ）: Sprint 14で実装済み。
FEAT-016（ニュースブックマーク）: 未実装（Could）。

## 技術的メモ

- FeedService: 日付範囲フィルタは `n.PublishedAt != null && n.PublishedAt >= from.Value` パターン（nullアイテムはフィルタ適用時に除外）
- DateRangeFilter.tsx: ISO変換 from=T00:00:00Z、to=T23:59:59.999Z。ローカルバリデーションは YYYY-MM-DD 文字列比較
- FeedController: to < from は Service呼び出し前に400 Bad Request早期リターン
- CollectorController: ユーザー向け GET /status は `[Authorize]` (JWT)、内部 POST /job-logs は `ApiKeyAuthFilter`
- FeedService: oshiId フィルタ時、ユーザーの推し一覧でバリデーションしてから絞り込む（他ユーザーの推しIDを指定されても空配列返却）
- DashboardPage: useSearchParams で URL クエリパラメータを読み書き、NewsFeedList/ProgramFeedList に key prop で強制再マウント
- collector requirements-dev.txt に pytest・responses が分離されている（requirements.txt は runtime のみ）
- FeedService.GetUnreadCountAsync: LastViewedFeedAt=null（未閲覧）は 0 を返す。mark-as-read でリセット後も画面滞在中はバッジ件数を維持する設計
- NotificationBadge: role="status" + aria-label でアクセシビリティ対応
