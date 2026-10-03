# デプロイ手順書（無料枠構成）

構成の方針と理由は [deploy-plan.md](deploy-plan.md) にあります。この手順書は、**アカウント作成や秘密情報の登録など、本人にしかできない作業**の手順です。

所要時間の目安は 60〜90 分です。上から順に進めてください。後の手順で、前の手順で控えた値を使います。

## 全体像

```
[ブラウザ] ──> Cloudflare Pages（frontend）
                  │ API呼び出し
                  ▼
              Render（backend） ──> Neon（PostgreSQL）
                  ▲
GitHub Actions ───┘ 2時間ごとに collector を実行
```

## 作業中に控える値

作業しながら、次の値をメモ帳などに控えてください。**秘密情報なので、チャットやリポジトリには貼らないでください。**

| 名前 | 例 | 取得する手順 |
|---|---|---|
| DB 接続文字列 | `Host=ep-xxx...;Database=neondb;...` | 手順 2 |
| JWT 秘密鍵 | ランダムな64文字程度 | 手順 3 |
| collector API キー | ランダムな64文字程度 | 手順 3 |
| backend の URL | `https://oshikatsu-api.onrender.com` | 手順 4 |
| frontend の URL | `https://oshikatsu.pages.dev` | 手順 5 |

---

## 手順 1. コードを GitHub に push する

Render と Cloudflare は GitHub のコードを取りに行くので、先に push が必要です。Claude に「push して」と頼めば代行します。

- [ ] `master` を push する
- [ ] 定期実行ワークフローを一時停止する（手順 7 まで、2時間ごとに失敗通知が届くのを防ぐため）
  ```bash
  gh workflow disable collect.yml
  ```

## 手順 2. Neon で DB を作る

1. [neon.com](https://neon.com) で GitHub アカウントを使ってサインアップする（クレジットカード不要）
2. プロジェクトを作成する
   - Project name: `oshikatsu`
   - Postgres version: **16**（ローカル開発と同じ）
   - Region: **AWS Asia Pacific (Singapore)**（Render と同じ地域にして通信を速くする）
3. ダッシュボードの **Connect** を開き、接続情報を表示する
   - **Connection pooling はオフ**にする（ホスト名に `-pooler` が付かない直接接続を使う。起動時のマイグレーションを確実に通すため）
4. 表示された接続文字列を **.NET（Npgsql）の形式に書き換えて**控える

   Neon が表示する形式（そのままでは使えない）:
   ```
   postgresql://USER:PASSWORD@ep-xxxx.ap-southeast-1.aws.neon.tech/neondb?sslmode=require
   ```
   書き換えた形式（これを控える）:
   ```
   Host=ep-xxxx.ap-southeast-1.aws.neon.tech;Database=neondb;Username=USER;Password=PASSWORD;SSL Mode=Require
   ```

テーブルは backend の初回起動時に自動で作られるので、ここで SQL を実行する必要はありません。

## 手順 3. 秘密鍵を2つ作る

Git Bash で次の2行を実行し、出力をそれぞれ控えます。

```bash
openssl rand -base64 48   # → JWT 秘密鍵
openssl rand -hex 32      # → collector API キー
```

- JWT 秘密鍵は **32文字以上**必要です。短いとログイン時にエラーになります。
- `docker-compose.yml` にある開発用の値（`oshikatsu_dev_...`）は、**本番では使わないでください**。

## 手順 4. Render に backend を置く

1. [render.com](https://render.com) で GitHub アカウントを使ってサインアップする
   - プランは **Hobby（無料）** のまま。**支払い方法は登録しない**（理由は「費用について」を参照）
2. **New → Web Service** を選び、GitHub の `shunta-hara/oshikatsu` を連携する（このリポジトリだけにアクセスを許可すれば十分）
3. 設定を入力する

   | 項目 | 値 |
   |---|---|
   | Name | `oshikatsu-api`（URL が `https://oshikatsu-api.onrender.com` になる。使用済みなら別の名前にする） |
   | Language | **Docker** |
   | Branch | `master` |
   | Region | **Singapore** |
   | Root Directory | `backend` |
   | Dockerfile Path | `./Dockerfile`（`backend/Dockerfile` を指していることを確認） |
   | Instance Type | **Free** |
   | Health Check Path | `/api/v1/health` |

4. **Environment Variables** に次の4つを登録する

   | Key | Value |
   |---|---|
   | `ConnectionStrings__DefaultConnection` | 手順 2 で控えた接続文字列 |
   | `JwtSettings__SecretKey` | 手順 3 の JWT 秘密鍵 |
   | `CollectorSettings__ApiKey` | 手順 3 の collector API キー |
   | `CORS_ALLOWED_ORIGINS` | `https://oshikatsu.pages.dev`（仮の値。手順 6 で実際の URL に直す） |

   - キー名のアンダースコアは **2つ**（`__`）です。
   - `PORT` は Render が自動で設定するので、登録しないでください。

5. **Create Web Service** を押し、デプロイが終わるまで待つ（初回は 5〜10 分）
6. 確認する: ブラウザで `https://<Name>.onrender.com/api/v1/health` を開き、`{"status":"ok",...}` が表示されれば成功。この URL のドメイン部分（末尾の `/` なし）を **backend の URL** として控える

ログに「必須設定 '...' が見つからないか空です」と出た場合は、環境変数の名前か値が間違っています。

## 手順 5. Cloudflare Pages に frontend を置く

1. [dash.cloudflare.com](https://dash.cloudflare.com) でサインアップする（クレジットカード不要）
2. **Workers & Pages → Create** を押し、「Make something new」画面の一番下にある **「Need to use the legacy Pages workflow? Continue to Pages」の Continue to Pages** を押す。続けて **Import an existing Git repository** を選び、GitHub の `shunta-hara/oshikatsu` を連携する
   - 「Continue with GitHub」など画面中央のボタンは **Workers** の作成になるので押さない。設定画面に「Deploy command（`npx wrangler deploy`）」の欄があれば、それは Workers の画面
   - 「legacy」と表示されるが、Pages は引き続き無料で使える
   - 誤って Workers で作ってしまった場合は、そのプロジェクトを削除してから Pages で作り直す
3. 設定を入力する

   | 項目 | 値 |
   |---|---|
   | Project name | `oshikatsu`（URL が `https://oshikatsu.pages.dev` になる。使用済みなら別の名前にする） |
   | Production branch | `master` |
   | Framework preset | `React (Vite)` |
   | Build command | `npm run build` |
   | Build output directory | `dist` |
   | Root directory | `frontend` |

4. **Environment variables** に次の2つを登録する

   | Variable name | Value |
   |---|---|
   | `VITE_API_BASE_URL` | 手順 4 の backend の URL（例: `https://oshikatsu-api.onrender.com`）。**末尾に `/` を付けない**（付けると URL が壊れる） |
   | `NODE_VERSION` | `24` |

5. **Save and Deploy** を押し、完了したら表示された URL（`https://<Project name>.pages.dev`）を **frontend の URL** として控える

`VITE_API_BASE_URL` はビルド時に埋め込まれます。あとで値を変えた場合は、Cloudflare で再デプロイ（Retry deployment）が必要です。

## 手順 6. Render の CORS 設定を実際の URL に直す

1. Render の `oshikatsu-api` → **Environment** を開く
2. `CORS_ALLOWED_ORIGINS` を、手順 5 で控えた frontend の URL にする（末尾の `/` なし。例: `https://oshikatsu.pages.dev`）
3. 保存すると自動で再デプロイされる

手順 5 の Project name が `oshikatsu` のままで URL が一致していれば、この手順は不要です。

## 手順 7. GitHub Actions で collector を動かす

1. GitHub のリポジトリ → **Settings → Secrets and variables → Actions → New repository secret** で、次の2つを登録する

   | Name | Secret |
   |---|---|
   | `BACKEND_BASE_URL` | 手順 4 の backend の URL（末尾の `/` なし） |
   | `COLLECTOR_API_KEY` | 手順 3 の collector API キー（Render に登録したものと**同じ値**） |

2. 手順 1 で止めた定期実行を再開する
   ```bash
   gh workflow enable collect.yml
   ```
3. 動作確認のため、手動で1回実行する
   ```bash
   gh workflow run collect.yml
   gh run watch
   ```
   GitHub の **Actions** タブで **Run workflow** を押しても同じです。

## 手順 8. 推し登録直後の即時収集を有効にする（オプション）

推しを登録した直後に GitHub Actions を自動起動し、数分以内にニュースを表示させるための設定です。設定しなくても2時間ごとの定期収集は動作します。

### 8a. fine-grained PAT を作成する

1. GitHub → **Settings → Developer settings → Personal access tokens → Fine-grained tokens → Generate new token**
2. 設定を入力する

   | 項目 | 値 |
   |---|---|
   | Token name | `oshikatsu-dispatch`（分かりやすい名前） |
   | Expiration | 任意（有効期限が短いほど安全。期限切れ後は再作成が必要） |
   | Repository access | **Only select repositories** → `shunta-hara/oshikatsu`（このリポジトリのみ） |
   | Permissions → Actions | **Read and write** |

3. **Generate token** を押し、表示されたトークン（`github_pat_...`）を控える  
   **画面を閉じると二度と表示されません。必ずコピーしてください。**

### 8b. Render に環境変数を追加する

Render の `oshikatsu-api` → **Environment** を開き、以下の2つを追加する。

| Key | Value |
|---|---|
| `GITHUB_DISPATCH_TOKEN` | 手順 8a で控えた PAT |
| `GITHUB_REPOSITORY` | `shunta-hara/oshikatsu`（`owner/リポジトリ名` の形式） |

- `GITHUB_WORKFLOW_FILE` は未設定で `collect.yml` が使われる（変更した場合は設定する）。
- `GITHUB_REF` は未設定で `master` が使われる（変更した場合は設定する）。
- 保存すると自動で再デプロイされる。

### 動作確認

推しを1人登録し、2〜3分後にダッシュボードにニュースが表示されれば成功です。  
GitHub の **Actions** タブで `collect.yml` の実行履歴を確認できます。

---

## 手順 9. 動作確認

- [ ] frontend の URL を開くとログイン画面が出る（Render がスリープしていると、最初の応答に1分ほどかかる）
- [ ] 新規登録して、推しを1人追加する
- [ ] 手順 7 の手動実行をもう一度行い、成功したあとにダッシュボードでニュースや出演番組が表示される
- [ ] `/bookmarks` を直接開いてもページが表示される

## うまくいかないとき

| 症状 | 主な原因 | 対処 |
|---|---|---|
| Cloudflare のビルドが `Could not read package.json` で失敗する | Root directory が `/` のまま。または Pages ではなく Workers で作成している | Pages で作り直し、Root directory を `frontend` にする（手順 5） |
| frontend で通信エラー。ブラウザのコンソールに CORS エラー | `CORS_ALLOWED_ORIGINS` と frontend の URL が一致していない | `https://` から始まる完全な URL を、末尾の `/` なしで設定する |
| 通信先の URL が `//api/v1` になっている | `VITE_API_BASE_URL` の末尾に `/` がある | `/` を消して Cloudflare で再デプロイする |
| Render のデプロイがすぐに失敗する | 必須の環境変数が欠けているか、名前を間違えている | ログのメッセージに出ている環境変数名を確認する（`__` は2つ） |
| Render のログに DB 接続エラーが出る | 接続文字列の形式が違う、または `SSL Mode=Require` がない | 手順 2 の書き換え形式になっているか確認する |
| ログインで 500 エラー | JWT 秘密鍵が短い | 手順 3 のコマンドで作り直す |
| Actions が `Required environment variable ... is not set` で失敗する | GitHub Secrets が未登録、または名前の間違い | 手順 7 の名前どおりに登録する |
| Actions が `Backend did not become ready` で失敗する | Render が2分以内に起きなかった、または Render が停止している | Render のダッシュボードで状態を確認し、手動でもう一度実行する |
| 初回アクセスが遅い | Render の無料枠は、15分アクセスがないとスリープする | 仕様どおり（約1分で復帰する） |

---

## 費用について

**手順どおりに進め、個人で使う規模にとどまる限り、月額 0 円で運用できます。** どのサービスもクレジットカードの登録なしで始められます。

| サービス | 無料枠 | この構成での使用量の見込み | 超えたとき |
|---|---|---|---|
| Cloudflare Pages | 静的配信は無制限、ビルドは月 500 回 | push 1回につきビルド1回 | ビルドが止まる（課金なし） |
| Render Hobby | 稼働 750 時間/月、送信帯域 5 GB/月 | 稼働は最大でも 744 時間/月なので収まる。API の応答は小さい JSON なので、帯域も個人利用なら十分 | 稼働時間の超過: 月末までサービス停止（課金なし）。帯域の超過: 支払い方法を登録していると $0.15/GB が請求される |
| Neon Free | ストレージ 0.5 GB、100 CU 時間/月、期限なし | 5分アクセスがないと自動停止するので、使った分だけ消費する。ニュースのデータは小さく、0.5 GB に届くのはかなり先 | DB が止まる（課金なし） |
| GitHub Actions | private リポジトリで 2,000 分/月 | 1回 1〜2 分 × 1日 12回 × 30日 ＝ 約 360〜720 分/月（推し10人の場合。登録直後の収集は1回約1分） | 支払い方法を登録していなければ実行が止まる |

### お金がかかる可能性があるケース
- **Render に支払い方法を登録した状態で、帯域 5 GB を超えたとき**: 超過分に $0.15/GB が請求されます。支払い方法を登録しなければ請求はできませんが、そのとき超過分がどう扱われるかは公式に明記されていません。サービスが止まる可能性があります。
- **プランを有料に切り替えたとき**: Render の常時起動（スリープなし）は有料プランが必要です。
- **独自ドメインを使うとき**: ドメインの取得費用が年額で別途かかります。`*.pages.dev` と `*.onrender.com` のままなら不要です。

### 無料枠の注意点
- Render の無料 PostgreSQL は30日で消えるので、使っていません（DB は Neon に置いています）。
- 無料枠の内容は変わることがあります。実際、Render は 2026-08-01 に Hobby の帯域を 100 GB から 5 GB に縮小しました。各サービスから料金改定のメールが届いたら確認してください。
- 使用量は各ダッシュボードで確認できます（Render: Billing、Neon: Usage、GitHub: Settings → Billing）。
