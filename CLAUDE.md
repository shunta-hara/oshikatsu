# oshikatsu — Claude 指示書

## プロジェクト概要
推し活をサポートするウェブサービス。推し（アイドル・アーティスト・キャラクターなど）の名前を登録すると、
出演番組やニュースを自動収集して一覧できる。

## 技術スタック
- **frontend/**: React + TypeScript（Vite, npm）
- **backend/**: C#（ASP.NET Core Web API）— メインAPI・認証・DB操作を担当
- **collector/**: Python — 出演番組・ニュースのスクレイピング/収集バッチを担当（定期実行）
- **DB**: PostgreSQL

## ルール
@.claude/rules/frontend.md
@.claude/rules/backend.md
@.claude/rules/collector.md
@.claude/rules/architecture.md
@.claude/rules/testing.md

## エージェント（.claude/agents/）

開発サイクル: **Planner → Generator → Evaluator** の順で使う。

| エージェント名 | 内容 |
|---|---|
| `planner` | アイデア（1〜4行）を詳細な製品仕様書（最大16機能・10スプリント）に展開する |
| `generator` | 仕様書の1スプリントを実装し、自己評価8項目をパスしてからエバリュエーターへ引き渡す |
| `evaluator` | 実装を実際に動かして評価し、合否と具体的な修正指示を出力する |

### いつエージェントサイクルを使うか

**サイクルを起動する（新機能・新スプリント）**:
- 複数ファイルにまたがる新機能の実装（例: 推し登録機能、ニュース収集バッチ）
- 仕様書（`docs/spec-*.md`）に基づくスプリント実行

**直接対応する（サイクルを使わない）**:
- 既存コードの1ファイル以内のバグ修正・小改修
- 設定変更・ドキュメント更新・質問への回答

### サイクルの進め方（オーケストレーター手順）

```
仕様書がない場合:
  → planner を呼ぶ
  → docs/spec-<feature>.md が生成されるまで待つ

実装ループ（全スプリント完了まで繰り返す）:
  1. generator を呼ぶ（sprint=N spec=docs/spec-<feature>.md）
  2. generator から「✅ 実装完了」が返ったら evaluator を呼ぶ（同じ引数）
  3a. evaluator「✅ 合格」→ N を +1 して 1. へ戻る
  3b. evaluator「❌ 不合格」→ 失敗レポートを添えて generator を再呼び出し（sprint=N）
  3c. 同一スプリントで3回連続不合格 → ループを停止しユーザーに報告
```

### エージェント間の情報の渡し方

- generator・evaluator には必ず `sprint=N spec=docs/spec-<feature>.md` を明示する
- evaluator が「❌ 不合格」を返したとき、失敗レポートを**要約せず全文**を generator に渡す
- スプリント番号の管理はオーケストレーター（メイン Claude）が行う

### オーケストレーターの禁止事項

- エージェントサイクル中にメイン Claude が自らコードを実装する
- evaluator を省略して次のスプリントへ進む
- 1回の generator 呼び出しで複数スプリントを実装させる
- 不合格レポートを要約・編集してから generator に渡す

## スキル（スラッシュコマンド）

| コマンド | 内容 |
|---|---|
| `/project:write-test` | テスト作成（フロント/バック/収集バッチいずれか自動判定） |
| `/project:add-api` | backend への ASP.NET Core API エンドポイント追加 |
| `/project:db-migration` | EF Core マイグレーションの作成・適用 |
| `/project:code-review` | コードレビュー |

## 重要な設計メモ
- frontend と backend の間の契約は OpenAPI（Swagger）で管理する。backend 側のエンドポイント変更時は Swagger 定義の更新を忘れないこと
- collector（Python）は backend の DB に直接書き込むか、backend が提供する内部APIを叩くかのどちらか一方に統一する（初回スプリントで方針を決定し `docs/` に記録する）
- 推しの名前・出演番組・ニュースのデータモデルは backend 側（C#/EF Core）が正とする
