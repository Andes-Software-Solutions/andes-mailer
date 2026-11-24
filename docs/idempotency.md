# Idempotency

A request can succeed and still look like a failure: the connection drops after the API saved the change but before the answer arrives. Without help, a client that tries again adds the member twice, or creates a second team. Idempotency keys fix that. The client labels each change with a unique key; if the same key arrives again, the API sends back the first response and does not repeat the change. This page covers the API contract, then how the Angular app sends keys. For the routes it covers, see [Teams](teams.md).

## How it fits together

- A key belongs to one **user intent**, such as "add these three people". The client creates it and sends it in the `Idempotency-Key` header.
- The API remembers the key and the response for a short time, per caller, in the memory of the API process.
- A repeat with the same key and the same request returns the **first success again**, marked with `Idempotent-Replayed: true`.
- The header is optional. A request without it behaves as it always did.

## Which routes

Every write on teams and team members:

| Route | Notes |
| --- | --- |
| `POST /api/teams` | A replay returns the same 201 and `Location`, so a retry never creates a second team. |
| `PUT /api/teams/{teamId}` | |
| `DELETE /api/teams/{teamId}` | |
| `POST /api/teams/{teamId}/members` | |
| `PUT /api/teams/{teamId}/members/{userId}` | |
| `DELETE /api/teams/{teamId}/members/{userId}` | |
| `DELETE /api/teams/{teamId}/members/me` | Leaving a team; see [Filter order](#filter-order). |

Other routes ignore the header. The OpenAPI document (and so Scalar) lists `Idempotency-Key` as an optional header on exactly these operations; the `.WithIdempotency()` endpoint extension marks them.

## The contract

### The header

```http
POST /api/teams/0199fa10-3c4d-7e21-b6a0-9d8c7b6a5f40/members
Idempotency-Key: 5b0c1c2e-6a9b-4f55-9d3f-0c3f3a7e41aa
Content-Type: application/json

{ "email": "ana@andes.example", "role": "TeamMember" }
```

| Rule | Detail |
| --- | --- |
| Length | 1 to 128 characters. |
| Characters | Visible ASCII only (no spaces or control characters). A UUID is a good choice. |
| Quotes | A value wrapped in double quotes is accepted and the quotes are removed. |
| Count | One header. Two `Idempotency-Key` headers, an empty value, or a bad character is a 400. |

### What the API compares

Two requests are "the same" when they have the same **caller**, **key**, and **fingerprint**:

- The caller is the `oid` claim of the token. Keys are per caller, so one person's key never replays another person's response, and the same string from two people means two different intents. A token with no `oid` gets a 403.
- The fingerprint is a SHA-256 hash of the HTTP method, the upper-cased path, and the bound request body (the DTO the endpoint reads). Query strings and other headers are not part of it.

### Outcomes

| Situation | Result |
| --- | --- |
| New key | The request runs. |
| Same key and fingerprint, first request **succeeded** (any 2xx) | The first response is returned again with `Idempotent-Replayed: true`. The change is not repeated. |
| Same key, **different** method, path, or body | 422 Unprocessable Content. The key already means another request; use a new one. |
| Same key and fingerprint while the first request is **still running** | 409 with `Retry-After: 1`. Wait and send the same request again. |
| First request **failed** (4xx, 5xx, or an exception) | Nothing is stored. The key is free, so the client can fix the request and reuse it. |
| No header | The request runs with no memory of it. |

Only successes are kept because a failure left nothing to repeat, and a client that fixes a validation error should not have to invent a new key.

### How long a key lives

Entries live for `Idempotency:Expiration`, counted from the moment the request **completed**. It defaults to 5 minutes and must be between 1 second and 24 hours. A request that never finishes holds its key for the same time, then the key frees itself.

```json
{
  "Idempotency": {
    "Expiration": "00:05:00"
  }
}
```

The API validates the value at startup and refuses to start with one outside the range.

### Filter order

`IdempotencyEndpointFilter` is added **first** on each route, before the permission and validation filters. That is deliberate: a change such as leaving a team makes the permission filter refuse the very same request from then on, so a client that retries `DELETE .../members/me` after a lost response would get a 404 instead of its 204.

The consequence to know: **a replay returns the caller's own earlier success even when the permission check would now fail**, such as after they leave a team or remove themselves. It never returns anything to a different caller, because the key is scoped to the caller's `oid`.

### Limits

- **One API instance.** The store is a dictionary in process memory, like local [attachment staging](emails-and-attachments.md). With two instances, a retry that reaches the other one runs the change again. Moving to several instances needs a shared store first.
- **A restart forgets every key.** A retry after a restart runs again.
- **Only teams and members.** Emails, templates, contact groups, and users do not take the header yet.

### Problem Details

Both error cases are standard Problem Details responses:

| Status | `type` | When |
| --- | --- | --- |
| 422 | `https://tools.ietf.org/html/rfc9110#section-15.5.21` | The key was used with a different request. |
| 409 | `https://tools.ietf.org/html/rfc9110#section-15.5.10` | The first request is still running. `Retry-After: 1` is set. |

## How the UI sends keys

The Angular app does not build the header by hand. Three pieces in `andes-mailer-ui/src/app/core/api/idempotency/` do it.

| Piece | Does |
| --- | --- |
| `IDEMPOTENCY_KEY` and `idempotent(key)` | An `HttpContextToken` that carries a key on one request. The `*Api` classes (`TeamsApi`, `TeamMembersApi`) take a key as an argument on every write and attach it with `idempotent(key)`. |
| `idempotencyInterceptor` | Adds `Idempotency-Key` to a request that carries a key, and retries it after a transient failure, with the same key. |
| `IdempotentAction` | Holds one key for one user intent, and decides when a new intent needs a new key. |

### The interceptor

Registered **before** `authInterceptor` in `app.config.ts`, so each retry passes through the auth interceptor and gets a fresh token. A request without a key passes through untouched, because only a keyed write is safe to repeat.

It retries up to **2** times when the failure is transient:

| Failure | Why it is safe to repeat |
| --- | --- |
| Status 0 (no response), 408, 502, 503, 504 | The request may never have reached the API, or it did and the answer was lost. The key makes either case safe. |
| 409 **with** a `Retry-After` header | The API says the first request with this key is still running. |

The wait is the `Retry-After` value when there is one, and otherwise 500 ms then 1 s. Any other error, including a plain 409 such as "already a member", is final: it goes straight to the caller.

### One key per intent

`IdempotentAction` is what makes **Save** safe to press twice. A store keeps one per thing the person can do (the settings form has one for Save and one for Leave; the members page has one for adding, and one per member for changing or removing).

```ts
const key = action.keyFor(body);   // a new UUID, or the previous one if body is unchanged
try {
  await firstValueFrom(api.update(team.id, body, key));
  action.settle();                 // success: the next save is a new intent
} catch (error) {
  action.settle(error);            // keeps the key only if the failure was transient
}
```

- The key **stays** while the payload is unchanged and the last attempt failed in a way that may not have reached the API. Pressing Save again after a dropped connection replays the first result.
- The key **resets** after a success, after any answer the API definitely gave (a 400, 403, or a plain 409), and whenever the payload changes. A changed payload with the old key would be a 422.

Buttons are also disabled while a request is in flight, so a double click sends one request, not two with the same key.

### Adding several people

The **Add members** dialog sends one `POST` per selected person, each under a key tied to that person's request. People it fails to add stay selected, and pressing the button again sends them with the **same** keys: anyone who was in fact added the first time is answered with a replay, and does not turn into a 409 "already a member".

## Add idempotency to a new route

1. In the API, add `.WithIdempotency()` **first** in the endpoint's chain, before any `AddEndpointFilter`. The OpenAPI header comes with it.
2. Make the route's result a typed HTTP result with a status code (`TypedResults.Created`, `Ok`, `NoContent`); only those are stored.
3. In the UI, take a key in the `*Api` method and attach it with `idempotent(key)`, and create an `IdempotentAction` in the store that calls it. Call `keyFor(payload)` before each send, and `settle()` or `settle(error)` after.
4. Cover replay, mismatch, and in-progress in an integration test; `IdempotencyIntegrationTests` shows each.

## Known limitations

- **Single instance and in-memory only.** See [Limits](#limits).
- **A replay skips the permission check.** See [Filter order](#filter-order).
- **A replay repeats the stored response, not a fresh read.** A replayed `PUT` returns the `version` from the first answer even if the team has changed since, so a client that gets `Idempotent-Replayed: true` should read the resource again before editing it further.
