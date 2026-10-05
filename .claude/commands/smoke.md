---
description: Live smoke test — start the API and hit read-only endpoints against the real Supabase backend
argument-hint: "[base-url, default http://localhost:5135]"
allowed-tools: Bash(dotnet:*), Bash(curl:*)
---

Run a **read-only** live smoke test of the AlSaqr API. Base URL: `$ARGUMENTS`, or `http://localhost:5135` if empty.

## Rules
- GET requests only. Never POST/PUT/PATCH/DELETE: this hits the real Supabase project, and CLAUDE.md "Tests" forbids writing test data to the shared dev database.
- Do not print secrets from `appsettings*.json`, user secrets, or env vars.
- Do not fix anything. Report only.

## Steps

1. If nothing answers on the base URL, start the API in the background:
   `dotnet run --project AlSaqr.API --launch-profile http` (run from the repo root, `run_in_background`).
   Poll `GET /healthz` until it returns 200, up to about 60s. If it never does, show the startup log and stop.

2. Hit each endpoint with
   `curl -s -o /dev/null -w "%{http_code} %{time_total}s" <url>`
   and compare the status with the expectation:

   | Endpoint | Expect |
   |---|---|
   | `/healthz` | 200 |
   | `/swagger/v1/swagger.json` | 200 (Development only) |
   | `/Cities?searchTerm=` | 200 |
   | `/Topics` | 200 |
   | `/Events?currentPage=1&itemsPerPage=5&searchTerm=` | 200 |
   | `/Events/online?currentPage=1&itemsPerPage=5&searchTerm=` | 200 |
   | `/Groups?currentPage=1&itemsPerPage=5&searchTerm=` | 200 |
   | `/Products?currentPage=1&itemsPerPage=5&searchTerm=` | 200 |
   | `/Category` | 200 |
   | `/Events/my`, `/Groups/my` (no auth) | 401 or 403, never 500 |
   | `/Events/does-not-exist` | 404 as RFC 7807 ProblemDetails, never 500 |

   Check controller routes under `AlSaqr.API/Controllers` first and adjust any path or query param that no longer matches.

3. For the paged endpoints, fetch the body once and confirm it parses as JSON and has the `PaginatedResult` shape. Request page 1 twice and confirm the item order is identical, since ordering must be deterministic (CLAUDE.md "Paginated reads").

4. Call the 404 and 401/403 cases and confirm the body has `type`, `title` and `status` (CLAUDE.md "Errors").

5. Stop the API process if you started it.

## Report

A table of endpoint, status, latency and PASS/FAIL, then a one-line verdict. Flag any 5xx, slow response (over 2s), non-JSON body, or missing ProblemDetails with the response body, trimmed.
