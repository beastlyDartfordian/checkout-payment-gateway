# TDD execution record

Tests were written and executed before the corresponding implementation. This record summarizes actual observed runs, not a proposed workflow. Intermediate logs stayed outside the repository to keep the submission focused.

| Cycle | Observed red | Green after minimal implementation |
| --- | --- | --- |
| Starter baseline | 1 passed, 1 failed: missing payment returned 204 instead of 404 | Baseline only |
| Retrieval | 1 passed, 4 failed: numeric suffix vs string, and missing ID response | 5 passed |
| Validation | 50 failed: POST did not exist and returned 404 | 55 passed total |
| Processing/failures | 53 passed, 17 failed: POST placeholder returned 501; numeric outcome serialization | 70 passed total |
| Observability | 2 failed: no health endpoint and no rejection log | 72 passed total |
| OpenAPI contract | 1 failed: Swagger omitted POST 201 response | 73 passed total |

Retrieval fixtures were adapted from integer suffixes to string suffixes when the production model changed; expected JSON behaviour stayed the same. Processing tests had a test-code compilation error corrected before the recorded behavioural red run. Refactoring removed the duplicate unused response model and updated dependencies; the complete suite remained green.

Assertions use JSON rather than deserializing into the production response model. Invalid request cases assert zero bank calls. Upstream failure cases assert no stored payment, one bank call, and safe error responses. HTTP handler fakes exercise the real bank client; they do not implement the simulator's odd/even rule. The real simulator is checked separately with `scripts/smoke.py`.

Final boundary review added an explicit current-expiry-month regression case, passing against the already tested expiry rule: **74 tests pass**. Safe log assertions inspect structured field values to avoid false failures when random GUIDs happen to contain the synthetic CVV digits.
