# 本番反映手順書（通知・ソース拡充・運用整備 / Sprint 42〜51）

対象: ブランチ `feature/notifications-sources-ops`（FEAT-044〜053）。初回デプロイの手順は [deploy-guide.md](deploy-guide.md) を参照。

**順番が重要です。上から順に進めてください。** 特に手順 3（Render の環境変数）は、手順 5（push）より前に済ませます。順番を逆にすると、新しい backend が起動できずにデプロイが失敗します。

所要時間の目安: 40〜60 分（YouTube を使わない場合は 30 分程度）

## 今回の変更で本番に起きること

| 対象 | 起きること | 自動 / 手動 |
|---|---|---|
| Render（backend） | 新しいマイグレーション 4 本が起動時に適用される | 自動（push で） |
| Cloudflare Pages（frontend） | 通知ボタン・運用パネル・ソース絞り込みが出る | 自動（push で） |
| Cloudflare Workers（scheduler） | cron が 2 本増える（通知 / 容量監視） | **手動**（`wrangler deploy`） |
| GitHub Actions（collector） | YouTube / X / イベントの収集が加わる | 自動（push で） + Secrets の登録 |

適用されるマイグレーション:

1. `AddPushSubscriptions`（`push_subscriptions` テーブル）
2. `AddLastNotifiedAtToPushSubscriptions`（通知の送信済み時刻）
3. `ExtendCollectorJobLogs`（収集ログに失敗理由・タレント ID）
4. `AddTalentSources`（`talent_sources` テーブルと `news_items.source_type`）

どれも**列やテーブルの追加だけ**で、既存データは消えません。

## 作業中に控える値

秘密情報なので、チャットやリポジトリには貼らないでください。

| 名前 | 取得する手順 |
|---|---|
| VAPID 公開鍵 | 手順 2 |
| VAPID 秘密鍵 | 手順 2 |
| YouTube API キー（任意） | 手順 4 |

---

## 手順 1. Neon でバックアップを取る

マイグレーション前の保険です。Neon の無料枠でもブランチを作れます。

1. [Neon コンソール](https://console.neon.tech) → プロジェクト `oshikatsu` → **Branches**
2. **Create branch** を押す
   - Name: `backup-before-notifications`
   - Parent: 本番のブランチ（`main` / `production`）
   - 時点: 現在
3. 作成できたことを確認する

- [ ] バックアップ用ブランチを作った

> ブランチも容量（0.5 GB）に数えられます。反映後に問題がなければ、手順 8 の後で削除してください。

## 手順 2. VAPID 鍵を作る

Web Push の送信に使う鍵ペアです。Git Bash で実行します。

```bash
npx web-push generate-vapid-keys
```

出力される `Public Key` と `Private Key` を控えます。

- **一度決めたら変えないでください。** 変えると、登録済みの通知購読がすべて無効になります（再購読が必要）。
- 秘密鍵は Render と手元の安全な場所（パスワードマネージャーなど）にだけ置きます。

- [ ] 公開鍵と秘密鍵を控えた

## 手順 3. Render に環境変数を追加する（push の前に）

Render の `oshikatsu-api` → **Environment** で、次の 3 つを追加します。

| Key | Value |
|---|---|
| `VapidSettings__PublicKey` | 手順 2 の Public Key |
| `VapidSettings__PrivateKey` | 手順 2 の Private Key |
| `VapidSettings__Subject` | `mailto:自分のメールアドレス` |

- キー名のアンダースコアは **2つ**（`__`）です。
- 保存すると、現在のコードのまま再デプロイが走りますが、無害です（追加の設定項目を読まないだけ）。
- 未設定のまま push すると、新しい backend は起動時のチェックで止まります。

- [ ] 3 つの環境変数を追加した

## 手順 4.（任意）YouTube の収集を有効にする

YouTube を使わない場合は、この手順を飛ばしてかまいません（collector は YouTube 収集を自動でスキップします）。

### 4a. API キーを作る

1. [Google Cloud Console](https://console.cloud.google.com) でプロジェクトを作る（クレジットカードは不要）
2. **APIs & Services → Library** で **YouTube Data API v3** を有効にする
3. **Credentials → Create credentials → API key** でキーを作る
4. 作ったキーの **API restrictions** で、**YouTube Data API v3 のみ**に制限する
5. キーを控える

無料枠は 1 日 10,000 ユニットです。collector は 1 回の収集でチャンネルごとに検索 API（1 回 100 ユニット）を呼ぶため、**チャンネル数 × 100 ユニット × 12 回/日**が消費量です。5 チャンネルで 6,000 ユニット/日になります。10 チャンネルを超える場合は、収集間隔か対象チャンネル数を見直してください。

### 4b. GitHub Secrets に登録する

リポジトリ → **Settings → Secrets and variables → Actions → New repository secret**

| Name | Secret |
|---|---|
| `YOUTUBE_API_KEY` | 4a のキー |

- [ ] （使う場合）`YOUTUBE_API_KEY` を登録した

### X の収集について（原則、設定しない）

X の収集は第三者の公開 RSS プロキシに頼るため、不安定で、規約上もグレーです。**設定しないことをおすすめします。** 設定しなければ X 収集は自動でスキップされます。使う場合のみ、GitHub Secrets に `X_RSS_BASE_URL`（プロキシのベース URL、末尾の `/` なし）を登録します。

## 手順 5. マージして push する

Claude に「`feature/notifications-sources-ops` を master にマージして push して」と頼みます。または自分で実行します。

```bash
git checkout master
git merge feature/notifications-sources-ops
git push origin master
```

push すると次が自動で始まります。

1. Render が backend をビルドして再デプロイする（5〜10 分）。起動時にマイグレーション 4 本が適用される
2. Cloudflare Pages が frontend をビルドする（2〜3 分）

### 確認する

- [ ] Render の **Logs** に「必須設定 '...' が見つからないか空です」が**出ていない**
- [ ] Render のデプロイが **Live** になった
- [ ] `https://oshikatsu-api.onrender.com/api/v1/health` を開くと `status: ok` に加えて DB サイズ（`dbSizeMb`）が表示される
- [ ] Cloudflare Pages のビルドが成功した

デプロイが失敗した場合は「うまくいかないとき」を見てください。

## 手順 6. scheduler（Cloudflare Workers）を再デプロイする

scheduler は push では更新されません。手動で実行します。

```bash
cd scheduler
npx wrangler deploy
npx wrangler secret list
```

- `secret list` に `GITHUB_DISPATCH_TOKEN` と `BACKEND_API_KEY` が表示されることを確認します。`BACKEND_API_KEY` が無ければ登録します。値は Render の `CollectorSettings__ApiKey` と**同じ**です。
  ```bash
  npx wrangler secret put BACKEND_API_KEY
  ```
- Cloudflare ダッシュボード → Workers & Pages → `oshikatsu-scheduler` → **Triggers** に、次の 4 つの cron が並んでいることを確認します。

| cron | 役割 |
|---|---|
| `17 */2 * * *` | collector の起動（既存） |
| `0 0 * * 1` | 古い記事の削除（既存） |
| `47 */2 * * *` | 通知ジョブ（新規） |
| `0 1 * * 1` | DB 容量チェック（新規） |

- [ ] `wrangler deploy` が成功した
- [ ] secret が 2 つ登録されている
- [ ] cron が 4 つ表示されている

## 手順 7. 動作確認

### 7a. 通知（Web Push）

1. frontend を開いてログインし、ダッシュボードのサイドバーの **「通知を受け取る」** を押す
2. ブラウザの許可ダイアログで **許可** する
3. ボタンが **「通知を解除する」** に変わることを確認する
4. 通知ジョブを手動で動かして、通知が届くか確認する
   ```bash
   curl -X POST https://oshikatsu-api.onrender.com/api/v1/internal/push/notify \
     -H "X-Api-Key: <CollectorSettings__ApiKey の値>"
   ```
   `successCount` などを含む JSON が返ります。新着が無い場合は通知は届きません（`skipCount` に数えられます）。新着を作りたい場合は、手順 7c で収集を 1 回走らせてからもう一度実行します。

- スマホ（iPhone）では、**ホーム画面に追加した PWA** でないと Web Push を受け取れません（iOS 16.4 以降）。まず PC のブラウザで確認するのが確実です。
- PC のブラウザで通知が届かない場合は、OS の通知設定（集中モード等）も確認してください。

- [ ] 購読できた
- [ ] 通知が届いた

### 7b. 運用パネルと容量表示

- [ ] ダッシュボードに **運用パネル** が表示される
- [ ] DB 使用量（MB とバー）が表示される
- [ ] 直近 24 時間のジョブ履歴が表示される（collector の実行後）

### 7c. collector

```bash
gh workflow run collect.yml
gh run watch
```

- [ ] 実行が成功した
- [ ] 実行ログに YouTube の「スキップ」または収集件数が出ている（キー未登録なら「YOUTUBE_API_KEY が未設定です。YouTube 収集をスキップします。」）
- [ ] フィードの **ソース絞り込み** で「すべて / ニュース / …」を切り替えられる

### 7d.（YouTube を使う場合）ソースを登録する

ソースの登録 UI はまだありません。内部 API で登録します。`<タレントID>` は DB の `talents.id`（数字）、`<チャンネルID>` は `UC` で始まる YouTube のチャンネル ID です。

```bash
curl -X POST https://oshikatsu-api.onrender.com/api/v1/internal/talent-sources \
  -H "X-Api-Key: <CollectorSettings__ApiKey の値>" \
  -H "Content-Type: application/json" \
  -d '{"talentId": <タレントID>, "sourceType": "youtube", "sourceId": "<チャンネルID>"}'
```

登録後、手順 7c をもう一度実行し、フィードに YouTube の動画が出ることを確認します。登録済みの一覧は `GET /api/v1/internal/talent-sources` で見られます。

- [ ] YouTube の動画がフィードに表示された

## 手順 8. 後片付け

- [ ] 1 日ほど様子を見て問題がなければ、手順 1 のバックアップブランチを Neon で削除する
- [ ] 2 時間おきの cron が動いていることを、Cloudflare の Logs で確認する

---

## うまくいかないとき

| 症状 | 主な原因 | 対処 |
|---|---|---|
| Render のデプロイが失敗し、ログに「必須設定 'VapidSettings:...'」 | 手順 3 の環境変数が未設定、またはキー名の `__` が 1 つ | 環境変数を確認して、**Manual Deploy → Deploy latest commit** |
| Render のログにマイグレーションの失敗が出る | DB 側の問題 | ログのエラーを確認する。戻す場合は下の「ロールバック」 |
| 「通知を受け取る」ボタンが出ない | ブラウザが Push 非対応（iOS の通常タブなど） | PC の Chrome / Edge で試す。iPhone は PWA として追加する |
| 許可ダイアログが出ずに「ブラウザ設定から許可してください」と出る | 通知を過去に拒否している | ブラウザのサイト設定で通知を許可し直す |
| `/push/notify` が 401 | API キーの値が違う | `CollectorSettings__ApiKey` と同じ値を使う |
| 通知が届かない（`failureCount` が増える） | VAPID 鍵の不一致、または購読の失効 | Render の鍵が手順 2 と同じか確認。失効した購読は自動削除されるので、再度購読する |
| YouTube が収集されない | キー未登録、ソース未登録、クォータ超過 | 実行ログのメッセージを確認する。ソース登録は手順 7d |
| Cloudflare の cron が 2 つしか出ない | `wrangler deploy` を忘れている | 手順 6 を実行する |
| 容量の警告通知が来た | DB が 400 MB を超えた | 古い記事の削除（週次 cron）を確認する。ブックマーク済みの記事は削除されない |

## ロールバック

1. **コード**: Render の `oshikatsu-api` → **Events / Deploys** から、前のデプロイの **Rollback** を押す。frontend は Cloudflare Pages の Deployments から前のものを **Rollback** する
2. **DB**: 今回のマイグレーションは追加だけなので、旧コードに戻しても動作します。DB 自体を戻す必要がある場合のみ、手順 1 のバックアップブランチから復元する（Neon の **Restore** 機能）
3. **scheduler**: 新しい cron を止める場合は、`scheduler/wrangler.toml` から該当の cron を消して `npx wrangler deploy` を実行する

## 未対応の事項（参考）

- ソースの登録 UI（FEAT-054）と通知履歴（FEAT-055）は未実装です。
- `GET /api/v1/health` は DB サイズを認証なしで返します。scheduler と Render のヘルスチェックのためで、意図した設計です。
- 収集先の robots.txt を取得できなかったとき、collector は取得を許可して進みます（404 は許可が標準的ですが、ネットワーク障害時は本来スキップが望ましい）。
