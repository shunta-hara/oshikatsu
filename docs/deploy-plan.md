# デプロイ方針（無料枠構成）

決定日: 2026-10-03

## 方針
個人利用規模を前提に、**月額 0 円**で運用できる構成にする。常時起動・即応性よりも費用ゼロを優先し、コールドスタートは許容する。

## 構成

| 層 | 配置先 | 無料枠の主な制約 |
|---|---|---|
| frontend（Vite ビルド成果物） | Cloudflare Pages | 静的配信は無制限。ビルド 500 回/月 |
| backend（ASP.NET Core） | Render（Hobby プラン / Web Service, Docker） | 15 分無通信でスリープ、復帰に約 1 分。750 インスタンス時間/月、送信帯域 5 GB/月 |
| DB（PostgreSQL） | Neon（Free プラン） | ストレージ 0.5 GB、100 CU 時間/月、5 分無通信でスケールゼロ。期限なし |
| collector（定期実行） | GitHub Actions（schedule） | private リポジトリで 2,000 分/月 |

```
[ブラウザ] ──静的ファイル──> Cloudflare Pages (frontend)
    │
    └──/api/v1/*──> Render (backend) ──> Neon (PostgreSQL)
                          ▲
GitHub Actions (cron) ────┘  X-Api-Key で内部API経由書き込み
  └─ collector コンテナ
```

## 不採用にした選択肢
- **Render の無料 PostgreSQL**: 作成 30 日で期限切れ（猶予 14 日後に削除）になるため不採用
- **Supabase**: 7 日間アクセスがないとプロジェクトが一時停止し、手動復旧が必要なため不採用
- **Railway**: 恒久的な無料枠がないため不採用
- **Koyeb**: 無料インスタンスは 1 つで、クレジットカード登録が必須なため不採用
- **AWS（ECS Fargate + RDS + EventBridge）**: 無料枠を超えやすく、構築の手間も大きいため不採用

## デプロイに向けてアプリ側で必要な対応
1. **frontend の API 接続先を環境変数化**: 本番では frontend と backend のオリジンが異なる。ビルド時の環境変数で backend の URL を指定できるようにする（開発時は従来どおり Vite プロキシ）
2. **SPA ルーティング**: Cloudflare Pages で `/bookmarks` などに直接アクセスしても `index.html` が返るようにする
3. **backend の CORS 許可オリジンを環境変数で設定**できるようにする
4. **backend のコンテナ化**: Render で動かす Dockerfile を用意する。接続文字列・JWT 秘密鍵・collector API キーはすべて環境変数から読む
5. **マイグレーションの本番適用手段**: 手作業の DB 変更なしで、デプロイ時にマイグレーションが適用される仕組みを用意する
6. **collector の定期実行ワークフロー**: GitHub Actions の schedule で collector を実行する。backend の URL と API キーは GitHub Secrets から渡す
7. **コールドスタート対策**: Render がスリープ中だと、最初のリクエストに約 1 分かかる。collector は収集前にヘルスチェックで backend を起こし、起動を待ってから収集する
8. **HTTPS 前提の設定**: 本番は各サービスが HTTPS を終端する。backend の `UseHttpsRedirection` などが Render のプロキシ環境と矛盾しないことを確認する

## 利用者側で必要な作業（Claude では代行できない）
- Cloudflare / Render / Neon のアカウント作成と、GitHub リポジトリとの連携
- 各サービスへの秘密情報（接続文字列・JWT 秘密鍵・API キー）の登録

## 注意点
- 無料枠の内容は変わることがある（Render は 2026-08-01 に Hobby プランの帯域を 100 GB から 5 GB に縮小した）。費用が発生しそうになったら構成を見直す
- collector の実行間隔は、Actions の分数と収集先への負荷を考え **6 時間ごと** を初期値とする
