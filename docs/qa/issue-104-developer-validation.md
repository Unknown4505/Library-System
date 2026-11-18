# Issue #104 — Own account API

Owner: thien33. Branch: thien2. Base: main c0aa046fa42f6c368e153cf36e5421913564b536.

## Contract for FE #107

- GET /api/account/me: bearer JWT required. 200 ApiResponseDto data contains only
  userId, username, fullName, role (string), isActive. Cache-Control: no-store.
- POST /api/account/change-password: JSON {"currentPassword":"...","newPassword":"..."}.
  Additional fields are rejected with 400; never supply target userId/role/username.
  Successful response: 200 ApiResponseDto, SUCCESS, null data.
- New password: >=8 characters, ASCII uppercase/lowercase and a digit, no controls,
  <=72 UTF-8 bytes (BCrypt input limit). Passwords are never trimmed; new must differ
  from current. Current input bounded at 1024 characters; request body <=8192 bytes.
- Wrong old password/invalid body or new password: 400 BAD_REQUEST, no change.
- Missing/invalid JWT or invalid/missing/nonpositive actor, missing/inactive account:
  401. Identity comes exclusively from validated JWT, never query/body input.
- Concurrent change/deactivation after password verification: 409 CONFLICT,
  safe retry message. Provider/unexpected error: safe 500 INTERNAL_ERROR.

Database update compares the original password hash using binary collation and
requires an active account in one SQL statement. Only PasswordHash is updated;
no schema changes or process-local lock. Two requests cannot both overwrite the
same original hash. Admin reset remains unchanged and Login regression is tested.

## Session and UI integration

Existing JWTs remain valid until expiry: no revocation store/security stamp exists.
Password change affects future password login only. FE should show success without
claiming global logout; if it chooses local logout it must explain that behavior.
CMS must protect its cookie-backed form with antiforgery and keep credentials out
of URLs, storage, logs, TempData and UI notifications. Confirmation matching belongs
to FE. No CMS UI, Employee, Salary or HR changes in this PR.

## Developer verification

Targeted Release tests: 5 passed, 0 failed, 0 skipped, including existing Users SQL
and credential lifecycle tests. Actual API JWT login and disposable SQL cover all
three roles, exact own-profile allowlist, invalid identity, bad/current/new password,
extra-field rejection, unchanged role/username, password login transition, inactive
account and repeated change. Separate SQL connections verify one-winner atomic update.
Full solution Release build: 0 errors, 119 warnings (existing dependencies/nullability).
SQL fixture verifies database cleanup. No independent Leader QA or FE acceptance claimed.
Real production log-sink audit and CMS visual integration remain outside this test run.

Full Release regression: 242 passed, 0 failed, 0 skipped. git diff --check passed.
