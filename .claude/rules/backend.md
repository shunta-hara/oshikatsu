# バックエンドルール（backend/, C# ASP.NET Core）

## レイヤー構成
- `Controllers/` → `Services/` → `Repositories/`（または `DbContext` 直接利用）の一方向依存を守る。Controller にビジネスロジックを書かない
- DTO（リクエスト/レスポンス用）と Entity（EF Core モデル）を分離する。Entity をそのまま API レスポンスとして返さない

## 非同期処理
- I/O を伴うメソッドは `async`/`await` を使い、`Task`/`Task<T>` を返す。同期メソッド内から `.Result` や `.Wait()` を呼ばない（デッドロックの原因になる）

## DB / EF Core
- スキーマ変更は必ずマイグレーション（`dotnet ef migrations add`）を通す。DB を直接手で変更しない
- クエリは N+1 を避ける（`Include`/射影を適切に使う）

## エラーハンドリング
- API キー・接続文字列などの秘匿情報をコードにハードコードしない（`appsettings.json` の秘密情報は `appsettings.Development.json`（gitignore対象）か User Secrets / 環境変数で管理する）
- 例外は握りつぶさず、適切な HTTP ステータスコードとエラーメッセージを返す

## 命名規則
- C# 標準の命名規則に従う（クラス/メソッド: PascalCase、ローカル変数/引数: camelCase、インターフェース: `I` プレフィックス）
