---
name: sprint3-oshi-context
description: Sprint 3 推し登録・管理 API (FEAT-002 backend) 実装時の判断・注意点
metadata:
  type: project
---

# Sprint 3: 推し登録・管理 API 実装メモ

## 実装した内容

- `POST /api/v1/oshis` — 推し登録（201）
- `GET /api/v1/oshis` — ログインユーザーの推し一覧（200）
- `DELETE /api/v1/oshis/{id}` — 推し削除（204/403/404）
- 20件上限 → 422 UnprocessableEntity
- 他ユーザーの推しへのアクセス → 403（`StatusCode(403, MessageResponseDto)` で返す）

## JWT sub クレームの取得

JwtBearer ミドルウェアは JWT の `sub` クレームを `ClaimTypes.NameIdentifier` にマップする。
`User.FindFirst(ClaimTypes.NameIdentifier)?.Value` で userId（int）を取得。

**Why:** TokenService は `JwtRegisteredClaimNames.Sub` に userId を設定しているが、
ミドルウェアが自動的に `ClaimTypes.NameIdentifier` に変換するため、両方フォールバックで対応した。

## 例外設計

| 例外 | HTTP | 意味 |
|------|------|------|
| `OshiLimitExceededException` | 422 | 20件上限超過 |
| `OshiNotFoundException` | 404 | 推しが存在しない |
| `OshiAccessDeniedException` | 403 | 他ユーザーの推し |

## curl での注意点

Bash から日本語（UTF-8）を curl に渡すと JSON デシリアライズエラーになる場合がある。
`--data-raw` を使い、ASCII 文字でテスト推奨。

## テスト

- InMemory DB は Guid.NewGuid() でテスト間独立
- 境界値: 19件→20件目成功、20件→21件目422
- 他ユーザーのカウントが自分の上限に影響しないこと（ユーザー分離）もテスト済み
