backend（ASP.NET Core）に新しい API エンドポイントを追加する。

## 手順

1. `.claude/rules/backend.md` と `.claude/rules/architecture.md` を確認する
2. 必要な Entity / DTO を確認・追加する（Entity と DTO は分離すること）
3. Repository/Service にロジックを実装する（Controller に直接ロジックを書かない）
4. Controller にエンドポイントを追加し、適切な HTTP メソッド・ステータスコードを使う
5. Swagger（OpenAPI）定義に反映されることを確認する（`dotnet run` 後 `/swagger` で確認）
6. xUnit でテストを追加する（正常系・異常系）
7. `dotnet build backend && dotnet test backend` で確認する
8. frontend/collector からこの API を利用する箇所があれば、契約変更がないか確認する
