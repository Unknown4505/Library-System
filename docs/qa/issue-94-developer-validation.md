# Issue #94 — Books inactive HTTP regression

Branch: `thien2`. Baseline: `main@f241864f20ef5f0b2ec270ad1d697302eb63f118`.

No production change was needed. The real API pipeline is tested with signed JWTs and an isolated disposable SQL Server database. Startup seeding uses a separate InMemory database; no application database or migration is touched.

| Request | Anonymous / invalid JWT / Staff / Manager | Admin |
|---|---|---|
| Active ID | 200 | 200 |
| Inactive ID | 404 NOT_FOUND wrapper | 200 |
| Missing ID | 404 NOT_FOUND wrapper | 404 NOT_FOUND wrapper |
| Active barcode | 200 | 200 |
| Inactive / missing barcode | 404 NOT_FOUND wrapper | 404 NOT_FOUND wrapper |
| List, including `includeInactive=true` | Active only | Active only |

The SQL-backed test also edits and reactivates an inactive record, checks public ID/barcode visibility after activation, soft-deletes it, and verifies its BookId, barcode and edited title remain stable. The repository still retrieves inactive records. No global filter or Admin barcode bypass was introduced.

Validation (2026-11-13): targeted Debug HTTP/SQL test 1/1 PASS; Release full suite 213/213 PASS, 0 failed, 0 skipped. Debug and Release solution builds: 0 errors, 111 warnings each, including existing dependency advisories. SQL fixture asserts its generated database was dropped; HTTP host checks temporary-folder removal.

Reproduce from this branch (set `BOOKKIOSK_QA_SQL_MASTER` to an approved disposable SQL Server master connection):

```powershell
dotnet build BookKiosk.slnx -c Debug
dotnet build BookKiosk.slnx -c Release
dotnet test BookKiosk.Tests -c Release --filter FullyQualifiedName~BooksVisibilityHttpTests
dotnet test BookKiosk.Tests -c Release
```

Exact commit SHA and post-commit targeted results are recorded in the PR. This is developer HTTP/service/SQL regression evidence, not independent Leader acceptance or physical Kiosk/WPF E2E. Keep the issue open pending review.
