---
name: project-oshi-tracking
description: oshikatsu 推し活トラッキング仕様書の状態。FEAT・スプリント番号の現在の最大値を追跡する。
metadata:
  type: project
---

docs/spec-oshi-tracking.md に仕様書を生成・追記済み。

現在の状態:
- FEAT-001〜022 / Sprint 1〜21: 実装・合格済み（2026-10-03 時点）
- FEAT-023〜028 / Sprint 22〜27: 2026-10-03 に追記（本番運用改善）、Sprint 22〜27 は実装・合格済み
- FEAT-029〜032 / Sprint 28〜29: 2026-10-04 に追記（UX改善）

2026-10-03 追記内容（FEAT-023〜028）:
- FEAT-023（Sprint 22 前半）: 一括登録 API（`/batch`）追加 / backend / Must
- FEAT-024（Sprint 22-23-24 各層）: ニュース・出演情報の重複解消暫定対策 / backend+collector+frontend / Must
- FEAT-025（Sprint 23 前半）: collector 一括送信・OSHI_ID 環境変数・workflow_dispatch oshi_id 入力 / collector / Must
- FEAT-026（Sprint 24 前半）: 推し登録直後の即時収集（GITHUB_DISPATCH_TOKEN 等環境変数）/ backend+frontend / Should
- FEAT-027（Sprint 25）: URL 末尾スラッシュ修正・大小文字検索・ENABLE_SWAGGER 制御 / frontend+backend / Should
- FEAT-028（Sprint 26-27）: Web デザイン刷新 Sprint A（基盤）+ Sprint B（画面）/ frontend / Should

2026-10-04 追記内容（FEAT-029〜032）:
- FEAT-029（Sprint 28）: フィード並び順を掲載日（PublishedAt/AiredAt）降順に変更（第2キー: ID降順）/ backend / Must
- FEAT-030（Sprint 28）: 画像箱除外 R1〜R5 の5ルール / backend / Must
- FEAT-031（Sprint 28 backend + Sprint 29 frontend）: ブックマーク一覧に `oshiId`・`q`・`from`/`to` 絞り込み追加 / backend+frontend / Must
- FEAT-032（Sprint 29）: 1024px以上で2カラム横並び、375px以下でタブ切り替え / frontend / Must

外部仕様として確定した項目:
- 一括 API: `POST /api/v1/internal/news/batch`、`POST /api/v1/internal/programs/batch`
- 単一推し収集環境変数: `OSHI_ID`
- dispatch 環境変数: `GITHUB_DISPATCH_TOKEN`、`GITHUB_REPOSITORY`、`GITHUB_WORKFLOW_FILE`（既定 collect.yml）、`GITHUB_REF`（既定 master）
- Swagger 制御: `ENABLE_SWAGGER=true`
- frontend 表記変更: 「出演番組」→「出演情報」、「放送日（時）」→「掲載日」
- ブックマーク絞り込み API: `GET /api/v1/bookmarks?oshiId=&q=&from=&to=`（from/to は JST オフセット付き日時、FEAT-018 と同じ形式）
- 画像箱判定 5ルール（R1〜R5）は backend 1か所で管理、ブックマーク一覧は除外対象外
- 並び順変更: ニュース=PublishedAt降順+ID降順、出演情報=AiredAt降順+ID降順。未読件数はCollectedAt基準のまま

2026-10-04 追記内容（FEAT-033〜034）:
- FEAT-033（Sprint 30）: 推しごとの「番組表で探す」リンク（Gガイド・J:COM）/ frontend / Should。Sprint 30 は実装・合格済み
- FEAT-034（Sprint 31）: 収集定期起動を Cloudflare Workers Cron Triggers に移行。scheduler/ ディレクトリ追加、GitHub workflow_dispatch API 呼び出し、3回リトライ、secrets 管理 / scheduler（新規層）/ Should

2026-10-04 追記内容（FEAT-035〜036）:
- FEAT-035（Sprint 32 backend + Sprint 33 frontend）: 転載記事の重複まとめ表示。タイトル正規化（媒体名除去・前置き除去・全角半角統一）+ 48時間以内・同一推し内でまとめ。保存は全件行い表示でまとめる。代表は元媒体優先（転載先リスト管理）→掲載日早い順。「ほか N 媒体」開閉UI。ブックマーク一覧は対象外 / backend+frontend / Must
- FEAT-036（Sprint 32 backend+collector + Sprint 33 frontend）: description の HTML 文字参照デコード不具合修正 + 概要の代わりに媒体名を表示。媒体名は RSS タイトル末尾 ` - 媒体名` または `<source>` から取得・保存 / collector+backend+frontend / Must

技術的背景:
- collector 書き込みは backend 内部 API 経由（直接 DB 書き込みは不採用）
- デプロイ先: frontend=Cloudflare Pages / backend=Render / DB=Neon / collector=GitHub Actions / scheduler=Cloudflare Workers
- 収集間隔: 2時間ごと（UTC 毎偶数時 17 分、`17 */2 * * *`）
- GitHub Actions schedule が 2026-08-26 以降不安定（9回中2回のみ起動）→ Cloudflare Workers Cron で補完
- 重複まとめ: タイトル正規化は backend 1か所で管理（FEAT-030 の画像箱判定と同じ考え方）。転載先媒体名リスト（Yahoo!・Infoseek・auWeb・ライブドア・goo・エキサイト・BIGLOBE・dメニュー・LINE NEWS・MSN・ニコニコ）も同1か所

**Why:** 2026-10-04 本番運用で、フィードが古い順・画像箱ノイズ・ブックマーク絞り込み不足・ニュースと出演情報が混在・定期収集の信頼性問題が確認された。同日さらに転載重複（同一記事が元媒体・転載先で重複表示）と description の &nbsp; 不具合が確認された。
**How to apply:** 次に仕様追記を依頼されたら FEAT-037〜、Sprint 34〜 から採番する。
