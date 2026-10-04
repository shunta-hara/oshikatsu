---
name: project-oshikatsu-sprints
description: oshikatsuプロジェクトのスプリント評価履歴と最終状態
metadata:
  type: project
---

Sprint 32 評価：合格（1回目）。FEAT-035（転載重複まとめ backend）・FEAT-036（媒体名表示・文字参照修正）。backend 473/473・collector 194/194。ArticleDeduplicator が転載ロジックと RepostMediaNames を1か所に集約。マイグレーション自動適用確認済み。

**How to apply:** Sprint 32 合格。次は Sprint 33（転載重複まとめ・媒体名の frontend 実装 FEAT-035/036 frontend 部分）。

---

Sprint 31 評価：合格（2回目）。FEAT-034（Cloudflare Workers 定期起動）。

**Why:** 1回目は ScheduledEvent→ScheduledController 型エラーで不合格。2回目で修正済み。tsc 0エラー、vitest 15/15、wrangler dry-run 成功。動的テスト: `/__scheduled` → 空トークン → 401 受信 → 非リトライ → エラーログ確認。GITHUB_DISPATCH_TOKEN は wrangler.toml [vars] に存在せず（secret）。AbortSignal.timeout タイムアウト実装・テスト3件確認済み。

**How to apply:** Sprint 31 合格。FEAT-034 完了。全スプリント（1〜31）完了。

---

Sprint 30 評価：合格（1回目）。259/259 全テスト通過。FEAT-033（番組表リンク）すべて確認済み。

**Why:** Sprint 30 は FEAT-033（推しごとの番組表検索リンク）。tvGuideUrls.ts + TvGuideLinks.tsx のみ。推し選択時のみ出演情報列に表示。Gガイド・J:COM 両リンクとも target="_blank" / rel="noopener noreferrer"、URLエンコード正常。backend/collector 変更なし。

**How to apply:** Sprint 30 合格。全スプリント完了（FEAT-001〜033）。

---

Sprint 29 評価：合格（1回目）。233/233 全テスト通過。FEAT-032/031(frontend) すべて確認済み。

**Why:** Sprint 29 は FEAT-032（ニュース/出演情報 2カラムレイアウト）・FEAT-031 frontend（ブックマーク絞り込みUI）。feed-two-col が 1024px+ で2列グリッド（computed: 453px 453px）、<1024px でタブ切り替え。role="tablist"/role="tab"/aria-selected 完備。

**How to apply:** Sprint 29 合格。次は Sprint 30（推しごとの番組表検索リンク FEAT-033）。

---

Sprint 28 評価：合格（1回目）。417/417 全テスト通過。FEAT-029/030/031 すべて確認済み。

**Why:** Sprint 28 は FEAT-029（掲載日降順）・FEAT-030（画像箱除外）・FEAT-031（ブックマーク絞り込み）。GalleryTitleDetector.cs（新規）をメモリフィルタとして利用。FeedService は全件 ToListAsync() 後にメモリフィルタ → ページングする設計（将来パフォーマンス課題だが仕様外）。

**How to apply:** Sprint 28 合格。次は Sprint 29（FEAT-032 2カラムレイアウト）。

---

Sprint 27 評価：合格（2回目）。219/219 全テスト通過・ビルド正常。OshiCard.tsx の FEAT-024 表記変更漏れ修正確認済み。

**Why:** Sprint 27 は FEAT-028 Sprint B（ダッシュボード・ブックマーク一覧のカード型レイアウト）。1回目は OshiCard.tsx「最新番組」「番組情報なし」が未修正で不合格。2回目で4箇所すべて「出演情報」に修正・テスト期待値も追随更新済み。

**How to apply:** Sprint 27 合格。次は Sprint 28 へ。

---

Sprint 26 評価：合格（1回目）。219/219 全テスト通過。FEAT-028 Sprint A 全完了条件確認済み。

**Why:** Sprint 26 は FEAT-028 Sprint A（デザイントークン・共通コンポーネント・NavBar・認証画面カード化）。App.css 削除、Vite テンプレート名残除去、ローズ系デザイントークン一元管理、コントラスト比 4.5:1 以上確認済み。

**How to apply:** Sprint 26 合格。次は Sprint 27（Webデザイン刷新 Sprint B — ダッシュボード・ブックマーク一覧のカード型レイアウト）。

## Sprint 26 評価 (2026-10-04): 合格（1回目）

- 対象: FEAT-028 Sprint A（Webデザイン刷新 基盤）
- 結果: **合格**
- ビルド: 0エラー
- テスト: 219/219 PASS

### 動的テスト
- App.css 削除・参照なし ✅
- Vite テンプレート名残なし（aa3bff, --code-bg, --social-bg, prefers-color-scheme） ✅
- #root 固定幅なし（min-height + flex のみ） ✅
- トークン :root 一元管理 ✅
- 認証画面カード中央配置（スクリーンショット確認） ✅
- NavBar（ダッシュボード・ブックマーク・ログアウト・active クラス）✅
- キーボードフォーカス: ボタン 2px solid rose, NavBar リンクはブラウザデフォルト（可視） ✅
- 375px 横スクロールなし ✅

### コントラスト比（計算検証）
- accent (#c0375f) on white: 5.34:1 ✅（CSS クレーム 4.93:1 より高め）
- text (#1a0d12) on bg (#fff5f7): 17.83:1 ✅
- text-muted (#5c4d55) on white: 7.94:1 ✅（CSS クレーム 6.77:1 より高め）
- danger (#c0392b) on white: 5.44:1 ✅（CSS クレーム 5.05:1 より高め）

### 技術メモ
- index.css: #fff0ef, #f0faf4 など error/success 背景色が :root 外に直書き（Sprint B で整理余地）
- NavBar <a> に :focus-visible カスタムスタイルなし（ブラウザデフォルト適用、Sprint B で補強推奨）

---

Sprint 25 評価：合格（2回目）。360/360 テスト通過。FEAT-027 全項目確認済み。

**Why:** Sprint 25 は FEAT-027（trailing slash/case-insensitive/Swagger Production 非公開）。1回目は FeedServiceTests.cs 既存39件削除で不合格。2回目で HEAD 復元+5件追加(360件)で合格。

**How to apply:** Sprint 25 合格。次は Sprint 26（Webデザイン刷新 Sprint A）。

## Sprint 25 評価 (2026-10-03): 不合格（1回目）

- 対象: FEAT-027（細かな不具合・設定の修正）
- 結果: **不合格**
- ビルド: 0エラー
- テスト: 316/316 PASS（既存 39 件削除が問題）
- 🔴 必須違反: FeedServiceTests.cs から 39 メソッド削除（HEAD: 55件 → 作業ツリー: 16件）
- 🟡 推奨違反 2件: (1) DB プロバイダ分岐を本番コードに入れる設計 (2) EscapeILikePattern が PostgreSQL で機能しない（Npgsql が ESCAPE '' を生成）

### 動的確認（完了分）
- Development Swagger: 200 ✅
- 大文字小文字区別なし（NHK/nhk）: 同じ結果 ✅
- VITE_API_BASE_URL 末尾スラッシュ除去: コード・テスト確認 ✅

Sprint 24 評価完了（合格、1回目）。FEAT-026（推し登録直後即時収集）・FEAT-024（frontend表記変更）両方合格。backend 342/342・frontend 217/217 全テスト通過。

**Why:** Sprint 24はFEAT-026+FEAT-024(frontend)。GitHubDispatchService singleton + fire-and-forget 設計確認済み。

**How to apply:** Sprint 24合格。次は Sprint 25（細かな不具合・設定の修正 FEAT-027）。

## Sprint 24 評価 (2026-10-03): 合格（1回目）

- 対象: FEAT-026（推し登録直後の即時収集）・FEAT-024（frontend 表記変更）
- 結果: **合格**
- ビルド: 0エラー
- テスト: backend 342/342, frontend 217/217

### 動的テスト
- GITHUB_DISPATCH_TOKEN 未設定 → POST /api/v1/oshis 201・info log「スキップします」✅
- ダミートークン+存在しないリポジトリ → 201 即座返却（fire-and-forget）→背後で 401・warn log（トークンなし）✅
- fire-and-forget: GitHubDispatchService singleton、DbContext非依存、全例外キャッチ ✅

### frontend 表記変更
- 「出演番組」→「出演情報」: DashboardPage・ProgramFeedList（aria-label・空状態）✅
- 「放送日（時）」→「掲載日」: git 歴史確認。元から UI に「放送日」ラベルなし（time要素のみ）→ 0件変更で trivially satisfied ✅
- BookmarkPage: 「出演番組」なし ✅

### deploy-guide.md
- 手順 8 追加（fine-grained PAT 作成手順・Render 環境変数登録手順）✅
- 旧手順 8 → 手順 9 に繰り下がり、番号崩れなし ✅

## Sprint 23 評価 (2026-10-03): 合格（1回目）

- 対象: FEAT-025（collector一括送信・単一推し収集モード）・FEAT-024（番組重複のcollector対策）
- 結果: **合格**
- ビルド: docker build 成功（CACHED）
- テスト: 167/167 PASS

### 全動的テスト
- OSHI_ID=abc → exit 1 + stderr "Configuration error" ✅
- OSHI_ID=9999 → WARNING log + exit 0 ✅
- OSHI_ID=1 → 1推しのみ収集・ジョブログ2件送信 ✅
- batch POST直接テスト: `{"receivedCount":1,"savedCount":1,"skippedCount":0}` ✅
- collect.yml: cron 6時間、env 経由 OSHI_ID、concurrency (collect-all / collect-oshi-{id}) ✅

### 技術メモ
- program_collector.py: `PROGRAM_KEYWORDS` リストは L21 の1か所で管理
- main.py: `news_urls_by_oshi` dict を run_news_collection → run_program_collection へ渡す設計
- collector/collector/__main__.py 存在確認済み（python -m collector 動作）

## Sprint 22 評価 (2026-10-03): 合格（2回目）

- 対象: FEAT-023（一括登録API）・FEAT-024（番組重複のbackend対策）
- 結果: **合格**
- ビルド: 成功（0エラー）
- テスト: 332/332 PASS

### 修正確認
- FIX 1: `CreateProgramAppearanceAsync` に news URL チェック追加（id:0, wasDuplicate:true で返す）✅
- FIX 2: `DbUpdateException` catch を `IsUniqueConstraintViolation` (SqlState==23505) に絞る ✅

### 23505 でスキップ時の挙動（受け入れ可能）
- 23505 発生時は同バッチ内の非衝突アイテムも全スキップ扱いになる
- 仕様は「concurrent で 500 にならない（片方はスキップ扱い）」のみ要求→満たしている
- collector リトライで次回収集可能なため受け入れ可能

### 全動的テスト
- POST /news/batch, /programs/batch 正常系・400/404・重複スキップ ✅
- programs/batch が同推し news URL をスキップ（FEAT-024） ✅
- 単件 programs API が news URL をスキップ（FEAT-024 FIX 1） ✅
- programs 一覧・dashboard で news 重複 URL の番組除外 ✅
- Swagger に batch エンドポイント反映 ✅
- 並列3リクエストで 500 にならない ✅

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
