# Risk prototype

This is a standalone, stateless extraction of the existing mock screening rules. It does not save customers, tickets, flags, or vendor responses. The existing backend still uses its original `Vendor.cs`; nothing calls this API yet.

Authentication, backend integration, and Docker are deferred. The prototype binds only to `127.0.0.1:5291` because its endpoints do not yet require a token. Do not expose it through a gateway or deploy it as a public service.

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

## Analyze a customer

`POST /internal/analyze`, with `Content-Type: application/json`:

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

Invalid or missing fields return `400` validation problem details. Unsupported content types return `415`. Callers retain their customer identifier and associate the result with that customer; the API stores no assessment history.

## Preserved behavior

- OFAC name match: `OfacMatch`, High. Matching ignores case and surrounding name whitespace. Missing middle names on either side do not prevent a match.
- Residence or citizenship outside US/MX: one `ClosedJurisdiction`, Medium.
- Occupation or employer containing cannabis, marijuana, dispensary, hemp, thc, or cbd: one `ProhibitedIndustry`, High. Matching remains substring-based and case-insensitive.
- Last four digits appearing in `DeceasedSSNs.csv`: `taxIdMatch` becomes `NoMatch`, score loses 20, `DeceasedSSN` risk code.
- Address containing correctional, penitentiary, state prison, detention center, or jail: `addressMatch` becomes `Partial`, score loses 10, `CorrectionalAddress` risk code.
- Either vendor finding raises one `VerificationFailed`, Medium. Both penalties accumulate. Risk codes are comma-separated, matching the existing backend representation.
- Highest rating wins; clean customers are Low. Flag order matches the old pipeline: verification, OFAC, jurisdiction, industry.

These are the project's mock rules, not a connection to an external verification provider. Name and date-of-birth vendor results remain `Match`; OFAC flags are separate. Flags are violation-type strings without database IDs, timestamps, or admin attribution. The future ticket service owns persistence and attribution.
