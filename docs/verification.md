# Verification — 31 October 2026

| Requirement/check | Evidence |
| --- | --- |
| Process authorized and declined payments | API tests assert exact response, one bank call, bank JSON mapping, Location, and subsequent retrieval; real simulator smoke passes |
| Reject invalid input before bank call | 48 invalid field/boundary cases and 3 malformed-body cases, each asserting zero bank calls |
| Card number, expiry, currency, amount, CVV rules | Parameterized invalid and valid boundary cases, fixed UTC clock, strict JSON numeric binding |
| Retrieve previous payment safely | Exact field/status tests for both outcomes, suffix leading zeros, response field allowlist, unknown ID 404 |
| Safe upstream failures | 502/503/504 cases assert no saved result, no leaked bank detail, one call and no retry |
| Observability | Health/log privacy tests, trace IDs, actual container logs inspected |
| Documentation/API discoverability | README, decisions, TDD history, demo guide; OpenAPI contract test |
| Packaging | Multi-stage Docker build and Compose startup succeed; running gateway reports uid=1654(app), not root |
| Automated verification | 74 tests pass, none skipped; 97.61% line coverage, 96.15% branch coverage |
| Build/format | Release build: zero warnings/errors; repository `.editorconfig` formatting check passes |
| Dependency audit | `dotnet list package --vulnerable --include-transitive` reports no vulnerable packages in either project with current NuGet sources |
| Preserve supplied files | `.editorconfig`, `imposters/bank_simulator.ejs`, and `docker-compose.yml` unchanged from starter |

Commands used: `dotnet test -c Release --logger trx --collect:"XPlat Code Coverage"`, `dotnet build -c Release --no-restore --warnaserror`, `dotnet format --verify-no-changes --no-restore`, `docker compose -f docker-compose.yml -f compose.api.yml up --build -d`, and `python3 scripts/smoke.py`.

The verification is for the local assessment implementation. No recruiter submission or public service deployment is performed by these commands.
