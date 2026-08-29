---
name: sprint2-auth-context
description: Sprint 2 JWT認証実装時の判断・注意点（パッケージバージョン、マイグレーション順序問題）
metadata:
  type: project
---

# Sprint 2 実装コンテキスト（2026-07-14）

## 採用パッケージ
- `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.9（.NET 10 SDK 10.0.301 に対応）
- `BCrypt.Net-Next` 4.2.0

**Why:** EF Core 9.x系と混在しているが、JwtBearer はランタイムとバージョンを揃えないと型不整合が起きるリスクがあるため 10.x を選択。

## マイグレーション順序問題

`dotnet ef migrations add` が UTC 時刻 `20260713xxxxxx` を生成したが、InitialCreate は手動で `20260714000000` が設定されており順序が逆転した。

**How to apply:** EF Core マイグレーションは字句順（yyyyMMddHHmmss）で適用されるため、手動でタイムスタンプを InitialCreate より大きくした `20260714000001` を使い手動作成した。

## ログアウト実装方式

JWT は stateless なので、ログアウトには `revoked_tokens` テーブルに `jti` を格納するサーバーサイド無効化を採用。`OnTokenValidated` イベントで毎リクエスト DB チェックを行う。

## JwtSettings 秘匿管理

- `appsettings.json`: SecretKey = ""（空欄、コミット対象）
- `appsettings.Development.json`: 開発用シークレットキーを設定（gitignore 済み）
