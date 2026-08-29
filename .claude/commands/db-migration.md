backend（EF Core / PostgreSQL）のマイグレーションを作成・適用する。

## 手順

1. Entity クラス（`backend/` 配下のモデル）の変更を確認する
2. マイグレーションを作成する
   ```bash
   dotnet ef migrations add <MigrationName> --project backend
   ```
3. 生成されたマイグレーションファイルの内容を確認する（意図しない列削除・型変更がないか）
4. ローカルDBに適用する
   ```bash
   dotnet ef database update --project backend
   ```
5. 既存データに影響する変更（NOT NULL列の追加等）がある場合は、デフォルト値やバックフィル方針を検討する
6. collector が直接DBに書き込む方式を採用している場合、スキーマ変更が collector 側の書き込みロジックに影響しないか確認する
