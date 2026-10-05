---
description: Validate the AlSaqr backend — restore, build, format check, and tests
allowed-tools: Bash(dotnet:*)
---

Run the project's validation suite from the repo root, in order, stopping to report at the first failing stage:

1. `dotnet restore AlSaqr.API.sln`
2. `dotnet build AlSaqr.API.sln --no-restore -c Release -warnaserror`
3. `dotnet format AlSaqr.API.sln --verify-no-changes --no-restore`
4. `dotnet test AlSaqr.API.sln --no-build -c Release --logger "trx" --collect:"XPlat Code Coverage"`

Notes:
- If the solution contains no test projects, say so explicitly. Do not report that tests passed.
- Tests MUST NOT run against the shared dev database (CLAUDE.md "Tests"). If a test needs Supabase/PostgreSQL, confirm it uses Testcontainers or an isolated test schema before running it.
- Report each stage as pass/fail with the relevant error output. Do not fix anything unless asked.

Finish with a short summary: stage results, test counts (passed/failed/skipped), and any CLAUDE.md MUST-rule concerns noticed (e.g. `Newtonsoft.Json` references, fire-and-forget `_ = ...Async()`, missing `[Table(..., Schema = "alsaqr-2026")]`):

```
grep -rnE "Newtonsoft|_ = [A-Za-z.]+Async\(" --include=*.cs .
```
