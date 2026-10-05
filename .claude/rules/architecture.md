# アーキテクチャルール（モノレポ全体）

## ディレクトリ構成
```
oshikatsu/
├── frontend/   # React + TypeScript（Vite）
├── backend/    # C# ASP.NET Core Web API（メインAPI・認証・DB操作）
├── collector/  # Python（出演番組・ニュース収集バッチ）
├── scheduler/  # Cloudflare Workers（Cron Triggers で collector の定期起動を担当）
├── docs/       # 仕様書（spec-*.md）・設計メモ
└── .claude/    # Claude Code ハーネス
```

## 役割分担（厳守）
- **backend**: 推し・出演番組・ニュースのデータモデルの正本。API・認証・DB操作を一手に担う
- **frontend**: backend の API のみを叩く。DB に直接アクセスしない
- **collector**: 番組表・ニュースソースから情報を収集し、backend 経由（または合意した方式）でDBに反映する。フロントエンドのロジックを持たない
- **scheduler**: Cloudflare Workers の Cron Triggers から (1) GitHub の workflow_dispatch API を呼び出して collector を定期起動する（2時間ごと）、および (2) backend の `DELETE /api/v1/internal/news/expired` エンドポイントを呼び出して古い記事を定期削除する（毎週月曜 0:00 UTC）。データ処理・収集ロジックは持たない

## API契約
- backend が提供する API は OpenAPI（Swagger）で定義・公開する
- frontend/collector はこの契約に従う。契約を破る変更をする場合は3者（frontend/backend/collector）すべての影響箇所を確認する

## 新規スプリントで守ること
- スプリントが複数の層（frontend/backend/collector）にまたがる場合、backend のデータモデル → API → frontend/collector の順に実装する（依存の下流から上流へ）
