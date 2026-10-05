---
name: project-oshikatsu-sprints
description: oshikatsuプロジェクトのスプリント評価履歴と最終状態
metadata:
  type: project
---

Sprint 40 評価：合格（1回目）。FEAT-042（frontend 推し登録・フィード表示調整）。frontend 284/284 テスト・ビルド 0エラー・tsc 0エラー。OshiForm.tsx に 409 ハンドリング追加（line 55-57、メッセージ「同名の推しはすでに登録されています」）。canonical name 表示・DELETE 後ダッシュボード更新・oshiId フィルタ継続動作すべて Playwright 動的確認済み。変更ファイル: OshiForm.tsx + OshiForm.test.tsx（1テスト追加）のみ。全スプリント（1〜40）完了（Sprint 39 は並列評価中）。

Sprint 37 評価：合格（1回目）。FEAT-039（公開 API タレントモデル対応）。backend 564/570（6失敗は NewsCleanupServiceTests の InMemory ExecuteDeleteAsync 非対応、評価対象外）。POST /oshis が user_oshis.id 返却・同一ユーザー重複 409・別ユーザー同名成功・GET id=user_oshis.id/name=talents.display_name・DELETE user_oshi のみ削除（talent/news 残存）・403 権限チェック・news/programs/bookmarks の oshiId→talent_id 解決・dispatch inputs=talent_id・TalentService 並行 INSERT UNIQUE 違反 catch→再 SELECT すべて curl および DB 確認済み。

Sprint 38 評価：合格（1回目）。FEAT-040（内部API talentId ベース化）。backend 538/544（6失敗は Sprint 35 起源の NewsCleanupServiceTests、InMemory ExecuteDeleteAsync 非対応）。GET /internal/oshis が [{talentId,name}]・1人以上登録タレントのみ返却・news/programs batch/単件が talentId 受付・404 確認・dispatch input talent_id 変更・Swagger 反映すべて curl 確認済み。MakeOshiIdNullable マイグレーション Up 適用済み（Down は NULL oshi_id 行なしなら成功、あれば FK23503）。DB は Up 状態のまま。

Sprint 36 評価：合格（1回目）。FEAT-038（タレント名正規化・同一タレント判定ロジック）。539/539 テスト（+47 新規）・ビルド 0エラー。TalentNameNormalizer（NFKC→小文字→空白除去）・TalentService（FindOrCreate）・DI 登録（ITalentService scoped）すべて確認済み。推奨違反1件（並行INSERT時のUNIQUE違反未処理 - FEAT-039で要対処）。全スプリント（1〜36）完了。

Sprint 35 評価：合格（1回目）。FEAT-037（タレントテーブル・DBマイグレーション）。492/492 テスト（+16 新規）・ビルド 0エラー。talents 42件・user_oshis 75件（oshis 75件と一致）・全 news_items/program_appearances に talent_id 設定済み・重複排除 0件残留・UNIQUE 制約確認済み。全スプリント（1〜35）完了。

Sprint 34 評価：合格（1回目）。FEAT-035/036 Sprint B（ブックマーク一覧への媒体名表示）。ビルド・テスト正常。

Sprint 33 評価：合格（1回目）。FEAT-035/036 frontend 部分。281/281 テスト・ビルド正常。OtherSourcesAccordion コンポーネント新規追加。ニュース・出演情報・ダッシュボード OshiCard すべてで「ほか N 媒体」ボタン・アコーディオン・媒体名表示確認済み。375px 水平スクロールなし。全スプリント（1〜33）完了。

Sprint 39 評価：合格（1回目）。FEAT-041（collector タレントベース化）。collector pytest 199/199・backend 564/570（6失敗は NewsCleanupServiceTests Sprint 35 起源、評価対象外）。client.get_oshis() が talentId 読み取り・post_news_batch/post_program_batch が talentId 送信・oshiId 残留なし・TALENT_ID 環境変数対応・collect.yml workflow input talent_id・concurrency group talent_id ベース・scheduler inputs:{} でタレント指定なし（全件収集用）すべて確認。

**How to apply:** Sprint 37〜40（FEAT-039〜042）すべて完了。次は Sprint 41（FEAT-043 古い記事の自動削除）。

---

Sprint 32 評価：合格（1回目）。FEAT-035（転載重複まとめ backend）・FEAT-036（媒体名表示・文字参照修正）。backend 473/473・collector 194/194。ArticleDeduplicator が転載ロジックと RepostMediaNames を1か所に集約。マイグレーション自動適用確認済み。

---

Sprint 31 評価：合格（2回目）。FEAT-034（Cloudflare Workers 定期起動）。

**Why:** 1回目は ScheduledEvent→ScheduledController 型エラーで不合格。2回目で修正済み。tsc 0エラー、vitest 15/15、wrangler dry-run 成功。動的テスト: `/__scheduled` → 空トークン → 401 受信 → 非リトライ → エラーログ確認。GITHUB_DISPATCH_TOKEN は wrangler.toml [vars] に存在せず（secret）。AbortSignal.timeout タイムアウト実装・テスト3件確認済み。

**How to apply:** Sprint 31 合格。FEAT-034 完了。全スプリント（1〜31）完了。

---

Sprint 30 評価：合格（1回目）。259/259 全テスト通過。FEAT-033（番組表リンク）すべて確認済み。

Sprint 25 評価：合格（2回目）。360/360 テスト通過。FEAT-027 全項目確認済み。

Sprint 24 評価完了（合格、1回目）。FEAT-026（推し登録直後即時収集）・FEAT-024（frontend表記変更）両方合格。backend 342/342・frontend 217/217 全テスト通過。

## Sprint 38 技術メモ

- MakeOshiIdNullable: oshi_id を news_items/program_appearances で nullable に変更。FEAT-040 以降 collector は talentId のみ送信するため必須変更
- Down 制約: NULL oshi_id 行が存在する場合 FK23503 でロールバック失敗。実運用では Sprint 38 API 使用開始後のロールバックは手動クリーンアップ要
- GitHubDispatchService: TriggerCollectAsync(int talentId) にシグネチャ変更、dispatch inputs = { talent_id: ... }
- Sprint 36 HEAD に存在した build エラー（InternalController 3-param vs テストの 2-param 呼び出し）を Sprint 38 が修正
- 重複除外は talent_id スコープ（同タレント内の source_url 重複をスキップ）

## FEAT 全体完了状況

FEAT-001〜043 のうち Sprint 38（FEAT-040）まで実装済み。
FEAT-041（collector タレントベース化）: Sprint 39 で実装予定。
FEAT-042（frontend 調整）: Sprint 40 で実装予定。
