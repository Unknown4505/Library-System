# Issue #103 — Supplier contact validation

Owner: thien33. Branch: thien1. Base: main c0aa046fa42f6c368e153cf36e5421913564b536.

## Contract for FE #105

Create and Update use the same rules, including legacy `phone` alias for `phoneNumber`.
Send one phone property, not both. Phone is required, raw maximum 20 characters.
Leading/trailing whitespace is trimmed. Supported inputs: domestic mobile
`0[35789]` plus eight ASCII digits, or geographic landline `02` plus nine digits.
An international `+84` can replace the leading domestic `0`.
Examples: `0912345678`, `+84912345678`, `02838221234`, `+842838221234`.
Stored representation is domestic digits. Internal spaces, hyphens, extensions,
service short codes and non-ASCII digits are not supported. This checks format,
not whether a telephone number is allocated or reachable.

Email is optional; blank/whitespace becomes null, surrounding whitespace is trimmed.
Raw maximum 100 characters; nonempty values require EmailAddress validation and no control characters.
Name, ContactName and Address limits are unchanged. Shared company contacts are permitted.
No schema/unique constraint changes. ContactName remains persisted from PR #102.

Invalid input returns HTTP 400 ApiResponseDto with BAD_REQUEST and a safe Vietnamese message.
Malformed JSON has the same wrapper; other controllers retain their existing contracts.
Valid missing update target remains 404. Writes remain Admin/Manager; Staff gets 403,
anonymous/invalid JWT gets 401. Successful Create/Update retains existing HTTP 200.

## Developer verification

Targeted Release tests: 15 passed, 0 failed, 0 skipped. Includes real API pipeline,
JWT and disposable SQL Server, invalid Create/Update no mutation, normalization,
ContactName roundtrip, shared contacts, malformed JSON and missing record.
Full solution Release build: 0 errors, 111 warnings (existing dependencies/nullability).
SQL QA fixture drops its generated database and verifies removal.
This is developer QA; independent Leader/FE acceptance is still required.

Full Release regression: 255 passed, 0 failed, 0 skipped. git diff --check passed.
