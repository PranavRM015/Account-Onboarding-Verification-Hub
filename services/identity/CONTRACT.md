# Identity service: how to use it

The Identity service handles **logins only**. It stores the username, the password (as a hash), the recovery question and answer, and the lock-out state. It does not store names, addresses, tax IDs or any other profile data. That stays in the Intake service.

It does four jobs:

1. It creates a login when Intake asks it to.
2. It checks usernames and passwords, and hands out a token (a JWT).
3. It handles forgot-password.
4. It publishes a public key, so every other service can check that a token is real.

---

## The ids

Every login has an id. **Identity does not make the id. Intake does.**

- Intake saves the profile first, and the database gives it a `customerId` or an `adminId`.
- Intake then asks Identity to create the login using that same id.
- Identity puts the id into the token, so other services know who is calling without asking anyone.

---

## Running it on your machine

| Thing | Value |
|---|---|
| Address | `http://localhost:5290` |
| Database | `VerificationHub_Identity` (create it with `dotnet ef database update` inside `services/identity`) |
| Connection string name | `IdentityConnection` |
| Start it | `dotnet run --launch-profile http` inside `services/identity` |
| Health check | `GET /health` (answers `{"status":"ok"}`) |

The dev settings (the private key and the internal key) are in `appsettings.Development.json`. In production both must be set as secrets (`Jwt:PrivateKeyPem` and `Internal:ApiKey`). The service will not start without them.

---

## 1. For Intake: creating a login

**These two endpoints are for other services only. The browser must never call them.**

Every call needs this header:

```
X-Internal-Key: <the shared internal key>
```

Without the right key you get `401`.

### Create a login

`POST /internal/accounts`

```json
{
  "role": "customer",
  "id": 42,
  "username": "pranav01",
  "password": "Str0ng!Passw0rd#2026",
  "passwordRecoveryQuestion": "What is your favorite food?",
  "passwordRecoveryAnswer": "Pizza"
}
```

| Field | Rule |
|---|---|
| `role` | `"admin"` or `"customer"` (all lowercase) |
| `id` | The `adminId` or `customerId` Intake just made. Must be more than 0. |
| `username` | 3 to 40 characters |
| `password` | At least 8 characters, with an uppercase letter, a lowercase letter, a number and a special character |
| `passwordRecoveryQuestion` | Must be one of the four questions below, spelled exactly like this |
| `passwordRecoveryAnswer` | Cannot be empty. Capital letters and spaces around it are ignored. |

The four recovery questions:

- `What is your favorite food?`
- `What was the name of the elementary school you went to?`
- `What is the name of your first pet?`
- `Where was your last vacation?`

**What you get back:**

| Code | Meaning | Body |
|---|---|---|
| `200` | Login created | `{ "role": "customer", "id": 42, "username": "pranav01" }` |
| `400` | Something in the request is wrong | A plain sentence you can show to the user |
| `401` | Wrong or missing internal key | empty |
| `409` | That username is taken, or that id already has a login | A plain sentence you can show to the user |
| `500` | Something broke inside Identity | A problem message |

### Check if a username is free (optional)

`GET /internal/accounts/username-available?role=customer&username=pranav01`

Answers `200` with `{ "available": true }` or `{ "available": false }`.

This is only a courtesy check. Two people can still pick the same name at the same moment, so always handle `409` from the create call too.

### What Intake should do on signup

1. Check the form and save the profile. You now have the id.
2. (Optional) call `username-available` first, so you can reject a taken name before you save anything.
3. Call `POST /internal/accounts` with that id.
4. If Identity answers **anything other than 200**, **delete the profile you just saved** and tell the user what went wrong.
5. Put a time limit on the call (a few seconds) so signup does not hang when Identity is down.

Intake should call Identity **last**. Then if Identity fails, Intake only has one thing to undo.

---

## 2. For the browser: signing in

These keep the same URLs the Angular app uses today.

### Log in

| Who | URL |
|---|---|
| Admin | `POST /api/admins/login` |
| Customer | `POST /api/users/login` |

Body:

```json
{ "username": "pranav01", "password": "Str0ng!Passw0rd#2026" }
```

Success (`200`) for a customer:

```json
{
  "message": "Customer logged in successfully",
  "username": "pranav01",
  "role": "customer",
  "customerId": 42,
  "token": "eyJhbGciOi...",
  "expiresAt": "2026-11-03T01:30:00Z"
}
```

An admin gets the same thing with `"role": "admin"` and `adminId` instead of `customerId`.

**`firstName` is not returned any more.** Identity does not know it. If the home page needs to greet the admin, it must ask Intake.

| Code | Meaning |
|---|---|
| `400` | Username or password missing |
| `401` | Wrong username or password (the message does not say which) |
| `423` | Account is locked. The message says how many minutes are left. |
| `429` | Too many requests. Wait a minute. |

**Lock-out:** 3 wrong passwords in a row locks the account for 15 minutes. Wrong answers in forgot-password count as well.

### Using the token

Send it on every request to any service:

```
Authorization: Bearer <token>
```

A token lasts 60 minutes by default.

### Log out

`POST /api/admins/logout` (works for both admins and customers; the token must be sent)

Answers `200` with `{ "message": "Logged out" }`. After this the token is dead.

> **Note:** only Identity knows about logged-out tokens. The other services cannot see them, so a logged-out token keeps working on other services until it expires. This is why tokens are short-lived.

### Who am I (customers only)

`GET /api/users/me` answers `200` with `{ "username": "...", "role": "customer" }`. An admin token gets `403`. No token gets `401`.

### Forgot password (same for both, use `/api/admins/` or `/api/users/`)

1. `GET /api/users/recovery-question?username=pranav01` answers `{ "question": "..." }`. An unknown username still gets a question, so nobody can use this to find out which accounts exist.
2. `POST /api/users/reset-password` with:

```json
{ "username": "pranav01", "answer": "pizza", "newPassword": "N3w!Passw0rd#2027" }
```

Answers `200` with `{ "message": "Password updated" }`. A wrong answer gets `401` (and counts towards the lock-out). A weak new password gets `400`. A correct answer also clears a lock-out.

**Rate limit:** the login, recovery and reset endpoints allow 10 requests per minute from one IP address. The internal endpoints have no limit.

---

## 3. For every other service: checking a token

You do not need any secret from Identity. You only need its **public key**.

- The public key is at `GET /.well-known/jwks.json`.
- The token is signed with **RS256**.
- Each key has an id (`kid`). The token header carries the same `kid`, so when Identity changes its key, you can pick the right one.

Check these things when you validate a token:

| Check | Value |
|---|---|
| Issuer | `AccountVerificationOnboardingHub` |
| Audience | `AccountVerificationOnboardingHub` |
| Signature | the key from the JWKS address |
| Expiry | must not be expired (30 seconds of grace is fine) |
| Role claim name | `role` |
| Name claim name | `name` |

In .NET, set `MapInboundClaims = false` on the JWT bearer options. If you do not, the claim names get renamed and `role` stops matching.

### What is inside a token

| Claim | Example | Notes |
|---|---|---|
| `name` | `pranav01` | The username |
| `role` | `customer` | Always lowercase: `admin` or `customer` |
| `adminId` | `7` | Only in admin tokens |
| `customerId` | `42` | Only in customer tokens |
| `jti` | `8f2c...` | A unique id for this token |
| `exp` | `1793670000` | When it expires |

Use `[Authorize(Roles = "admin")]` to protect admin endpoints. Read the id straight from the token. There is no need to look anything up in the Identity database, and no other service is allowed to read it.

---

## 4. Things that changed from the old backend

- The role names are now `admin` and `customer`, all lowercase. The old ones were `Admin` and `User`.
- The login answer has no `firstName`.
- There is no cookie sign-in any more. Only the `Authorization` header works.
- There is no public signup endpoint on Identity. Signup goes through Intake.
- All tokens made by the old backend stop working when we switch over. Everyone will have to log in again once.

## 5. Existing accounts

The script `services/identity/database/MigrateFromMonolith.sql` copies the existing admins and customers (ids and password hashes) into the Identity database. Nobody has to reset a password. It is safe to run more than once.

## 6. First admin

If the Identity database has no admins, it can make one at startup from these settings: `BootstrapAdmin:Username`, `BootstrapAdmin:Password` and `BootstrapAdmin:AdminId` (default `1`). **Intake must have a profile with that same `adminId`**, or the admin will have a login but no profile.
