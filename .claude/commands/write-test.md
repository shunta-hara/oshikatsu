対象ファイル・機能を確認し、どの層（frontend/backend/collector）に属するかを判定してからテストを作成する。

## 手順

1. 引数または直近の変更から対象ファイルを特定する
2. 層を判定する
   - `frontend/` 配下 → Vitest + React Testing Library。正常系・異常系（エラー表示）・境界値をカバー
   - `backend/` 配下 → xUnit。Controller/Service の正常系・異常系（バリデーションエラー・404等）をカバー
   - `collector/` 配下 → pytest。外部HTTPはモック化し、リトライ・タイムアウト・パースエラーをカバー
3. `.claude/rules/testing.md` の方針に従ってテストを作成する
4. 作成後、該当層のテストコマンドを実行してパスすることを確認する
   - frontend: `npm run test --prefix frontend`
   - backend: `dotnet test backend`
   - collector: `pytest collector`
