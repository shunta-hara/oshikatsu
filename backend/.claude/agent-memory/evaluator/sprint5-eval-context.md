---
name: sprint5-eval-context
description: OshikatsuApi Sprint 5 (news collector batch) PASSED 2026-07-14; DateTime→DateTimeOffset fix on NewsItem/ProgramAppearance verified; User/RevokedToken DateTime.UtcNow is safe
metadata:
  type: project
---

Sprint 5 evaluation (2026-07-14): FINAL RESULT = PASS. All endpoints verified live on localhost:7070 with ASPNETCORE_ENVIRONMENT=Development.

Dynamic tests all passed (11 cases):
- POST /api/v1/auth/register → 201 (User.CreatedAt DateTime.UtcNow works fine)
- POST /api/v1/auth/login → 200
- POST /api/v1/oshis → 201
- POST /internal/news with +00:00 format → 200 (DateTimeOffset fix confirmed)
- POST /internal/news same URL again → 200, wasDuplicate=true (dedup works)
- POST /internal/programs with +00:00 format → 200
- POST /api/v1/auth/logout → 200 (RevokedToken DateTime.UtcNow works fine)
- GET /api/v1/oshis with revoked token → 401
- POST /internal/news no API key → 401
- GET /internal/oshis → 200, returns list
- POST /internal/news (no publishedAt) → 200, isPublishedAtEstimated=true

Collector pytest: 68/68 (100%). Backend dotnet test: 103/103 (100%).

Key findings on User/RevokedToken DateTime safety:
- User.CreatedAt/UpdatedAt and RevokedToken.ExpiresAt/RevokedAt use DateTime type
- All writes use DateTime.UtcNow (Kind=Utc) — never user-supplied JSON input
- Auth endpoints receive only email/password strings, not DateTime values
- No Kind=Local risk confirmed; live tests show all auth flows work correctly

Sprint 5 completion conditions: 6/6 met.
FEAT-006 acceptance criteria: 5/5 met.

Code quality:
- No hardcoded secrets (config reads from env vars)
- All Python functions have type hints
- HTTP requests have timeout (30s default)
- Retry with exponential backoff (max 3) implemented in NewsFetcher and BackendClient
- Controller is thin; no logic; delegates to InternalCollectorService

**Why:** All Sprint 5 completion conditions and FEAT-006 acceptance criteria met.

**How to apply:** Sprint 6 should implement program appearance collector. collector/collector/client.py already has post_program_appearance() stub ready. Use same server start pattern (ASPNETCORE_ENVIRONMENT=Development, port 7070).
