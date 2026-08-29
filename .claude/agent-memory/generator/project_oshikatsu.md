---
name: project-oshikatsu
description: oshikatsu プロジェクトの実装状況と開発環境の注意点
metadata:
  type: project
---

# oshikatsu プロジェクト状況

Sprint 1-15 実装完了（2026-08-30）。

**Why:** backend/ディレクトリが存在しなかった新規プロジェクト。

## 実装済み内容

### backend/（Sprint 1-6, 10-15）
- `backend/OshikatsuApi.sln` — ソリューションファイル
- `backend/OshikatsuApi/` — ASP.NET Core 10 Web API プロジェクト
  - EF Core + Npgsql で PostgreSQL 接続
  - 5エンティティ: User / Oshi / NewsItem / ProgramAppearance / Bookmark
  - `GET /api/v1/health` エンドポイント
  - Swagger UI (`/swagger`)
  - JWT 認証: `POST /api/v1/auth/register`, `POST /api/v1/auth/login`, `POST /api/v1/auth/logout`
  - 推し管理: `POST/GET /api/v1/oshis`, `DELETE /api/v1/oshis/{id}`
  - collector 内部API: `POST /api/v1/internal/news`, `POST /api/v1/internal/programs`（APIキー認証）
  - フィードAPI: `GET /api/v1/news`（oshiId・q・from・to クエリパラメータ対応）, `GET /api/v1/programs`（oshiId・from・to 対応）
  - 未読API: `GET /api/v1/feed/unread-count`, `POST /api/v1/feed/mark-as-read`
  - 収集ジョブ: `GET /api/v1/collector/status`, `POST /api/v1/internal/jobs`
  - **ブックマークAPI**: `POST /api/v1/news/{id}/bookmark`（登録）, `DELETE /api/v1/news/{id}/bookmark`（解除）, `GET /api/v1/bookmarks`（一覧）
  - **CORS 設定済み**: `http://localhost:5173` を許可（FrontendCorsPolicy）
- `backend/OshikatsuApi.Tests/` — xUnit テストプロジェクト（255テスト合格）

### frontend/（Sprint 7-15）
- `frontend/` — Vite + React + TypeScript プロジェクト
  - react-router-dom でルーティング。未認証は `/auth` へリダイレクト
  - `src/types/auth.ts`, `src/types/oshi.ts`, `src/types/feed.ts`, `src/types/collector.ts`, `src/types/bookmark.ts` — zod スキーマ + 型定義
  - `src/api/client.ts` — fetch ラッパー（ApiError クラス、Bearer トークン自動付与）
  - `src/api/auth.ts`, `src/api/oshi.ts`, `src/api/feed.ts`, `src/api/collector.ts`, `src/api/bookmark.ts` — API 関数
  - `src/components/feed/BookmarkButton.tsx` — ブックマークトグルボタン（Sprint 15 追加）
  - `src/pages/BookmarkPage.tsx` — ブックマーク一覧ページ（Sprint 15 追加）
  - `/bookmarks` ルート追加（Sprint 15）
  - Vitest + RTL テスト 193件合格

## 環境の注意点

- **dotnet SDK インストール済み** — `export PATH="$PATH:/c/Users/shunt/.dotnet/tools"` で利用可能
- **backend ビルド前に既存プロセス停止が必要**: `powershell.exe -Command "Stop-Process -Name OshikatsuApi -Force -ErrorAction SilentlyContinue"`
- サーバー起動は `ASPNETCORE_ENVIRONMENT=Development dotnet run --project backend/OshikatsuApi --no-build`（Development mode 必須）
- backend デフォルト HTTP ポート: 5000（Vite proxy は `http://localhost:5000` に転送）
- `appsettings.Development.json` は `.gitignore` 対象（DB接続文字列を含むため）
- CollectorSettings.ApiKey = `oshikatsu_dev_collector_api_key`（開発環境）
- Python はローカル未インストール。collector テストは Docker 経由

## 重要な実装メモ

- **DateTimeOffset 使用**: NewsItem / ProgramAppearance の日時フィールドは `DateTimeOffset` を使用（DateTime だと Python の `+00:00` フォーマットが Npgsql に弾かれる）
- **日付範囲フィルタ**: `GET /api/v1/news?from=...&to=...` は PublishedAt で絞り込む。`GET /api/v1/programs?from=...&to=...` は AiredAt で絞り込む。
- **frontend テスト**: `vi.clearAllMocks()` は `mockOnce` キューをクリアしない。テスト間のモック汚染を防ぐには `vi.resetAllMocks()` を使う
- **loading 状態テスト**: `userEvent` は内部で全マイクロタスクを flush するため async handler の中間状態テストには `fireEvent` が適切
- **isBookmarked**: `NewsItemResponseDto` は `bool? IsBookmarked` (nullable)。frontend schema は `z.boolean().optional()` で既存テストモックへの影響なし
- **BookmarkService.GetBookmarksAsync**: `Include(b => b.NewsItem).ThenInclude(n => n.Oshi)` を使用。InMemory DB でも動作する

**How to apply:** Sprint 16 以降も `vi.resetAllMocks()` を使う。ブックマーク関連の型は `types/bookmark.ts` に集約済み。
