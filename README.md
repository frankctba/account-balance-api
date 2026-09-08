# Account Balance API

A small HTTP API that keeps account balances consistent across deposits, withdrawals and transfers.
State lives in memory for the lifetime of the process.

## Requirements

- [.NET SDK 10.0](https://dotnet.microsoft.com/download) (LTS)

## Running

```bash
dotnet run --project src/AccountApi.Api
```

The API listens on `http://localhost:5000`. The file
[`src/AccountApi.Api/AccountApi.http`](src/AccountApi.Api/AccountApi.http) contains a ready-to-run
request sequence for VS Code (REST Client extension) or JetBrains Rider.

## Testing

```bash
dotnet test
```

The suite has three kinds of tests, all running against real implementations (no mocks):

| Kind        | Location                                                       | What it proves                                                                  |
|-------------|----------------------------------------------------------------|---------------------------------------------------------------------------------|
| Unit        | `tests/AccountApi.Tests/Unit/AccountTests.cs`                   | Account invariants: positive amounts, no negative balance.                      |
| Unit        | `tests/AccountApi.Tests/Unit/AccountServiceTests.cs`            | Every business rule, asserted on persisted state in the real in-memory store.   |
| Concurrency | `tests/AccountApi.Tests/Unit/AccountServiceConcurrencyTests.cs` | Parallel operations keep balances exact; transfers never create or lose funds.  |
| Integration | `tests/AccountApi.Tests/Integration/AccountApiTests.cs`         | Full HTTP flow with literal response bodies, error cases, no side effects on GET.|

## API

| Method | Path                       | Description                                  | Success            |
|--------|----------------------------|----------------------------------------------|--------------------|
| POST   | `/reset`                   | Removes every account.                       | `200` `OK`         |
| GET    | `/balance?account_id={id}` | Current balance. Never creates an account.   | `200` `20`         |
| POST   | `/event`                   | Applies a deposit, withdraw or transfer.     | `201` see below    |

### Events

```jsonc
// deposit: creates the destination account on first use
{"type": "deposit", "destination": "100", "amount": 10}
// -> {"destination": {"id": "100", "balance": 10}}

// withdraw
{"type": "withdraw", "origin": "100", "amount": 5}
// -> {"origin": {"id": "100", "balance": 15}}

// transfer: creates the destination account if needed
{"type": "transfer", "origin": "100", "destination": "300", "amount": 15}
// -> {"origin": {"id": "100", "balance": 0}, "destination": {"id": "300", "balance": 15}}
```

Amounts are JSON numbers with decimal precision. Account ids are opaque strings.

### Errors

| Status | When                                                        | Body                                   |
|--------|-------------------------------------------------------------|----------------------------------------|
| `404`  | Origin account does not exist (balance, withdraw, transfer) | `0` (mandated by the API contract)     |
| `422`  | Insufficient funds                                          | RFC 9457 problem details with `detail` |
| `400`  | Non-positive amount, missing field, unknown type, bad JSON  | RFC 9457 problem details               |

A failed operation never changes any balance.

## Project layout

```
src/AccountApi.Core   business rules, no dependency on ASP.NET
src/AccountApi.Api    HTTP contracts, routing and error mapping only
tests/AccountApi.Tests
```

- `Account` is the single entity. It owns the invariants (positive amounts, no negative balance).
- `AccountService` implements the four operations and is the only place that touches the store.
- `IAccountStore` is the persistence boundary. `InMemoryAccountStore` is the only implementation.
- The API layer validates the shape of the request, calls the service and maps results and
  domain exceptions to HTTP. It contains no business rules.

## Design decisions

**Single lock in the service.** Every operation runs under one `lock`. This is the simplest way to
make transfers atomic (two accounts change together or not at all) and to guarantee that no reader
ever sees an intermediate state. The trade-off is that all operations are serialized, which is fine
for an in-memory service and easy to reason about. Snapshots are taken inside the lock so the
response always reflects the state produced by that exact operation.

**Validate before mutating.** In a transfer, the origin is looked up and debited (which validates
funds and amount) before the destination is credited. Any failure happens before the first write.

**GET never writes.** Only deposit and transfer create accounts. `GET /balance` on an unknown
account returns `404` and leaves the store untouched, and the tests assert this explicitly.

**Synchronous domain.** The store has no I/O, so the service is synchronous. Wrapping it in
`Task` would add noise without adding concurrency. If a real database were introduced, the
store interface and service would become `async` at that point.

**Exceptions for rule violations.** Domain exceptions (`AccountNotFoundException`,
`InsufficientFundsException`, `InvalidAmountException`) are mapped to HTTP in one endpoint
filter. This keeps the service free of HTTP concerns and the mapping in a single, visible place.

**`decimal` for money.** Binary floating point is unsuitable for currency. Amounts are strictly
JSON numbers; strings such as `"10"` are rejected.

**No extra abstractions.** No repository generics, mediator, mapper or validation library. Four
endpoints do not justify them.

## What would change for production

- **Persistence**: replace `InMemoryAccountStore` with a database-backed store and move atomicity
  from the in-process lock to database transactions (or optimistic concurrency with a version
  column per account).
- **Horizontal scaling**: the in-memory state and the process-wide lock only work on one
  instance. With shared storage, per-account row locks (acquired in a stable order) or a
  serializable transaction replace the global lock.
- **Idempotency**: events should carry a client-generated id so that retries do not apply the
  same deposit twice.
- **Ledger**: record each event as an immutable entry and derive balances from it, which gives
  an audit trail and makes reconciliation possible.
- **Observability**: structured logging, metrics per event type and request tracing.
