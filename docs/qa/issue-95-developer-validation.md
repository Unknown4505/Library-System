# Issue #95 — Remove the diagnostic error route

Branch: `thien3`. Baseline: `main@f241864f20ef5f0b2ec270ad1d697302eb63f118`.

`BooksController.TestError()` and its route/conditional-compilation block are removed from application source. Middleware behavior is unchanged. A test invokes the middleware directly with an unexpected exception and verifies a safe 500 ApiResponseDto, null data, no private exception detail, and Error logging of the original exception.

The real API runs in Production environment in the tests. GET `/api/books/test-error` returns 404; reflection verifies that the action does not exist in the compiled controller. Public Books GET and Admin POST/PUT/DELETE remain operational through the real pipeline. Tests never add a throwing HTTP endpoint.

Validation (2026-11-13): Debug route/middleware tests initially 2/2 PASS; after adding the CRUD smoke, full Release suite 215/215 PASS, 0 failed, 0 skipped. Debug and Release solution builds after the final test addition: 0 errors, 24 warnings each (incremental build; existing dependency advisories remain). Initial clean compilation reported 111 warnings. Generated SQL databases from the full suite and host temporary directories are cleaned and verified.

Reproduce (set `BOOKKIOSK_QA_SQL_MASTER` to an approved disposable master connection for all SQL tests):

```powershell
dotnet build BookKiosk.slnx -c Debug
dotnet build BookKiosk.slnx -c Release
dotnet test BookKiosk.Tests -c Debug --filter FullyQualifiedName~BooksDiagnosticRouteTests
dotnet test BookKiosk.Tests -c Release --filter FullyQualifiedName~BooksDiagnosticRouteTests
dotnet test BookKiosk.Tests -c Release
```

Exact commit SHA and final post-commit Debug/Release Production HTTP results are recorded in the PR. Developer checks do not close the issue or replace independent review/merge approval. No Leader-private tests or data are included.
