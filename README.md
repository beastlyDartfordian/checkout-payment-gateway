# Payment Gateway — .NET assessment

An ASP.NET Core API for processing a payment through the supplied bank simulator and retrieving the result. Based on [Checkout.com's .NET starter](https://github.com/cko-recruitment/payment-gateway-challenge-dotnet) and [assessment requirements](https://github.com/cko-recruitment/#assessment).

## Run with Docker

Requires Docker Desktop (or Docker Engine with Compose). From the repository root:

```sh
docker compose -f docker-compose.yml -f compose.api.yml up --build -d
```

- Swagger UI: http://localhost:5067/swagger
- Gateway health: http://localhost:5067/health
- Bank simulator: http://localhost:8080/payments

The API container runs as a non-root user. Compose enables Development for local Swagger access. The image defaults to Production, where Swagger is disabled. The original simulator files and `.editorconfig` are unchanged.

```sh
# Inspect gateway logs
docker compose -f docker-compose.yml -f compose.api.yml logs gateway
# Stop both services (clears in-memory payment data)
docker compose -f docker-compose.yml -f compose.api.yml down
```

Compose starts the bank before the gateway, but does not wait for bank readiness. If started immediately, wait for startup before sending the demo request. `/health` reports gateway process health, not bank availability. The supplied Compose file may emit an obsolete `version` warning; this is harmless.

## Run with .NET

Requires .NET 8 SDK, with an 8.0.4xx or later .NET 8 feature band. Docker is only needed for the real simulator; automated tests run without it.

```sh
dotnet restore
dotnet test
docker compose up -d bank_simulator
dotnet run --project src/PaymentGateway.Api --no-launch-profile --urls http://localhost:5067 --environment Development
```

`Bank__BaseUrl` overrides the bank base URL; default `http://localhost:8080/`. Bank requests have a five-second timeout and no automatic retry. Stop the Compose gateway first if running the API directly on the same port.

## API

### Create a payment

```sh
curl -i http://localhost:5067/api/Payments \
  -H 'Content-Type: application/json' \
  -d '{"cardNumber":"2222405343248877","expiryMonth":4,"expiryYear":2031,"currency":"GBP","amount":1050,"cvv":"987"}'
```

Use synthetic test cards only. Update expiry if running after March 2031.

`201 Created` with a `Location` header pointing to the retrieval endpoint:

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "status": "Authorized",
  "cardNumberLastFour": "8877",
  "expiryMonth": 4,
  "expiryYear": 2031,
  "currency": "GBP",
  "amount": 1050
}
```

The ID above is illustrative; each completed payment receives a new GUID. Declined payments also return `201` because a payment result was created; `status` is `Declined`.

### Retrieve a payment

```sh
curl -i http://localhost:5067/api/Payments/REPLACE_WITH_PAYMENT_ID
```

`200 OK` returns the same safe fields as creation. Unknown or malformed IDs return `404`.

### Validation and assumptions

All fields are required and non-null. JSON numbers must be integers, not quoted numbers.

| Field | Accepted values |
| --- | --- |
| `cardNumber` | String of 14–19 ASCII digits; no spaces or Luhn validation |
| `expiryMonth` | Integer 1–12 |
| `expiryYear` | Integer 1–9999; combined month/year strictly later than current UTC month |
| `currency` | Exactly `GBP`, `USD`, or `EUR`; case-sensitive |
| `amount` | Positive integer, 1–2,147,483,647, in minor currency units |
| `cvv` | String of 3–4 ASCII digits; leading zeros preserved |

The spec does not state whether zero/negative amounts or the current expiry month are accepted. This submission rejects non-positive amounts and interprets “in the future” strictly. Expiry uses an injectable `TimeProvider`, so tests remain deterministic. Card suffixes are strings to preserve leading zeros.

Invalid requests return `400` with validation Problem Details and field names. This represents the challenge's **Rejected** outcome; no payment is created and the bank is not called. Error messages do not echo submitted values.

| Condition | HTTP response | Payment stored? |
| --- | --- | --- |
| Authorized/declined bank response | `201`, payment result | Yes |
| Invalid request | `400`, validation errors | No |
| Unknown payment | `404` | No |
| Bank `503` or network failure | `503`, Problem Details | No |
| Bank timeout | `504`, Problem Details | No |
| Other bank HTTP failure or invalid response | `502`, Problem Details | No |

An upstream failure is not a decline. A timeout/network failure may mean the bank processed the request without the gateway receiving the result. No automatic retry is performed. This exercise does not implement idempotency or reconciliation; a client must not blindly retry an uncertain payment.

## Tests and TDD

```sh
dotnet test
dotnet build -c Release --warnaserror
dotnet test -c Release --collect:"XPlat Code Coverage"
# Optional real simulator smoke checks; Python 3, running services required
python3 scripts/smoke.py
```

Tests exercise HTTP requests through ASP.NET Core's in-process test server. A recording HTTP handler controls only the bank boundary; routing, JSON binding, validation, controller, mapping, and in-memory storage run together. Exact response JSON and outbound bank payload are asserted. Tests cover authorization, declines, validation boundaries, leading zeros, safe responses/logs, missing IDs, upstream failures, timeouts, and health. They need no Docker or network.

See [TDD record](docs/tdd.md) for observed red/green runs, [design decisions](docs/design.md), [review/demo guide](docs/demo.md), and [verification results](docs/verification.md). CI builds with warnings treated as errors, runs tests with coverage, and builds the container.

## Observability and scope

Structured logs record payment ID/outcome, validation rejection, bank failure status, and trace ID. Standard HTTP client logs provide upstream duration and status; logs exclude request/response bodies, full card numbers, and CVVs. Problem Details include a trace ID. `/health` is a liveness check; a bank outage does not make the gateway process unhealthy.

Storage is in-memory and thread-safe; data disappears on restart and is not shared across replicas. Only the safe payment response fields are stored. No database, CQRS, mediator, generic repository framework, retry library, or additional infrastructure is needed for this exercise.

This is a local assessment submission, not a production payment service. Production would require merchant authentication/authorization, TLS, durable storage, idempotency/reconciliation, protected deployment and monitoring, and a separate review of sensitive payment data handling. No public service is deployed by this repository.
