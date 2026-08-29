---
name: sprint2-eval-context
description: Sprint 2 evaluation result for oshikatsu backend — auth API (register/login/logout) PASSED 2026-07-14
metadata:
  type: project
---

Sprint 2 evaluation (2026-07-14): FINAL RESULT = PASS. All endpoints verified live on localhost:6060 with ASPNETCORE_ENVIRONMENT=Development.

Dynamic tests all passed (10/10 cases):
- register 201, duplicate 409, invalid email 400, short password 400
- login 200, wrong password 401
- logout 200, revoked token 401, no auth 401

DB confirmed: password_hash stored as BCrypt $2a$11$ (60 chars), not plaintext. revoked_tokens table populated correctly. Both migrations applied (InitialCreate + AddRevokedTokens).

Code quality: 0 mandatory violations. 1 recommendation (Program.cs:82 — `SecretKey ?? string.Empty` fallback causes lazy exception on first request rather than startup validation).

Secrets: appsettings.json has empty SecretKey/ConnectionString. backend/.gitignore covers `**/appsettings.Development.json` correctly (root .gitignore pattern `backend/appsettings.Development.json` is imprecise but backend-level gitignore compensates).

**Why:** Note that the server must be started with `ASPNETCORE_ENVIRONMENT=Development` to load appsettings.Development.json (which has the JWT secret and DB connection). Without it, the JWT middleware crashes on first request with IDX10703 (key length zero).

**How to apply:** For future sprint evaluations, always set ASPNETCORE_ENVIRONMENT=Development when running the server, and use a port not in conflict with prior evaluator runs.
