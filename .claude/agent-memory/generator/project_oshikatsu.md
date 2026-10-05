---
name: project-oshikatsu
description: oshikatsu プロジェクトの実装状況と開発環境の注意点
metadata:
  type: project
---

# oshikatsu プロジェクト状況

Sprint 1-37 実装完了（Sprint 37 マージ衝突解消: 2026-10-05）。

**Why:** backend/ディレクトリが存在しなかった新規プロジェクト。

## 実装済み内容

### collector/（Sprint 23）
- **一括送信（FEAT-025）**: `post_news_batch` / `post_program_batch` メソッド追加。推し1人分を1回のAPIコールで送信。
- **単一推し収集モード（FEAT-025）**: `OSHI_ID` 環境変数で特定の推しのみ収集可能。非整数は config でエラー終了。存在しない ID は警告ログ + 正常終了。
- **番組重複フィルタ（FEAT-024）**: `PROGRAM_KEYWORDS` 定数による候補絞り込み。同実行のニュース URL との重複除外。
- **workflow 更新（FEAT-025）**: `workflow_dispatch` に `oshi_id` 入力追加。concurrency グループで全体実行と特定推し実行を分離。

### backend/（Sprint 1-6, 10-16, 20, 22, 24-25）
- `backend/OshikatsuApi.sln` — ソリューションファイル
- `backend/OshikatsuApi/` — ASP.NET Core 10 Web API プロジェクト
  - EF Core + Npgsql で PostgreSQL 接続
  - 5エンティティ: User / Oshi / NewsItem / ProgramAppearance / Bookmark
  - `GET /api/v1/health` エンドポイント
  - **Swagger 公開制御（Sprint 25/FEAT-027）**: Development は常に公開、Production では `ENABLE_SWAGGER=true` のときのみ公開。`SwaggerHelper.IsEnabled(envName, envVar)` で判定。
  - JWT 認証: `POST /api/v1/auth/register`, `POST /api/v1/auth/login`, `POST /api/v1/auth/logout`
  - 推し管理: `POST/GET /api/v1/oshis`, `DELETE /api/v1/oshis/{id}`
  - collector 内部API: `POST /api/v1/internal/news`, `POST /api/v1/internal/programs`（APIキー認証）
  - フィードAPI: `GET /api/v1/news`（oshiId・q・from・to クエリパラメータ対応）, `GET /api/v1/programs`（oshiId・from・to 対応）
  - **大文字小文字を区別しない検索（Sprint 25/FEAT-027）**: FeedService の keyword search で `n.Title.ToLower().Contains(query.ToLowerInvariant())` を使用。Npgsql は `lower()+strpos` に変換、InMemory は C# の string.Contains() をそのまま使用。EF Core が Contains() の `%`・`_` を自動エスケープするため、ProviderName 分岐も手動エスケープも不要。
  - 未読API: `GET /api/v1/feed/unread-count`, `POST /api/v1/feed/mark-as-read`
  - 収集ジョブ: `GET /api/v1/collector/status`, `POST /api/v1/internal/jobs`
  - **ブックマークAPI**: `POST /api/v1/news/{id}/bookmark`（登録）, `DELETE /api/v1/news/{id}/bookmark`（解除）, `GET /api/v1/bookmarks`（一覧）
  - **CORS 設定済み**: `http://localhost:5173` を許可（FrontendCorsPolicy）
  - **一括登録API（Sprint 22/FEAT-023）**: `POST /api/v1/internal/news/batch`, `POST /api/v1/internal/programs/batch`（APIキー認証）
  - **FEAT-024 番組重複排除**: 番組一括・単件保存時にニュースの既存 sourceUrl をスキップ。FeedService・DashboardService で番組一覧からニュースと重複する番組を除外
  - **FEAT-026 即時収集 dispatch（Sprint 24）**: `IGitHubDispatchService` / `GitHubDispatchService` 追加。推し登録後に fire-and-forget で GitHub Actions workflow_dispatch を起動。env: GITHUB_DISPATCH_TOKEN / GITHUB_REPOSITORY / GITHUB_WORKFLOW_FILE / GITHUB_REF。未設定時はスキップ。失敗時も 201 を返す。トークンはログに出さない。
  - **Sprint 35 / FEAT-037**: talents / user_oshis テーブル新設。NewsItem / ProgramAppearance に talent_id (nullable FK) 追加。マイグレーション SQL で既存 oshis → talents + user_oshis 変換、news_items / program_appearances の talent_id 設定、同一 talent_id + source_url 重複の最古1件残し削除を実行。
  - **Sprint 36 / FEAT-038**: タレント名正規化ロジック実装。`TalentNameNormalizer.Normalize(string name)` が NFKC → 小文字 → 空白除去を実行（`Services/TalentNameNormalizer.cs`）。`ITalentService`/`TalentService` に `FindOrCreateTalentAsync(string name)` を追加（`Services/TalentService.cs`）。Program.cs に `ITalentService` を DI 登録済み。新規テスト 31件（TalentNameNormalizerTests 15件 + TalentServiceTests 16件）。
  - **Sprint 38 / FEAT-040**: 内部 API を talentId ベースへ変更。InternalOshiDto.TalentId / CreateNewsItemRequestDto.TalentId / CreateProgramAppearanceRequestDto.TalentId / BatchCreateNewsRequestDto.TalentId / BatchCreateProgramsRequestDto.TalentId / InternalNewsResponseDto.TalentId / InternalProgramResponseDto.TalentId。OshiId を nullable（int?）に変更（NewsItem・ProgramAppearance）。GitHubDispatchService: dispatch inputs を `oshi_id` → `talent_id` に変更。マイグレーション 20261005110000_MakeOshiIdNullable.cs を追加。FeedService / DashboardService / BookmarkService / ArticleDeduplicator を nullable OshiId 対応に更新。
  - **Sprint 37 マージ解消**: FeedService / DashboardService / BookmarkService を talent_id ベースに統一（sprint-38 merge）。FeedServiceProgramDeduplicationTests の cross-oshi テストを talent_id モデルに更新（Talent + UserOshi を直接作成する形に変更）。
- `backend/OshikatsuApi.Tests/` — xUnit テストプロジェクト（564テスト合格 as of Sprint 37マージ解消、6テストは NewsCleanupService の ExecuteDeleteAsync InMemory 非対応による既存失敗）

### frontend/（Sprint 7-15, 18-20, 24-25）
- `frontend/` — Vite + React + TypeScript プロジェクト
  - react-router-dom でルーティング。未認証は `/auth` へリダイレクト
  - `src/types/auth.ts`, `src/types/oshi.ts`, `src/types/feed.ts`, `src/types/collector.ts`, `src/types/bookmark.ts` — zod スキーマ + 型定義
  - `src/api/client.ts` — fetch ラッパー（ApiError クラス、Bearer トークン自動付与）
  - **VITE_API_BASE_URL 末尾スラッシュ修正（Sprint 25/FEAT-027）**: `baseUrl.replace(/\/+$/, "")` で末尾スラッシュを除去してから `/api/v1` を付加。
  - `src/api/auth.ts`, `src/api/oshi.ts`, `src/api/feed.ts`, `src/api/collector.ts`, `src/api/bookmark.ts` — API 関数
  - `src/components/feed/BookmarkButton.tsx` — ブックマークトグルボタン（Sprint 15 追加）
  - `src/pages/BookmarkPage.tsx` — ブックマーク一覧ページ（Sprint 15 追加）
  - `/bookmarks` ルート追加（Sprint 15）
  - **NavBar（Sprint 18）**: 認証済み全画面共通ナビゲーションバー（ダッシュボード・ブックマーク・ログアウト）
  - **main.tsx に `index.css` import 追加（Sprint 18）**: CSS が一切適用されていなかった根本原因を修正
  - **FEAT-026 成功メッセージ（Sprint 24）**: OshiForm で推し追加成功後に「登録しました。数分後にニュースが表示されます」を表示
  - **FEAT-024 表記変更（Sprint 24）**: 画面上「出演番組」→「出演情報」に変更（ProgramFeedList・DashboardPage の見出し・空状態メッセージ・aria-label）
  - Vitest + RTL テスト 219件合格

## 環境の注意点

- **dotnet SDK インストール済み** — `export PATH="$PATH:/c/Users/shunt/.dotnet/tools"` で利用可能
- **backend ビルド前に既存プロセス停止が必要**: `taskkill //F //IM OshikatsuApi.exe 2>/dev/null || true`
- サーバー起動は `ASPNETCORE_ENVIRONMENT=Development dotnet run --project backend/OshikatsuApi --no-build`（Development mode 必須）
- backend デフォルト HTTP ポート: 5000（Vite proxy は `http://localhost:5000` に転送）
- `appsettings.Development.json` は `.gitignore` 対象（DB接続文字列を含むため）
- CollectorSettings.ApiKey = `oshikatsu_dev_collector_api_key`（開発環境）
- Python はローカル未インストール。collector テストは Docker 経由
- PostgreSQL container: `oshikatsu-postgres-1:5432`、接続文字列: `Host=localhost;Port=5432;Database=oshikatsu;Username=oshikatsu;Password=oshikatsu_dev`

## 重要な実装メモ

- **DateTimeOffset 使用**: NewsItem / ProgramAppearance の日時フィールドは `DateTimeOffset` を使用（DateTime だと Python の `+00:00` フォーマットが Npgsql に弾かれる）
- **日付範囲フィルタ**: `GET /api/v1/news?from=...&to=...` は PublishedAt で絞り込む。`GET /api/v1/programs?from=...&to=...` は AiredAt で絞り込む。
- **frontend テスト**: `vi.clearAllMocks()` は `mockOnce` キューをクリアしない。テスト間のモック汚染を防ぐには `vi.resetAllMocks()` を使う
- **loading 状態テスト**: `userEvent` は内部で全マイクロタスクを flush するため async handler の中間状態テストには `fireEvent` が適切
- **isBookmarked**: `NewsItemResponseDto` は `bool? IsBookmarked` (nullable)。frontend schema は `z.boolean().optional()` で既存テストモックへの影響なし
- **BookmarkService.GetBookmarksAsync**: `Include(b => b.NewsItem).ThenInclude(n => n.Oshi)` を使用。InMemory DB でも動作する
- **CSS 未適用問題（Sprint 18）**: `main.tsx` に `import "./index.css"` がなく CSS が一切当たっていなかった。NavBar 追加時に `main.tsx` にインポート追加で解決
- **NavBar の logout テスト互換性**: DashboardPage テストが `vi.mock("../api/auth")` でモックすると NavBar の logout も同じモックが適用される（同一モジュールのため）。NavBar を DashboardPage 内にレンダリングすれば既存テストに変更不要
- **複数 navigation ランドマーク問題**: NavBar と FeedPagination が同じページに共存すると `getByRole("navigation")` が複数マッチして失敗。BookmarkPage.test.tsx の該当テストを `getByRole("navigation", { name: "ページネーション" })` に修正必要
- **FeedService keyword search**: `n.Title.ToLower().Contains(query.ToLowerInvariant())` を使うこと。`EF.Functions.ILike` や ProviderName 分岐・EscapeILikePattern は不要。Npgsql は `lower()+strpos` に自動変換し、EF Core の Contains() が `%`・`_` を自動エスケープする。
- **SwaggerHelper**: `OshikatsuApi.Infrastructure.SwaggerHelper.IsEnabled(envName, enableSwaggerEnv)` で Swagger 公開可否を判定。テスト可能な静的メソッドに分離済み。
- **VITE_API_BASE_URL 末尾スラッシュ**: `client.ts` の `getApiBase()` で `replace(/\/+$/, "")` により複数の末尾スラッシュも除去
- **FeedServiceTests.cs の追記ルール**: 既存 55 テストを保持したまま Sprint 25 の新テストを末尾に追加する。ファイルを上書き・置換しない。

**How to apply:** Sprint 16 以降も `vi.resetAllMocks()` を使う。ブックマーク関連の型は `types/bookmark.ts` に集約済み。NavBar を含む認証済みページのテストには AuthProvider + MemoryRouter が必要。
