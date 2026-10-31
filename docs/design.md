# Design decisions

## Shape

One API project and one test project. A controller orchestrates validation → bank call → safe response → in-memory storage. `BankClient` isolates HTTP mapping, timeout, and failure classification. The repository uses `ConcurrentDictionary` for concurrent HTTP requests. One response model is shared by create/retrieve because their public fields are identical; the unused starter retrieval DTO was removed.

No service interface is needed solely for mocking: tests replace the HTTP handler under the real bank client. ASP.NET Core data annotations handle field rules; `IValidatableObject` handles combined expiry with an injected clock. Framework JSON binding enforces integer types. Validation messages are sanitized because binding errors can contain submitted values.

## Contracts

- Keep starter `/api/Payments` route. POST returns 201 and a Location header for both completed bank outcomes. GET returns 200 or 404.
- Serialize outcome names as strings. Reject invalid inputs with 400 Validation Problem Details; Rejected is not a stored payment status or a completed-status enum value.
- Restrict currencies to GBP/USD/EUR, reject non-positive amounts, and require a strictly future UTC expiry month. These are explicit assumptions, not extra production features.
- Store and return card suffix only; never persist full card number or CVV. Card/CVV are strings because numeric types lose leading zeros.
- Store only a confirmed bank response. Expose no bank authorization code because the required merchant response does not include it.
- Bound upstream request at five seconds, honor request cancellation, and do not retry a payment. Preserve uncertainty on network failure/timeout rather than inventing a decline.

## Operations

Local HTTP is deliberate. Production TLS would terminate at a configured hosting boundary. The multi-stage Docker image contains only published runtime output and runs as the built-in non-root app user. Compose binds the gateway to loopback; the supplied simulator Compose configuration is unchanged.

Health is process liveness, not a bank dependency probe. Payment outcome/rejection/failure logs and HTTP client timing give useful diagnostics without storing sensitive bodies. A future production deployment would export metrics/traces and define alerts/SLOs; no telemetry platform is introduced for the challenge.

The starter targets .NET 8; retain that target for assessment compatibility. Test-host dependencies are aligned to .NET 8; outdated test tools and Swagger dependencies were updated to remove advisories reported by the NuGet audit. Runtime/framework upgrades should be planned separately for long-lived hosting.

## Known limitations

In-memory state is lost on restart; there is no multi-instance sharing or merchant isolation. Each POST is a new payment: no idempotency, persistent attempt journal, or reconciliation. An upstream authorization can be uncertain if cancellation or process failure occurs before persistence. The API must not claim otherwise. These limitations should drive production work, not inflate the exercise.

## Review against interview competencies

| Competency | Evidence |
| --- | --- |
| Observability | Structured safe logs, trace IDs, HTTP client timings, liveness, log privacy tests |
| Packaging/Hosting | Non-root multi-stage image, Compose, environment-based bank URL, CI build |
| Technical documentation | Runnable README, assumptions, failure matrix, TDD record, demo guide |
| API design | Explicit statuses, Location, safe stable response, validation Problem Details |
| Code design | Small controller, bank boundary, thread-safe in-memory store, built-in validation |
| Testing mindset | Observed red/green cycles, deterministic clock/data, API/bank payload assertions, smoke checks |
