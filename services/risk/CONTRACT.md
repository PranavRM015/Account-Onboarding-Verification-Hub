# Risk service: Intake integration contract

Risk is a standalone, stateless API that runs the existing mock screening rules. It does not save customers, tickets, flags, or vendor responses. Intake calls it before creating a ticket in Review. The original backend still uses its original `Vendor.cs` until the Intake developer wires in this service.

The analysis request and response fields below are unchanged from the prototype. These are the only endpoints Intake needs: `POST /internal/analyze` and the optional availability check `GET /health`. There are no account-creation, ticket-saving, or flag-clearing endpoints in Risk.

## For Intake: the complete call

1. Obtain the customer profile from Customer intake, keeping the customer ID in Intake.
2. POST the screening fields below to Risk, forwarding the signed-in admin's token unchanged in `Authorization: Bearer <token>`. Set `Content-Type: application/json`.
3. On `200`, pass the returned `vendorResponse`, `flags`, and `riskRating` to Review along with Intake's customer/ticket context. A High rating is a successful assessment, not an HTTP error.
4. On any non-`200`, timeout, or connection failure, do not substitute Low risk or proceed as if screening succeeded. Follow Intake's failure path and release the customer claim if one was acquired.

Configure the Risk base address in Intake rather than hardcoding it. Use an asynchronous HTTP call with a bounded timeout (15 seconds is a starting point). Risk's JWKS HTTP request times out after 5 seconds. An analysis writes nothing, so retrying the same analysis after a transient failure is safe; coordinate retries and the overall deadline with Intake's claim handling. Do not retry `400`/`403` without correcting the request or caller. A `401` requires a valid admin token.

## Authentication

`POST /internal/analyze` requires a valid Identity JWT with `role: "admin"`, all lowercase. Customer tokens receive `403`. Cookies and `X-Internal-Key` are not accepted in place of a Bearer token. Identity's account-creation API uses an internal key; Risk instead follows the architecture's forwarded-admin-token policy.

Risk validates the RS256 signature using Identity's `/.well-known/jwks.json`, the issuer, audience, expiry, and a 30-second clock allowance. It maps the `role` and `name` claims without renaming them. It never needs Identity's private key, credentials database, or internal API key. Legacy monolith tokens are not supported; use tokens from the new Identity service.

Signing keys are cached and refreshed, including refresh attempts for an unrecognized key ID. Identity should overlap old and new public keys while rotating them. With cached keys, Risk can continue validating tokens if Identity is temporarily down. With no usable cached keys, a failed key retrieval returns `503`. `/health` does not call Identity: it checks the running Risk process with its loaded screening files, not end-to-end authentication readiness.

As specified by Identity's contract, Risk does not consult Identity's revoked-token table. A logged-out token may remain usable here until it expires; Intake should stop forwarding it after logout.

## Run locally

From the repository root, with the .NET 10 SDK installed:

```powershell
dotnet run --project services/risk/Risk.Api.csproj --launch-profile http
```

In another terminal:

```powershell
Invoke-RestMethod http://127.0.0.1:5291/health
```

`GET /health` returns `200` with `{ "status": "ok" }`. Both CSV files are copied to build and publish output and loaded at startup. Restart after changing them. Missing files stop startup rather than produce a misleading Low rating.

Run Identity separately on port 5290 when using the default development settings. Risk itself needs no database or migrations. The launch profile binds Risk to `http://127.0.0.1:5291`; `ASPNETCORE_URLS` or `--urls` can configure another address when running without that launch profile.

## Configuration

| Setting / environment variable | Value |
|---|---|
| `Identity:JwksUrl` / `Identity__JwksUrl` | Required full JWKS URL. Development default: `http://localhost:5290/.well-known/jwks.json`. No production default; set it for the deployment. |
| `Identity:Issuer` / `Identity__Issuer` | `AccountVerificationOnboardingHub`; must match Identity. |
| `Identity:Audience` / `Identity__Audience` | `AccountVerificationOnboardingHub`; must match Identity. |
| `Identity:RequireHttps` / `Identity__RequireHttps` | `true` by default, `false` in Development for local HTTP. Set `false` explicitly only for HTTP on a trusted private service network. |
| `ASPNETCORE_URLS` | Listening address; Docker uses `http://+:8080`. |

Invalid/missing JWKS configuration prevents startup. Identity may be started later; Risk fetches keys when validating tokens. Keep `/internal/**` off the public gateway and make the call from Intake's backend, not Angular. No browser CORS configuration is needed for this service-to-service call.

## Docker

From the repository root:

```powershell
docker build -t verificationhub-risk -f services/risk/Dockerfile services/risk
```

For Docker Desktop with Identity running on the host at port 5290:

```powershell
docker run --rm --name risk -p 127.0.0.1:5291:8080 -e Identity__JwksUrl=http://host.docker.internal:5290/.well-known/jwks.json -e Identity__RequireHttps=false verificationhub-risk
```

When Identity and Intake are containers on the same Docker network, set the JWKS URL to Identity's network name and actual container port. Intake uses `http://risk:8080` if this container is named `risk`; it must not use its own `localhost`. For HTTPS deployments, leave `Identity__RequireHttps=true` and set the HTTPS JWKS URL. The image runs as the runtime image's non-root app user and includes both CSV files. Container orchestration and public gateway routing belong to the team's deployment setup.

## Analyze a customer

`POST /internal/analyze`, with these headers:

```text
Authorization: Bearer <Identity admin JWT>
Content-Type: application/json
```

```json
{
  "firstName": "Sam",
  "middleName": "Lee",
  "lastName": "Taylor",
  "taxIdLastFourDigits": "1234",
  "residentialAddress": "12 Main Street",
  "countryOfResidence": "US",
  "countryOfCitizenship": "US",
  "occupation": "Teacher",
  "employer": "City School"
}
```

All fields except `middleName` are required. Names have a 50-character limit, address 200, occupation/employer 100. Tax ID suffix is exactly four ASCII digits. Countries must be two uppercase letters; this checks format, not membership in an ISO list. No credentials, full tax ID, customer ID, admin ID, or ticket ID is needed.

Success (`200`):

```json
{
  "riskRating": "Low",
  "flags": [],
  "vendorResponse": {
    "nameMatch": "Match",
    "dateOfBirthMatch": "Match",
    "addressMatch": "Match",
    "taxIdMatch": "Match",
    "score": 50,
    "riskCodes": ""
  }
}
```

Callers retain their customer identifier and associate the result with that customer; the API stores no assessment history. `flags` is an array of violation-type strings, not saved flag objects. `riskCodes` remains a comma-separated string, not an array. No timestamp or attribution is returned: Review creates database IDs, timestamps, and admin attribution when saving the assessment.

| HTTP status | Meaning / Intake action |
|---|---|
| `200` | Assessment completed. `riskRating` is `Low`, `Medium`, or `High`; save the findings through Review. |
| `400` | Missing, malformed, or invalid JSON/fields. ASP.NET validation problem details include an `errors` object. Correct the payload. |
| `401` | Missing, invalid, expired, wrong-issuer/audience/signature token. Response has a Bearer challenge; do not rely on a JSON body. Sign in again as needed. |
| `403` | Token is valid but does not have the lowercase `admin` role. Do not retry as the same caller. |
| `415` | Unsupported content type. Send JSON. |
| `503` | Identity keys could not be obtained and no usable keys are cached. Problem-details response. Retry within Intake's bounded policy or fail the operation. |
| `500` | Unexpected service error. Generic problem details; do not continue ticket creation as if analysis succeeded. |

Connection failures and timeouts have no HTTP response. Intake handles them using the same failed-analysis path. Validation occurs after authentication, so an invalid token can return `401`/`403` before field validation.

## Preserved behavior

- OFAC name match: `OfacMatch`, High. Matching ignores case and surrounding name whitespace. Missing middle names on either side do not prevent a match.
- Residence or citizenship outside US/MX: one `ClosedJurisdiction`, Medium.
- Occupation or employer containing cannabis, marijuana, dispensary, hemp, thc, or cbd: one `ProhibitedIndustry`, High. Matching remains substring-based and case-insensitive.
- Last four digits appearing in `DeceasedSSNs.csv`: `taxIdMatch` becomes `NoMatch`, score loses 20, `DeceasedSSN` risk code.
- Address containing correctional, penitentiary, state prison, detention center, or jail: `addressMatch` becomes `Partial`, score loses 10, `CorrectionalAddress` risk code.
- Either vendor finding raises one `VerificationFailed`, Medium. Both penalties accumulate. Risk codes are comma-separated, matching the existing backend representation.
- Highest rating wins; clean customers are Low. Flag order matches the old pipeline: verification, OFAC, jurisdiction, industry.

These are the project's mock rules, not a connection to an external verification provider. Name and date-of-birth vendor results remain `Match`; OFAC flags are separate. Flags are violation-type strings without database IDs, timestamps, or admin attribution. The future ticket service owns persistence and attribution.
