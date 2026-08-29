# テストルール

## frontend（React + TypeScript）
- テストフレームワーク: Vitest + React Testing Library
- コンポーネントの正常系・異常系（エラー表示）・境界値（空リスト等）をカバーする
- 実行: `npm run test --prefix frontend` または `npm test`（frontend/ 配下）

## backend（C# ASP.NET Core）
- テストフレームワーク: xUnit
- Controller/Service は正常系・異常系（バリデーションエラー・404等）を最低限カバーする
- 実行: `dotnet test backend`

## collector（Python）
- テストフレームワーク: pytest
- 外部HTTPリクエストは実際には叩かず、モック（`responses`/`unittest.mock`）を使う
- リトライ・タイムアウト・パースエラー時の挙動をテストする
- 実行: `pytest collector`

## 共通
- 新規機能追加時はテストを同時に書く（後回しにしない）
- テストがないままの `git commit` は避ける
