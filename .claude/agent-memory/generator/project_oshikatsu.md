---
name: project-oshikatsu
description: oshikatsu プロジェクトの実装状況と開発環境の注意点
metadata:
  type: project
---

# oshikatsu プロジェクト状況

Sprint 1-7 実装完了（2026-07-14）。

**Why:** backend/ディレクトリが存在しなかった新規プロジェクト。

## 実装済み内容

### backend/（Sprint 1-6）
- `backend/OshikatsuApi.sln` — ソリューションファイル
- `backend/OshikatsuApi/` — ASP.NET Core 10 Web API プロジェクト
  - EF Core + Npgsql で PostgreSQL 接続
  - 4エンティティ: User / Oshi / NewsItem / ProgramAppearance
  - `GET /api/v1/health` エンドポイント
  - Swagger UI (`/swagger`)
  - JWT 認証: `POST /api/v1/auth/register`, `POST /api/v1/auth/login`, `POST /api/v1/auth/logout`
  - 推し管理: `POST/GET /api/v1/oshis`, `DELETE /api/v1/oshis/{id}`
  - collector 内部API: `POST /api/v1/internal/news`, `POST /api/v1/internal/programs`（APIキー認証）
  - **CORS 設定済み**: `http://localhost:5173` を許可（FrontendCorsPolicy）
- `backend/OshikatsuApi.Tests/` — xUnit テストプロジェクト（103テスト合格）

### frontend/（Sprint 7）
- `frontend/` — Vite + React + TypeScript プロジェクト
  - react-router-dom でルーティング。未認証は `/auth` へリダイレクト
  - `src/types/auth.ts` — zod スキーマ + 型定義
  - `src/api/client.ts` — fetch ラッパー（ApiError クラス、Bearer トークン自動付与）
  - `src/api/auth.ts` — register / login / logout API 関数
  - `src/context/AuthContext.tsx` — トークンを localStorage 管理する Context
  - `src/components/ProtectedRoute.tsx` — 認証ガード
  - `src/components/auth/LoginForm.tsx` — ログインフォーム
  - `src/components/auth/RegisterForm.tsx` — 新規登録フォーム
  - `src/pages/AuthPage.tsx` — ログイン/登録タブ切り替えページ
  - `src/pages/DashboardPage.tsx` — ダッシュボード（ログアウトボタン付き）
  - Vitest + RTL テスト 24件合格

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
- **frontend テスト**: `vi.clearAllMocks()` は `mockOnce` キューをクリアしない。テスト間のモック汚染を防ぐには `vi.resetAllMocks()` を使う
- **loading 状態テスト**: `userEvent` は内部で全マイクロタスクを flush するため async handler の中間状態テストには `fireEvent` が適切

**How to apply:** Sprint 8 以降の frontend テストでも `vi.resetAllMocks()` を使う。loading 状態テストは `fireEvent` を使う。
