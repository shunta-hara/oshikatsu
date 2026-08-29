現在の差分（`git diff`）をレビューする。触っている層（frontend/backend/collector）のルールファイルを確認したうえでレビューする。

## 観点

- `.claude/rules/architecture.md` の役割分担（frontend は DB 直叩き禁止、backend がデータモデルの正本、等）を破っていないか
- frontend: `any` 型の使用、コンポーネントとロジックの分離、エラーハンドリング漏れ
- backend: 層分離（Controller にロジック直書きしていないか）、DTOとEntityの混同、秘匿情報のハードコード、非同期処理の誤用（`.Result`/`.Wait()`）
- collector: タイムアウト・リトライの有無、robots.txt/利用規約への配慮、サイレント失敗
- テストが正常系・異常系・境界値をカバーしているか

## 出力

- 重大度（🔴必須修正 / 🟡推奨）付きで、ファイル:行番号・問題点・修正案を列挙する
