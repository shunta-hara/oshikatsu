# collector 書き込み方式

## 採用方式

**backend が提供する内部 API 経由の書き込み**

collector は PostgreSQL に直接アクセスせず、すべての書き込みを backend の内部 API エンドポイントへの HTTP リクエスト経由で行う。

## 理由

1. **アーキテクチャの一貫性**: データモデルの正本は backend (C#/EF Core) であり、collector が直接 DB に書くとスキーマの二重管理が発生する。
2. **バリデーションの一元化**: ビジネスルール（重複排除・必須フィールドチェック）を backend にまとめ、collector はデータ収集に専念できる。
3. **セキュリティ**: DB クレデンシャルを collector コンテナに持たせない。API キーだけで認証する。
4. **将来の拡張性**: 収集ソースを増やす際も、collector は API を叩くだけでよい。

## API キー認証

- collector は HTTP リクエストの `X-Api-Key` ヘッダーに API キーを付与する
- backend は環境変数 `CollectorSettings__ApiKey` から期待するキーを読み込む
- collector は環境変数 `COLLECTOR_API_KEY` からキーを読み込む
- コードへのハードコード禁止

## 内部 API エンドポイント仕様

### POST /api/v1/internal/news

collector が収集したニュースアイテムを登録する。

**認証**: `X-Api-Key` ヘッダー必須

**リクエストボディ**:
```json
{
  "oshiId": 1,
  "title": "記事タイトル",
  "summary": "本文要約（任意）",
  "sourceUrl": "https://example.com/news/123",
  "publishedAt": "2026-07-14T10:00:00+00:00",
  "collectedAt": "2026-07-14T12:00:00+00:00"
}
```

- `publishedAt` が省略された場合、backend が `collectedAt` で代替し `isPublishedAtEstimated = true` を設定する

**レスポンス 200**:
```json
{
  "id": 42,
  "oshiId": 1,
  "title": "記事タイトル",
  "sourceUrl": "https://example.com/news/123",
  "publishedAt": "2026-07-14T10:00:00+00:00",
  "isPublishedAtEstimated": false,
  "collectedAt": "2026-07-14T12:00:00+00:00",
  "createdAt": "2026-07-14T12:00:01+00:00",
  "wasDuplicate": false
}
```

- 同一 `oshiId + sourceUrl` が既に存在する場合は既存レコードを返し `wasDuplicate: true`（冪等）

**レスポンス 400**: バリデーションエラー
**レスポンス 401**: API キーなし・不正

---

### POST /api/v1/internal/programs

collector が収集した出演番組情報を登録する。

**認証**: `X-Api-Key` ヘッダー必須

**リクエストボディ**:
```json
{
  "oshiId": 1,
  "programName": "番組名",
  "broadcaster": "放送局名（任意）",
  "airedAt": "2026-07-14T20:00:00+00:00",
  "sourceUrl": "https://tv.example.com/schedule/456",
  "collectedAt": "2026-07-14T12:00:00+00:00"
}
```

- `airedAt` が省略された場合、backend が `collectedAt` で代替し `isAiredAtEstimated = true` を設定する

**レスポンス 200**:
```json
{
  "id": 99,
  "oshiId": 1,
  "programName": "番組名",
  "broadcaster": "放送局名",
  "airedAt": "2026-07-14T20:00:00+00:00",
  "isAiredAtEstimated": false,
  "sourceUrl": "https://tv.example.com/schedule/456",
  "collectedAt": "2026-07-14T12:00:00+00:00",
  "createdAt": "2026-07-14T12:00:01+00:00",
  "wasDuplicate": false
}
```

- 同一 `oshiId + sourceUrl` が既に存在する場合は既存レコードを返し `wasDuplicate: true`（冪等）

**レスポンス 400**: バリデーションエラー
**レスポンス 401**: API キーなし・不正

---

## 収集データソース

### ニュース収集（FEAT-006）

| 項目 | 内容 |
|------|------|
| 収集元 | Google News RSS |
| フィード URL | `https://news.google.com/rss/search?q={oshi_name}&hl=ja&gl=JP&ceid=JP:ja` |
| 許可根拠 | Google の公開フィード。robots.txt で RSS エンドポイントへのクロールは許可されている |
| 実装モジュール | `collector/collector/news_fetcher.py` |

### 出演番組収集（FEAT-007）

| 項目 | 内容 |
|------|------|
| 収集元 | Google News RSS（番組出演関連記事） |
| フィード URL | `https://news.google.com/rss/search?q={oshi_name}+出演+番組&hl=ja&gl=JP&ceid=JP:ja` |
| 許可根拠 | Google の公開フィード。robots.txt で RSS エンドポイントへのクロールは許可されている |
| 実装モジュール | `collector/collector/program_fetcher.py` |
| 備考 | 検索クエリに「出演 番組」を付加して TV 出演関連記事に絞り込む。記事タイトルを番組名として使用し、pubDate を放送日時の推定値として扱う（`isAiredAtEstimated = true`） |

国内向けに NHK 番組表 API（`api.nhk.or.jp`）などの利用が可能になれば、
`program_fetcher.py` と同一インターフェースで実装した別フェッチャーモジュールを追加し、
`program_collector.py` の `ProgramFetcher` 引数に差し替えることで対応できる（拡張ポイント）。
