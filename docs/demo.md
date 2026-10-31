# Review and demonstration guide

1. Start with `docker compose -f docker-compose.yml -f compose.api.yml up --build -d`.
2. Open http://localhost:5067/swagger and show POST/GET contracts.
3. Run `python3 scripts/smoke.py`: authorized, declined, retrieval, bank unavailable, rejected input, unknown ID, health.
4. Run `dotnet test`. Explain a representative validation theory, safe response assertion, exact outbound bank payload, and failure/no-retry assertion.
5. Walk through controller → bank client → in-memory store. Explain why no additional application layers are needed here.
6. Show gateway logs: outcome, rejection, failure, correlation, no sensitive bodies. Explain liveness versus dependency readiness.
7. Discuss assumptions and production gaps from `docs/design.md`: expiry, amounts, idempotency, uncertainty, durability, authentication, TLS, monitoring.

Suggested 60-minute pacing: 5 minutes scope, 10 demo, 15 tests/TDD, 15 design/failure trade-offs, 10 hosting/observability, 5 questions. Let interviewer questions change the order.

Useful prompts to rehearse:

- Why does a declined payment return 201? A result resource was created; business status differs from HTTP transport success.
- Why not retry a timeout? Bank might have authorized it; no idempotency contract exists in the supplied simulator.
- Why no database? The challenge explicitly permits in-memory storage; restart and scaling limitations are documented.
- Why API integration tests? They catch binding/validation/serialization/routing issues with little mocking, while controlling the external boundary.
- What would you change first for production? Establish merchant identity, durability, idempotency and reconciliation, sensitive-data boundaries, and operational requirements before scaling infrastructure.
