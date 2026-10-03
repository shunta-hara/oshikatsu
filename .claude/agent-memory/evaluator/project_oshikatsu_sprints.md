---
name: project-oshikatsu-sprints
description: oshikatsuプロジェクトのスプリント評価履歴と最終状態
metadata:
  type: project
---

Sprint 21 評価完了（合格）。FEAT-022（collectorのGitHub Actions定期実行・コールドスタート対策）の全必須観点が合格。全21スプリント完了。

**Why:** FEAT-001〜022をカバーするために21スプリントが計画・実施された。Sprint 21が最終スプリント。

**How to apply:** 全スプリント完了。新規機能追加時は新仕様書から再プランニングする。

## Sprint 21 評価 (2026-10-03): 合格

- 対象: FEAT-022（collectorのGitHub Actions定期実行・コールドスタート対策）
- 結果: **合格**
- ビルド/テスト: docker build 成功・130/130（test_health_wait.py 7テスト含む）
- 動的テスト:
  1. `check_health()` backend未起動→False ✅
  2. タイムアウト→sys.exit(1) + ERRORログ確認 ✅
  3. 遅延スタート（collector先行20s→backend起動）→attempt 9でready検出・exit 0 ✅
  4. backend起動済み→即時成功（attempt 1）✅
- コード品質: 必須違反0件、型ヒント完備（wait_for_backend→bool、check_health→bool）
- Secrets未設定時: ValueError→stderr出力→sys.exit(1)で分かりやすくFAIL（推奨事項として評価）
- 技術メモ:
  - health_wait.py: `time.monotonic()` でdeadline管理、`min(poll_interval, remaining)` でオーバーシュート防止
  - config.py `_require()`: 空文字列もエラー扱い（GitHub Secretsの未設定展開に対応）
  - Dockerfile: requirements.txt + requirements-dev.txt 両方インストール（pytestをコンテナ内で実行可能）

## Sprint 20 評価 (2026-10-03) - 2回目: 合格

- 対象: FEAT-021（backendコンテナ化・CORS・HTTPS・マイグレーション自動適用）
- 結果: **合格**
- ビルド/テスト: backend 287/287 全合格
- 修正確認:
  1. PORT 未設定時の UseUrls 無条件呼び出し修正: Program.cs L20-24 `if (!string.IsNullOrEmpty(port))` で条件分岐 ✅
     - 動的確認: `dotnet run --urls :5000` で :5000 LISTEN + health 200 確認
  2. ForwardedHeaders KnownNetworks/KnownProxies クリア: Program.cs L188-189 ✅
     - コードレビュー確認 + Docker PORT=9000 動作確認
- 後片付け: 全テストコンテナ停止・ポート5000/8080/9000 非リッスン確認

## Sprint 19 評価 (2026-10-03)

- 対象: FEAT-020（frontendのAPIベースURL環境変数化・SPAルーティング）
- 結果: 合格
- ビルド/テスト: frontend 216/216
- 動的テスト: Viteプロキシ経由API呼び出し・登録→ダッシュボード遷移・SPA直接アクセス（/bookmarks）・ナビゲーションバー確認 すべて正常
- コード品質: 必須違反0件、🟡推奨違反1件（末尾スラッシュ未対応 - 上限3件以内）
- 実装: frontend/src/api/client.ts（getApiBase 関数）、frontend/public/_redirects（`/* /index.html 200`）

## Sprint 18 評価 (2026-10-03)

- 対象: FEAT-019（共通スタイル・ナビゲーション統一）
- 結果: 合格 ※Sprint 18 eval記録参照

## Sprint 17 評価 (2026-10-03)

- 対象: FEAT-018（日付範囲フィルタのJST基準化）
- 結果: 合格
- ビルド/テスト: frontend 193/193
- 動的テスト: JST境界確認（news/programs両方）、Playwright UIテストで+09:00オフセット付きAPIリクエスト確認
- コード品質: 必須違反0件
- 修正箇所: DateRangeFilter.tsx（+09:00文字列テンプレート）、NewsFeedList.tsx / ProgramFeedList.tsx（JST文字列を直接 getNews/getPrograms に渡す）

## Sprint 16 評価 (2026-10-03)

- 対象: FEAT-017（UTC以外のオフセット付き日時での500エラー修正）
- 結果: 合格
- ビルド/テスト: backend 269/269
- 動的テスト: POST /internal/news,programs,job-logs（+09:00）各200・UTC正変換確認、GET /news?from,programs?to（+09:00）各200確認
- コード品質: 必須違反0件、ToUniversalTime()はServices層のみ
- 修正箇所: FeedService（from/to両フィルタ）、InternalCollectorService（publishedAt/airedAt/collectedAt）、CollectorJobLogService（executedAt）

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
FEAT-016（ニュースブックマーク）: Sprint 15で実装済み。
FEAT-017（UTC以外オフセット付き日時500エラー修正）: Sprint 16で実装済み。
FEAT-018（日付範囲フィルタJST基準化）: Sprint 17で実装済み。

## 技術的メモ

- Npgsql UTC正規化: オフセット付きDateTimeOffset（+09:00等）をtimestamptzカラムに書き込む前に.ToUniversalTime()必須。クエリパラメータ(from/to)も同様。Services層で一元処理
- FeedService: 日付範囲フィルタは `n.PublishedAt != null && n.PublishedAt >= from.Value` パターン（nullアイテムはフィルタ適用時に除外）
- DateRangeFilter.tsx: ISO変換 from=T00:00:00Z、to=T23:59:59.999Z。ローカルバリデーションは YYYY-MM-DD 文字列比較
- FeedController: to < from は Service呼び出し前に400 Bad Request早期リターン
- CollectorController: ユーザー向け GET /status は `[Authorize]` (JWT)、内部 POST /job-logs は `ApiKeyAuthFilter`
- FeedService: oshiId フィルタ時、ユーザーの推し一覧でバリデーションしてから絞り込む（他ユーザーの推しIDを指定されても空配列返却）
- DateRangeFilter.tsx (Sprint 17): new Date() 非使用。`${date}T00:00:00+09:00` / `${date}T23:59:59.999+09:00` の文字列テンプレートでJSTオフセット付き文字列を構築。実行環境のタイムゾーン依存を排除。FEAT-017（Sprint 16）のToUniversalTime()と連携してDB比較が正常動作
- DashboardPage: useSearchParams で URL クエリパラメータを読み書き、NewsFeedList/ProgramFeedList に key prop で強制再マウント
- collector requirements-dev.txt に pytest・responses が分離されている（requirements.txt は runtime のみ）
- FeedService.GetUnreadCountAsync: LastViewedFeedAt=null（未閲覧）は 0 を返す。mark-as-read でリセット後も画面滞在中はバッジ件数を維持する設計
- NotificationBadge: role="status" + aria-label でアクセシビリティ対応
- getApiBase (Sprint 19): VITE_API_BASE_URL 未設定 → `/api/v1`、設定時 → `${VITE_API_BASE_URL}/api/v1`。末尾スラッシュのトリミングは未実装（仕様外、推奨違反として記録）
- _redirects (Sprint 19): `frontend/public/_redirects` に `/* /index.html 200` でCloudflare Pages SPA対応。npm run build でdistにコピーされる
